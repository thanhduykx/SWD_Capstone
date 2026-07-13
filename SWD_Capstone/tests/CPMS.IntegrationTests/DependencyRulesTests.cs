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
    public void BLL_ReferencesOnlyDomainAndShared()
    {
        var references = ProjectReferences("src/CPMS.BLL/CPMS.BLL.csproj");

        Assert.Equal(
            ["../CPMS.Domain/CPMS.Domain.csproj", "../CPMS.Shared/CPMS.Shared.csproj"],
            references.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).Select(Normalize));
    }

    [Fact]
    public void DAL_ReferencesBLLAndDomain()
    {
        var references = ProjectReferences("src/CPMS.DAL/CPMS.DAL.csproj")
            .Select(Normalize)
            .ToArray();

        Assert.Contains("../CPMS.BLL/CPMS.BLL.csproj", references);
        Assert.Contains("../CPMS.Domain/CPMS.Domain.csproj", references);
    }

    [Fact]
    public void PresentationEndpoints_DoNotAccessDalOrEntityFramework()
    {
        var root = FindRepositoryRoot();
        var endpointRoots = new[]
        {
            Path.Combine(root, "src", "CPMS.API", "Controllers"),
            Path.Combine(root, "src", "CPMS.API", "Hubs"),
            Path.Combine(root, "src", "CPMS.API", "Services")
        };
        foreach (var sourcePath in endpointRoots.SelectMany(path =>
                     Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories)))
        {
            var source = File.ReadAllText(sourcePath);
            Assert.DoesNotContain("CpmsDbContext", source, StringComparison.Ordinal);
            Assert.DoesNotContain("Microsoft.EntityFrameworkCore", source, StringComparison.Ordinal);
            Assert.DoesNotContain("CPMS.DAL", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void BLL_DoesNotReferenceEntityFrameworkOrDAL()
    {
        var root = FindRepositoryRoot();
        var bllRoot = Path.Combine(root, "src", "CPMS.BLL");
        var sources = Directory.GetFiles(bllRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(File.ReadAllText);

        foreach (var source in sources)
        {
            Assert.DoesNotContain("Microsoft.EntityFrameworkCore", source, StringComparison.Ordinal);
            Assert.DoesNotContain("CPMS.DAL", source, StringComparison.Ordinal);
        }
    }

    private static string[] ProjectReferences(string projectPath)
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
