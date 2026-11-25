using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lattice.Core.Generation;
using Xunit;

namespace Lattice.Core.Tests;

public sealed class AaaDiagnosticsTests
{
    [Fact]
    public void Reports_which_declared_class_capture_is_invalid()
    {
        var assembly = typeof(SequenceKnowledgeBaseLoader).Assembly;
        var resource = assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith("python.core.json", StringComparison.Ordinal) && name.StartsWith("Lattice.Core.SequenceKnowledge.", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var document = JsonNode.Parse(reader.ReadToEnd())!.AsObject();
        var rules = document["inputRules"]!.AsArray();
        var declaration = rules
            .Select(node => node!.AsObject())
            .Single(node => (string?)node["id"] == "recognize-declared-class");
        var captures = declaration["captures"]!.AsArray();

        var results = new StringBuilder();
        for (var index = 0; index < captures.Count; index++)
        {
            var variant = JsonNode.Parse(document.ToJsonString())!.AsObject();
            var variantRule = JsonNode.Parse(declaration.ToJsonString())!.AsObject();
            var variantCaptures = variantRule["captures"]!.AsArray();
            for (var remove = variantCaptures.Count - 1; remove >= 0; remove--)
            {
                if (remove != index)
                {
                    variantCaptures.RemoveAt(remove);
                }
            }

            var ok = TryLoadSingle(variant, variantRule, out var error);
            results.Append($" c{index}={(ok ? "ok" : "FAIL")}");
            if (!ok)
            {
                results.Append($":[{(captures[index]!.ToJsonString().Length > 220 ? captures[index]!.ToJsonString()[..220] : captures[index]!.ToJsonString())}]");
            }
        }

        var bare = JsonNode.Parse(declaration.ToJsonString())!.AsObject();
        bare.Remove("captures");
        var bareOk = TryLoadSingle(
            JsonNode.Parse(document.ToJsonString())!.AsObject(),
            bare,
            out _);
        Assert.True(false, $"bare={bareOk}{results}");
    }

    private static bool TryLoadSingle(JsonObject document, JsonObject rule, out string? error)
    {
        var inputRules = document["inputRules"]!.AsArray();
        inputRules.Clear();
        inputRules.Add(rule);
        return SequenceKnowledgeBaseLoader.TryLoad(document.ToJsonString(), out _, out error);
    }
}
