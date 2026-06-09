using System.Globalization;
using CPMS.Core.Entities;
using CPMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CPMS.Api.Services;

public sealed class SemesterResolverService(CpmsDbContext dbContext)
{
    public async Task<Semester> ResolveForDateAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var term = ResolveTerm(date);
        var semester = await dbContext.Semesters
            .SingleOrDefaultAsync(x => x.Code == term.Code, cancellationToken);
        if (semester is not null)
        {
            var changed = false;
            if (semester.Name != term.Name)
            {
                semester.Name = term.Name;
                changed = true;
            }

            if (semester.AcademicYear != term.AcademicYear)
            {
                semester.AcademicYear = term.AcademicYear;
                changed = true;
            }

            if (semester.StartDate != term.StartDate || semester.EndDate != term.EndDate)
            {
                semester.StartDate = term.StartDate;
                semester.EndDate = term.EndDate;
                changed = true;
            }

            if (term.IsCurrentTerm && !semester.IsActive)
            {
                await dbContext.Semesters
                    .Where(x => x.IsActive && x.Id != semester.Id)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsActive, false), cancellationToken);
                semester.IsActive = true;
                changed = true;
            }

            if (changed)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            return semester;
        }

        if (term.IsCurrentTerm)
        {
            await dbContext.Semesters
                .Where(x => x.IsActive)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsActive, false), cancellationToken);
        }

        semester = new Semester
        {
            Code = term.Code,
            Name = term.Name,
            AcademicYear = term.AcademicYear,
            StartDate = term.StartDate,
            EndDate = term.EndDate,
            IsActive = term.IsCurrentTerm
        };
        dbContext.Semesters.Add(semester);
        await dbContext.SaveChangesAsync(cancellationToken);
        return semester;
    }

    private static SemesterTerm ResolveTerm(DateOnly date)
    {
        var currentDate = DateOnly.FromDateTime(DateTime.Today);
        var year = date.Year;
        var month = date.Month;
        var (prefix, name, startMonth, endMonth) = month switch
        {
            >= 1 and <= 4 => ("SP", "Spring", 1, 4),
            >= 5 and <= 8 => ("SU", "Summer", 5, 8),
            _ => ("FA", "Fall", 9, 12)
        };
        var startDate = new DateOnly(year, startMonth, 1);
        var endDate = new DateOnly(year, endMonth, DateTime.DaysInMonth(year, endMonth));

        return new SemesterTerm(
            $"{prefix}{year}",
            $"{name} {year}",
            year.ToString(CultureInfo.InvariantCulture),
            startDate,
            endDate,
            currentDate >= startDate && currentDate <= endDate);
    }

    private sealed record SemesterTerm(
        string Code,
        string Name,
        string AcademicYear,
        DateOnly StartDate,
        DateOnly EndDate,
        bool IsCurrentTerm);
}
