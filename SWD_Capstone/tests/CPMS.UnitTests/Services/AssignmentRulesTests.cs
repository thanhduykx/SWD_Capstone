using CPMS.BLL.Exceptions;
using CPMS.BLL.Services;

namespace CPMS.UnitTests.Services;

public sealed class AssignmentRulesTests
{

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(9)]
    public void ReviewSlot_RejectsValueOutsideOneToFive(int slot)
    {
        Action action = () => AssignmentRules.ValidateReviewSlot(slot);

        Assert.Throws<BusinessRuleException>(action);
    }

    [Fact]
    public void StudentAvailability_AcceptsExactlyFiveDistinctSlots()
    {
        AssignmentRules.ValidateStudentAvailability([(1, 1), (1, 2), (2, 1), (3, 4), (6, 5)]);
    }

    [Fact]
    public void StudentAvailability_RejectsFewerThanFiveSlots()
    {
        Action action = () => AssignmentRules.ValidateStudentAvailability([(1, 1), (1, 2), (2, 1), (3, 4)]);

        Assert.Throws<BusinessRuleException>(action);
    }

    [Fact]
    public void ReviewRound_AllowsModeratorToCloseDraftRegistration()
    {
        Exception? exception = Record.Exception(() => AssignmentRules.EnsureReviewRoundTransition(
            CPMS.BLL.Enums.ReviewRoundStatus.Draft,
            CPMS.BLL.Enums.ReviewRoundStatus.Closed));

        Assert.Null(exception);
    }

    [Fact]
    public void ReviewSession_RejectsFourthGroup()
    {
        Action action = () => AssignmentRules.EnsureReviewSessionCapacity(3);

        Assert.Throws<BusinessRuleException>(action);
    }

    [Fact]
    public void ReviewAssignment_RejectsSupervisorAsReviewer()
    {
        Action action = () => AssignmentRules.ValidateReviewAssignment(10, [10, 12]);

        Assert.Throws<BusinessRuleException>(action);
    }

    [Fact]
    public void ReviewAssignment_RejectsDuplicateReviewerInFlexibleList()
    {
        Action action = () => AssignmentRules.ValidateReviewAssignment(10, [11, 11]);

        Assert.Throws<BusinessRuleException>(action);
    }

    [Fact]
    public void ReviewTwo_RejectsReviewerRepeatedFromReviewOne()
    {
        Action action = () => AssignmentRules.ValidateReviewAssignment(10, [11, 12], [9, 11]);

        Assert.Throws<BusinessRuleException>(action);
    }

    [Fact]
    public void ReviewAssignment_AcceptsFlexiblePanelWithFourReviewers()
    {
        AssignmentRules.ValidateReviewAssignment(10, [11, 12, 13, 14]);
    }

    [Fact]
    public void ReviewAssignment_RejectsMoreThanFourReviewers()
    {
        Action action = () => AssignmentRules.ValidateReviewAssignment(10, [11, 12, 13, 14, 15]);

        Assert.Throws<BusinessRuleException>(action);
    }

    [Fact]
    public void ReviewAssignment_RejectsSingleReviewer()
    {
        Action action = () => AssignmentRules.ValidateReviewAssignment(10, [11]);

        Assert.Throws<BusinessRuleException>(action);
    }

    [Fact]
    public void ReviewSlotConflict_RejectsLecturerDoubleBooked()
    {
        Action action = () => AssignmentRules.EnsureLecturerAvailableForReviewSlot(true);

        Assert.Throws<BusinessRuleException>(action);
    }

    [Fact]
    public void ReviewRound_RejectsGroupAlreadyScheduled()
    {
        Action action = () => AssignmentRules.EnsureGroupCanBeScheduledForReviewType(true);

        Assert.Throws<BusinessRuleException>(action);
    }

    [Fact]
    public void CouncilAssignment_RejectsSupervisorConflict()
    {
        Action action = () => AssignmentRules.ValidateCouncilAssignment([2, 7, 8], [7]);

        Assert.Throws<BusinessRuleException>(action);
    }

    [Fact]
    public void CouncilMember_RejectsLecturerNotAssignedToCouncil()
    {
        Action action = () => AssignmentRules.EnsureCouncilMember(9, [2, 7, 8]);

        Assert.Throws<BusinessRuleException>(action);
    }

    [Fact]
    public void ChairmanAction_RejectsNonChairman()
    {
        Action action = () => AssignmentRules.EnsureChairman(7, 2);

        Assert.Throws<BusinessRuleException>(action);
    }

    [Fact]
    public void CouncilAssignment_RejectsNinthGroup()
    {
        Action action = () => AssignmentRules.ValidateCouncilCapacity(8);

        Assert.Throws<BusinessRuleException>(action);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(10.01)]
    public void Score_RejectsValueOutsideZeroToTen(decimal value)
    {
        Action action = () => AssignmentRules.ValidateScore(value);

        Assert.Throws<BusinessRuleException>(action);
    }

    [Fact]
    public void ClosedDefenseSession_RejectsScoreEditing()
    {
        Action action = () => AssignmentRules.EnsureSessionEditable(true);

        Assert.Throws<BusinessRuleException>(action);
    }

    [Fact]
    public void NotStartedDefenseSession_RejectsScoreSubmission()
    {
        Action action = () => AssignmentRules.EnsureSessionStarted(null, false);

        Assert.Throws<BusinessRuleException>(action);
    }
}
