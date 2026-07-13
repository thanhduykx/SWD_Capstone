using CPMS.Core.Enums;

namespace CPMS.API.Contracts.Reviews;

public sealed record ReviewSchedulingBoardResponse(
    int SemesterId,
    ReviewType ReviewType,
    DateOnly WeekStart,
    IReadOnlyCollection<ReviewSchedulingLecturerResponse> Lecturers,
    IReadOnlyCollection<ReviewSchedulingAvailabilityResponse> Availability,
    IReadOnlyCollection<ReviewSchedulingAvailabilitySubmissionResponse> AvailabilitySubmissions,
    IReadOnlyCollection<ReviewSchedulingGroupResponse> Groups,
    IReadOnlyCollection<ReviewSchedulingSessionResponse> Sessions);
public sealed record ReviewSchedulingLecturerResponse(int Id, string Code, string FullName, string Department, string Email);
public sealed record ReviewSchedulingAvailabilityResponse(int LecturerId, int DayOfWeek, int Slot);
public sealed record ReviewSchedulingAvailabilitySubmissionResponse(int LecturerId, bool IsSubmitted, DateTime? SubmittedAt, int SlotCount);
public sealed record ReviewSchedulingGroupResponse(int Id, string Code, string ProjectName, int SupervisorId, string SupervisorCode, int StudentCount);
public sealed record ReviewSchedulingSessionResponse(
    int Id,
    string Code,
    int GroupId,
    string GroupCode,
    ReviewType Type,
    ReviewSessionStatus Status,
    IReadOnlyCollection<int> ReviewerIds,
    DateTime SessionDate,
    int DayOfWeek,
    int Slot,
    string Room);
public sealed record RandomAssignReviewSessionsRequest(
    int SemesterId,
    ReviewType ReviewType,
    DateOnly WeekStart,
    int ReviewersPerSession,
    string? RoomPrefix,
    int? Seed);
public sealed record RandomAssignReviewSessionsResponse(
    int TotalCandidateGroups,
    int AssignedCount,
    int SentEmailCount,
    int FailedEmailCount,
    IReadOnlyCollection<RandomAssignUnassignedGroupResponse> UnassignedGroups,
    IReadOnlyCollection<ReviewSchedulingSessionResponse> Sessions);
public sealed record RandomAssignUnassignedGroupResponse(int GroupId, string GroupCode, string Reason);
public sealed record ReviewRoundManagementResponse(
    int Id,
    int SemesterId,
    ReviewType Type,
    ReviewRoundStatus Status,
    DateOnly WeekStartDate,
    DateOnly WeekEndDate,
    DateTime CreatedAt,
    DateTime? OpenedAt,
    DateTime? ClosedAt,
    int RegistrationCount);
public sealed record CreateReviewRoundRequest(int SemesterId, ReviewType ReviewType, DateOnly WeekStartDate);
public sealed record UpdateRoundStatusRequest(ReviewRoundStatus Status);
