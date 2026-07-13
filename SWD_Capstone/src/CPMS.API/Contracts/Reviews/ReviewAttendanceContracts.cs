namespace CPMS.API.Contracts.Reviews;

public sealed record ReviewAttendanceListResponse(
    int SessionId,
    string SessionCode,
    int GroupId,
    string GroupCode,
    DateTime SessionDate,
    int Slot,
    string Room,
    IReadOnlyCollection<AttendanceStudentResponse> Students);
public sealed record AttendanceStudentResponse(
    int StudentId, string StudentCode, string FullName, bool? IsPresent, string? Note);
public sealed record SubmitAttendanceRequest(int GroupId, IReadOnlyCollection<AttendanceEntryRequest> Entries);
public sealed record AttendanceEntryRequest(int StudentId, bool IsPresent, string? Note);
public sealed record AddCommentRequest(int GroupId, string Content);
