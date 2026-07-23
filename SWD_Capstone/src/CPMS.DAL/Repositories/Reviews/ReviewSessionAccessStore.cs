using CPMS.BLL.Reviews.Access;
using CPMS.DAL.Data;
using CPMS.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace CPMS.DAL.Repositories.Reviews;

public sealed class ReviewSessionAccessStore(CpmsDbContext dbContext)
    : IReviewSessionAccessStore
{
    public Task<ReviewSessionAccessState?> FindGroupAsync(
        int sessionId,
        int groupId,
        CancellationToken cancellationToken) =>
        dbContext.GroupReviewSlots
            .Where(slot => slot.SessionId == sessionId && slot.GroupId == groupId)
            .Select(slot => new ReviewSessionAccessState(
                slot.SessionId,
                slot.GroupId,
                slot.AccessCodeHash,
                slot.AccessCodeVersion))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<int?> FindAssignedLecturerAsync(
        int sessionId,
        int groupId,
        int lecturerUserId,
        CancellationToken cancellationToken)
    {
        // /my lấy từ checklist submission — verify phải khớp nguồn này trước.
        int? fromSubmission = await (
            from submission in dbContext.ReviewChecklistSubmissions
            join lecturer in dbContext.Lecturers on submission.ReviewerId equals lecturer.Id
            where submission.SessionId == sessionId
                && submission.GroupId == groupId
                && lecturer.UserId == lecturerUserId
            select (int?)lecturer.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (fromSubmission.HasValue)
        {
            return fromSubmission;
        }

        // Fallback: vẫn cho GV trong panel session (tránh lệch dữ liệu panel vs submission).
        return await (
            from assignment in dbContext.ReviewSessionReviewers
            join lecturer in dbContext.Lecturers on assignment.LecturerId equals lecturer.Id
            where assignment.SessionId == sessionId
                && lecturer.UserId == lecturerUserId
                && dbContext.GroupReviewSlots.Any(slot =>
                    slot.SessionId == sessionId && slot.GroupId == groupId)
            select (int?)lecturer.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<int>> FindGroupIdsInSessionAsync(
        int sessionId,
        CancellationToken cancellationToken) =>
        await dbContext.GroupReviewSlots
            .Where(slot => slot.SessionId == sessionId)
            .OrderBy(slot => slot.GroupPosition)
            .Select(slot => slot.GroupId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<int>> FindAssignedGroupIdsAsync(
        int sessionId,
        int lecturerUserId,
        CancellationToken cancellationToken) =>
        await (
            from submission in dbContext.ReviewChecklistSubmissions
            join lecturer in dbContext.Lecturers on submission.ReviewerId equals lecturer.Id
            where submission.SessionId == sessionId && lecturer.UserId == lecturerUserId
            select submission.GroupId
        )
        .Distinct()
        .ToListAsync(cancellationToken);

    public async Task SaveAccessCodeAsync(
        int sessionId,
        int groupId,
        string accessCode,
        string accessCodeHash,
        int codeVersion,
        int generatedByUserId,
        DateTime generatedAt,
        CancellationToken cancellationToken)
    {
        GroupReviewSlot groupSlot = await dbContext.GroupReviewSlots.SingleOrDefaultAsync(
            item => item.SessionId == sessionId && item.GroupId == groupId,
            cancellationToken)
            ?? throw new KeyNotFoundException("Review group was not found in this session.");
        groupSlot.AccessCode = accessCode;
        groupSlot.AccessCodeHash = accessCodeHash;
        groupSlot.AccessCodeVersion = codeVersion;
        groupSlot.AccessCodeGeneratedByUserId = generatedByUserId;
        groupSlot.AccessCodeGeneratedAt = generatedAt;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveGrantAsync(
        int sessionId,
        int groupId,
        int lecturerId,
        int codeVersion,
        DateTime verifiedAt,
        CancellationToken cancellationToken)
    {
        ReviewSessionAccessGrant? grant = await dbContext.ReviewSessionAccessGrants
            .SingleOrDefaultAsync(
                item => item.SessionId == sessionId
                    && item.GroupId == groupId
                    && item.LecturerId == lecturerId,
                cancellationToken);
        if (grant is null)
        {
            dbContext.ReviewSessionAccessGrants.Add(new ReviewSessionAccessGrant
            {
                SessionId = sessionId,
                GroupId = groupId,
                LecturerId = lecturerId,
                CodeVersion = codeVersion,
                VerifiedAt = verifiedAt
            });
        }
        else
        {
            grant.CodeVersion = codeVersion;
            grant.VerifiedAt = verifiedAt;
        }
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<bool> HasCurrentGrantAsync(
        int sessionId,
        int groupId,
        int lecturerId,
        int codeVersion,
        CancellationToken cancellationToken) =>
        dbContext.ReviewSessionAccessGrants.AnyAsync(
            grant => grant.SessionId == sessionId
                && grant.GroupId == groupId
                && grant.LecturerId == lecturerId
                && grant.CodeVersion == codeVersion,
            cancellationToken);
}
