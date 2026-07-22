using System.Globalization;
using CPMS.BLL.Exceptions;
using CPMS.BLL.Models;

namespace CPMS.BLL.Academic;

public sealed class SemesterService(ISemesterStore store) : ISemesterService
{
    public async Task<PagedResult<SemesterResult>> GetAllAsync(
        PagedRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await ResolveAsync(DateOnly.FromDateTime(DateTime.Today), cancellationToken);
        (IReadOnlyList<SemesterResult> Items, int Total) result = await store.GetAllAsync(new SemesterQuery(
            request.SafePage, request.SafePageSize, request.Search?.Trim(), request.FilterBy?.Trim(),
            request.FilterValue?.Trim(), request.SortBy?.Trim(), request.Desc), cancellationToken);
        return new PagedResult<SemesterResult>(
            result.Items, request.SafePage, request.SafePageSize, result.Total);
    }

    public Task<SemesterResult> ResolveAsync(DateOnly date, CancellationToken cancellationToken) =>
        store.UpsertResolvedAsync(ResolveTerm(date), cancellationToken);

    public Task<SemesterResult?> FindByIdAsync(int semesterId, CancellationToken cancellationToken) =>
        store.FindByIdAsync(semesterId, cancellationToken);

    public Task<SemesterResult> CreateAsync(
        CreateSemesterCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.EndDate < command.StartDate)
            throw new BusinessRuleException("Semester end date must not be before start date.");
        if (string.IsNullOrWhiteSpace(command.Code) || string.IsNullOrWhiteSpace(command.Name) ||
            string.IsNullOrWhiteSpace(command.AcademicYear))
            throw new BusinessRuleException("Code, name, and academic year are required.");
        if (command.IsActive && !ContainsDate(command.StartDate, command.EndDate, CurrentVietnamDate()))
            throw new BusinessRuleException(
                "Không thể kích hoạt kỳ học ngoài thời gian bắt đầu và kết thúc của kỳ.");
        return store.CreateAsync(new SemesterTermData(
            command.Code.Trim().ToUpperInvariant(), command.Name.Trim(), command.AcademicYear.Trim(),
            command.StartDate, command.EndDate, command.IsActive), cancellationToken);
    }

    public async Task<SemesterResult> ToggleStatusAsync(int semesterId, CancellationToken cancellationToken)
    {
        SemesterResult semester = await store.FindByIdAsync(semesterId, cancellationToken)
            ?? throw new InvalidOperationException("Semester not found.");
        DateOnly today = CurrentVietnamDate();
        if (!semester.IsActive && !ContainsDate(semester.StartDate, semester.EndDate, today))
            throw new BusinessRuleException(
                $"Không thể mở kỳ {semester.Code}. Ngày hiện tại {today:dd/MM/yyyy} không nằm trong thời gian " +
                $"{semester.StartDate:dd/MM/yyyy} - {semester.EndDate:dd/MM/yyyy}.");
        return await store.ToggleStatusAsync(semesterId, cancellationToken);
    }

    public Task DeleteAsync(int semesterId, CancellationToken cancellationToken) =>
        store.DeleteAsync(semesterId, cancellationToken);

    public Task<IReadOnlyList<SemesterGroupResult>> GetGroupsAsync(
        int semesterId, CancellationToken cancellationToken) =>
        store.GetGroupsAsync(semesterId, cancellationToken);

    private static SemesterTermData ResolveTerm(DateOnly date)
    {
        var year = date.Year;
        (string? prefix, string? name, int startMonth, int endMonth) = date.Month switch
        {
            >= 1 and <= 4 => ("SP", "Spring", 1, 4),
            >= 5 and <= 8 => ("SU", "Summer", 5, 8),
            _ => ("FA", "Fall", 9, 12)
        };
        var start = new DateOnly(year, startMonth, 1);
        var end = new DateOnly(year, endMonth, DateTime.DaysInMonth(year, endMonth));
        var today = DateOnly.FromDateTime(DateTime.Today);
        return new SemesterTermData(
            $"{prefix}{year % 100:D2}", $"{name} {year}", year.ToString(CultureInfo.InvariantCulture),
            start, end, today >= start && today <= end);
    }

    private static DateOnly CurrentVietnamDate() => DateOnly.FromDateTime(
        DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).DateTime);

    private static bool ContainsDate(DateOnly startDate, DateOnly endDate, DateOnly date) =>
        date >= startDate && date <= endDate;
}
