using System.Text.Json;
using System.Text.RegularExpressions;
using Lattice.Core.Generation;
using Xunit;

namespace Lattice.Core.Tests.Generation;

/// <summary>
/// Guards the sequence knowledge against the shortcuts the migration removed. The tests read the embedded
/// knowledge file itself, so they fail when a task identity, a fixed benchmark answer, a stored token rule,
/// or a complete prompt-shaped program is reintroduced.
/// </summary>
public sealed class SequenceKnowledgeGuardTests
{
    [Fact]
    public void Keeps_benchmark_and_task_identities_out_of_knowledge()
    {
        var knowledge = ReadKnowledge();

        Assert.DoesNotMatch(new Regex(@"(?i)\bpy-\d{3}\b"), knowledge);
        Assert.DoesNotMatch(new Regex(@"(?i)\bbenchmark\b"), knowledge);

        using var document = JsonDocument.Parse(knowledge);
        var factIds = document.RootElement.GetProperty("inputRules").EnumerateArray()
            .Select(rule => rule.GetProperty("factId").GetString()!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.All(
            factIds,
            factId => Assert.Matches(
                new Regex(@"^(language|intent|program|value)\.[a-z0-9.-]+$"),
                factId));
    }

    [Fact]
    public void Stores_no_token_rule_that_could_emit_a_program()
    {
        using var document = JsonDocument.Parse(ReadKnowledge());

        Assert.False(document.RootElement.TryGetProperty("rules", out var rules) &&
            rules.GetArrayLength() > 0);
        Assert.False(document.RootElement.TryGetProperty("encodings", out var encodings) &&
            encodings.GetArrayLength() > 0);
    }

    [Fact]
    public void Keeps_every_validation_reachable_from_extraction()
    {
        using var document = JsonDocument.Parse(ReadKnowledge());
        var root = document.RootElement;

        var producedFacts = ProducibleFacts(root);

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
