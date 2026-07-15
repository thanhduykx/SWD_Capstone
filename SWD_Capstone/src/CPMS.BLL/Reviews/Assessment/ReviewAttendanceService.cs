using CPMS.BLL.Common.Exceptions;
using CPMS.BLL.Enums;
using CPMS.BLL.Exceptions;

namespace CPMS.BLL.Reviews.Assessment;

public sealed class ReviewAttendanceService(IReviewAttendanceStore store) : IReviewAttendanceService
{
    public async Task<ReviewAttendanceResult> GetAttendanceAsync(
        int sessionId, int? groupId, ReviewActor actor, CancellationToken cancellationToken)
    {
        var context = await RequireContextAsync(sessionId, groupId, actor.UserId, cancellationToken);
        EnsureCanViewAttendance(context, actor);
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
        var context = await RequireContextAsync(sessionId, groupId, actor.UserId, cancellationToken);
        if (actor.Role != UserRole.Lecturer || context.LecturerId is null ||
            context.SessionStatus != ReviewSessionStatus.Published ||
            !context.ReviewerIds.Contains(context.LecturerId.Value))
        {
            throw new UnauthorizedAccessException("Chỉ giảng viên được phân công mới được lưu điểm danh.");
        }
        if (context.GroupStatus == ReviewGroupStatus.Completed)
        {
            throw new ConflictException("Điểm danh đã khóa sau khi nhóm hoàn tất review.");
        }
        if (entries.Count == 0 || entries.Select(x => x.StudentId).Distinct().Count() != entries.Count)
        {
            throw new BusinessRuleException("Danh sách điểm danh phải gồm các sinh viên khác nhau.");
        }
        if (entries.Any(x => x.Note?.Length > 1000))
        {
            throw new BusinessRuleException("Ghi chú điểm danh không được vượt quá 1000 ký tự.");
        }
        var validStudentIds = await store.GetStudentIdsAsync(context.GroupId, cancellationToken);
        if (entries.Any(x => !validStudentIds.Contains(x.StudentId)))
        {
            throw new BusinessRuleException("Mỗi dòng điểm danh phải thuộc đúng nhóm đã chọn.");
        }
        await store.SaveAttendanceAsync(
            sessionId, context.LecturerId.Value, entries, DateTime.UtcNow, cancellationToken);
        return await BuildAttendanceAsync(context, cancellationToken);
    }

