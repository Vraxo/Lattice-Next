using Lattice.Core.Reasoning;
using Xunit;

namespace Lattice.Core.Tests.Reasoning;

public sealed class SemanticDirectedCycleTests
{
    [Fact]
    public void Derives_cycle_membership_from_a_directed_path_returning_to_its_start()
    {
        var loader = new SemanticWorldModelLoader();
        Assert.True(loader.TryLoad(CreateModel(), out var model, out var error), error);

        var reasoner = new SemanticWorldModelReasoner(model!);

        Assert.True(reasoner.IsInstanceOf("cycle-example", "closed-cycle"));
        Assert.False(reasoner.IsInstanceOf("open-example", "closed-cycle"));
        Assert.False(reasoner.IsInstanceOf("short-cycle-example", "closed-cycle"));

        var evidence = reasoner.GetMembershipEvidence("cycle-example", "closed-cycle");
        Assert.Contains(evidence, source => source.SourceId.EndsWith(
            ":relation:cycle-example:starts-at:node-a", StringComparison.Ordinal));
        Assert.Contains(evidence, source => source.SourceId.EndsWith(
            ":relation:node-a:next-node:node-b", StringComparison.Ordinal));
        Assert.Contains(evidence, source => source.SourceId.EndsWith(
            ":relation:node-b:next-node:node-c", StringComparison.Ordinal));
        Assert.Contains(evidence, source => source.SourceId.EndsWith(
            ":relation:node-c:next-node:node-a", StringComparison.Ordinal));
    }

    private static string CreateModel() =>
        """
        {
          "formatVersion": 3,
          "id": "directed-cycle-test",
          "name": "Directed cycle test",
          "concepts": [
            { "id": "entity", "label": "entity", "kind": "type" },
            { "id": "node", "label": "node", "kind": "type" },
            { "id": "closed-cycle", "label": "closed cycle", "kind": "type" },
            { "id": "cycle-example", "label": "cycle example", "kind": "individual" },
            { "id": "open-example", "label": "open example", "kind": "individual" },
            { "id": "short-cycle-example", "label": "short cycle example", "kind": "individual" },
            { "id": "node-a", "label": "node A", "kind": "individual" },
            { "id": "node-b", "label": "node B", "kind": "individual" },
            { "id": "node-c", "label": "node C", "kind": "individual" },
            { "id": "node-d", "label": "node D", "kind": "individual" },
            { "id": "node-e", "label": "node E", "kind": "individual" },
            { "id": "node-f", "label": "node F", "kind": "individual" },
            { "id": "node-g", "label": "node G", "kind": "individual" },
            { "id": "node-h", "label": "node H", "kind": "individual" },
            { "id": "node-i", "label": "node I", "kind": "individual" }
          ],
          "lexemes": [],
          "relations": [
            { "id": "is-a", "verb": "is a", "order": 0, "comparisonPriority": 0, "subjectKind": "type", "objectKind": "type" },
            { "id": "instance-of", "verb": "is an instance of", "order": 0, "comparisonPriority": 0, "subjectKind": "individual", "objectKind": "type" },
            { "id": "starts-at", "domain": "entity", "range": "node", "verb": "starts at", "order": 0, "comparisonPriority": 0 },
            { "id": "next-node", "domain": "node", "range": "node", "verb": "is followed by", "order": 1, "comparisonPriority": 0 }
          ],
          "definitions": [
            { "concept": "entity", "definitionKind": "primitive", "necessaryConditions": [], "sufficientExpression": null },
            { "concept": "node", "definitionKind": "primitive", "necessaryConditions": [], "sufficientExpression": null },
            {
              "concept": "closed-cycle",
              "definitionKind": "defined",
              "necessaryConditions": [],
              "sufficientExpression": {
                "kind": "cycle",
                "startRelation": "starts-at",
                "relation": "next-node",
                "targetType": "node",
                "minimumLength": 3
              }
            }
          ],
          "relationAssertions": [
            { "subject": "node", "relation": "is-a", "object": "entity" },
            { "subject": "closed-cycle", "relation": "is-a", "object": "entity" },
            { "subject": "cycle-example", "relation": "instance-of", "object": "entity" },
            { "subject": "open-example", "relation": "instance-of", "object": "entity" },
            { "subject": "short-cycle-example", "relation": "instance-of", "object": "entity" },
            { "subject": "node-a", "relation": "instance-of", "object": "node" },
            { "subject": "node-b", "relation": "instance-of", "object": "node" },
            { "subject": "node-c", "relation": "instance-of", "object": "node" },
            { "subject": "node-d", "relation": "instance-of", "object": "node" },
            { "subject": "node-e", "relation": "instance-of", "object": "node" },
            { "subject": "node-f", "relation": "instance-of", "object": "node" },
            { "subject": "node-g", "relation": "instance-of", "object": "node" },
            { "subject": "node-h", "relation": "instance-of", "object": "node" },
            { "subject": "node-i", "relation": "instance-of", "object": "node" },
            { "subject": "cycle-example", "relation": "starts-at", "object": "node-a" },
            { "subject": "node-a", "relation": "next-node", "object": "node-b" },
            { "subject": "node-b", "relation": "next-node", "object": "node-c" },
            { "subject": "node-c", "relation": "next-node", "object": "node-a" },
            { "subject": "open-example", "relation": "starts-at", "object": "node-d" },
            { "subject": "node-d", "relation": "next-node", "object": "node-e" },
            { "subject": "node-e", "relation": "next-node", "object": "node-f" },
            { "subject": "node-f", "relation": "next-node", "object": "node-g" },
            { "subject": "short-cycle-example", "relation": "starts-at", "object": "node-h" },
            { "subject": "node-h", "relation": "next-node", "object": "node-i" },
            { "subject": "node-i", "relation": "next-node", "object": "node-h" }
          ],
          "dataProperties": [],
          "dataAssertions": []
        }
        """;
}
