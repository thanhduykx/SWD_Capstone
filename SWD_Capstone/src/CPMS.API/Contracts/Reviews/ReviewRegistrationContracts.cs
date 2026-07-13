using CPMS.Core.Enums;

namespace CPMS.API.Contracts.Reviews;

public sealed record SaveReviewAvailabilityWeekRequest(IReadOnlyCollection<ReviewAvailabilitySlotRequest> Slots);
public sealed record ReviewAvailabilitySlotRequest(int DayOfWeek, int Slot);
public sealed record ReviewAvailabilitySlotResponse(int DayOfWeek, int Slot);
public sealed record ReviewAvailabilityWeekResponse(
    int RoundId,
    int SemesterId,
    int LecturerId,
    DateOnly WeekStart,
    bool IsSubmitted,
    DateTime? SubmittedAt,
    IReadOnlyCollection<ReviewAvailabilitySlotResponse> Slots);

public sealed record StudentReviewRoundResponse(
    int Id,
    int SemesterId,
    ReviewType Type,
    ReviewRoundStatus Status,
    DateOnly WeekStartDate,
    DateOnly WeekEndDate,
    bool IsGroupRegistered);

public sealed record StudentSlotGridResponse(
    int RoundId,
    ReviewType Type,
    ReviewRoundStatus Status,
    DateOnly WeekStartDate,
    DateOnly WeekEndDate,
    int RequiredAvailabilityCount,
    IReadOnlyCollection<StudentSlotRegistrationResponse> MyAvailability);

public sealed record StudentSlotRegistrationResponse(
    int Id,
    int GroupId,
    string GroupCode,
    int DayOfWeek,
    int Slot);

public sealed record ReviewSlotSelection(int DayOfWeek, int Slot);
public sealed record SaveStudentAvailabilityRequest(int RoundId, IReadOnlyCollection<ReviewSlotSelection> Slots);

public sealed record StudentScheduleResponse(
    int SessionId,
    string SessionCode,
    ReviewType Type,
    DateTime SessionDate,
    int Slot,
    string Room,
    ReviewSessionStatus Status,
    ReviewGroupStatus GroupStatus,
    DateTime? CompletedAt);

public sealed record StudentAttendanceResponse(
    int SessionId,
    string SessionCode,
    bool IsPresent,
    string? Note,
    DateTime MarkedAt);

public sealed record ReviewCommentResponse(
    int Id,
    int AuthorUserId,
    string AuthorName,
    string Content,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