    public async Task<IReadOnlyList<ReviewCommentResult>> GetCommentsAsync(
        int sessionId, int? groupId, ReviewActor actor, CancellationToken cancellationToken)
    {
        var context = await RequireContextAsync(sessionId, groupId, actor.UserId, cancellationToken);
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
        var context = await RequireContextAsync(sessionId, groupId, actor.UserId, cancellationToken);
        if (actor.Role != UserRole.Lecturer || context.LecturerId is null ||
            context.SessionStatus != ReviewSessionStatus.Published ||
            !context.ReviewerIds.Contains(context.LecturerId.Value))
        {
            throw new UnauthorizedAccessException("Chỉ giảng viên được phân công mới được thêm nhận xét.");
        }
        if (context.GroupStatus == ReviewGroupStatus.Completed)
        {
            throw new ConflictException("Nhận xét đã khóa sau khi nhóm hoàn tất review.");
        }
        var normalizedContent = content?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedContent) || normalizedContent.Length > 2000)
        {
            throw new BusinessRuleException("Nội dung nhận xét bắt buộc và không được vượt quá 2000 ký tự.");
        }
        return await store.AddCommentAsync(
            sessionId, context.GroupId, actor.UserId, normalizedContent, cancellationToken);
    }

    public async Task<CompleteReviewResult> CompleteAsync(
        int sessionId, int groupId, ReviewActor actor, CancellationToken cancellationToken)
    {
        var context = await RequireContextAsync(sessionId, groupId, actor.UserId, cancellationToken);
        if (actor.Role != UserRole.Lecturer || context.LecturerId is null ||
            !context.ReviewerIds.Contains(context.LecturerId.Value))
        {
            throw new UnauthorizedAccessException("Chỉ giảng viên được phân công mới được hoàn tất review.");
        }
        if (context.SessionStatus != ReviewSessionStatus.Published)
        {
            throw new ConflictException("Chỉ phiên review đã công bố mới có thể hoàn tất.");
        }
        if (context.GroupStatus == ReviewGroupStatus.Completed)
        {
            return new CompleteReviewResult(
                sessionId, groupId, context.GroupStatus, context.CompletedAt);
        }
        var counts = await store.GetCompletionCountsAsync(sessionId, groupId, cancellationToken);
        var reviewerCount = context.ReviewerIds.Count;
        if (counts.StudentCount == 0)
        {
            throw new ConflictException("Nhóm chưa có sinh viên để điểm danh.");
        }
        if (counts.AttendanceCount != counts.StudentCount)
        {
            throw new ConflictException(
                $"Chưa điểm danh đủ sinh viên ({counts.AttendanceCount}/{counts.StudentCount}). " +
                "Hãy bấm Lưu điểm danh trước khi kết thúc.");
        }
        if (counts.SubmittedCount != reviewerCount)
        {
            throw new ConflictException(
                $"Chưa đủ bài chấm checklist từ các giảng viên được phân công " +
                $"({counts.SubmittedCount}/{reviewerCount}). " +
                "Mỗi giảng viên cần gửi bài chấm ở tab Dự án review trước khi kết thúc buổi.");
        }
        return await store.CompleteAsync(
            sessionId, groupId, context.LecturerId.Value, DateTime.UtcNow, cancellationToken);
    }

    private async Task<ReviewAttendanceContext> RequireContextAsync(
        int sessionId, int? groupId, int userId, CancellationToken cancellationToken)
    {
        var lookup = await store.LoadContextAsync(sessionId, groupId, userId, cancellationToken);
        if (!lookup.SessionExists) throw new KeyNotFoundException("Không tìm thấy phiên review.");
        return lookup.Context ?? throw new KeyNotFoundException(
            "Buổi review này có nhiều nhóm. Vui lòng mở lại đúng nhóm từ lịch chấm.");
    }

    private static void EnsureCanViewAttendance(ReviewAttendanceContext context, ReviewActor actor)
    {
        if (context.SessionStatus != ReviewSessionStatus.Published)
            throw new UnauthorizedAccessException("Phiên review chưa được công bố.");
        var allowed = actor.Role switch
        {
            UserRole.Lecturer => context.LecturerId.HasValue && context.ReviewerIds.Contains(context.LecturerId.Value),
            UserRole.Student => context.StudentGroupId == context.GroupId && context.GroupStatus == ReviewGroupStatus.Completed,
            _ => false
        };
        if (!allowed) throw new UnauthorizedAccessException("Bạn không có quyền xem điểm danh này.");
    }

    private static void EnsureCanViewComments(ReviewAttendanceContext context, ReviewActor actor)
    {
        if (actor.Role is UserRole.SystemAdministrator or UserRole.TrainingDepartment) return;
        if (context.SessionStatus == ReviewSessionStatus.Published &&
            ((actor.Role == UserRole.Lecturer && context.LecturerId.HasValue &&
              context.ReviewerIds.Contains(context.LecturerId.Value)) ||
             (actor.Role == UserRole.Student && context.StudentGroupId == context.GroupId &&
              context.GroupStatus == ReviewGroupStatus.Completed))) return;
        throw new UnauthorizedAccessException("Bạn không có quyền xem các nhận xét này.");
    }

    private async Task<ReviewAttendanceResult> BuildAttendanceAsync(
        ReviewAttendanceContext context, CancellationToken cancellationToken) =>
        new(context.SessionId, context.SessionCode, context.GroupId, context.GroupCode,
            context.GroupStatus, context.SessionDate, context.Slot, context.Room,
            await store.GetStudentsAsync(context.SessionId, context.GroupId, cancellationToken));
}
