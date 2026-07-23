using CPMS.BLL.Reviews.Access;
using CPMS.DAL.Data;
using CPMS.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace CPMS.DAL.Repositories.Reviews;

public sealed class ReviewSessionAccessStore(CpmsDbContext dbContext)
    : IReviewSessionAccessStore
{
    public Task<ReviewSessionAccessState?> FindSessionAsync(
        int sessionId,
        CancellationToken cancellationToken) =>
        dbContext.ReviewSessions
            .Where(session => session.Id == sessionId)
            .Select(session => new ReviewSessionAccessState(
                session.Id,
                session.AccessCodeHash,
                session.AccessCodeVersion))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<ReviewSessionAccessState>> FindSlotSessionsAsync(
        int sessionId,
        CancellationToken cancellationToken)
    {
        var anchor = await dbContext.ReviewSessions
            .Where(session => session.Id == sessionId)
            .Select(session => new
            {
                session.SemesterId,
                session.Type,
                session.SessionDate,
                session.Slot
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (anchor is null)
        {
            return [];
        }

        return await dbContext.ReviewSessions
            .Where(session =>
                session.SemesterId == anchor.SemesterId
                && session.Type == anchor.Type
                && session.SessionDate == anchor.SessionDate
                && session.Slot == anchor.Slot)
            .Select(session => new ReviewSessionAccessState(
                session.Id,
                session.AccessCodeHash,
                session.AccessCodeVersion))
            .ToArrayAsync(cancellationToken);
    }

    public Task<int?> FindAssignedLecturerAsync(
        int sessionId,
        int lecturerUserId,
        CancellationToken cancellationToken) =>
        dbContext.ReviewSessionReviewers
            .Where(assignment =>
                assignment.SessionId == sessionId
                && dbContext.Lecturers.Any(lecturer =>
                    lecturer.Id == assignment.LecturerId
                    && lecturer.UserId == lecturerUserId))
            .Select(assignment => (int?)assignment.LecturerId)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task SaveAccessCodeAsync(
        IReadOnlyCollection<int> sessionIds,
        string accessCodeHash,
        int codeVersion,
        int generatedByUserId,
        DateTime generatedAt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessionIds);
        ReviewSession[] sessions = await dbContext.ReviewSessions
            .Where(item => sessionIds.Contains(item.Id))
            .ToArrayAsync(cancellationToken);
        if (sessions.Length != sessionIds.Count)
        {
            throw new KeyNotFoundException("One or more review sessions were not found.");
        }

        foreach (ReviewSession session in sessions)
        {
            session.AccessCodeHash = accessCodeHash;
            session.AccessCodeVersion = codeVersion;
            session.AccessCodeGeneratedByUserId = generatedByUserId;
            session.AccessCodeGeneratedAt = generatedAt;
        }
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveGrantAsync(
        int sessionId,
        int lecturerId,
        int codeVersion,
        DateTime verifiedAt,
        CancellationToken cancellationToken)
    {
        ReviewSessionAccessGrant? grant = await dbContext.ReviewSessionAccessGrants
            .SingleOrDefaultAsync(
                item => item.SessionId == sessionId && item.LecturerId == lecturerId,
                cancellationToken);
        if (grant is null)
        {
            dbContext.ReviewSessionAccessGrants.Add(new ReviewSessionAccessGrant
            {
                SessionId = sessionId,
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
        int lecturerId,
        int codeVersion,
        CancellationToken cancellationToken) =>
        dbContext.ReviewSessionAccessGrants.AnyAsync(
            grant => grant.SessionId == sessionId
                && grant.LecturerId == lecturerId
                && grant.CodeVersion == codeVersion,
            cancellationToken);
}
