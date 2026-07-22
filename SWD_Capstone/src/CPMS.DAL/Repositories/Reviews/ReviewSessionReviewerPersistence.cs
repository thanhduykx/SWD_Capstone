using CPMS.DAL.Data;
using CPMS.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace CPMS.DAL.Repositories.Reviews;

internal static class ReviewSessionReviewerPersistence
{
    public static IQueryable<int> ReviewerIds(CpmsDbContext dbContext, int sessionId) =>
        dbContext.ReviewSessionReviewers
            .Where(x => x.SessionId == sessionId)
            .OrderBy(x => x.Position)
            .Select(x => x.LecturerId);

    public static void Add(
        CpmsDbContext dbContext,
        int sessionId,
        IReadOnlyCollection<int> reviewerIds)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(reviewerIds);
        dbContext.ReviewSessionReviewers.AddRange(reviewerIds.Select((lecturerId, index) =>
            new ReviewSessionReviewer
            {
                SessionId = sessionId,
                LecturerId = lecturerId,
                Position = index + 1
            }));
    }

    public static async Task ReplaceAsync(
        CpmsDbContext dbContext,
        int sessionId,
        IReadOnlyCollection<int> reviewerIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(reviewerIds);
        List<ReviewSessionReviewer> existing = await dbContext.ReviewSessionReviewers
            .Where(x => x.SessionId == sessionId)
            .ToListAsync(cancellationToken);
        var desiredPositions = reviewerIds.Select((lecturerId, index) => new { lecturerId, position = index + 1 })
            .ToDictionary(x => x.lecturerId, x => x.position);
        dbContext.ReviewSessionReviewers.RemoveRange(existing.Where(x => !desiredPositions.ContainsKey(x.LecturerId)));
        foreach (ReviewSessionReviewer reviewer in existing.Where(x => desiredPositions.ContainsKey(x.LecturerId)))
            reviewer.Position = desiredPositions[reviewer.LecturerId];
        dbContext.ReviewSessionReviewers.AddRange(desiredPositions
            .Where(x => existing.All(current => current.LecturerId != x.Key))
            .Select(x => new ReviewSessionReviewer
            {
                SessionId = sessionId,
                LecturerId = x.Key,
                Position = x.Value
            }));
    }
}
