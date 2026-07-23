using CPMS.BLL.Enums;
using CPMS.BLL.Models;

namespace CPMS.BLL.Defense.Management;

public sealed record DefenseRoundState(
    int Id, int SemesterId, CouncilType Type, DateOnly StartDate, DateOnly EndDate);
public sealed record DefenseCouncilState(int Id, string Code, int SemesterId, CouncilType Type);
public sealed record DefenseGroupState(int Id, string Code, int SemesterId);
public interface IDefenseManagementStore
{
    Task<PagedResult<DefenseRoundResult>> GetRoundsAsync(PagedRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<DefenseBoardResult>> GetBoardsAsync(
        int semesterId, CancellationToken cancellationToken);
    Task<int?> FindModeratorIdAsync(int userId, bool allowFallback, CancellationToken cancellationToken);
    Task<bool> SemesterExistsAsync(int semesterId, CancellationToken cancellationToken);
    Task<bool> RoundCodeExistsAsync(int semesterId, string code, CancellationToken cancellationToken);
    Task<DefenseRoundResult> CreateRoundAsync(
        CreateDefenseRoundCommand input, int moderatorId, CancellationToken cancellationToken);
    Task<int> CountExistingLecturersAsync(IReadOnlyCollection<int> lecturerIds, CancellationToken cancellationToken);
    Task<DefenseBoardResult> CreateBoardAsync(
        CreateDefenseBoardCommand input, int moderatorId, CancellationToken cancellationToken);
    Task<bool> CouncilExistsAsync(int councilId, CancellationToken cancellationToken);
    Task AddCouncilMemberIfMissingAsync(
        int councilId, int lecturerId, CouncilMemberRole role, CancellationToken cancellationToken);
    Task<DefenseRoundState?> FindRoundAsync(int roundId, CancellationToken cancellationToken);
    Task<DefenseCouncilState?> FindCouncilAsync(int councilId, CancellationToken cancellationToken);
    Task<DefenseGroupState?> FindGroupAsync(int groupId, CancellationToken cancellationToken);
    Task<DefenseSessionAssignmentResult> CreateSessionAsync(
        AssignDefenseSessionCommand input, int moderatorId,
        DefenseCouncilState council, DefenseGroupState group, CancellationToken cancellationToken);
    Task<int?> FindLecturerIdAsync(int userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<DefenseSessionAssignmentResult>> GetLecturerSessionsAsync(
        int lecturerId, CancellationToken cancellationToken);
}
