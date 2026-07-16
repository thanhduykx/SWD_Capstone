using System.Xml.Linq;

namespace CPMS.IntegrationTests;

public sealed class DependencyRulesTests
{
    [Fact]
    public void SourceTree_ContainsExactlyApiBllAndDalProjects()
    {
        var root = FindRepositoryRoot();
        IOrderedEnumerable<string?> projects = Directory.GetFiles(
                Path.Combine(root, "src"), "*.csproj", SearchOption.AllDirectories)
            .Select(Path.GetFileNameWithoutExtension)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase);

        Assert.Equal(["CPMS.API", "CPMS.BLL", "CPMS.DAL"], projects);
    }

    [Fact]
    public void BLL_HasNoProjectReferences()
    {
        var references = ProjectReferences("src/CPMS.BLL/CPMS.BLL.csproj");

        Assert.Empty(references);
    }

    [Fact]
    public void DAL_ReferencesOnlyBLL()
    {
        var references = ProjectReferences("src/CPMS.DAL/CPMS.DAL.csproj")
            .Select(Normalize)
            .ToArray();

        Assert.Equal(["../CPMS.BLL/CPMS.BLL.csproj"], references);
    }

    [Fact]
    public void API_ReferencesBLLAndDAL()
    {
        IOrderedEnumerable<string> references = ProjectReferences("src/CPMS.API/CPMS.API.csproj")
            .Select(Normalize)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase);

        Assert.Equal(
            ["../CPMS.BLL/CPMS.BLL.csproj", "../CPMS.DAL/CPMS.DAL.csproj"],
            references);
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
        IEnumerable<string> sources = Directory.GetFiles(bllRoot, "*.cs", SearchOption.AllDirectories)
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
