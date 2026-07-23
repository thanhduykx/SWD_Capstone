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

    public Task<int?> FindAssignedLecturerAsync(
        int sessionId,
        int groupId,
        int lecturerUserId,
        CancellationToken cancellationToken) =>
        dbContext.ReviewChecklistSubmissions
            .Where(submission =>
                submission.SessionId == sessionId
                && submission.GroupId == groupId
                && dbContext.Lecturers.Any(lecturer =>
                    lecturer.Id == submission.ReviewerId
                    && lecturer.UserId == lecturerUserId))
            .Select(submission => (int?)submission.ReviewerId)
            .Distinct()
            .SingleOrDefaultAsync(cancellationToken);

    public async Task SaveAccessCodeAsync(
        int sessionId,
        int groupId,
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
