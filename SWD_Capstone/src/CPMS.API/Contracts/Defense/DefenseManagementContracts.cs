using CPMS.Core.Enums;

namespace CPMS.API.Contracts.Defense;

public sealed record CreateDefenseRoundRequest(
    string Code, string Name, int SemesterId, CouncilType Type, DateOnly StartDate, DateOnly EndDate);
public sealed record DefenseRoundResponse(
    int Id, string Code, string Name, int SemesterId, CouncilType Type,
    DateOnly StartDate, DateOnly EndDate, DefenseRoundStatus Status);
public sealed record CreateDefenseBoardRequest(
    string Code, int SemesterId, CouncilType Type, int ChairmanId, int SecretaryId,
    IReadOnlyCollection<int> MemberLecturerIds);
public sealed record AddDefenseBoardMemberRequest(int LecturerId, CouncilMemberRole Role);
public sealed record DefenseBoardResponse(
    int Id, string Code, int SemesterId, CouncilType Type, CouncilStatus Status,
    int ChairmanId, int SecretaryId, IReadOnlyCollection<int> MemberLecturerIds);
public sealed record AssignDefenseSessionRequest(
    string Code, int DefenseRoundId, int CouncilId, int GroupId,
    DateTime SessionDate, int Slot, string Room);
public sealed record DefenseSessionAssignmentResponse(
    int Id, string Code, int DefenseRoundId, int CouncilId, string CouncilCode,
    int GroupId, string GroupCode, DateTime SessionDate, int Slot, string Room,
    DateTime? StartedAt, DateTime? EndedAt, bool IsLocked);
