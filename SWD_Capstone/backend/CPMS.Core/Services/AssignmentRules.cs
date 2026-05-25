using CPMS.Core.Exceptions;

namespace CPMS.Core.Services;

public sealed class AssignmentRules
{
    public void ValidateReviewAssignment(
        int supervisorId,
        int reviewer1Id,
        int reviewer2Id,
        IReadOnlyCollection<int>? previousRoundReviewerIds = null)
    {
        if (reviewer1Id == reviewer2Id)
        {
            throw new BusinessRuleException("A review session must have two different reviewers.");
        }

        if (reviewer1Id == supervisorId || reviewer2Id == supervisorId)
        {
            throw new BusinessRuleException("The supervisor cannot review their own capstone group.");
        }

        if (previousRoundReviewerIds is not null &&
            (previousRoundReviewerIds.Contains(reviewer1Id) || previousRoundReviewerIds.Contains(reviewer2Id)))
        {
            throw new BusinessRuleException("Review 2 reviewers cannot repeat reviewers from Review 1.");
        }
    }

    public void ValidateCouncilAssignment(IReadOnlyCollection<int> memberIds, IReadOnlyCollection<int> supervisorIds)
    {
        if (memberIds.Intersect(supervisorIds).Any())
        {
            throw new BusinessRuleException("A supervisor cannot sit on a council assessing their own group.");
        }
    }

    public void EnsureCouncilMember(int lecturerId, IReadOnlyCollection<int> councilMemberIds)
    {
        if (!councilMemberIds.Contains(lecturerId))
        {
            throw new BusinessRuleException("Only assigned council members can join and score this defense session.");
        }
    }

    public void EnsureChairman(int lecturerId, int chairmanId)
    {
        if (lecturerId != chairmanId)
        {
            throw new BusinessRuleException("Only the council chairman can start or close the defense session.");
        }
    }

    public void ValidateCouncilCapacity(int assignedGroupCount)
    {
        if (assignedGroupCount >= 8)
        {
            throw new BusinessRuleException("A council cannot assess more than eight groups in one session.");
        }
    }

    public void ValidateScore(decimal value)
    {
        if (value is < 0 or > 10)
        {
            throw new BusinessRuleException("A score must be between 0 and 10.");
        }
    }

    public void EnsureSessionEditable(bool isLocked)
    {
        if (isLocked)
        {
            throw new BusinessRuleException("Scores are immutable after the chairman closes the defense session.");
        }
    }

    public void EnsureSessionStarted(DateTime? startedAt, bool isLocked)
    {
        EnsureSessionEditable(isLocked);
        if (startedAt is null)
        {
            throw new BusinessRuleException("Scoring is locked until the council chairman starts the defense session.");
        }
    }
}
