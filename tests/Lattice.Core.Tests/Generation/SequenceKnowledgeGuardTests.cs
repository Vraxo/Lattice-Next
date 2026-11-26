using System.Text.Json;
using System.Text.RegularExpressions;
using Lattice.Core.Generation;
using Xunit;

namespace Lattice.Core.Tests.Generation;

/// <summary>
/// Guards the sequence knowledge against the shortcuts the migration removed. The tests read the embedded
/// knowledge file itself, so they fail when a task identity, a fixed benchmark answer, or a complete
/// prompt-shaped program is reintroduced — including inside a family whose own removal ticket is still
/// blocked.
/// </summary>
public sealed class SequenceKnowledgeGuardTests
{
    /// <summary>
    /// The only rules allowed to emit a multi-line program today. Each one belongs to a family whose removal
    /// ticket is blocked, so the fragment cannot be deleted before its replacement exists.
    /// </summary>
    private static readonly string[] RecordedMultiLineRules =
    [
        // 9.22 — fixed parity filters and their sample data.
        "python-even-list-filter",
        "python-odd-value-list-parity-filter",
        "python-even-number-list-parity-filter",
        // 9.23 — fixed loop totals carrying the collection literals.
        // 9.24 — fixed dictionary mappings and iterations.
        "python-value-offset-dictionary-mapping",
        "python-number-square-dictionary-mapping",
        "python-name-length-dictionary-mapping",
        "python-dictionary-product-iteration",
        "python-dictionary-name-count-iteration",
        "python-dictionary-iteration-open",
        // 9.25 — fixed record sorting and grouped aggregation.
        "python-multi-field-record-sorting",
        "python-case-insensitive-multi-field-record-sorting",
        "single-field-record-sort-output",
        "single-field-record-sort-output-case-insensitive",
        "record-aggregation-output",
        // 8.14 — fixed sign classification.
        "python-sign-classification-positive",
        "python-sign-classification-negative",
        "python-sign-classification-zero"
    ];

    [Fact]
    public void Keeps_benchmark_and_task_identities_out_of_knowledge()
    {
        var knowledge = ReadKnowledge();

        Assert.DoesNotMatch(new Regex(@"(?i)\bpy-\d{3}\b"), knowledge);
        Assert.DoesNotMatch(new Regex(@"(?i)\bbenchmark\b"), knowledge);

        using var document = JsonDocument.Parse(knowledge);
        var factIds = document.RootElement.GetProperty("inputRules").EnumerateArray()
            .Select(rule => rule.GetProperty("factId").GetString()!)
            .Concat(document.RootElement.GetProperty("rules").EnumerateArray()
                .SelectMany(rule => rule.GetProperty("requiredFacts").EnumerateArray()
                    .Select(fact => fact.GetString()!)))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.All(
            factIds,
            factId => Assert.Matches(
                new Regex(@"^(language|intent|program|value)\.[a-z0-9.-]+$"),
                factId));
    }

