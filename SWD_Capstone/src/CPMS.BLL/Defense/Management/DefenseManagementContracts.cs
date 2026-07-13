using CPMS.Core.Enums;
using CPMS.Shared.Models;

namespace CPMS.BLL.Defense.Management;

public sealed record DefenseActor(int UserId, bool AllowModeratorFallback);
public sealed record DefenseRoundResult(
    int Id, string Code, string Name, int SemesterId, CouncilType Type,
    DateOnly StartDate, DateOnly EndDate, DefenseRoundStatus Status);
public sealed record CreateDefenseRoundCommand(
    string Code, string Name, int SemesterId, CouncilType Type, DateOnly StartDate, DateOnly EndDate);
public sealed record CreateDefenseBoardCommand(
    string Code, int SemesterId, CouncilType Type, int ChairmanId, int SecretaryId,
    IReadOnlyCollection<int> MemberLecturerIds);
public sealed record DefenseBoardResult(
    int Id, string Code, int SemesterId, CouncilType Type, CouncilStatus Status,
    int ChairmanId, int SecretaryId, IReadOnlyList<int> MemberLecturerIds);
public sealed record AssignDefenseSessionCommand(
    string Code, int DefenseRoundId, int CouncilId, int GroupId,
    DateTime SessionDate, int Slot, string Room);
public sealed record DefenseSessionAssignmentResult(
    int Id, string Code, int DefenseRoundId, int CouncilId, string CouncilCode,
    int GroupId, string GroupCode, DateTime SessionDate, int Slot, string Room,
    DateTime? StartedAt, DateTime? EndedAt, bool IsLocked);
public interface IDefenseManagementService
{
    Task<PagedResult<DefenseRoundResult>> GetRoundsAsync(PagedRequest request, CancellationToken cancellationToken);
    Task<DefenseRoundResult> CreateRoundAsync(
        CreateDefenseRoundCommand command, DefenseActor actor, CancellationToken cancellationToken);
    Task<DefenseBoardResult> CreateBoardAsync(
        CreateDefenseBoardCommand command, DefenseActor actor, CancellationToken cancellationToken);
    Task AddBoardMemberAsync(
        int councilId, int lecturerId, CouncilMemberRole role, CancellationToken cancellationToken);
    Task<DefenseSessionAssignmentResult> AssignSessionAsync(
        AssignDefenseSessionCommand command, DefenseActor actor, CancellationToken cancellationToken);
    Task<IReadOnlyList<DefenseSessionAssignmentResult>> GetLecturerSessionsAsync(
        int userId, CancellationToken cancellationToken);
}
