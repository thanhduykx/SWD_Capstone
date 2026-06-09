using CPMS.Core.Common;
using CPMS.Core.Enums;

namespace CPMS.Core.Entities;

public sealed class User : AuditableEntity
{
    public int Id { get; set; }
    public required string Username { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }
    public int FailedLoginAttempts { get; set; }
    public DateTime? LockedUntil { get; set; }
}

public sealed class RefreshToken
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public required string TokenHash { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? DeviceInfo { get; set; }
}

public sealed class Lecturer
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public required string Code { get; set; }
    public required string FullName { get; set; }
    public required string Department { get; set; }
    public bool IsPartTime { get; set; }
}

public sealed class Student
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public required string Code { get; set; }
    public required string FullName { get; set; }
    public required string ClassCode { get; set; }
    public string? Batch { get; set; }
    public required string Major { get; set; }
    public int? GroupId { get; set; }
}

public sealed class TrainingDepartment
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public required string DepartmentName { get; set; }
    public required string StaffCode { get; set; }
    public required string Position { get; set; }
}

public sealed class SystemAdministrator
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public required string AdminLevel { get; set; }
    public required string PermissionScope { get; set; }
}

public sealed class EvaluationPanel
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public required string FullName { get; set; }
    public required string Department { get; set; }
}
