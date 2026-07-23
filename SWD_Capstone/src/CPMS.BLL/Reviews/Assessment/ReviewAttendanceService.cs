using CPMS.BLL.Common.Exceptions;
using CPMS.BLL.Enums;
using CPMS.BLL.Exceptions;
using CPMS.BLL.Services;

namespace CPMS.BLL.Reviews.Assessment;

public sealed class ReviewAttendanceService(IReviewAttendanceStore store) : IReviewAttendanceService
{
    public async Task<ReviewAttendanceResult> GetAttendanceAsync(
        int sessionId, int? groupId, ReviewActor actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ReviewAttendanceContext context = await RequireContextAsync(sessionId, groupId, actor.UserId, cancellationToken);
        EnsureCanViewAttendance(context, actor);
        EnsureAttendanceHasOpened(context);
        return await BuildAttendanceAsync(context, cancellationToken);
    }

    public async Task<ReviewAttendanceResult> SubmitAttendanceAsync(
        int sessionId,
        int groupId,
        IReadOnlyCollection<AttendanceEntry> entries,
        ReviewActor actor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(actor);
        ReviewAttendanceContext context = await RequireContextAsync(sessionId, groupId, actor.UserId, cancellationToken);
        if (actor.Role != UserRole.Lecturer || context.LecturerId is null ||
            context.SessionStatus != ReviewSessionStatus.Published ||
            !context.ReviewerIds.Contains(context.LecturerId.Value))
        {
            throw new UnauthorizedAccessException("Only an assigned reviewer can submit attendance.");
        }
        if (context.GroupStatus == ReviewGroupStatus.Completed)
        {
            throw new ConflictException("Attendance is locked after the group's review is completed.");
        }
        EnsureAttendanceHasOpened(context);
        if (entries.Count == 0 || entries.Select(x => x.StudentId).Distinct().Count() != entries.Count)
        {
            throw new BusinessRuleException("Attendance entries must contain distinct students.");
        }
        if (entries.Any(x => x.Note?.Length > 1000))
        {
            throw new BusinessRuleException("Attendance notes cannot exceed 1000 characters.");
        }
        IReadOnlySet<int> validStudentIds = await store.GetStudentIdsAsync(context.GroupId, cancellationToken);
        if (entries.Any(x => !validStudentIds.Contains(x.StudentId)))
        {
            throw new BusinessRuleException("Every attendance entry must belong to the selected group.");
        }
        await store.SaveAttendanceAsync(
            sessionId, context.LecturerId.Value, entries, DateTime.UtcNow, cancellationToken);
        return await BuildAttendanceAsync(context, cancellationToken);
    }

    public async Task<IReadOnlyList<ReviewCommentResult>> GetCommentsAsync(
        int sessionId, int? groupId, ReviewActor actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ReviewAttendanceContext context = await RequireContextAsync(sessionId, groupId, actor.UserId, cancellationToken);
        EnsureCanViewComments(context, actor);
        return await store.GetCommentsAsync(sessionId, context.GroupId, cancellationToken);
    }

