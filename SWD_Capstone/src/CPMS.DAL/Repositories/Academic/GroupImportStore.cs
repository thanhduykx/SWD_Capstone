using CPMS.BLL.Academic.Import;
using CPMS.DAL.Entities;
using CPMS.BLL.Enums;
using CPMS.DAL.Data;
using Microsoft.EntityFrameworkCore;

namespace CPMS.DAL.Repositories.Academic;

public sealed class GroupImportStore(CpmsDbContext dbContext) : IGroupImportStore
{
    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var result = await operation(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }
    public Task<bool> LecturerExistsAsync(int lecturerId, CancellationToken cancellationToken) =>
        dbContext.Lecturers.AnyAsync(x => x.Id == lecturerId, cancellationToken);
    public Task<int> FindDefaultLecturerIdAsync(CancellationToken cancellationToken) =>
        dbContext.Lecturers.OrderBy(x => x.Id).Select(x => x.Id).FirstOrDefaultAsync(cancellationToken);
    public async Task<(int Id, bool Created)> UpsertTopicAsync(
        int semesterId, string code, string title, CancellationToken cancellationToken)
    {
        var topic = await dbContext.Topics.FirstOrDefaultAsync(x =>
            x.SemesterId == semesterId && x.Code == code, cancellationToken);
        var created = topic is null;
        if (topic is null)
        {
            topic = new Topic { Code = code, SemesterId = semesterId, NameVn = title, NameEn = title };
            dbContext.Topics.Add(topic);
        }
        topic.NameVn = title;
        topic.NameEn = title;
        await dbContext.SaveChangesAsync(cancellationToken);
        return (topic.Id, created);
    }
    public async Task<(int Id, bool Created)> UpsertGroupAsync(
        int semesterId, string code, int topicId, int? lecturerId, CancellationToken cancellationToken)
    {
        var group = await dbContext.CapstoneGroups.FirstOrDefaultAsync(x =>
            x.SemesterId == semesterId && x.Code == code, cancellationToken);
        var created = group is null;
        if (group is null)
        {
            group = new CapstoneGroup { Code = code, SemesterId = semesterId };
            dbContext.CapstoneGroups.Add(group);
        }
        group.TopicId = topicId;
        group.LecturerId = lecturerId;
        group.Status = GroupStatus.Active;
        await dbContext.SaveChangesAsync(cancellationToken);
        return (group.Id, created);
    }
    public Task<ImportUserState?> FindUserAsync(
        string username, string email, CancellationToken cancellationToken) =>
        dbContext.Users.Where(x => x.Username == username || x.Email == email)
            .Select(x => new ImportUserState(x.Id, x.Role)).FirstOrDefaultAsync(cancellationToken);
    public async Task<ImportUserState> CreateStudentUserAsync(
        string username, string email, string passwordHash, CancellationToken cancellationToken)
    {
        var user = new User
        {
            Username = username, Email = email, PasswordHash = passwordHash,
            Role = UserRole.Student, IsActive = true
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new ImportUserState(user.Id, user.Role);
    }
    public Task<ImportStudentState?> FindStudentAsync(
        string studentCode, CancellationToken cancellationToken) =>
        dbContext.Students.Where(x => x.Code == studentCode)
            .Select(x => new ImportStudentState(x.Id, x.UserId)).FirstOrDefaultAsync(cancellationToken);
    public async Task<bool> UpsertStudentAsync(
        int userId,
        string studentCode,
        string fullName,
        int groupId,
        bool isLeader,
        CancellationToken cancellationToken)
    {
        var student = await dbContext.Students.FirstOrDefaultAsync(
            x => x.Code == studentCode, cancellationToken);
        var created = student is null;
        if (student is null)
        {
            student = new Student
            {
                UserId = userId, Code = studentCode, FullName = fullName,
                ClassCode = "EXE101", Major = "Unspecified"
            };
            dbContext.Students.Add(student);
        }
        student.FullName = fullName;
        student.GroupId = groupId;
        student.IsLeader = isLeader;
        await dbContext.SaveChangesAsync(cancellationToken);
        return created;
    }
}
