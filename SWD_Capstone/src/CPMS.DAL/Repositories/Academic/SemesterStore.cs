using CPMS.BLL.Academic;
using CPMS.DAL.Entities;
using CPMS.DAL.Data;
using Microsoft.EntityFrameworkCore;

namespace CPMS.DAL.Repositories.Academic;

public sealed class SemesterStore(CpmsDbContext dbContext) : ISemesterStore
{
    public async Task<(IReadOnlyList<SemesterResult> Items, int Total)> GetAllAsync(
        SemesterQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        IQueryable<Semester> query = ApplyFilters(dbContext.Semesters.AsNoTracking(), request);
        query = ApplySort(query, request);
        var total = await query.CountAsync(cancellationToken);
        List<SemesterResult> items = await query.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(x => MapProjection(x)).ToListAsync(cancellationToken);
        return (items, total);
    }

    private static IQueryable<Semester> ApplyFilters(IQueryable<Semester> query, SemesterQuery request)
    {
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var searchPattern = PostgreSqlSearchPattern.Contains(request.Search);
            query = query.Where(x =>
                EF.Functions.ILike(x.Code, searchPattern, PostgreSqlSearchPattern.EscapeCharacter) ||
                EF.Functions.ILike(x.Name, searchPattern, PostgreSqlSearchPattern.EscapeCharacter) ||
                EF.Functions.ILike(x.AcademicYear, searchPattern, PostgreSqlSearchPattern.EscapeCharacter));
        }
        if (!string.IsNullOrWhiteSpace(request.FilterBy) && !string.IsNullOrWhiteSpace(request.FilterValue))
        {
            query = request.FilterBy.ToLowerInvariant() switch
            {
                "isactive" when bool.TryParse(request.FilterValue, out var active) => query.Where(x => x.IsActive == active),
                "academicyear" => query.Where(x => x.AcademicYear == request.FilterValue),
                _ => query
            };
        }
        return query;
    }

    private static IQueryable<Semester> ApplySort(IQueryable<Semester> query, SemesterQuery request) =>
        request.SortBy?.ToLowerInvariant() switch
        {
            "code" => request.Desc ? query.OrderByDescending(x => x.Code) : query.OrderBy(x => x.Code),
            "name" => request.Desc ? query.OrderByDescending(x => x.Name) : query.OrderBy(x => x.Name),
            "academicyear" => request.Desc ? query.OrderByDescending(x => x.AcademicYear) : query.OrderBy(x => x.AcademicYear),
            "enddate" => request.Desc ? query.OrderByDescending(x => x.EndDate) : query.OrderBy(x => x.EndDate),
            "isactive" => request.Desc ? query.OrderByDescending(x => x.IsActive) : query.OrderBy(x => x.IsActive),
            _ => request.Desc ? query.OrderByDescending(x => x.StartDate) : query.OrderBy(x => x.StartDate)
        };

    public Task<SemesterResult?> FindByCodeAsync(string code, CancellationToken cancellationToken) =>
        dbContext.Semesters.Where(x => x.Code == code).Select(x => MapProjection(x))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<SemesterResult?> FindByIdAsync(int semesterId, CancellationToken cancellationToken) =>
        dbContext.Semesters.Where(x => x.Id == semesterId).Select(x => MapProjection(x))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<SemesterResult> UpsertResolvedAsync(
        SemesterTermData term, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(term);
        Semester? semester = await dbContext.Semesters.SingleOrDefaultAsync(x => x.Code == term.Code, cancellationToken);
        if (term.IsActive)
            await dbContext.Semesters.Where(x => x.IsActive && (semester == null || x.Id != semester.Id))
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsActive, false), cancellationToken);
        if (semester is null)
        {
            semester = NewEntity(term);
            dbContext.Semesters.Add(semester);
        }
        else
        {
            semester.Name = term.Name;
            semester.AcademicYear = term.AcademicYear;
            semester.StartDate = term.StartDate;
            semester.EndDate = term.EndDate;
            if (term.IsActive) semester.IsActive = true;
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(semester);
    }

    public async Task<SemesterResult> CreateAsync(
        SemesterTermData input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.IsActive)
            await dbContext.Semesters.Where(x => x.IsActive)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsActive, false), cancellationToken);
        Semester semester = NewEntity(input);
        dbContext.Semesters.Add(semester);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(semester);
    }

    public async Task<SemesterResult> ToggleStatusAsync(
        int semesterId, CancellationToken cancellationToken)
    {
        Semester semester = await dbContext.Semesters.SingleAsync(x => x.Id == semesterId, cancellationToken);
        semester.IsActive = !semester.IsActive;
        if (semester.IsActive)
            await dbContext.Semesters.Where(x => x.IsActive && x.Id != semesterId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsActive, false), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(semester);
    }

    public async Task DeleteAsync(int semesterId, CancellationToken cancellationToken)
    {
        bool hasGroups = await dbContext.CapstoneGroups.AnyAsync(x => x.SemesterId == semesterId, cancellationToken);
        if (hasGroups) throw new CPMS.BLL.Exceptions.BusinessRuleException("Không thể xóa kỳ học đang có nhóm đồ án (Capstone Groups) tham gia.");
        
        try
        {
            var rows = await dbContext.Semesters.Where(x => x.Id == semesterId).ExecuteDeleteAsync(cancellationToken);
            if (rows == 0) throw new InvalidOperationException("Semester not found.");
        }
        catch (Exception ex) when (ex.Message.Contains("foreign key") || ex.Message.Contains("constraint"))
        {
            throw new CPMS.BLL.Exceptions.BusinessRuleException("Không thể xóa kỳ học này vì đã có dữ liệu ràng buộc (đề tài, lịch review, đợt bảo vệ...) tham chiếu đến.");
        }
    }

    public async Task<IReadOnlyList<SemesterGroupResult>> GetGroupsAsync(
        int semesterId, CancellationToken cancellationToken) =>
        await dbContext.CapstoneGroups.Where(x => x.SemesterId == semesterId).OrderBy(x => x.Code)
            .Select(x => new SemesterGroupResult(
                x.Id, x.Code,
                dbContext.Topics.Where(topic => topic.Id == x.TopicId).Select(topic => topic.NameEn)
                    .SingleOrDefault() ?? "No Topic",
                dbContext.Lecturers.Where(lecturer => x.LecturerId != null && lecturer.Id == x.LecturerId)
                    .Select(lecturer => lecturer.FullName).SingleOrDefault() ?? "No Supervisor",
                x.Status,
                dbContext.Students.Where(student => student.GroupId == x.Id).OrderBy(student => student.Code)
                    .Select(student => new SemesterGroupMemberResult(
                        student.Id, student.UserId, student.Code, student.FullName, student.ClassCode, student.Major,
                        dbContext.Users.Where(user => user.Id == student.UserId).Select(user => user.Email)
                            .FirstOrDefault() ?? string.Empty,
                        student.IsLeader)).ToList()))
            .ToListAsync(cancellationToken);

    private static Semester NewEntity(SemesterTermData x) => new()
    {
        Code = x.Code, Name = x.Name, AcademicYear = x.AcademicYear,
        StartDate = x.StartDate, EndDate = x.EndDate, IsActive = x.IsActive
    };
    private static SemesterResult Map(Semester x) =>
        new(x.Id, x.Code, x.Name, x.AcademicYear, x.StartDate, x.EndDate, x.IsActive);
    private static SemesterResult MapProjection(Semester x) =>
        new(x.Id, x.Code, x.Name, x.AcademicYear, x.StartDate, x.EndDate, x.IsActive);
}
