using CPMS.BLL.Enums;

namespace CPMS.API.Contracts.Defense;

public sealed record CreateDefenseRoundRequest(
    string Code,
    string Name,
    [property: JsonRequired] int SemesterId,
    [property: JsonRequired] CouncilType Type,
    [property: JsonRequired] DateOnly StartDate,
    [property: JsonRequired] DateOnly EndDate);
public sealed record DefenseRoundResponse(
    int Id, string Code, string Name, int SemesterId, CouncilType Type,
    DateOnly StartDate, DateOnly EndDate, DefenseRoundStatus Status);
public sealed record CreateDefenseBoardRequest(
    string Code,
    [property: JsonRequired] int SemesterId,
    [property: JsonRequired] CouncilType Type,
    [property: JsonRequired] int ChairmanId,
    [property: JsonRequired] int SecretaryId,
    IReadOnlyCollection<int> MemberLecturerIds);
public sealed record AddDefenseBoardMemberRequest(
    [property: JsonRequired] int LecturerId,
    [property: JsonRequired] CouncilMemberRole Role);
public sealed record DefenseBoardResponse(
    int Id, string Code, int SemesterId, CouncilType Type, CouncilStatus Status,
    int ChairmanId, int SecretaryId, IReadOnlyCollection<int> MemberLecturerIds);
public sealed record AssignDefenseSessionRequest(
    string Code,
    [property: JsonRequired] int DefenseRoundId,
    [property: JsonRequired] int CouncilId,
    [property: JsonRequired] int GroupId,
    [property: JsonRequired] DateTime SessionDate,
    [property: JsonRequired] int Slot,
    string Room);
public sealed record DefenseSessionAssignmentResponse(
    int Id, string Code, int DefenseRoundId, int CouncilId, string CouncilCode,
    int GroupId, string GroupCode, DateTime SessionDate, int Slot, string Room,
    DateTime? StartedAt, DateTime? EndedAt, bool IsLocked);
