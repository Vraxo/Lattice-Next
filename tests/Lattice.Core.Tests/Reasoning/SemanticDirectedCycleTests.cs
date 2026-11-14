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
        Assert.False(reasoner.IsInstanceOf("discontinuous-example", "closed-cycle"));

        var evidence = reasoner.GetMembershipEvidence("cycle-example", "closed-cycle");
        Assert.Contains(evidence, source => source.SourceId.EndsWith(
            ":relation:cycle-example:has-first-node:node-a", StringComparison.Ordinal));
        Assert.Contains(evidence, source => source.SourceId.EndsWith(
            ":relation:node-a:next-node:node-b", StringComparison.Ordinal));
        Assert.Contains(evidence, source => source.SourceId.EndsWith(
            ":relation:node-b:next-node:node-c", StringComparison.Ordinal));
        Assert.Contains(evidence, source => source.SourceId.EndsWith(
            ":relation:node-c:next-node:node-a", StringComparison.Ordinal));
        Assert.Contains(evidence, source => source.SourceId.EndsWith(
            ":relation:node-a:ends-at:point-two", StringComparison.Ordinal));
        Assert.Contains(evidence, source => source.SourceId.EndsWith(
            ":relation:node-b:starts-at:point-two", StringComparison.Ordinal));
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
            { "id": "point", "label": "point", "kind": "type" },
            { "id": "closed-cycle", "label": "closed cycle", "kind": "type" },
            { "id": "cycle-example", "label": "cycle example", "kind": "individual" },
            { "id": "open-example", "label": "open example", "kind": "individual" },
            { "id": "short-cycle-example", "label": "short cycle example", "kind": "individual" },
            { "id": "discontinuous-example", "label": "discontinuous example", "kind": "individual" },
            { "id": "node-a", "label": "node A", "kind": "individual" },
            { "id": "node-b", "label": "node B", "kind": "individual" },
            { "id": "node-c", "label": "node C", "kind": "individual" },
            { "id": "node-d", "label": "node D", "kind": "individual" },
            { "id": "node-e", "label": "node E", "kind": "individual" },
            { "id": "node-f", "label": "node F", "kind": "individual" },
            { "id": "node-g", "label": "node G", "kind": "individual" },
            { "id": "node-h", "label": "node H", "kind": "individual" },
            { "id": "node-i", "label": "node I", "kind": "individual" },
            { "id": "node-j", "label": "node J", "kind": "individual" },
            { "id": "node-k", "label": "node K", "kind": "individual" },
            { "id": "node-l", "label": "node L", "kind": "individual" },
            { "id": "point-one", "label": "point one", "kind": "individual" },
            { "id": "point-two", "label": "point two", "kind": "individual" },
            { "id": "point-three", "label": "point three", "kind": "individual" },
            { "id": "point-four", "label": "point four", "kind": "individual" },
            { "id": "point-five", "label": "point five", "kind": "individual" },
            { "id": "point-six", "label": "point six", "kind": "individual" },
            { "id": "point-seven", "label": "point seven", "kind": "individual" },
            { "id": "point-eight", "label": "point eight", "kind": "individual" },
            { "id": "point-nine", "label": "point nine", "kind": "individual" }
          ],
          "lexemes": [],
          "relations": [
            { "id": "is-a", "verb": "is a", "order": 0, "comparisonPriority": 0, "subjectKind": "type", "objectKind": "type" },
            { "id": "instance-of", "verb": "is an instance of", "order": 0, "comparisonPriority": 0, "subjectKind": "individual", "objectKind": "type" },
            { "id": "has-first-node", "domain": "entity", "range": "node", "verb": "starts at", "order": 0, "comparisonPriority": 0 },
            { "id": "next-node", "domain": "node", "range": "node", "verb": "is followed by", "order": 1, "comparisonPriority": 0 },
            { "id": "starts-at", "domain": "node", "range": "point", "verb": "has start point", "order": 2, "comparisonPriority": 0 },
            { "id": "ends-at", "domain": "node", "range": "point", "verb": "has end point", "order": 3, "comparisonPriority": 0 }
          ],
          "definitions": [
            { "concept": "entity", "definitionKind": "primitive", "necessaryConditions": [], "sufficientExpression": null },
            { "concept": "node", "definitionKind": "primitive", "necessaryConditions": [], "sufficientExpression": null },
            { "concept": "point", "definitionKind": "primitive", "necessaryConditions": [], "sufficientExpression": null },
            {
              "concept": "closed-cycle",
              "definitionKind": "defined",
              "necessaryConditions": [],
              "sufficientExpression": {
                "kind": "cycle",
                "startRelation": "has-first-node",
                "relation": "next-node",
                "targetType": "node",
                "minimumLength": 3,
                "sourceConnectionRelation": "ends-at",
                "targetConnectionRelation": "starts-at"
              }
            }
          ],
          "relationAssertions": [
            { "subject": "node", "relation": "is-a", "object": "entity" },
            { "subject": "point", "relation": "is-a", "object": "entity" },
            { "subject": "closed-cycle", "relation": "is-a", "object": "entity" },
            { "subject": "cycle-example", "relation": "instance-of", "object": "entity" },
            { "subject": "open-example", "relation": "instance-of", "object": "entity" },
            { "subject": "short-cycle-example", "relation": "instance-of", "object": "entity" },
            { "subject": "discontinuous-example", "relation": "instance-of", "object": "entity" },
            { "subject": "node-a", "relation": "instance-of", "object": "node" },
            { "subject": "node-b", "relation": "instance-of", "object": "node" },
            { "subject": "node-c", "relation": "instance-of", "object": "node" },
            { "subject": "node-d", "relation": "instance-of", "object": "node" },
            { "subject": "node-e", "relation": "instance-of", "object": "node" },
            { "subject": "node-f", "relation": "instance-of", "object": "node" },
            { "subject": "node-g", "relation": "instance-of", "object": "node" },
            { "subject": "node-h", "relation": "instance-of", "object": "node" },
            { "subject": "node-i", "relation": "instance-of", "object": "node" },
            { "subject": "node-j", "relation": "instance-of", "object": "node" },
            { "subject": "node-k", "relation": "instance-of", "object": "node" },
            { "subject": "node-l", "relation": "instance-of", "object": "node" },
            { "subject": "point-one", "relation": "instance-of", "object": "point" },
            { "subject": "point-two", "relation": "instance-of", "object": "point" },
            { "subject": "point-three", "relation": "instance-of", "object": "point" },
            { "subject": "point-four", "relation": "instance-of", "object": "point" },
            { "subject": "point-five", "relation": "instance-of", "object": "point" },
            { "subject": "point-six", "relation": "instance-of", "object": "point" },
            { "subject": "point-seven", "relation": "instance-of", "object": "point" },
            { "subject": "point-eight", "relation": "instance-of", "object": "point" },
            { "subject": "point-nine", "relation": "instance-of", "object": "point" },
            { "subject": "cycle-example", "relation": "has-first-node", "object": "node-a" },
            { "subject": "node-a", "relation": "next-node", "object": "node-b" },
            { "subject": "node-b", "relation": "next-node", "object": "node-c" },
            { "subject": "node-c", "relation": "next-node", "object": "node-a" },
            { "subject": "open-example", "relation": "has-first-node", "object": "node-d" },
            { "subject": "node-d", "relation": "next-node", "object": "node-e" },
            { "subject": "node-e", "relation": "next-node", "object": "node-f" },
            { "subject": "node-f", "relation": "next-node", "object": "node-g" },
            { "subject": "short-cycle-example", "relation": "has-first-node", "object": "node-h" },
            { "subject": "node-h", "relation": "next-node", "object": "node-i" },
            { "subject": "node-i", "relation": "next-node", "object": "node-h" },
            { "subject": "node-a", "relation": "starts-at", "object": "point-one" },
            { "subject": "node-a", "relation": "ends-at", "object": "point-two" },
            { "subject": "node-b", "relation": "starts-at", "object": "point-two" },
            { "subject": "node-b", "relation": "ends-at", "object": "point-three" },
            { "subject": "node-c", "relation": "starts-at", "object": "point-three" },
            { "subject": "node-c", "relation": "ends-at", "object": "point-one" },
            { "subject": "node-h", "relation": "starts-at", "object": "point-four" },
            { "subject": "node-h", "relation": "ends-at", "object": "point-five" },
            { "subject": "node-i", "relation": "starts-at", "object": "point-five" },
            { "subject": "node-i", "relation": "ends-at", "object": "point-four" },
            { "subject": "discontinuous-example", "relation": "has-first-node", "object": "node-j" },
            { "subject": "node-j", "relation": "next-node", "object": "node-k" },
            { "subject": "node-k", "relation": "next-node", "object": "node-l" },
            { "subject": "node-l", "relation": "next-node", "object": "node-j" },
            { "subject": "node-j", "relation": "starts-at", "object": "point-nine" },
            { "subject": "node-j", "relation": "ends-at", "object": "point-six" },
            { "subject": "node-k", "relation": "starts-at", "object": "point-seven" },
            { "subject": "node-k", "relation": "ends-at", "object": "point-eight" },
            { "subject": "node-l", "relation": "starts-at", "object": "point-eight" },
            { "subject": "node-l", "relation": "ends-at", "object": "point-nine" }
          ],
          "dataProperties": [],
          "dataAssertions": []
        }
        """;
}
