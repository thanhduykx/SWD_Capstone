using CPMS.BLL.Enums;

namespace CPMS.BLL.Reviews.Scheduling;

public sealed record ReviewAssignmentScope(int SemesterId, ReviewType ReviewType, DateOnly WeekStart);
public sealed record LecturerSlot(int LecturerId, int DayOfWeek, int Slot);
public sealed record AssignmentGroup(int Id, string Code, int SupervisorId);
public sealed record OccupiedReviewerSlot(int LecturerId, DateTime SessionDate, int Slot);
public sealed record AssignmentPanel(
    int SessionId,
    string Code,
    ReviewType Type,
    ReviewSessionStatus Status,
    IReadOnlyList<int> ReviewerIds,
    DateTime SessionDate,
    int DayOfWeek,
    int Slot,
    string Room,
    int GroupCount);

public sealed record ReviewAssignmentSnapshot(
    bool SemesterExists,
    string? SemesterCode,
    int? RoundId,
    ReviewRoundStatus? RoundStatus,
    IReadOnlyList<LecturerSlot> SubmittedAvailability,
    IReadOnlyList<AssignmentGroup> Groups,
    IReadOnlyDictionary<int, IReadOnlySet<int>> PreviousReviewerIdsByGroup,
    IReadOnlyList<OccupiedReviewerSlot> OccupiedReviewerSlots,
    IReadOnlySet<string> UsedSessionCodes,
    IReadOnlyDictionary<int, IReadOnlySet<string>> StudentAvailabilityByGroup,
    IReadOnlyList<AssignmentPanel> ReusablePanels);

public sealed record CreateAssignmentPanel(
    string Code,
    int SemesterId,
    int DayOfWeek,
    int Slot,
    string Room,
    ReviewType Type,
    IReadOnlyList<int> ReviewerIds,
    DateTime SessionDate);

public interface IReviewAssignmentStore
{
    Task<TResult> ExecuteLockedAsync<TResult>(
        ReviewAssignmentScope scope,
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken);

    Task<ReviewAssignmentSnapshot> LoadSnapshotAsync(
        ReviewAssignmentScope scope, CancellationToken cancellationToken);

    Task<AssignmentPanel> CreatePanelAsync(
        CreateAssignmentPanel panel, CancellationToken cancellationToken);

    Task AssignGroupAsync(
        int sessionId,
        int groupId,
        int groupPosition,
        IReadOnlyList<int> reviewerIds,
        ReviewType reviewType,
        DateTime changedAt,
        CancellationToken cancellationToken);
}
