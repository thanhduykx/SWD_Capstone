using CPMS.Api.Configuration;
using Microsoft.Extensions.Configuration;

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
}
