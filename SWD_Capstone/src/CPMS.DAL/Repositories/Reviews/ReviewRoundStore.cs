using CPMS.BLL.Reviews.Scheduling;
using CPMS.Core.Entities;
using CPMS.Core.Enums;
using CPMS.DAL.Data;
using Microsoft.EntityFrameworkCore;

namespace CPMS.DAL.Repositories.Reviews;

public sealed class ReviewRoundStore(CpmsDbContext dbContext) : IReviewRoundStore
{
    public async Task<IReadOnlyList<ReviewRoundState>> GetRoundsAsync(
        int semesterId, CancellationToken cancellationToken) =>
        await dbContext.ReviewRounds.Where(x => x.SemesterId == semesterId)
            .OrderByDescending(x => x.WeekStartDate)
            .Select(x => new ReviewRoundState(
                x.Id, x.SemesterId, x.Type, x.Status, x.WeekStartDate, x.WeekEndDate,
                x.CreatedAt, x.OpenedAt, x.ClosedAt,
                dbContext.StudentSlotRegistrations.Where(registration => registration.ReviewRoundId == x.Id)
                    .Select(registration => registration.GroupId).Distinct().Count()))
            .ToListAsync(cancellationToken);

    public Task<bool> SemesterExistsAsync(int semesterId, CancellationToken cancellationToken) =>
        dbContext.Semesters.AnyAsync(x => x.Id == semesterId, cancellationToken);

    public async Task<int?> FindModeratorIdAsync(
        int userId, bool allowFallback, CancellationToken cancellationToken)
    {
        var moderatorId = await dbContext.TrainingDepartments.Where(x => x.UserId == userId)
            .Select(x => (int?)x.Id).SingleOrDefaultAsync(cancellationToken);
        if (moderatorId is null && allowFallback)
        {
            moderatorId = await dbContext.TrainingDepartments.OrderBy(x => x.Id)
                .Select(x => (int?)x.Id).FirstOrDefaultAsync(cancellationToken);
        }
        return moderatorId;
    }

    public Task<bool> RoundExistsAsync(
        int semesterId, ReviewType reviewType, DateOnly weekStartDate, CancellationToken cancellationToken) =>
        dbContext.ReviewRounds.AnyAsync(x =>
            x.SemesterId == semesterId && x.Type == reviewType && x.WeekStartDate == weekStartDate,
            cancellationToken);

    public async Task<ReviewRoundState> CreateRoundAsync(
        int semesterId,
        ReviewType reviewType,
        DateOnly weekStartDate,
        DateOnly weekEndDate,
        int moderatorId,
        CancellationToken cancellationToken)
    {
        var round = new ReviewRound
        {
            SemesterId = semesterId,
            Type = reviewType,
            WeekStartDate = weekStartDate,
            WeekEndDate = weekEndDate,
            Status = ReviewRoundStatus.Draft,
            CreatedByModeratorId = moderatorId
        };
        dbContext.ReviewRounds.Add(round);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(round, 0);
    }

    public Task<ReviewRoundState?> FindRoundAsync(int roundId, CancellationToken cancellationToken) =>
        dbContext.ReviewRounds.Where(x => x.Id == roundId)
            .Select(x => new ReviewRoundState(
                x.Id, x.SemesterId, x.Type, x.Status, x.WeekStartDate, x.WeekEndDate,
                x.CreatedAt, x.OpenedAt, x.ClosedAt,
                dbContext.StudentSlotRegistrations.Where(registration => registration.ReviewRoundId == x.Id)
                    .Select(registration => registration.GroupId).Distinct().Count()))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<(int ActiveGroups, int CompletedRegistrations)> GetRegistrationCompletionAsync(
        int roundId, int semesterId, int requiredSlotCount, CancellationToken cancellationToken)
    {
        var activeGroupIds = await dbContext.CapstoneGroups
            .Where(x => x.SemesterId == semesterId && x.Status == GroupStatus.Active)
            .Select(x => x.Id).ToListAsync(cancellationToken);
        var completed = await dbContext.StudentSlotRegistrations
            .Where(x => x.ReviewRoundId == roundId && activeGroupIds.Contains(x.GroupId))
            .GroupBy(x => x.GroupId)
            .CountAsync(group => group.Count() == requiredSlotCount, cancellationToken);
        return (activeGroupIds.Count, completed);
    }

    public async Task<ReviewRoundState> UpdateRoundStatusAsync(
        int roundId,
        ReviewRoundStatus status,
        DateTime? openedAt,
        DateTime? closedAt,
        CancellationToken cancellationToken)
    {
        var round = await dbContext.ReviewRounds.SingleAsync(x => x.Id == roundId, cancellationToken);
        round.Status = status;
        round.OpenedAt = openedAt;
        round.ClosedAt = closedAt;
        await dbContext.SaveChangesAsync(cancellationToken);
        var registeredGroups = await dbContext.StudentSlotRegistrations
            .Where(x => x.ReviewRoundId == roundId).Select(x => x.GroupId).Distinct()
            .CountAsync(cancellationToken);
        return Map(round, registeredGroups);
    }

    private static ReviewRoundState Map(ReviewRound round, int registeredGroupCount) =>
        new(round.Id, round.SemesterId, round.Type, round.Status, round.WeekStartDate, round.WeekEndDate,
            round.CreatedAt, round.OpenedAt, round.ClosedAt, registeredGroupCount);
}
