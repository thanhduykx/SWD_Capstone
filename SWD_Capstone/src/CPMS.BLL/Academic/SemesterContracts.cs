using CPMS.BLL.Enums;
using CPMS.BLL.Models;

namespace CPMS.BLL.Academic;

public sealed record SemesterResult(
    int Id, string Code, string Name, string AcademicYear, DateOnly StartDate, DateOnly EndDate, bool IsActive);
public sealed record CreateSemesterCommand(
    string Code, string Name, string AcademicYear, DateOnly StartDate, DateOnly EndDate, bool IsActive);
public sealed record SemesterGroupResult(
    int Id, string Code, string TopicName, string SupervisorName, GroupStatus Status,
    IReadOnlyList<SemesterGroupMemberResult> Members);
public sealed record SemesterGroupMemberResult(
    int Id, string Code, string FullName, string ClassCode, string Major, string Email, bool IsLeader);
public interface ISemesterService
{
    Task<PagedResult<SemesterResult>> GetAllAsync(PagedRequest request, CancellationToken cancellationToken);
    Task<SemesterResult> ResolveAsync(DateOnly date, CancellationToken cancellationToken);
    Task<SemesterResult> CreateAsync(CreateSemesterCommand command, CancellationToken cancellationToken);
    Task<SemesterResult> ToggleStatusAsync(int semesterId, CancellationToken cancellationToken);
    Task DeleteAsync(int semesterId, CancellationToken cancellationToken);
    Task<IReadOnlyList<SemesterGroupResult>> GetGroupsAsync(int semesterId, CancellationToken cancellationToken);
}
