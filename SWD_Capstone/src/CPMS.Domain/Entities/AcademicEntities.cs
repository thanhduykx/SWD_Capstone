using CPMS.Core.Common;
using CPMS.Core.Enums;

namespace CPMS.Core.Entities;

public sealed class Semester
{
    public int Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required string AcademicYear { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public bool IsActive { get; set; }
}

public sealed class Topic
{
    public int Id { get; set; }
    public required string Code { get; set; }
    public required string NameVn { get; set; }
    public required string NameEn { get; set; }
    public int SemesterId { get; set; }
}

public sealed class CapstoneGroup
{
    public int Id { get; set; }
    public required string Code { get; set; }
    public int TopicId { get; set; }
    public int SemesterId { get; set; }
    public int LecturerId { get; set; }
    public GroupStatus Status { get; set; } = GroupStatus.Active;
}

public sealed class Syllabus : AuditableEntity
{
    public int Id { get; set; }
    public int TrainingDepartmentId { get; set; }
    public required string Name { get; set; }
    public required string Major { get; set; }
    public required string AcademicYear { get; set; }
    public bool IsActive { get; set; }
}

public sealed class Clo
{
    public int Id { get; set; }
    public int SyllabusId { get; set; }
    public required string Code { get; set; }
    public required string Description { get; set; }
    public decimal Weight { get; set; }
}

public sealed class RuleKeyword : AuditableEntity
{
    public int Id { get; set; }
    public int CloId { get; set; }
    public int CreatedByAdminId { get; set; }
    public required string KeywordText { get; set; }
    public decimal Weight { get; set; }
}
