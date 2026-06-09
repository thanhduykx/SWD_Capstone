using CPMS.Core.Exceptions;
using CPMS.Core.Services;

namespace ServiceLayer.Tests.Services;

public sealed class AssignmentRulesTests
{
    private readonly AssignmentRules _rules = new();

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    public void ReviewSlot_RejectsValueOutsideOneToEight(int slot)
    {
        var action = () => _rules.ValidateReviewSlot(slot);

        Assert.Throws<BusinessRuleException>(action);
    }

    [Fact]
    public void ReviewAssignment_RejectsSupervisorAsReviewer()
    {
        var action = () => _rules.ValidateReviewAssignment(10, 10, 12);

        Assert.Throws<BusinessRuleException>(action);
    }

    [Fact]
    public void ReviewAssignment_RejectsDuplicateReviewerInFlexibleList()
    {
        var action = () => _rules.ValidateReviewAssignment(10, [11, 11]);

        Assert.Throws<BusinessRuleException>(action);
    }

    [Fact]
    public void ReviewTwo_RejectsReviewerRepeatedFromReviewOne()
    {
        var action = () => _rules.ValidateReviewAssignment(10, 11, 12, [9, 11]);

        Assert.Throws<BusinessRuleException>(action);
    }

    [Fact]
    public void ReviewSlotConflict_RejectsLecturerDoubleBooked()
    {
        var action = () => _rules.EnsureLecturerAvailableForReviewSlot(true);

        Assert.Throws<BusinessRuleException>(action);
    }

    [Fact]
    public void ReviewRound_RejectsGroupAlreadyScheduled()
    {
        var action = () => _rules.EnsureGroupCanBeScheduledForReviewType(true);

        Assert.Throws<BusinessRuleException>(action);
    }

    [Fact]
    public void CouncilAssignment_RejectsSupervisorConflict()
    {
        var action = () => _rules.ValidateCouncilAssignment([2, 7, 8], [7]);

        Assert.Throws<BusinessRuleException>(action);
    }

    [Fact]
    public void CouncilMember_RejectsLecturerNotAssignedToCouncil()
    {
        var action = () => _rules.EnsureCouncilMember(9, [2, 7, 8]);

        Assert.Throws<BusinessRuleException>(action);
    }

    [Fact]
    public void ChairmanAction_RejectsNonChairman()
    {
        var action = () => _rules.EnsureChairman(7, 2);

        Assert.Throws<BusinessRuleException>(action);
    }

    [Fact]
    public void CouncilAssignment_RejectsNinthGroup()
    {
        var action = () => _rules.ValidateCouncilCapacity(8);

        Assert.Throws<BusinessRuleException>(action);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(10.01)]
    public void Score_RejectsValueOutsideZeroToTen(decimal value)
    {
        var action = () => _rules.ValidateScore(value);

        Assert.Throws<BusinessRuleException>(action);
    }

    [Fact]
    public void ClosedDefenseSession_RejectsScoreEditing()
    {
        var action = () => _rules.EnsureSessionEditable(true);

        Assert.Throws<BusinessRuleException>(action);
    }

    [Fact]
    public void NotStartedDefenseSession_RejectsScoreSubmission()
    {
        var action = () => _rules.EnsureSessionStarted(null, false);

        Assert.Throws<BusinessRuleException>(action);
    }
}
