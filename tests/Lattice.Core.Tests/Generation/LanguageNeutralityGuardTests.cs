using Xunit;

namespace Lattice.Core.Tests.Generation;

/// <summary>
/// The second-language proof only holds while shared code stays neutral: a language's words belong in its pack.
/// These guards fail when a language name or a language spelling reappears in shared code.
/// </summary>
public sealed class LanguageNeutralityGuardTests
{
    private static readonly string[] LanguageNames = ["python", "javascript"];

    private static readonly string[] LanguageSpellings =
        ["print(", "console.log", "def ", "elif ", "else:", ".items()", "Object.entries"];

    /// <summary>
    /// The one shared file that names a language, and why it may: the recommendation catalogue describes
    /// software ecosystems, whose concepts the domain knowledge declares as `python-package`. It neither parses
    /// nor realizes a program, so it is recorded here rather than moved into a pack.
    /// </summary>
    private static readonly string[] RecordedLanguageReferences = ["TaskRecommendationResponder.cs"];

    [Fact]
    public void The_generation_kernel_names_no_language_and_spells_no_language_syntax()
    {
        var generationKernel = Path.Combine(RepositoryRoot(), "src", "Lattice.Core", "Generation");
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(generationKernel, "*.cs", SearchOption.AllDirectories))
        {
            foreach (var (number, line) in CodeLines(file))
            {
                offenders.AddRange(LanguageNames
                    .Where(name => line.Contains(name, StringComparison.OrdinalIgnoreCase))
                    .Select(name => $"{Path.GetFileName(file)}:{number} names '{name}'"));
                offenders.AddRange(LanguageSpellings
                    .Where(spelling => line.Contains(spelling, StringComparison.Ordinal))
                    .Select(spelling => $"{Path.GetFileName(file)}:{number} spells '{spelling}'"));
            }
        }

        Assert.True(
            offenders.Count == 0,
            $"Shared generation code must name no language and spell none: {string.Join("; ", offenders)}");
    }

    [Fact]
    public void Shared_code_names_a_language_only_where_the_history_records_it()
    {
        var offenders = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var project in new[] { Path.Combine("src", "Lattice.Core"), Path.Combine("src", "Lattice.Runtime") })
        {
            foreach (var file in Directory.EnumerateFiles(
                Path.Combine(RepositoryRoot(), project), "*.cs", SearchOption.AllDirectories))
            {
                var namesLanguage = CodeLines(file).Any(line => LanguageNames
                    .Any(name => line.Text.Contains(name, StringComparison.OrdinalIgnoreCase)));
                if (namesLanguage)
                {
                    offenders.Add(Path.GetFileName(file));
                }
            }
        }

        Assert.Equal(RecordedLanguageReferences, offenders.ToArray());
    }

    /// <summary>Every line of a source file that is not a comment, with its one-based number.</summary>
    private static IEnumerable<(int Number, string Text)> CodeLines(string file)
    {
        var lines = File.ReadAllLines(file);
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index].Trim();
            if (line.StartsWith("//", StringComparison.Ordinal) ||
                line.StartsWith("*", StringComparison.Ordinal) ||
                line.StartsWith("/*", StringComparison.Ordinal))
            {
                continue;
            }

            var comment = line.IndexOf("//", StringComparison.Ordinal);
            if (comment >= 0 && !line[..comment].Contains(':'))
            {
                line = line[..comment].Trim();
            }

            if (line.Length > 0)
            {
                yield return (index + 1, line);
            }
        }
    }

    private static string RepositoryRoot()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            DirectoryInfo? directory = new(start);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "knowledge", "languages", "python.core.json")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException(
            "Could not locate the repository root from the declared generation language packs.");
    }
}
