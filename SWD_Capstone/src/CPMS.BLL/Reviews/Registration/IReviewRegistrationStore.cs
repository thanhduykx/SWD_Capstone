using CPMS.BLL.Enums;

namespace CPMS.BLL.Reviews.Registration;

public sealed record ReviewRoundData(
    int Id,
    int SemesterId,
    ReviewType Type,
    ReviewRoundStatus Status,
    DateOnly WeekStartDate,
    DateOnly WeekEndDate);

public sealed record LecturerAvailabilityData(DateTime? SubmittedAt, IReadOnlyList<ReviewAvailabilitySlot> Slots);
public sealed record StudentProfileData(int Id, int? GroupId, bool IsLeader, int? GroupSemesterId, string? GroupCode = null);
public sealed record StudentRoundData(ReviewRoundData Round, int RegistrationCount);
public sealed record StudentRegistrationData(int Id, int GroupId, string GroupCode, int DayOfWeek, int Slot);
public sealed record StudentScheduleData(
    int SessionId, string SessionCode, int GroupId, string GroupCode,
    ReviewType Type, DateTime SessionDate, int Slot, string Room,
    ReviewSessionStatus SessionStatus, ReviewGroupStatus GroupStatus, DateTime? CompletedAt);
public sealed record StudentCheckInData(
    int SessionId, string SessionCode, ReviewType Type, DateTime SessionDate, int Slot, string Room,
    ReviewSessionStatus SessionStatus, ReviewGroupStatus GroupStatus, DateTime? ConfirmedAt);
public sealed record StudentAttendanceData(
    int SessionId, string SessionCode, bool IsPresent, string? Note, DateTime MarkedAt, ReviewGroupStatus GroupStatus);
public sealed record StudentCommentAccessData(ReviewSessionStatus SessionStatus, ReviewGroupStatus GroupStatus);
public sealed record StudentCommentData(
    int Id, int AuthorUserId, string AuthorName, string Content, DateTime CreatedAt, DateTime? UpdatedAt);

public interface IReviewRegistrationStore
{
    Task<TResult> ExecuteLockedAsync<TResult>(
        int roundId,
        Func<CancellationToken, Task<TResult>> action,
        CancellationToken cancellationToken);
    Task<int?> FindLecturerIdByUserIdAsync(int userId, CancellationToken cancellationToken);
    Task<ReviewRoundData?> FindRoundAsync(int roundId, CancellationToken cancellationToken);
    Task<LecturerAvailabilityData> GetLecturerAvailabilityAsync(int roundId, int lecturerId, CancellationToken cancellationToken);
    Task<IReadOnlyList<SlotRegistrationCount>> GetLecturerSlotRegistrationCountsAsync(int roundId, CancellationToken cancellationToken);
    Task ReplaceLecturerAvailabilityAsync(ReviewRoundData round, int lecturerId, IReadOnlyCollection<ReviewAvailabilitySlot> slots, DateTime changedAt, CancellationToken cancellationToken);
    Task SubmitLecturerAvailabilityAsync(ReviewRoundData round, int lecturerId, DateTime submittedAt, CancellationToken cancellationToken);
    Task<StudentProfileData?> FindStudentByUserIdAsync(int userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<int>> GetActiveSemesterIdsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<StudentRoundData>> GetStudentRoundsAsync(IReadOnlyCollection<int> semesterIds, int? groupId, CancellationToken cancellationToken);
    Task<IReadOnlyList<StudentRegistrationData>> GetStudentRegistrationsAsync(int roundId, int groupId, CancellationToken cancellationToken);
    Task<IReadOnlyList<SlotRegistrationCount>> GetStudentSlotRegistrationCountsAsync(int roundId, CancellationToken cancellationToken);
    Task ReplaceStudentAvailabilityAsync(int roundId, int groupId, int studentId, IReadOnlyCollection<ReviewAvailabilitySlot> slots, CancellationToken cancellationToken);
    Task<bool> ClearStudentAvailabilityAsync(int roundId, int groupId, CancellationToken cancellationToken);
    Task<IReadOnlyList<StudentScheduleData>> GetStudentScheduleAsync(int groupId, CancellationToken cancellationToken);
    Task<IReadOnlyList<StudentAttendanceData>> GetStudentAttendanceAsync(int studentId, CancellationToken cancellationToken);
    Task<IReadOnlyList<StudentCheckInData>> GetStudentCheckInsAsync(int studentId, int groupId, CancellationToken cancellationToken);
    Task<DateTime> ConfirmStudentCheckInAsync(int userId, int studentId, int groupId, int sessionId, DateTime confirmedAt, CancellationToken cancellationToken);
    Task<StudentCommentAccessData?> GetStudentCommentAccessAsync(int sessionId, int groupId, CancellationToken cancellationToken);
    Task<IReadOnlyList<StudentCommentData>> GetStudentCommentsAsync(int sessionId, int groupId, CancellationToken cancellationToken);
}
