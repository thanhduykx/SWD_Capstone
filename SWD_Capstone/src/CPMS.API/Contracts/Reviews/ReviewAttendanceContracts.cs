namespace CPMS.API.Contracts.Reviews;

public sealed record ReviewAttendanceListResponse(
    int SessionId,
    string SessionCode,
    int GroupId,
    string GroupCode,
    string GroupStatus,
    DateTime SessionDate,
    int Slot,
    string Room,
    IReadOnlyCollection<AttendanceStudentResponse> Students);
public sealed record AttendanceStudentResponse(
    int StudentId, string StudentCode, string FullName, bool? IsPresent, string? Note, DateTime? StudentConfirmedAt);
public sealed record SubmitAttendanceRequest(
    [property: JsonRequired] int GroupId,
    IReadOnlyCollection<AttendanceEntryRequest> Entries);
public sealed record AttendanceEntryRequest(
    [property: JsonRequired] int StudentId,
    [property: JsonRequired] bool IsPresent,
    string? Note);
public sealed record AddCommentRequest([property: JsonRequired] int GroupId, string Content);
