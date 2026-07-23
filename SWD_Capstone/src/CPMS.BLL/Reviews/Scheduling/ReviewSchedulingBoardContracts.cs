using CPMS.BLL.Enums;

namespace CPMS.BLL.Reviews.Scheduling;

public sealed record ReviewSchedulingBoardResult(
    int SemesterId,
    ReviewType ReviewType,
    DateOnly WeekStart,
    IReadOnlyList<ReviewSchedulingLecturer> Lecturers,
    IReadOnlyList<ReviewSchedulingAvailability> Availability,
    IReadOnlyList<ReviewSchedulingAvailabilitySubmission> AvailabilitySubmissions,
    IReadOnlyList<ReviewSchedulingGroup> Groups,
    IReadOnlyList<ReviewSchedulingSession> Sessions);

public sealed record ReviewSchedulingLecturer(int Id, string Code, string FullName, string Department, string Email);
public sealed record ReviewSchedulingAvailability(int LecturerId, int DayOfWeek, int Slot);
public sealed record ReviewSchedulingAvailabilitySubmission(int LecturerId, bool IsSubmitted, DateTime? SubmittedAt, int SlotCount);
public sealed record ReviewSchedulingGroup(int Id, string Code, string ProjectName, int SupervisorId, string SupervisorCode, int StudentCount);
public sealed record ReviewSchedulingSession(
    int Id,
    string Code,
    int GroupId,
    string GroupCode,
    ReviewType Type,
    ReviewSessionStatus Status,
    IReadOnlyList<int> ReviewerIds,
    DateTime SessionDate,
    int DayOfWeek,
    int Slot,
    string Room,
    ReviewGroupStatus GroupStatus = ReviewGroupStatus.Scheduled,
    IReadOnlyList<ReviewSchedulingReviewerFeedback>? ReviewerFeedback = null,
    bool HasAccessCode = false,
    DateTime? AccessCodeGeneratedAt = null,
    string? AccessCode = null);

public sealed record ReviewSchedulingReviewerFeedback(
    int ReviewerId,
    string ReviewerCode,
    string ReviewerName,
    string Comment,
    DateTime SubmittedAt);

public sealed record StudentSlotRegistrationResult(int Id, int GroupId, string GroupCode, int DayOfWeek, int Slot);

public interface IReviewSchedulingBoardService
{
    Task<ReviewSchedulingBoardResult> GetBoardAsync(
        int semesterId, ReviewType reviewType, DateOnly weekStart, CancellationToken cancellationToken);

    Task<IReadOnlyList<StudentSlotRegistrationResult>> GetStudentRegistrationsAsync(
        int roundId, CancellationToken cancellationToken);
}
