using CPMS.DAL.Entities;
using CPMS.DAL.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CPMS.IntegrationTests;

public sealed class ReviewWorkflowModelTests
{
    private static CpmsDbContext CreateContext()
    {
        DbContextOptions<CpmsDbContext> options = new DbContextOptionsBuilder<CpmsDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only;Username=model_only;Password=model_only")
            .Options;
        return new CpmsDbContext(options);
    }

    [Fact]
    public void StudentAvailability_AllowsFiveDistinctSlotsPerGroupAndRound()
    {
        using CpmsDbContext context = CreateContext();
        IEntityType entity = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(StudentSlotRegistration))!;

        IIndex uniqueIndex = entity.GetIndexes().Single(index => index.IsUnique);

        Assert.Equal(
            [nameof(StudentSlotRegistration.ReviewRoundId), nameof(StudentSlotRegistration.GroupId), nameof(StudentSlotRegistration.DayOfWeek), nameof(StudentSlotRegistration.Slot)],
            uniqueIndex.Properties.Select(property => property.Name));
    }

    [Fact]
    public void ReviewSession_EnforcesThreeOrderedGroups()
    {
        using CpmsDbContext context = CreateContext();
        IEntityType entity = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(GroupReviewSlot))!;

        Assert.Contains(entity.GetCheckConstraints(), constraint =>
            constraint.Name == "ck_group_review_slots_position" && constraint.Sql!.Contains("BETWEEN 1 AND 3", StringComparison.Ordinal));
        Assert.Contains(entity.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(
                [nameof(GroupReviewSlot.SessionId), nameof(GroupReviewSlot.GroupPosition)]));
    }

    [Fact]
    public void ReviewSubmission_IsUniquePerSessionGroupAndReviewer()
    {
        using CpmsDbContext context = CreateContext();
        IEntityType entity = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(ReviewChecklistSubmission))!;

        Assert.Contains(entity.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(
                [nameof(ReviewChecklistSubmission.SessionId), nameof(ReviewChecklistSubmission.GroupId), nameof(ReviewChecklistSubmission.ReviewerId)]));
    }

    [Fact]
    public void Group_CannotHaveMultipleLeaders()
    {
        using CpmsDbContext context = CreateContext();
        IEntityType entity = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Student))!;

        Assert.Contains(entity.GetIndexes(), index => index.IsUnique &&
            index.Properties.Single().Name == nameof(Student.GroupId) &&
                index.GetFilter() == "\"is_leader\" = TRUE");
    }

    [Fact]
    public void LecturerAvailability_IsUniqueWithinExactReviewRound()
    {
        using CpmsDbContext context = CreateContext();
        IEntityType availability = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(ReviewAvailability))!;
        IEntityType submission = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(ReviewAvailabilitySubmission))!;

        Assert.Contains(availability.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(
                [nameof(ReviewAvailability.ReviewRoundId), nameof(ReviewAvailability.LecturerId), nameof(ReviewAvailability.DayOfWeek), nameof(ReviewAvailability.Slot)]));
        Assert.Contains(submission.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(
                [nameof(ReviewAvailabilitySubmission.ReviewRoundId), nameof(ReviewAvailabilitySubmission.LecturerId)]));
    }

    [Fact]
    public void ReviewRound_IsUniquePerSemesterAndType()
    {
        using CpmsDbContext context = CreateContext();
        IEntityType entity = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(ReviewRound))!;

        Assert.Contains(entity.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(
                [nameof(ReviewRound.SemesterId), nameof(ReviewRound.Type)]));
    }

    [Fact]
    public void EmailDeliveryLog_PersistsBodyForPostCommitDispatch()
    {
        using CpmsDbContext context = CreateContext();
        IEntityType entity = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(EmailDeliveryLog))!;

        Assert.False(entity.FindProperty(nameof(EmailDeliveryLog.Body))!.IsNullable);
    }
}
