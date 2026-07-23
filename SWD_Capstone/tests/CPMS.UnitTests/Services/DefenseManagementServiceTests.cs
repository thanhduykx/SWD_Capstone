using CPMS.BLL.Defense.Management;
using CPMS.BLL.Enums;
using CPMS.BLL.Exceptions;
using CPMS.BLL.Models;

namespace CPMS.UnitTests.Services;

public sealed class DefenseManagementServiceTests
{
    [Fact]
    public async Task GetBoards_RejectsInvalidSemesterWithoutQueryingStore()
    {
        var store = new FakeStore([]);
        var service = new DefenseManagementService(store);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.GetBoardsAsync(0, CancellationToken.None));

        Assert.False(store.BoardsQueried);
    }

    [Fact]
    public async Task GetBoards_ReturnsBoardsFromRequestedSemester()
    {
        DefenseBoardResult[] boards =
        [
            new(4, "HD-04", 2, CouncilType.Defense1, CouncilStatus.Pending, 11, 12, [11, 12, 13, 14, 15])
        ];
        var store = new FakeStore(boards);
        var service = new DefenseManagementService(store);

        IReadOnlyList<DefenseBoardResult> result =
            await service.GetBoardsAsync(2, CancellationToken.None);

        Assert.True(store.BoardsQueried);
        Assert.Equal(2, store.RequestedSemesterId);
        Assert.Equal("HD-04", Assert.Single(result).Code);
    }

    private sealed class FakeStore(IReadOnlyList<DefenseBoardResult> boards) : IDefenseManagementStore
    {
        public bool BoardsQueried { get; private set; }
        public int RequestedSemesterId { get; private set; }

        public Task<IReadOnlyList<DefenseBoardResult>> GetBoardsAsync(
            int semesterId, CancellationToken cancellationToken)
        {
            BoardsQueried = true;
            RequestedSemesterId = semesterId;
            return Task.FromResult(boards);
        }

        public Task<PagedResult<DefenseRoundResult>> GetRoundsAsync(
            PagedRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<int?> FindModeratorIdAsync(
            int userId, bool allowFallback, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> SemesterExistsAsync(
            int semesterId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> RoundCodeExistsAsync(
            int semesterId, string code, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<DefenseRoundResult> CreateRoundAsync(
            CreateDefenseRoundCommand input, int moderatorId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<int> CountExistingLecturersAsync(
            IReadOnlyCollection<int> lecturerIds, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<DefenseBoardResult> CreateBoardAsync(
            CreateDefenseBoardCommand input, int moderatorId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<bool> CouncilExistsAsync(
            int councilId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task AddCouncilMemberIfMissingAsync(
            int councilId, int lecturerId, CouncilMemberRole role, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<DefenseRoundState?> FindRoundAsync(
            int roundId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<DefenseCouncilState?> FindCouncilAsync(
            int councilId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<DefenseGroupState?> FindGroupAsync(
            int groupId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<DefenseSessionAssignmentResult> CreateSessionAsync(
            AssignDefenseSessionCommand input, int moderatorId, DefenseCouncilState council,
            DefenseGroupState group, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<int?> FindLecturerIdAsync(
            int userId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<DefenseSessionAssignmentResult>> GetLecturerSessionsAsync(
            int lecturerId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
