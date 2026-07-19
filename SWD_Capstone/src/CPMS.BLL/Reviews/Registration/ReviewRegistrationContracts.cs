using CPMS.BLL.Enums;

namespace CPMS.BLL.Reviews.Registration;

public sealed record ReviewAvailabilitySlot(int DayOfWeek, int Slot);

public sealed record ReviewAvailabilityWeekResult(
    int RoundId,
    int SemesterId,
    int LecturerId,
    DateOnly WeekStart,
    bool IsSubmitted,
    DateTime? SubmittedAt,
    IReadOnlyCollection<ReviewAvailabilitySlot> Slots);

public sealed record StudentReviewRoundResult(
    int Id,
    int SemesterId,
    ReviewType Type,
    ReviewRoundStatus Status,
    DateOnly WeekStartDate,
    DateOnly WeekEndDate,
    bool IsGroupRegistered,
    bool IsLeader = false,
    int? GroupId = null,
    string? GroupCode = null);

public sealed record StudentSlotRegistrationResult(
    int Id,
    int GroupId,
    string GroupCode,
    int DayOfWeek,
    int Slot);

public sealed record StudentSlotGridResult(
    int RoundId,
    ReviewType Type,
    ReviewRoundStatus Status,
    DateOnly WeekStartDate,
    DateOnly WeekEndDate,
    int RequiredAvailabilityCount,
    IReadOnlyCollection<StudentSlotRegistrationResult> MyAvailability,
    bool IsLeader = false,
    int? GroupId = null,
    string? GroupCode = null);

public sealed record StudentScheduleResult(
    int SessionId,
    string SessionCode,
    ReviewType Type,
    DateTime SessionDate,
    int Slot,
    string Room,
    ReviewSessionStatus Status,
    ReviewGroupStatus GroupStatus,
    DateTime? CompletedAt);

public sealed record StudentAttendanceResult(
    int SessionId,
    string SessionCode,
    bool IsPresent,
    string? Note,
    DateTime MarkedAt);

public sealed record StudentReviewCommentResult(
    int Id,
    int AuthorUserId,
    string AuthorName,
    string Content,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public interface IReviewRegistrationService
{
    Task<ReviewAvailabilityWeekResult> GetLecturerAvailabilityAsync(int userId, int roundId, CancellationToken cancellationToken);
    Task<ReviewAvailabilityWeekResult> SaveLecturerAvailabilityAsync(int userId, int roundId, IReadOnlyCollection<ReviewAvailabilitySlot>? slots, CancellationToken cancellationToken);
    Task<ReviewAvailabilityWeekResult> SubmitLecturerAvailabilityAsync(int userId, int roundId, CancellationToken cancellationToken);
    Task<IReadOnlyList<StudentReviewRoundResult>> GetStudentRoundsAsync(int userId, int? semesterId, CancellationToken cancellationToken);
    Task<StudentSlotGridResult> GetStudentSlotGridAsync(int userId, int roundId, CancellationToken cancellationToken);
    Task<StudentSlotGridResult> SaveStudentAvailabilityAsync(int userId, int roundId, IReadOnlyCollection<ReviewAvailabilitySlot>? slots, CancellationToken cancellationToken);
    Task ClearStudentAvailabilityAsync(int userId, int roundId, CancellationToken cancellationToken);
    Task<IReadOnlyList<StudentScheduleResult>> GetStudentScheduleAsync(int userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<StudentAttendanceResult>> GetStudentAttendanceAsync(int userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<StudentReviewCommentResult>> GetStudentCommentsAsync(int userId, int sessionId, CancellationToken cancellationToken);
}
