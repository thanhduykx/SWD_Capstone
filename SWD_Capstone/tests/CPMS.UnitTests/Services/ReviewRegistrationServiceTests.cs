using CPMS.BLL.Reviews.Registration;
using CPMS.BLL.Enums;
using CPMS.BLL.Exceptions;
using CPMS.BLL.Services;

namespace CPMS.UnitTests.Services;

public sealed class ReviewRegistrationServiceTests
{
    [Fact]
    public async Task SaveStudentAvailability_RejectsNonLeaderWithoutWriting()
    {
        var store = new FakeStore
        {
            Student = new StudentProfileData(10, 20, false, 30),
            Round = OpenRound()
        };
        var service = new ReviewRegistrationService(store, new AssignmentRules());

        BusinessRuleException exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.SaveStudentAvailabilityAsync(1, 1, FiveSlots(), CancellationToken.None));

        Assert.Equal("Only the group leader can register a review slot.", exception.Message);
        Assert.False(store.StudentAvailabilityReplaced);
    }

    [Fact]
    public async Task SubmitLecturerAvailability_RequiresAtLeastOneSlot()
    {
        var store = new FakeStore
        {
            LecturerId = 7,
            Round = OpenRound(),
            LecturerAvailability = new LecturerAvailabilityData(null, [])
        };
        var service = new ReviewRegistrationService(store, new AssignmentRules());

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.SubmitLecturerAvailabilityAsync(1, 1, CancellationToken.None));

        Assert.False(store.LecturerAvailabilitySubmitted);
    }

    [Fact]
    public async Task GetStudentSchedule_ExposesOnlyPublishedSessions()
    {
        var store = new FakeStore
        {
            Student = new StudentProfileData(10, 20, true, 30),
            Schedules =
            [
                new(1, "DRAFT", 20, "GROUP-20", ReviewType.Review1, DateTime.UtcNow, 1, "A", ReviewSessionStatus.Draft, ReviewGroupStatus.Scheduled, null),
                new(2, "PUBLISHED", 20, "GROUP-20", ReviewType.Review1, DateTime.UtcNow, 2, "B", ReviewSessionStatus.Published, ReviewGroupStatus.Scheduled, null)
            ]
        };
        var service = new ReviewRegistrationService(store, new AssignmentRules());

        IReadOnlyList<StudentScheduleResult> result = await service.GetStudentScheduleAsync(1, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("PUBLISHED", result[0].SessionCode);
    }

    [Fact]
    public async Task GetStudentRounds_MarksOnlyExactlyFiveSlotsAsRegistered()
    {
        var store = new FakeStore
        {
            Student = new StudentProfileData(10, 20, true, 30),
            StudentRounds = [new StudentRoundData(OpenRound(), 5), new StudentRoundData(OpenRound(2), 4)]
        };
        var service = new ReviewRegistrationService(store, new AssignmentRules());

        IReadOnlyList<StudentReviewRoundResult> result = await service.GetStudentRoundsAsync(1, 30, CancellationToken.None);

        Assert.True(result[0].IsGroupRegistered);
        Assert.False(result[1].IsGroupRegistered);
    }

    [Fact]
    public async Task ConfirmStudentCheckIn_RecordsSignatureForAssignedPublishedSession()
    {
        var past = DateTime.UtcNow.AddHours(-1);
        var store = new FakeStore
        {
            Student = new StudentProfileData(10, 20, false, 30),
            CheckIns = [new(2, "PUBLISHED", ReviewType.Review1, past, 2, "B", ReviewSessionStatus.Published, ReviewGroupStatus.Scheduled, null)]
        };
        var service = new ReviewRegistrationService(store, new AssignmentRules());

        StudentCheckInResult result = await service.ConfirmStudentCheckInAsync(1, 2, CancellationToken.None);

        Assert.NotNull(result.ConfirmedAt);
        Assert.True(store.StudentCheckInConfirmed);
        Assert.False(result.CanConfirm);
    }

    [Fact]
    public async Task ConfirmStudentCheckIn_RejectsSigningTooEarly()
    {
        var store = new FakeStore
        {
            Student = new StudentProfileData(10, 20, false, 30),
            CheckIns = [new(2, "FUTURE", ReviewType.Review1, DateTime.UtcNow.AddHours(2), 2, "B", ReviewSessionStatus.Published, ReviewGroupStatus.Scheduled, null)]
        };
        var service = new ReviewRegistrationService(store, new AssignmentRules());

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.ConfirmStudentCheckInAsync(1, 2, CancellationToken.None));
        Assert.False(store.StudentCheckInConfirmed);
    }

    private static ReviewRoundData OpenRound(int id = 1) =>
        new(id, 30, ReviewType.Review1, ReviewRoundStatus.Open, new DateOnly(2026, 7, 13), new DateOnly(2026, 7, 18));

    private static ReviewAvailabilitySlot[] FiveSlots() =>
        [new(1, 1), new(1, 2), new(2, 1), new(2, 2), new(3, 1)];

    private sealed class FakeStore : IReviewRegistrationStore
    {
        public int? LecturerId { get; init; }
        public ReviewRoundData? Round { get; init; }
        public LecturerAvailabilityData LecturerAvailability { get; init; } = new(null, []);
        public StudentProfileData? Student { get; init; }
        public IReadOnlyList<StudentRoundData> StudentRounds { get; init; } = [];
        public IReadOnlyList<StudentScheduleData> Schedules { get; init; } = [];
        public IReadOnlyList<StudentCheckInData> CheckIns { get; init; } = [];
        public bool StudentAvailabilityReplaced { get; private set; }
        public bool LecturerAvailabilitySubmitted { get; private set; }
        public bool StudentCheckInConfirmed { get; private set; }

        public Task<int?> FindLecturerIdByUserIdAsync(int userId, CancellationToken cancellationToken) => Task.FromResult(LecturerId);
        public Task<ReviewRoundData?> FindRoundAsync(int roundId, CancellationToken cancellationToken) => Task.FromResult(Round);
        public Task<LecturerAvailabilityData> GetLecturerAvailabilityAsync(int roundId, int lecturerId, CancellationToken cancellationToken) => Task.FromResult(LecturerAvailability);
        public Task ReplaceLecturerAvailabilityAsync(ReviewRoundData round, int lecturerId, IReadOnlyCollection<ReviewAvailabilitySlot> slots, DateTime changedAt, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SubmitLecturerAvailabilityAsync(ReviewRoundData round, int lecturerId, DateTime submittedAt, CancellationToken cancellationToken) { LecturerAvailabilitySubmitted = true; return Task.CompletedTask; }
        public Task<StudentProfileData?> FindStudentByUserIdAsync(int userId, CancellationToken cancellationToken) => Task.FromResult(Student);
        public Task<IReadOnlyList<int>> GetActiveSemesterIdsAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<int>>([30]);
        public Task<IReadOnlyList<StudentRoundData>> GetStudentRoundsAsync(IReadOnlyCollection<int> semesterIds, int? groupId, CancellationToken cancellationToken) => Task.FromResult(StudentRounds);
        public Task<IReadOnlyList<StudentRegistrationData>> GetStudentRegistrationsAsync(int roundId, int groupId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<StudentRegistrationData>>([]);
        public Task ReplaceStudentAvailabilityAsync(int roundId, int groupId, int studentId, IReadOnlyCollection<ReviewAvailabilitySlot> slots, CancellationToken cancellationToken) { StudentAvailabilityReplaced = true; return Task.CompletedTask; }
        public Task<bool> ClearStudentAvailabilityAsync(int roundId, int groupId, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<IReadOnlyList<StudentScheduleData>> GetStudentScheduleAsync(int groupId, CancellationToken cancellationToken) => Task.FromResult(Schedules);
        public Task<IReadOnlyList<StudentAttendanceData>> GetStudentAttendanceAsync(int studentId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<StudentAttendanceData>>([]);
        public Task<IReadOnlyList<StudentCheckInData>> GetStudentCheckInsAsync(int studentId, int groupId, CancellationToken cancellationToken) => Task.FromResult(CheckIns);
        public Task<DateTime> ConfirmStudentCheckInAsync(int userId, int studentId, int groupId, int sessionId, DateTime confirmedAt, CancellationToken cancellationToken) { StudentCheckInConfirmed = true; return Task.FromResult(confirmedAt); }
        public Task<StudentCommentAccessData?> GetStudentCommentAccessAsync(int sessionId, int groupId, CancellationToken cancellationToken) => Task.FromResult<StudentCommentAccessData?>(null);
        public Task<IReadOnlyList<StudentCommentData>> GetStudentCommentsAsync(int sessionId, int groupId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<StudentCommentData>>([]);
    }
}
