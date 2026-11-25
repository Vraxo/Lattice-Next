using System.Text;
using System.Text.Json.Nodes;
using Lattice.Core.Generation;
using Xunit;

namespace Lattice.Core.Tests;

public sealed class AaaDiagnosticsTests
{
    [Fact]
    public void Reports_which_capture_property_is_invalid()
    {
        var assembly = typeof(SequenceKnowledgeBaseLoader).Assembly;
        var resource = assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith("python.core.json", StringComparison.Ordinal) &&
                name.StartsWith("Lattice.Core.SequenceKnowledge.", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var document = JsonNode.Parse(reader.ReadToEnd())!.AsObject();
        var declaration = document["inputRules"]!.AsArray()
            .Select(node => node!.AsObject())
            .Single(node => (string?)node["id"] == "recognize-declared-class");

        var exact = """
            {"factId":"program.requirement.class.field","valueId":"field","startAfterTokens":["with","a"],"stopBeforeTokens":[","]}
            """;
        var variants = new (string Name, string Json)[]
        {
            ("exact", exact),
            ("noStop", """{"factId":"program.requirement.class.field","valueId":"field","startAfterTokens":["with","a"]}"""),
            ("noStart", """{"factId":"program.requirement.class.field","valueId":"field","stopBeforeTokens":[","]}"""),
            ("oneStart", """{"factId":"program.requirement.class.field","valueId":"field","startAfterTokens":["with"],"stopBeforeTokens":[","]}"""),
            ("wordStop", """{"factId":"program.requirement.class.field","valueId":"field","startAfterTokens":["with","a"],"stopBeforeTokens":["and"]}"""),
            ("classFact", """{"factId":"program.requirement.class","valueId":"field","startAfterTokens":["with","a"],"stopBeforeTokens":[","]}"""),
            ("otherValueId", """{"factId":"program.requirement.class.field","valueId":"fieldName","startAfterTokens":["with","a"],"stopBeforeTokens":[","]}"""),
            ("noRepeat", exact)
        };

        var results = new StringBuilder();
        foreach (var (name, json) in variants)
        {
            var variant = JsonNode.Parse(document.ToJsonString())!.AsObject();
            var rule = JsonNode.Parse(declaration.ToJsonString())!.AsObject();
            if (name == "noRepeat")
            {
                rule.Remove("repeatMatches");
            }

            var captures = rule["captures"]!.AsArray();
            captures.Clear();
            captures.Add(JsonNode.Parse(json));
            var inputRules = variant["inputRules"]!.AsArray();
            inputRules.Clear();
            inputRules.Add(rule);
            var ok = SequenceKnowledgeBaseLoader.TryLoad(variant.ToJsonString(), out _, out _);
            results.Append($" {name}={(ok ? "ok" : "FAIL")}");
        }

        Assert.True(false, results.ToString());
    }
}
