using CPMS.API.Contracts.Academic;
using CPMS.API.Controllers;
using CPMS.BLL.Academic.Import;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CPMS.IntegrationTests;

public sealed class ImportControllerTests
{
    [Fact]
    public async Task ImportGroups_RejectsOversizedFileBeforeCallingImportService()
    {
        var service = new RecordingGroupImportService();
        var controller = new ImportController(service);
        using var content = new MemoryStream(
            new byte[GroupImportLimits.MaximumWorkbookBytes + 1]);
        var file = new FormFile(content, 0, content.Length, "file", "groups.xlsx");

        ActionResult<GroupImportResult> result = await controller.ImportGroups(
            new ImportGroupsRequest(file, 1, 1), CancellationToken.None);

        ObjectResult response = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, response.StatusCode);
        Assert.False(service.WasCalled);
    }

    private sealed class RecordingGroupImportService : IGroupImportService
    {
        public bool WasCalled { get; private set; }

        public Task<GroupImportResult> ImportAsync(
            Stream stream,
            int semesterId,
            int? lecturerId = null,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return Task.FromResult(new GroupImportResult(0, 0, 0, 0, 0, 0));
        }
    }
}
