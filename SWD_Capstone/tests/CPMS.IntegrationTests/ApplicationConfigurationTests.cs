using CPMS.API.Configuration;
using CPMS.DAL;
using CPMS.DAL.ExternalServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CPMS.IntegrationTests;

public sealed class ApplicationConfigurationTests
{
    [Fact]
    public void AddDeploymentOverrides_EnvironmentVariableOverridesFileValue()
    {
        const string configurationKey = "CpmsRegression:DeploymentOverride";
        const string environmentVariable = "CpmsRegression__DeploymentOverride";
        const string deploymentValue = "from-deployment-environment";
        var originalValue = Environment.GetEnvironmentVariable(environmentVariable);

        try
        {
            Environment.SetEnvironmentVariable(environmentVariable, deploymentValue);
            var configuration = new ConfigurationManager();
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [configurationKey] = "from-appsettings"
            });

            ApplicationConfiguration.AddDeploymentOverrides(configuration);

            Assert.Equal(deploymentValue, configuration[configurationKey]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(environmentVariable, originalValue);
        }
    }

    [Fact]
    public void AddDALServices_UsesAppSettingsOnlySnapshotForGemini()
    {
        var appSettings = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gemini:ApiKey"] = "appsettings-key",
                ["Gemini:Model"] = "gemini-2.5-flash",
                ["Gemini:TimeoutSeconds"] = "30"
            })
            .Build();
        var mergedConfiguration = new ConfigurationBuilder()
            .AddConfiguration(appSettings)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gemini:ApiKey"] = "environment-style-override"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDALServices(
            mergedConfiguration,
            appSettings.GetSection(GeminiOptions.SectionName));

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<GeminiOptions>();

        Assert.Equal("appsettings-key", options.ApiKey);
    }
}