    public async Task<ReviewCommentResult> AddCommentAsync(
        int sessionId,
        int groupId,
        string? content,
        ReviewActor actor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ReviewAttendanceContext context = await RequireContextAsync(sessionId, groupId, actor.UserId, cancellationToken);
        if (actor.Role != UserRole.Lecturer || context.LecturerId is null ||
            context.SessionStatus != ReviewSessionStatus.Published ||
            !context.ReviewerIds.Contains(context.LecturerId.Value))
        {
            throw new UnauthorizedAccessException("Only an assigned reviewer can add comments.");
        }
        if (context.GroupStatus == ReviewGroupStatus.Completed)
        {
            throw new ConflictException("Comments are locked after the group's review is completed.");
        }
        var normalizedContent = content?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedContent) || normalizedContent.Length > 2000)
        {
            throw new BusinessRuleException("Comment content is required and cannot exceed 2000 characters.");
        }
        return await store.AddCommentAsync(
            sessionId, context.GroupId, actor.UserId, normalizedContent, cancellationToken);
    }

    public async Task<CompleteReviewResult> CompleteAsync(
        int sessionId, int groupId, ReviewActor actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ReviewAttendanceContext context = await RequireContextAsync(sessionId, groupId, actor.UserId, cancellationToken);
        if (actor.Role != UserRole.Lecturer || context.LecturerId is null ||
            !context.ReviewerIds.Contains(context.LecturerId.Value))
        {
            throw new UnauthorizedAccessException("Only an assigned reviewer can complete the review.");
        }
        if (context.SessionStatus != ReviewSessionStatus.Published)
        {
            throw new ConflictException("Only a published review session can be completed.");
        }
        if (context.GroupStatus == ReviewGroupStatus.Completed)
        {
            return new CompleteReviewResult(
                sessionId, groupId, context.GroupStatus, context.CompletedAt);
        }
        (int StudentCount, int AttendanceCount, int SubmittedCount) counts =
            await store.GetCompletionCountsAsync(sessionId, groupId, cancellationToken);
        if (counts.StudentCount == 0 || counts.SubmittedCount != context.ReviewerIds.Count)
        {
            throw new ConflictException(
                "All reviewer submissions are required before completion.");
        }

        await store.FinalizeUnsignedCheckInsAsync(
            sessionId, groupId, context.LecturerId.Value, DateTime.UtcNow, cancellationToken);
        counts = await store.GetCompletionCountsAsync(sessionId, groupId, cancellationToken);
        if (counts.AttendanceCount != counts.StudentCount)
        {
            throw new ConflictException("Attendance could not be finalized for every student.");
        }
        return await store.CompleteAsync(
            sessionId, groupId, context.LecturerId.Value, DateTime.UtcNow, cancellationToken);
    }

    private async Task<ReviewAttendanceContext> RequireContextAsync(
        int sessionId, int? groupId, int userId, CancellationToken cancellationToken)
    {
        ReviewAttendanceContextLookup lookup = await store.LoadContextAsync(sessionId, groupId, userId, cancellationToken);
        if (!lookup.SessionExists) throw new KeyNotFoundException("Review session not found.");
        return lookup.Context ?? throw new KeyNotFoundException("No group assigned to this session.");
    }

    private static void EnsureCanViewAttendance(ReviewAttendanceContext context, ReviewActor actor)
    {
        if (context.SessionStatus != ReviewSessionStatus.Published)
            throw new UnauthorizedAccessException("The review session is not published.");
        var allowed = actor.Role switch
        {
            UserRole.Lecturer => context.LecturerId.HasValue && context.ReviewerIds.Contains(context.LecturerId.Value),
            UserRole.Student => context.StudentGroupId == context.GroupId && context.GroupStatus == ReviewGroupStatus.Completed,
            _ => false
        };
        if (!allowed) throw new UnauthorizedAccessException("You cannot view this attendance.");
    }

    private static void EnsureAttendanceHasOpened(ReviewAttendanceContext context)
    {
        if (!ReviewSessionTiming.HasStarted(context.SessionDate, context.Slot, DateTime.UtcNow))
        {
            throw new BusinessRuleException(
                $"Attendance opens when slot {context.Slot} starts.");
        }
    }

    private static void EnsureCanViewComments(ReviewAttendanceContext context, ReviewActor actor)
    {
        if (actor.Role is UserRole.SystemAdministrator or UserRole.TrainingDepartment) return;
        if (context.SessionStatus == ReviewSessionStatus.Published &&
            ((actor.Role == UserRole.Lecturer && context.LecturerId.HasValue &&
              context.ReviewerIds.Contains(context.LecturerId.Value)) ||
             (actor.Role == UserRole.Student && context.StudentGroupId == context.GroupId &&
              context.GroupStatus == ReviewGroupStatus.Completed))) return;
        throw new UnauthorizedAccessException("You cannot view these comments.");
    }

    private async Task<ReviewAttendanceResult> BuildAttendanceAsync(
        ReviewAttendanceContext context, CancellationToken cancellationToken) =>
        new(context.SessionId, context.SessionCode, context.GroupId, context.GroupCode,
            context.GroupStatus, context.SessionDate, context.Slot, context.Room,
            await store.GetStudentsAsync(context.SessionId, context.GroupId, cancellationToken));
}
