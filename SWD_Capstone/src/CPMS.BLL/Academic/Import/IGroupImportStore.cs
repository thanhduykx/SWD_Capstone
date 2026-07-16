using CPMS.BLL.Enums;

namespace CPMS.BLL.Academic.Import;

public sealed record ImportUserState(int Id, UserRole Role);
public sealed record ImportStudentState(int Id, int UserId);
public sealed record CreatedImportAccount(int UserId, string Username, string Email, string FullName);
public interface IGroupImportStore
{
    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken);
    Task<bool> LecturerExistsAsync(int lecturerId, CancellationToken cancellationToken);
    Task<int> FindDefaultLecturerIdAsync(CancellationToken cancellationToken);
    Task<(int Id, bool Created)> UpsertTopicAsync(
        int semesterId, string code, string title, CancellationToken cancellationToken);
    Task<(int Id, bool Created)> UpsertGroupAsync(
        int semesterId, string code, int topicId, int? lecturerId, CancellationToken cancellationToken);
    Task<ImportUserState?> FindUserAsync(
        string username, string email, CancellationToken cancellationToken);
    Task<ImportUserState> CreateStudentUserAsync(
        string username, string email, string passwordHash, CancellationToken cancellationToken);
    Task<ImportStudentState?> FindStudentAsync(string studentCode, CancellationToken cancellationToken);
    Task<bool> UpsertStudentAsync(
        int userId,
        string studentCode,
        string fullName,
        int groupId,
        bool isLeader,
        CancellationToken cancellationToken);
}
