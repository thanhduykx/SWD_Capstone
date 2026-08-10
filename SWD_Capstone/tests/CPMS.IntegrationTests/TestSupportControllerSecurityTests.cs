using CPMS.API.Controllers;
using CPMS.BLL.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace CPMS.IntegrationTests;

public sealed class TestSupportControllerSecurityTests
{
    [Fact]
    public void GetTestAccounts_IsHiddenOutsideDevelopment_EvenWhenConfiguredOn()
    {
        TestSupportController controller = CreateController(Environments.Production, exposeTestSupport: true);

        ActionResult<IReadOnlyCollection<TestAccountResponse>> result = controller.GetTestAccounts();

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public void GetTestAccounts_RequiresExplicitOptInDuringDevelopment()
    {
        TestSupportController controller = CreateController(Environments.Development, exposeTestSupport: false);

        ActionResult<IReadOnlyCollection<TestAccountResponse>> result = controller.GetTestAccounts();

        Assert.IsType<NotFoundResult>(result.Result);
    }

    private static TestSupportController CreateController(string environmentName, bool exposeTestSupport)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Swagger:ExposeTestSupport"] = exposeTestSupport.ToString(),
                ["TestData:DefaultPassword"] = "integration-test-password-only"
            })
            .Build();

        return new TestSupportController(
            new TestEnvironment { EnvironmentName = environmentName },
            configuration,
            new NoOpResetService());
    }

    private sealed class NoOpResetService : ITestDataResetService
    {
        public Task ResetAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "CPMS";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
