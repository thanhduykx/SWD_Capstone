using CPMS.Core.Entities;
using CPMS.Core.Enums;
using CPMS.Core.Services;
using CPMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CPMS.Api.Controllers;

[ApiController]
[Route("api/review-sessions")]
[Authorize(Roles = "SystemAdministrator,TrainingDepartment")]
public sealed class ReviewSessionsController(CpmsDbContext dbContext, AssignmentRules rules) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ReviewSession>> Create(CreateReviewSessionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Slot is < 1 or > 5)
        {
            return BadRequest(new { error = "Review slot must be between 1 and 5." });
        }

        var group = await dbContext.CapstoneGroups.SingleOrDefaultAsync(x => x.Id == request.GroupId, cancellationToken);
        if (group is null)
        {
            return NotFound(new { error = "Capstone group does not exist." });
        }

        rules.ValidateReviewAssignment(
            group.LecturerId,
            request.Reviewer1Id,
            request.Reviewer2Id,
            request.Type == ReviewType.Review2 ? request.PreviousReviewerIds : null);

        var session = new ReviewSession
        {
            Code = request.Code.Trim(),
            SemesterId = group.SemesterId,
            DayOfWeek = (int)request.SessionDate.DayOfWeek,
            Slot = request.Slot,
            Room = request.Room.Trim(),
            Type = request.Type,
            Reviewer1Id = request.Reviewer1Id,
            Reviewer2Id = request.Reviewer2Id,
            SessionDate = request.SessionDate
        };
        dbContext.ReviewSessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);
        dbContext.GroupReviewSlots.Add(new GroupReviewSlot
        {
            SessionId = session.Id,
            GroupId = group.Id,
            GroupPosition = request.GroupPosition,
            ConflictFlag = false
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(Create), new { session.Id }, session);
    }
}

public sealed record CreateReviewSessionRequest(
    string Code,
    int GroupId,
    int GroupPosition,
    ReviewType Type,
    int Reviewer1Id,
    int Reviewer2Id,
    int[] PreviousReviewerIds,
    int Slot,
    string Room,
    DateTime SessionDate);
