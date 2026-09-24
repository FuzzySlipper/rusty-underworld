using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace AbyssRpg.Architecture.Tests;

public sealed class ArchitectureLawTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void Kit_does_not_encode_reference_ruleset_vocabulary_or_references()
    {
        string kit = SourceDirectory("AbyssRpg.Kit");
        string source = ReadSources(kit);

        // NOTE: "Abyss" as a place name stays a human review check — the Kit's
        // own namespace (AbyssRpg.Kit) contains that substring, so no
        // substring test can enforce it. Everything else below is exact.
        foreach (string forbidden in new[] { "UltimaUnderworld", "Ultima", "Underworld", "Stygian", "UW1", "UW2", "UU1", "UnderworldGodot", "OpenUnderground", "UnityUnderground" })
            Assert.DoesNotContain(forbidden, source, StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain("AbyssRpg.Rulesets.UltimaUnderworld", ProjectFile("AbyssRpg.Kit"), StringComparison.Ordinal);
    }

    [Fact]
    public void Project_references_follow_the_abyssrpg_dependency_graph()
    {
        AssertProjectReferences("UltimaUnderworld.Import", []);
        AssertProjectReferences("AbyssRpg.Kit", []);
        AssertProjectReferences("AbyssRpg.Rulesets.UltimaUnderworld", ["AbyssRpg.Kit"]);
        AssertProjectReferences("AbyssRpg.Host", ["AbyssRpg.Kit", "AbyssRpg.Rulesets.UltimaUnderworld"]);
        AssertProjectReferences("UltimaUnderworld.Import.Tool", ["UltimaUnderworld.Import"]);

        foreach (string project in ActiveRuntimeProjects()) AssertPackageReference(project, "Rusty.Engine");
    }

    [Fact]
    public void Offline_importer_is_not_a_runtime_dependency()
    {
        XDocument importer = XDocument.Load(ProjectFile("UltimaUnderworld.Import"));
        Assert.Empty(importer.Descendants("PackageReference"));

        foreach (string project in ActiveRuntimeProjects())
            Assert.DoesNotContain("UltimaUnderworld.Import", File.ReadAllText(ProjectFile(project)), StringComparison.Ordinal);
    }

    [Fact]
    public void Host_concrete_ruleset_references_stay_at_builtin_composition_seams()
    {
        // The product-composition seam: selection stays identity-level, and
        // only the product entry (AbyssProduct.cs) and its spatial session
        // (AbyssSpatialSession.cs) may name the concrete ruleset types they
        // compose. (A dagger-shaped IGameSession interface in Kit would
        // remove even that; carried as UW-T02 follow-up, not invented here.)
        string host = SourceDirectory("AbyssRpg.Host");
        string[] concreteReferences = SourceFiles(host)
            .Where(path => File.ReadAllText(path).Contains("AbyssRpg.Rulesets.UltimaUnderworld", StringComparison.Ordinal))
            .Select(path => Path.GetFileName(path)!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["AbyssProduct.cs", "AbyssSpatialSession.cs"], concreteReferences);
    }

    [Fact]
    public void Active_runtime_projects_reject_implicit_runtime_authorities()
    {
        (string Label, string Pattern)[] projectAndSourceForbidden =
        [
            ("reflection discovery", @"\b(System\.Reflection|Assembly\.(Load|GetAssemblies)|Type\.GetType|\.GetTypes\s*\()"),
            ("service locator", @"\b(IServiceProvider|ServiceProvider|GetRequiredService\s*\(|GetService\s*\()"),
            ("generic command dispatch", @"\b(ICommandBus|CommandBus|CommandDispatcher|DispatchCommand\s*\(|GenericCommand)"),
            ("runtime C# compilation", @"\b(CSharpCompilation|CodeDom|Microsoft\.CodeAnalysis|Roslyn)"),
            ("parallel update loop", @"\b(new\s+Thread\s*\(|Task\.Run\s*\(|PeriodicTimer\s*\(|System\.Threading\.Timer|System\.Timers\.Timer|while\s*\(\s*true\s*\))"),
        ];
        (string Label, string Pattern)[] safeCodeBoundaryEscapes =
        [
            ("unsafe code", @"\bunsafe\b"),
            ("handwritten native interop", @"\b(DllImport|LibraryImport|GCHandle|Native[A-Z]\w*)"),
        ];

        foreach (string project in ActiveRuntimeProjects())
        {
            foreach (string file in SourceFiles(SourceDirectory(project)))
            {
                string source = File.ReadAllText(file);
                AssertNoForbiddenPatterns(file, source, projectAndSourceForbidden);
                AssertNoForbiddenPatterns(file, source, safeCodeBoundaryEscapes);
            }

            string projectFile = ProjectFile(project);
            AssertNoForbiddenPatterns(projectFile, File.ReadAllText(projectFile), projectAndSourceForbidden);
        }
    }

    private static IEnumerable<string> ActiveRuntimeProjects() =>
    ["AbyssRpg.Kit", "AbyssRpg.Rulesets.UltimaUnderworld", "AbyssRpg.Host"];

    private static void AssertProjectReferences(string project, IReadOnlyList<string> expected)
    {
        XDocument document = XDocument.Load(ProjectFile(project));
        string[] actual = document.Descendants("ProjectReference")
            .Select(reference => (string?)reference.Attribute("Include"))
            .OfType<string>()
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(include => ProjectReferenceName(include))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected.OrderBy(name => name, StringComparer.Ordinal), actual);
    }

    private static string ProjectReferenceName(string include)
    {
        string fileName = Path.GetFileNameWithoutExtension(include);
        return fileName;
    }

    private static void AssertPackageReference(string project, string expected)
    {
        XDocument document = XDocument.Load(ProjectFile(project));
        string[] actual = document.Descendants("PackageReference")
            .Select(reference => (string?)reference.Attribute("Include"))
            .OfType<string>()
            .OrderBy(include => include, StringComparer.Ordinal)
            .ToArray();

        Assert.Contains(expected, actual);
        Assert.DoesNotContain(document.Descendants("ProjectReference"), reference =>
            ((string?)reference.Attribute("Include"))?.Contains("rusty-engine", StringComparison.OrdinalIgnoreCase) == true);

        Assert.Equal(
            "$(RustyEnginePackageVersion)",
            document.Descendants("PackageReference")
                .Single(reference => (string?)reference.Attribute("Include") == expected)
                .Attribute("Version")?.Value);
    }

    private static string ProjectFile(string project) => project.EndsWith(".Tests", StringComparison.Ordinal)
        ? Path.Combine(RepositoryRoot, "tests", project, $"{project}.csproj")
        : Path.Combine(RepositoryRoot, "src", project, $"{project}.csproj");

    private static string SourceDirectory(string project) => project.EndsWith(".Tests", StringComparison.Ordinal)
            ? Path.Combine(RepositoryRoot, "tests", project)
            : Path.Combine(RepositoryRoot, "src", project);

    private static string ReadSources(string path) => string.Join(
        Environment.NewLine,
        SourceFiles(path).Select(File.ReadAllText));

    private static void AssertNoForbiddenPatterns(string file, string source, IEnumerable<(string Label, string Pattern)> patterns)
    {
        foreach ((string label, string pattern) in patterns)
        {
            Assert.False(
                Regex.IsMatch(source, pattern, RegexOptions.CultureInvariant),
                $"{file}: contains forbidden {label} pattern.");
        }
    }

    private static IEnumerable<string> SourceFiles(string path) => Directory.Exists(path)
        ? Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj"))
        : [];

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))) return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root from the test output directory.");
    }
}
