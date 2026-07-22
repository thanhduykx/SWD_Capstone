using CPMS.BLL.Documents;
using CPMS.BLL.Enums;
using CPMS.DAL.Data;
using CPMS.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace CPMS.DAL.Repositories;

internal sealed class ProjectDocumentStore(CpmsDbContext db) : IProjectDocumentStore
{
    public Task<bool> IsStudentInGroupAsync(int userId, int groupId, CancellationToken cancellationToken) =>
        db.Students.AnyAsync(x => x.UserId == userId && x.GroupId == groupId, cancellationToken);

    public async Task<bool> CanLecturerAccessGroupAsync(int userId, int groupId, CancellationToken cancellationToken)
    {
        var lecturerId = await db.Lecturers.Where(x => x.UserId == userId).Select(x => (int?)x.Id).SingleOrDefaultAsync(cancellationToken);
        return lecturerId is not null && (await db.CapstoneGroups.AnyAsync(x => x.Id == groupId && x.LecturerId == lecturerId, cancellationToken)
            || await db.GroupReviewSlots.AnyAsync(slot => slot.GroupId == groupId && db.ReviewSessions.Any(session =>
                session.Id == slot.SessionId && (session.Reviewer1Id == lecturerId || session.Reviewer2Id == lecturerId)), cancellationToken));
    }

    public Task<int?> FindActiveSyllabusIdAsync(CancellationToken cancellationToken) =>
        db.Syllabuses.Where(x => x.IsActive).Select(x => (int?)x.Id).FirstOrDefaultAsync(cancellationToken);

    public async Task<int> GetNextVersionAsync(int groupId, CancellationToken cancellationToken) =>
        await db.Documents.CountAsync(x => x.GroupId == groupId, cancellationToken) + 1;

    public async Task<IReadOnlyList<ProjectDocumentData>> ListAsync(int groupId, CancellationToken cancellationToken) =>
        await DocumentHistoryQuery().Where(x => x.GroupId == groupId)
            .OrderByDescending(x => x.UploadedAt).ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

    public async Task<ProjectDocumentData?> FindAsync(int documentId, CancellationToken cancellationToken) =>
        await DocumentHistoryQuery().SingleOrDefaultAsync(x => x.Id == documentId, cancellationToken);

    public async Task<ProjectDocumentData> CreateAsync(CreateProjectDocumentData input, CancellationToken cancellationToken)
    {
        var entity = new CapstoneDocument
        {
            GroupId = input.GroupId, SyllabusId = input.SyllabusId, UploadedById = input.UploadedById,
            DocType = input.DocType, FileName = input.FileName, FilePath = input.StorageKey,
            FileSize = input.FileSize, VersionNo = input.VersionNo
        };
        db.Documents.Add(entity); await db.SaveChangesAsync(cancellationToken);
        return await FindAsync(entity.Id, cancellationToken)
            ?? throw new InvalidOperationException("The uploaded document could not be reloaded.");
    }

    public Task<string> GetProjectNameAsync(int groupId, CancellationToken cancellationToken) =>
        db.CapstoneGroups.Where(x => x.Id == groupId)
            .Select(x => db.Topics.Where(topic => topic.Id == x.TopicId).Select(topic => topic.NameVn).FirstOrDefault() ?? x.Code)
            .SingleAsync(cancellationToken);

    private IQueryable<ProjectDocumentData> DocumentHistoryQuery() =>
        db.Documents.AsNoTracking().Select(x => new ProjectDocumentData(
            x.Id, x.GroupId, x.DocType, x.FileName, x.FilePath, x.FileSize,
            x.VersionNo, x.Status, x.UploadedAt, x.UploadedById,
            db.Students.Where(student => student.UserId == x.UploadedById)
                .Select(student => student.FullName).FirstOrDefault()
            ?? db.Lecturers.Where(lecturer => lecturer.UserId == x.UploadedById)
                .Select(lecturer => lecturer.FullName).FirstOrDefault()
            ?? db.Users.Where(user => user.Id == x.UploadedById)
                .Select(user => user.Username).First()));
}
