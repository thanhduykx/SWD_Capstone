using CPMS.BLL.Enums;

namespace CPMS.API.Contracts.Academic;

public sealed record CreateSemesterRequest(
    string Code,
    string Name,
    string AcademicYear,
    [property: JsonRequired] DateOnly StartDate,
    [property: JsonRequired] DateOnly EndDate,
    [property: JsonRequired] bool IsActive);
public sealed record SemesterResponse(
    int Id, string Code, string Name, string AcademicYear, DateOnly StartDate, DateOnly EndDate, bool IsActive);
public sealed record SemesterGroupResponse(
    int Id, string Code, string TopicName, string SupervisorName, GroupStatus Status,
    IReadOnlyList<GroupMemberResponse> Members);
public sealed record GroupMemberResponse(
    int Id, int UserId, string Code, string FullName, string ClassCode, string Major, string Email, bool IsLeader);