    [Fact]
    public void Keeps_complete_solution_fragments_confined_to_recorded_families()
    {
        using var document = JsonDocument.Parse(ReadKnowledge());
        var multiLineRules = document.RootElement.GetProperty("rules").EnumerateArray()
            .Where(rule => EmittedLines(rule) >= 2)
            .Select(rule => rule.GetProperty("id").GetString()!)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            RecordedMultiLineRules.OrderBy(id => id, StringComparer.Ordinal).ToArray(),
            multiLineRules);
    }

    [Fact]
    public void Keeps_every_rule_and_validation_reachable_from_extraction()
    {
        using var document = JsonDocument.Parse(ReadKnowledge());
        var root = document.RootElement;

        var producedFacts = ProducibleFacts(root);

        var unreachable = root.GetProperty("rules").EnumerateArray()
            .SelectMany(rule => rule.GetProperty("requiredFacts").EnumerateArray()
                .Select(fact => (Rule: rule.GetProperty("id").GetString()!, Fact: fact.GetString()!)))
            .Where(item => !producedFacts.Contains(item.Fact))
            .ToArray();
        Assert.Empty(unreachable.Select(item => $"{item.Rule} needs {item.Fact}"));

        var unreachableValidations = root.GetProperty("validations").EnumerateArray()
            .SelectMany(validation => new[] { "factId", "appliesWhenFactId", "relatedFactId" }
                .Where(name => validation.TryGetProperty(name, out _))
                .Select(name => (
                    Validation: validation.GetProperty("id").GetString()!,
                    Fact: validation.GetProperty(name).GetString()!)))
            .Where(item => !producedFacts.Contains(item.Fact))
            .ToArray();
        Assert.Empty(unreachableValidations.Select(item => $"{item.Validation} needs {item.Fact}"));
    }

    [Fact]
    public void Generates_different_programs_when_the_request_values_change()
    {
        var small = Generate("Create a Python program that sets total to 5 then print total.");
        var large = Generate("Create a Python program that sets total to 9 then print total.");

        Assert.Contains("total = 5", small, StringComparison.Ordinal);
        Assert.Contains("total = 9", large, StringComparison.Ordinal);
        Assert.DoesNotContain("total = 9", small, StringComparison.Ordinal);
        Assert.DoesNotContain("total = 5", large, StringComparison.Ordinal);
    }

    [Fact]
    public void Generates_different_programs_when_the_request_names_change()
    {
        var classify = Generate(
            "Create a Python program with a function named classify that takes age and returns 'adult' " +
            "when age is at least 18 and otherwise returns 'minor', then print classify(20).");
        var label = Generate(
            "Create a Python program with a function named label that takes age and returns 'adult' " +
            "when age is at least 18 and otherwise returns 'minor', then print label(20).");

        Assert.Contains("def classify(age):", classify, StringComparison.Ordinal);
        Assert.Contains("print(classify(20))", classify, StringComparison.Ordinal);
        Assert.Contains("def label(age):", label, StringComparison.Ordinal);
        Assert.Contains("print(label(20))", label, StringComparison.Ordinal);
        Assert.DoesNotContain("label", classify, StringComparison.Ordinal);
        Assert.DoesNotContain("classify", label, StringComparison.Ordinal);
    }

    [Fact]
    public void Keeps_deferred_program_prefixes_backed_by_knowledge()
    {
        using var document = JsonDocument.Parse(ReadKnowledge());
        var producedFacts = ProducibleFacts(document.RootElement);

        var deadPrefixes = SemanticProgramPlanner.DeferredProgramFactPrefixes
            .Where(prefix => !producedFacts.Any(fact =>
                fact.StartsWith(prefix, StringComparison.Ordinal)))
            .ToArray();

        Assert.Empty(deadPrefixes.Select(prefix => $"{prefix} matches no producible fact"));
    }

    private static HashSet<string> ProducibleFacts(JsonElement root)
    {
        var producedFacts = new HashSet<string>(StringComparer.Ordinal)
        {
            "value.output",
            "value.output.expression"
        };

        foreach (var rule in root.GetProperty("inputRules").EnumerateArray())
        {
            producedFacts.Add(rule.GetProperty("factId").GetString()!);
            if (!rule.TryGetProperty("captures", out var captures))
            {
                continue;
            }

            foreach (var capture in captures.EnumerateArray())
            {
                if (capture.TryGetProperty("factId", out var captureFact))
                {
                    producedFacts.Add(captureFact.GetString()!);
                }
            }
        }

        foreach (var rule in root.GetProperty("rules").EnumerateArray())
        {
            if (!rule.TryGetProperty("satisfiesFacts", out var satisfies))
            {
                continue;
            }

            foreach (var fact in satisfies.EnumerateArray())
            {
                producedFacts.Add(fact.GetString()!);
            }
        }

        return producedFacts;
    }

    private static string Generate(string input)
    {
        Assert.True(
            SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var loadError),
            loadError);
        Assert.True(
            SequenceInputFactExtractor.TryExtract(knowledge!, input, out var facts, out var extractionError),
            extractionError);
        Assert.True(
            SemanticProgramPlanner.TryGenerateProgram(
                facts,
                LoadPythonPack(),
                out var code,
                out _,
                out var generationError),
            generationError);
        return code;
    }

    private static GenerationLanguagePack LoadPythonPack()
    {
        var loader = new GenerationLanguagePackLoader();
        Assert.True(loader.TryLoadEmbedded(out var packs, out var error), error);
        return packs["python"];
    }

    private static int EmittedLines(JsonElement rule)
    {
        var text = new System.Text.StringBuilder();
        if (rule.TryGetProperty("nextToken", out var nextToken))
        {
            text.Append(nextToken.GetString());
        }

        if (rule.TryGetProperty("tokenBinding", out var binding))
        {
            foreach (var name in new[] { "prefix", "suffix" })
            {
                if (binding.TryGetProperty(name, out var part))
                {
                    text.Append(part.GetString());
                }
            }
        }

        return text.ToString().Count(character => character == '\n');
    }

    private static string ReadKnowledge()
    {
        var assembly = typeof(SequenceKnowledgeBaseLoader).Assembly;
        var resource = assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith("SequenceKnowledge.python.core.json", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
