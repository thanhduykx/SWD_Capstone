using System.Xml.Linq;

namespace CPMS.IntegrationTests;

public sealed class DependencyRulesTests
{
    [Theory]
    [InlineData("src/CPMS.Domain/CPMS.Domain.csproj")]
    [InlineData("src/CPMS.Shared/CPMS.Shared.csproj")]
    public void InnerProjects_DoNotReferenceOtherProjects(string projectPath)
    {
        var references = ProjectReferences(projectPath);

        Assert.Empty(references);
    }

    [Fact]
    public void Application_ReferencesOnlyDomainAndShared()
    {
        var references = ProjectReferences("src/CPMS.Application/CPMS.Application.csproj");

        Assert.Equal(
            ["../CPMS.Domain/CPMS.Domain.csproj", "../CPMS.Shared/CPMS.Shared.csproj"],
            references.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).Select(Normalize));
    }

    [Fact]
    public void Infrastructure_ReferencesApplicationAndDomain()
    {
        var references = ProjectReferences("src/CPMS.Infrastructure/CPMS.Infrastructure.csproj")
            .Select(Normalize)
            .ToArray();

        Assert.Contains("../CPMS.Application/CPMS.Application.csproj", references);
        Assert.Contains("../CPMS.Domain/CPMS.Domain.csproj", references);
    }

    private static IReadOnlyCollection<string> ProjectReferences(string projectPath)
    {
        var root = FindRepositoryRoot();
        var document = XDocument.Load(Path.Combine(root, projectPath));
        return document
            .Descendants("ProjectReference")
            .Select(x => x.Attribute("Include")?.Value)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .ToArray();
    }

    private static string Normalize(string path) => path.Replace('\\', '/');

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CPMS.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Cannot find repository root.");
    }
}
