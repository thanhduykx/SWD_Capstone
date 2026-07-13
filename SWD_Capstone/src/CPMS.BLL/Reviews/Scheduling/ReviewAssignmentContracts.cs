using CPMS.Core.Enums;

namespace CPMS.BLL.Reviews.Scheduling;

public sealed record GenerateReviewAssignmentsCommand(
    int SemesterId,
    ReviewType ReviewType,
    DateOnly WeekStart,
    int ReviewersPerSession,
    string? RoomPrefix,
    int? Seed);

public sealed record GenerateReviewAssignmentsResult(
    int TotalCandidateGroups,
    int AssignedCount,
    IReadOnlyList<UnassignedReviewGroup> UnassignedGroups,
    IReadOnlyList<ReviewSchedulingSession> Sessions);

public sealed record UnassignedReviewGroup(int GroupId, string GroupCode, string Reason);

public interface IReviewAssignmentService
{
    Task<GenerateReviewAssignmentsResult> GenerateAsync(
        GenerateReviewAssignmentsCommand command, CancellationToken cancellationToken);
}
