using CPMS.BLL.Exceptions;
using CPMS.BLL.Enums;
using CPMS.BLL.Reviews.Registration;

namespace CPMS.BLL.Services;

public static class AssignmentRules
{
    public const int RequiredStudentAvailabilityCount = 5;
    public const int MaxGroupsPerReviewSession = 3;
    public const int MaxLecturersPerAvailabilitySlot = 3;
    public const int MaxGroupsPerAvailabilitySlot = 3;

    public static void ValidateReviewSlot(int slot)
    {
        if (slot is < 1 or > 5)
        {
            throw new BusinessRuleException("Review slot must be between 1 and 5.");
        }
    }

    public static void ValidateReviewAssignment(
        int supervisorId,
        IReadOnlyCollection<int> reviewerIds,
        IReadOnlyCollection<int>? previousRoundReviewerIds = null)
    {
        ArgumentNullException.ThrowIfNull(reviewerIds);

        if (reviewerIds.Count < 2)
        {
            throw new BusinessRuleException("A review session must have at least two reviewers.");
        }
        if (reviewerIds.Count > MaxLecturersPerAvailabilitySlot)
        {
            throw new BusinessRuleException(
                $"A review session can contain at most {MaxLecturersPerAvailabilitySlot} reviewers.");
        }
        if (reviewerIds.Count != reviewerIds.Distinct().Count())
        {
            throw new BusinessRuleException("A review session cannot repeat the same reviewer.");
        }

        if (reviewerIds.Contains(supervisorId))
        {
            throw new BusinessRuleException("The supervisor cannot review their own capstone group.");
        }

        if (previousRoundReviewerIds is not null && reviewerIds.Any(previousRoundReviewerIds.Contains))
        {
            throw new BusinessRuleException("Review 2 reviewers cannot repeat reviewers from Review 1.");
        }
    }

    public static void EnsureSlotRegistrationCapacity(
        IReadOnlyCollection<ReviewAvailabilitySlot> selectedSlots,
        IReadOnlyCollection<ReviewAvailabilitySlot> currentSlots,
        IReadOnlyCollection<SlotRegistrationCount> registrationCounts,
        int maximum,
        string participantLabel)
    {
        ArgumentNullException.ThrowIfNull(selectedSlots);
        ArgumentNullException.ThrowIfNull(currentSlots);
        ArgumentNullException.ThrowIfNull(registrationCounts);
        var current = currentSlots.ToHashSet();
        var counts = registrationCounts.ToDictionary(x => (x.DayOfWeek, x.Slot), x => x.RegisteredCount);
        ReviewAvailabilitySlot? fullSlot = selectedSlots.FirstOrDefault(slot =>
            counts.GetValueOrDefault((slot.DayOfWeek, slot.Slot)) - (current.Contains(slot) ? 1 : 0) >= maximum);
        if (fullSlot is not null)
        {
            throw new BusinessRuleException(
                $"Day {fullSlot.DayOfWeek}, slot {fullSlot.Slot} already has {maximum}/{maximum} {participantLabel} registrations.");
        }
    }

    public static void EnsureLecturerAvailableForReviewSlot(bool hasSlotConflict)
    {
        if (hasSlotConflict)
        {
            throw new BusinessRuleException("A lecturer cannot be assigned to more than one review session in the same date and slot.");
        }
    }

    public static void EnsureGroupCanBeScheduledForReviewType(bool alreadyScheduled)
    {
        if (alreadyScheduled)
        {
            throw new BusinessRuleException("A capstone group already has a session for this review round.");
        }
    }

    public static void ValidateStudentAvailability(IReadOnlyCollection<(int DayOfWeek, int Slot)> slots)
    {
        ArgumentNullException.ThrowIfNull(slots);
        if (slots.Count != RequiredStudentAvailabilityCount || slots.Distinct().Count() != RequiredStudentAvailabilityCount)
        {
            throw new BusinessRuleException($"Exactly {RequiredStudentAvailabilityCount} distinct available slots must be selected.");
        }

        if (slots.Any(x => x.DayOfWeek is < 1 or > 6 || x.Slot is < 1 or > 5))
        {
            throw new BusinessRuleException("Student availability must use Monday-Saturday and slots 1-5.");
        }
    }

    public static void EnsureReviewRoundTransition(ReviewRoundStatus current, ReviewRoundStatus next)
    {
        if (current == next) return;
        var valid = (current, next) switch
        {
            (ReviewRoundStatus.Draft, ReviewRoundStatus.Open) => true,
            (ReviewRoundStatus.Draft, ReviewRoundStatus.Closed) => true,
            (ReviewRoundStatus.Open, ReviewRoundStatus.Closed) => true,
            (ReviewRoundStatus.Closed, ReviewRoundStatus.Open) => true,
            (ReviewRoundStatus.Open, ReviewRoundStatus.Draft) => true,
            _ => false
        };
        if (!valid)
        {
            throw new BusinessRuleException($"Review round cannot transition from {current} to {next}.");
        }
    }

    public static void EnsureReviewSessionCapacity(int existingGroupCount)
    {
        if (existingGroupCount is < 0 or >= MaxGroupsPerReviewSession)
        {
            throw new BusinessRuleException($"A review session can contain at most {MaxGroupsPerReviewSession} groups.");
        }
    }

    public static void ValidateCouncilAssignment(IReadOnlyCollection<int> memberIds, IReadOnlyCollection<int> supervisorIds)
    {
        if (memberIds.Intersect(supervisorIds).Any())
        {
            throw new BusinessRuleException("A supervisor cannot sit on a council assessing their own group.");
        }
    }

    public static void EnsureCouncilMember(int lecturerId, IReadOnlyCollection<int> councilMemberIds)
    {
        if (!councilMemberIds.Contains(lecturerId))
        {
            throw new BusinessRuleException("Only assigned council members can join and comment in this defense session.");
        }
    }

    public static void EnsureChairman(int lecturerId, int chairmanId)
    {
        if (lecturerId != chairmanId)
        {
            throw new BusinessRuleException("Only the council chairman can start or close the defense session.");
        }
    }

    public static void ValidateCouncilCapacity(int assignedGroupCount)
    {
        if (assignedGroupCount >= 8)
        {
            throw new BusinessRuleException("A council cannot assess more than eight groups in one session.");
        }
    }

    public static void ValidateScore(decimal value)
    {
        if (value is < 0 or > 10)
        {
            throw new BusinessRuleException("A score must be between 0 and 10.");
        }
    }

    public static void EnsureSessionEditable(bool isLocked)
    {
        if (isLocked)
        {
            throw new BusinessRuleException("Scores are immutable after the chairman closes the defense session.");
        }
    }

    public static void EnsureSessionStarted(DateTime? startedAt, bool isLocked)
    {
        EnsureSessionEditable(isLocked);
        if (startedAt is null)
        {
            throw new BusinessRuleException("Scoring is locked until the council chairman starts the defense session.");
        }
    }
}
