using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using CPMS.Api.Services;
using CPMS.Core.Entities;
using CPMS.Core.Enums;
using CPMS.Core.Exceptions;
using CPMS.Core.Services;
using CPMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CPMS.Api.Services;

public sealed class ExcelImportService(
    CpmsDbContext dbContext,
    SemesterResolverService semesterResolver,
    IReviewEmailSender emailSender)
{
    private const string TemporaryPassword = "Abcd@1234";
    private static readonly string PrecomputedPasswordHash = BCrypt.Net.BCrypt.HashPassword(TemporaryPassword, workFactor: 12);

    public async Task<ExcelImportResult> ImportSelectedGroupColumnsAsync(
        Stream fileStream,
        int lecturerId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fileStream);

        if (lecturerId <= 0 || !await dbContext.Lecturers.AnyAsync(x => x.Id == lecturerId, cancellationToken))
        {
            throw new BusinessRuleException($"Lecturer {lecturerId} does not exist.");
        }

        var activeSemester = await semesterResolver.ResolveForDateAsync(DateOnly.FromDateTime(DateTime.Today), cancellationToken);
        var semesterId = activeSemester.Id;

        using var cleanedStream = RemoveDataValidation(fileStream);
        using var workbook = new XLWorkbook(cleanedStream);
        var worksheet = workbook.Worksheets.FirstOrDefault()
            ?? throw new BusinessRuleException("The uploaded Excel workbook contains no worksheets.");
        var importedGroups = ExcelGroupWorkbookParser.Parse(worksheet);

        var groupsCreated = 0;
        var topicsCreated = 0;
        var studentsCreated = 0;
        var studentAccountsCreated = 0;
        var emailsSent = 0;
        var emailsFailed = 0;
        var accountNotifications = new List<(User User, string FullName)>();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        foreach (var importedGroup in importedGroups)
        {
            var stableSuffix = $"{semesterId}_{importedGroup.FirstRowNumber}";
            var topicCode = BuildCode("TOPIC", importedGroup.ProjectTitle, stableSuffix);
            var topic = await dbContext.Topics
                .FirstOrDefaultAsync(x => x.SemesterId == semesterId && x.Code == topicCode, cancellationToken);
            if (topic is null)
            {
                topic = new Topic
                {
                    Code = topicCode,
                    NameVn = importedGroup.ProjectTitle,
                    NameEn = importedGroup.ProjectTitle,
                    SemesterId = semesterId
                };
                dbContext.Topics.Add(topic);
                await dbContext.SaveChangesAsync(cancellationToken);
                topicsCreated++;
            }
            else
            {
                topic.NameVn = importedGroup.ProjectTitle;
                topic.NameEn = importedGroup.ProjectTitle;
            }

            var groupCode = BuildCode("GROUP", importedGroup.ProjectTitle, stableSuffix);
            var group = await dbContext.CapstoneGroups
                .FirstOrDefaultAsync(x => x.SemesterId == semesterId && x.Code == groupCode, cancellationToken);
            if (group is null)
            {
                group = new CapstoneGroup
                {
                    Code = groupCode,
                    TopicId = topic.Id,
                    SemesterId = semesterId,
                    LecturerId = lecturerId,
                    Status = GroupStatus.Active
                };
                dbContext.CapstoneGroups.Add(group);
                await dbContext.SaveChangesAsync(cancellationToken);
                groupsCreated++;
            }
            else
            {
                group.TopicId = topic.Id;
                group.LecturerId = lecturerId;
                group.Status = GroupStatus.Active;
            }

            foreach (var importedStudent in importedGroup.Students)
            {
                var generatedUsername = AccountUsernameGenerator.Generate(importedStudent.FullName, importedStudent.StudentCode);
                var user = await dbContext.Users.FirstOrDefaultAsync(
                    x => x.Username == generatedUsername || x.Email == importedStudent.Email,
                    cancellationToken);
                var isNewUser = false;

                if (user is null)
                {
                    user = new User
                    {
                        Username = generatedUsername,
                        Email = importedStudent.Email,
                        PasswordHash = PrecomputedPasswordHash,
                        Role = UserRole.Student,
                        IsActive = true
                    };
                    dbContext.Users.Add(user);
                    await dbContext.SaveChangesAsync(cancellationToken);
                    studentAccountsCreated++;
                    isNewUser = true;
                }
                else if (user.Role != UserRole.Student)
                {
                    throw new BusinessRuleException(
                        $"The account matching MSSV '{importedStudent.StudentCode}' or email '{importedStudent.Email}' is not a student account.");
                }

                var isLeader = importedStudent.FullName.Equals(importedGroup.LeaderName, StringComparison.OrdinalIgnoreCase);
                var student = await dbContext.Students
                    .FirstOrDefaultAsync(x => x.Code == importedStudent.StudentCode, cancellationToken);
                if (student is null)
                {
                    student = new Student
                    {
                        UserId = user.Id,
                        Code = importedStudent.StudentCode,
                        FullName = importedStudent.FullName,
                        ClassCode = "EXE101",
                        Major = "Unspecified",
                        GroupId = group.Id,
                        IsLeader = isLeader
                    };
                    dbContext.Students.Add(student);
                    studentsCreated++;
                }
                else
                {
                    if (student.UserId != user.Id)
                    {
                        throw new BusinessRuleException($"MSSV '{importedStudent.StudentCode}' belongs to a different user account.");
                    }

                    student.FullName = importedStudent.FullName;
                    student.GroupId = group.Id;
                    student.IsLeader = isLeader;
                }

                await dbContext.SaveChangesAsync(cancellationToken);
                if (isNewUser)
                {
                    accountNotifications.Add((user, importedStudent.FullName));
                }
            }
        }

        await transaction.CommitAsync(cancellationToken);

        foreach (var (user, fullName) in accountNotifications)
        {
            try
            {
                await SendAccountCreatedEmailAsync(user, fullName, TemporaryPassword, cancellationToken);
                emailsSent++;
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                emailsFailed++;
            }
        }

        return new ExcelImportResult(groupsCreated, topicsCreated, studentsCreated, studentAccountsCreated, emailsSent, emailsFailed);
    }

    [Obsolete("Use ImportSelectedGroupColumnsAsync so lecturer assignment is explicit and only supported columns are imported.")]
    public async Task<ExcelImportResult> ImportGroupsAsync(Stream fileStream, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fileStream);

        // 1. Resolve current active semester
        var activeSemester = await semesterResolver.ResolveForDateAsync(DateOnly.FromDateTime(DateTime.Today), cancellationToken);
        var semesterId = activeSemester.Id;

        using var cleanedStream = RemoveDataValidation(fileStream);
        using var workbook = new XLWorkbook(cleanedStream);
        var worksheet = workbook.Worksheets.FirstOrDefault() 
            ?? throw new BusinessRuleException("The uploaded Excel workbook contains no worksheets.");

        var rows = worksheet.RowsUsed().Skip(1).ToList(); // Skip header row
        
        var groupsCreated = 0;
        var topicsCreated = 0;
        var studentsCreated = 0;
        var studentAccountsCreated = 0;
        var emailsSent = 0;
        var emailsFailed = 0;

        // Group rows in memory by group block
        // A group block starts with a row where STT (Col 1) is a number.
        // Members of the same group continue in subsequent rows where STT is empty.
        var groupBlocks = new List<List<IXLRow>>();
        List<IXLRow>? currentBlock = null;

        foreach (var row in rows)
        {
            var sttVal = GetCellString(row, 1);
            if (!string.IsNullOrWhiteSpace(sttVal) && Regex.IsMatch(sttVal, @"^\d+$"))
            {
                if (currentBlock != null)
                {
                    groupBlocks.Add(currentBlock);
                }
                currentBlock = [];
            }

            if (currentBlock != null)
            {
                currentBlock.Add(row);
            }
        }
        if (currentBlock != null)
        {
            groupBlocks.Add(currentBlock);
        }

        foreach (var block in groupBlocks)
        {
            var firstRow = block[0];
            var stt = GetCellString(firstRow, 1);
            var groupName = GetCellString(firstRow, 2);
            var projectName = GetCellString(firstRow, 3);
            var projectDesc = GetCellString(firstRow, 4);

            if (string.IsNullOrWhiteSpace(projectName))
            {
                // Skip blocks without project/group names
                continue;
            }

            // Parse lecturer details from first row (Column 10: Class & Lecturer info)
            var classAndLecturerInfo = GetCellString(firstRow, 10);
            var lecturerCode = ParseLecturerCode(classAndLecturerInfo) ?? "unassigned";
            var lecturerId = await GetOrCreateLecturerIdAsync(lecturerCode, cancellationToken);

            // Create/Get Topic
            var topicCode = $"TOPIC_{Regex.Replace(projectName, @"[^A-Za-z0-9]", "")}_{stt}";
            if (topicCode.Length > 50) topicCode = topicCode[..50];
            
            var topic = await dbContext.Topics
                .FirstOrDefaultAsync(x => x.SemesterId == semesterId && x.Code == topicCode, cancellationToken);
            if (topic == null)
            {
                topic = new Topic
                {
                    Code = topicCode,
                    NameVn = projectName,
                    NameEn = projectName,
                    SemesterId = semesterId
                };
                dbContext.Topics.Add(topic);
                await dbContext.SaveChangesAsync(cancellationToken);
                topicsCreated++;
            }

            // Create/Get Capstone Group
            var finalGroupCode = !string.IsNullOrWhiteSpace(groupName) ? groupName.Trim() : $"Group_{stt}_{projectName}";
            if (finalGroupCode.Length > 50) finalGroupCode = finalGroupCode[..50];

            var group = await dbContext.CapstoneGroups
                .FirstOrDefaultAsync(x => x.SemesterId == semesterId && x.Code == finalGroupCode, cancellationToken);
            if (group == null)
            {
                group = new CapstoneGroup
                {
                    Code = finalGroupCode,
                    TopicId = topic.Id,
                    SemesterId = semesterId,
                    LecturerId = lecturerId,
                    Status = GroupStatus.Active
                };
                dbContext.CapstoneGroups.Add(group);
                await dbContext.SaveChangesAsync(cancellationToken);
                groupsCreated++;
            }

            var leaderName = block
                .Select(row => GetCellString(row, 11))
                .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name))?.Trim();

            // Create/Update Students in this group
            foreach (var row in block)
            {
                var studentName = GetCellString(row, 5);
                var studentCode = GetCellString(row, 6);
                var studentEmail = GetCellString(row, 9);
                var studentClassInfo = GetCellString(row, 10);
                var major = GetCellString(row, 8); // Specialization (Chuyên Ngành)
                if (string.IsNullOrWhiteSpace(major))
                {
                    major = GetCellString(row, 7); // Fallback to Ngành
                }
                if (string.IsNullOrWhiteSpace(major))
                {
                    major = "SE";
                }

                if (string.IsNullOrWhiteSpace(studentCode) || string.IsNullOrWhiteSpace(studentName) || string.IsNullOrWhiteSpace(studentEmail))
                {
                    continue;
                }

                var classCode = ParseClassCode(studentClassInfo) ?? "SE";

                // Check if user account already exists
                var generatedUsername = AccountUsernameGenerator.Generate(studentName, studentCode);
                var user = await dbContext.Users
                    .FirstOrDefaultAsync(x => x.Username == generatedUsername || x.Email == studentEmail, cancellationToken);

                var isNewUser = false;
                if (user == null)
                {
                    user = new User
                    {
                        Username = generatedUsername,
                        Email = studentEmail,
                        PasswordHash = PrecomputedPasswordHash,
                        Role = UserRole.Student,
                        IsActive = true
                    };
                    dbContext.Users.Add(user);
                    await dbContext.SaveChangesAsync(cancellationToken);
                    studentAccountsCreated++;
                    isNewUser = true;
                }

                var isLeader = !string.IsNullOrWhiteSpace(leaderName) &&
                               studentName.Trim().Equals(leaderName, StringComparison.OrdinalIgnoreCase);

                // Check if student profile already exists
                var student = await dbContext.Students
                    .FirstOrDefaultAsync(x => x.Code == studentCode, cancellationToken);
                if (student == null)
                {
                    student = new Student
                    {
                        UserId = user.Id,
                        Code = studentCode,
                        FullName = studentName,
                        ClassCode = classCode,
                        Major = major,
                        GroupId = group.Id,
                        IsLeader = isLeader
                    };
                    dbContext.Students.Add(student);
                    studentsCreated++;
                }
                else
                {
                    student.GroupId = group.Id;
                    student.ClassCode = classCode;
                    student.Major = major;
                    student.IsLeader = isLeader;
                }

                await dbContext.SaveChangesAsync(cancellationToken);

                // Send email notification for newly created user account
                if (isNewUser)
                {
                    try
                    {
                        await SendAccountCreatedEmailAsync(user, studentName, TemporaryPassword, cancellationToken);
                        emailsSent++;
                    }
                    catch
                    {
                        emailsFailed++;
                    }
                }
            }
        }

        return new ExcelImportResult(groupsCreated, topicsCreated, studentsCreated, studentAccountsCreated, emailsSent, emailsFailed);
    }

    private static string BuildCode(string prefix, string projectTitle, string stableSuffix)
    {
        var normalizedTitle = Regex.Replace(projectTitle.ToUpperInvariant(), @"[^A-Z0-9]+", "_").Trim('_');
        if (string.IsNullOrWhiteSpace(normalizedTitle))
        {
            normalizedTitle = "PROJECT";
        }

        var code = $"{prefix}_{stableSuffix}_{normalizedTitle}";
        return code.Length <= 50 ? code : code[..50].TrimEnd('_');
    }

    private static string GetCellString(IXLRow row, int colIndex)
    {
        return row.Cell(colIndex).GetString().Trim();
    }

    private static string? ParseLecturerCode(string classAndLecturerInfo)
    {
        if (string.IsNullOrWhiteSpace(classAndLecturerInfo))
        {
            return null;
        }

        var match = Regex.Match(classAndLecturerInfo, @"-\(([^)]+)\)$");
        if (match.Success)
        {
            return match.Groups[1].Value.Trim();
        }

        var parts = classAndLecturerInfo.Split('-');
        if (parts.Length > 1)
        {
            return parts[^1].Replace("(", "").Replace(")", "").Trim();
        }

        return null;
    }

    private static string? ParseClassCode(string classAndLecturerInfo)
    {
        if (string.IsNullOrWhiteSpace(classAndLecturerInfo))
        {
            return null;
        }
        var firstPart = classAndLecturerInfo.Split(' ')[0];
        return firstPart.Trim();
    }

    private async Task<int> GetOrCreateLecturerIdAsync(string lecturerCode, CancellationToken cancellationToken)
    {
        var lecturer = await dbContext.Lecturers
            .FirstOrDefaultAsync(x => x.Code == lecturerCode, cancellationToken);
        if (lecturer != null)
        {
            return lecturer.Id;
        }

        // Create user and profile for lecturer if not exists
        var email = $"{lecturerCode}@fpt.edu.vn";
        var user = await dbContext.Users
            .FirstOrDefaultAsync(x => x.Username == lecturerCode || x.Email == email, cancellationToken);

        if (user == null)
        {
            user = new User
            {
                Username = lecturerCode,
                Email = email,
                PasswordHash = PrecomputedPasswordHash,
                Role = UserRole.Lecturer,
                IsActive = true
            };
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        lecturer = new Lecturer
        {
            UserId = user.Id,
            Code = lecturerCode,
            FullName = lecturerCode,
            Department = "General",
            IsPartTime = false
        };
        dbContext.Lecturers.Add(lecturer);
        await dbContext.SaveChangesAsync(cancellationToken);

        return lecturer.Id;
    }

    private async Task SendAccountCreatedEmailAsync(
        User user,
        string fullName,
        string password,
        CancellationToken cancellationToken)
    {
        var subject = "CPMS student account login information";
        var textBody = string.Join(Environment.NewLine, [
            $"Hello {fullName},",
            string.Empty,
            "Your CPMS student account has been created successfully from the official EXE101 group list import.",
            "Use the credentials below to sign in:",
            $"Username / Account: {user.Username}",
            $"Initial password: {password}",
            string.Empty,
            "Important notes:",
            "- Keep this account information private.",
            "- Do not share your password with anyone.",
            "- Sign in as soon as possible and change your password.",
            "- If you did not expect this account, contact the moderator immediately."
        ]);
        var htmlBody = BuildAccountCreatedEmailHtml(fullName, user.Username, password);

        await emailSender.SendHtmlAsync(user.Email, subject, htmlBody, textBody, Array.Empty<EmailInlineImage>(), cancellationToken);
    }

    private static string BuildAccountCreatedEmailHtml(string fullName, string username, string initialPassword)
    {
        var safeFullName = WebUtility.HtmlEncode(fullName);
        var safeUsername = WebUtility.HtmlEncode(username);
        var safePassword = WebUtility.HtmlEncode(initialPassword);

        return $$"""
<!doctype html>
<html lang="en">
  <head>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <title>CPMS student account information</title>
  </head>
  <body style="margin:0;padding:0;background:#f3f6fb;font-family:Arial,Helvetica,sans-serif;color:#172033;">
    <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#f3f6fb;margin:0;padding:28px 12px;">
      <tr>
        <td align="center">
          <table role="presentation" width="640" cellspacing="0" cellpadding="0" style="width:640px;max-width:100%;background:#ffffff;border:1px solid #e1e7f0;border-radius:12px;overflow:hidden;">
            <tr>
              <td style="padding:24px 28px 10px 28px;text-align:left;">
                <div style="font-size:20px;font-weight:800;color:#0f5ea8;letter-spacing:.2px;">FPT University</div>
              </td>
            </tr>
            <tr>
              <td style="padding:10px 28px 28px 28px;">
                <h1 style="margin:0 0 10px 0;font-size:24px;line-height:32px;color:#10243e;">CPMS student account created</h1>
                <p style="margin:0 0 18px 0;font-size:15px;line-height:23px;color:#44546a;">Hello <strong>{{safeFullName}}</strong>, your CPMS student account has been created successfully from the EXE101 group list import. Use the credentials below to sign in.</p>

                <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="border-collapse:separate;border-spacing:0;margin:18px 0;border:1px solid #d8e2ef;border-radius:10px;overflow:hidden;">
                  <tr>
                    <td style="padding:13px 16px;background:#f7fafc;width:34%;font-size:13px;color:#667085;border-bottom:1px solid #d8e2ef;">Username / Account</td>
                    <td style="padding:13px 16px;background:#ffffff;font-size:16px;font-weight:700;color:#0f5ea8;border-bottom:1px solid #d8e2ef;">{{safeUsername}}</td>
                  </tr>
                  <tr>
                    <td style="padding:13px 16px;background:#f7fafc;font-size:13px;color:#667085;">Initial password</td>
                    <td style="padding:13px 16px;background:#ffffff;font-size:16px;font-weight:700;color:#cc4b00;">{{safePassword}}</td>
                  </tr>
                </table>

                <div style="margin:18px 0;padding:14px 16px;background:#fff7ed;border:1px solid #fed7aa;border-radius:10px;color:#7c2d12;font-size:14px;line-height:22px;">
                  Keep this account information private. Do not share your password with anyone.
                </div>

                <p style="margin:0 0 10px 0;font-size:14px;line-height:22px;color:#44546a;">Next steps:</p>
                <ol style="margin:0 0 18px 20px;padding:0;color:#44546a;font-size:14px;line-height:22px;">
                  <li>Sign in to CPMS using the username and initial password above.</li>
                  <li>Change your password after the first sign-in.</li>
                </ol>

                <p style="margin:20px 0 0 0;font-size:12px;line-height:18px;color:#98a2b3;">This is an automated CPMS email. Please keep it for your first sign-in only.</p>
              </td>
            </tr>
          </table>
        </td>
      </tr>
    </table>
  </body>
</html>
""";
    }

    private static MemoryStream RemoveDataValidation(Stream sourceStream)
    {
        var memoryStream = new MemoryStream();
        sourceStream.CopyTo(memoryStream);
        memoryStream.Position = 0;

        try
        {
            using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Update, leaveOpen: true))
            {
                var sheetEntries = archive.Entries
                    .Where(e => e.FullName.StartsWith("xl/worksheets/sheet", StringComparison.OrdinalIgnoreCase) && 
                                e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var entry in sheetEntries)
                {
                    string xmlContent;
                    using (var reader = new StreamReader(entry.Open()))
                    {
                        xmlContent = reader.ReadToEnd();
                    }

                    var updatedXml = Regex.Replace(
                        xmlContent, 
                        @"<(?:[a-zA-Z0-9\-]+:)?dataValidations[^>]*>[\s\S]*?</(?:[a-zA-Z0-9\-]+:)?dataValidations>", 
                        "", 
                        RegexOptions.IgnoreCase);

                    if (updatedXml != xmlContent)
                    {
                        entry.Delete();
                        var newEntry = archive.CreateEntry(entry.FullName);
                        using var writer = new StreamWriter(newEntry.Open());
                        writer.Write(updatedXml);
                    }
                }
            }

            memoryStream.Position = 0;
            return memoryStream;
        }
        catch
        {
            memoryStream.Position = 0;
            return memoryStream;
        }
    }
}

public sealed record ExcelImportResult(
    int GroupsCreated,
    int TopicsCreated,
    int StudentsCreated,
    int StudentAccountsCreated,
    int EmailsSent,
    int EmailsFailed);
