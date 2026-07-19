namespace CPMS.BLL.Academic;

public sealed record SemesterQuery(
    int Page, int PageSize, string? Search, string? FilterBy, string? FilterValue, string? SortBy, bool Desc);
public sealed record SemesterTermData(
    string Code, string Name, string AcademicYear, DateOnly StartDate, DateOnly EndDate, bool IsActive);
public interface ISemesterStore
{
    Task<(IReadOnlyList<SemesterResult> Items, int Total)> GetAllAsync(
        SemesterQuery request, CancellationToken cancellationToken);
    Task<SemesterResult?> FindByCodeAsync(string code, CancellationToken cancellationToken);
    Task<SemesterResult> UpsertResolvedAsync(SemesterTermData term, CancellationToken cancellationToken);
    Task<SemesterResult> CreateAsync(SemesterTermData input, CancellationToken cancellationToken);
    Task<SemesterResult> ToggleStatusAsync(int semesterId, CancellationToken cancellationToken);
    Task DeleteAsync(int semesterId, CancellationToken cancellationToken);
    Task<IReadOnlyList<SemesterGroupResult>> GetGroupsAsync(int semesterId, CancellationToken cancellationToken);
}
