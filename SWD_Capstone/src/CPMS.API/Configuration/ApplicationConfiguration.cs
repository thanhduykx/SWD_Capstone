using Microsoft.Extensions.Configuration;

namespace CPMS.Api.Configuration;

internal static class ApplicationConfiguration
{
    internal static void AddDeploymentOverrides(ConfigurationManager configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        configuration.AddEnvironmentVariables();
    }
}
