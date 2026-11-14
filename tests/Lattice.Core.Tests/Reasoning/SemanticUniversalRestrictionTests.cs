using Lattice.Core.Reasoning;
using Xunit;

namespace Lattice.Core.Tests.Reasoning;

public sealed class SemanticUniversalRestrictionTests
{
    [Fact]
    public void Polygon_requires_a_nonempty_closed_boundary_composed_only_of_line_segments()
    {
        var loader = new SemanticWorldModelLoader();
        Assert.True(loader.TryLoad(CreateModel(), out var model, out var error), error);

        var reasoner = new SemanticWorldModelReasoner(model!);

        Assert.True(reasoner.IsInstanceOf("valid-shape", "boundary"));
        Assert.True(reasoner.IsInstanceOf("valid-shape", "closed-boundary"));
        Assert.True(reasoner.IsInstanceOf("valid-shape", "polygon"));

        Assert.True(reasoner.IsInstanceOf("open-shape", "boundary"));
        Assert.False(reasoner.IsInstanceOf("open-shape", "closed-boundary"));
        Assert.False(reasoner.IsInstanceOf("open-shape", "polygon"));

        Assert.True(reasoner.IsInstanceOf("mixed-shape", "closed-boundary"));
        Assert.False(reasoner.IsInstanceOf("mixed-shape", "polygon"));

        Assert.True(reasoner.IsInstanceOf("curved-shape", "closed-boundary"));
        Assert.False(reasoner.IsInstanceOf("curved-shape", "polygon"));

        var evidence = reasoner.GetMembershipEvidence("valid-shape", "polygon");
        Assert.Contains(evidence, source => source.SourceId.EndsWith(
            ":relation:valid-boundary:has-boundary-element:valid-edge-one", StringComparison.Ordinal));
        Assert.Contains(evidence, source => source.SourceId.EndsWith(
            ":relation:valid-boundary:has-boundary-element:valid-edge-two", StringComparison.Ordinal));
    }

    private static string CreateModel() =>
        """
        {
          "formatVersion": 3,
          "id": "universal-restriction-test",
          "name": "Universal restriction test",
          "concepts": [
            { "id": "entity", "label": "entity", "kind": "type" },
            { "id": "shape", "label": "shape", "kind": "type" },
            { "id": "boundary", "label": "boundary", "kind": "type" },
            { "id": "closed-cycle", "label": "closed cycle", "kind": "type" },
            { "id": "closed-boundary", "label": "closed boundary", "kind": "type" },
            { "id": "geometric-element", "label": "geometric element", "kind": "type" },
            { "id": "line-segment", "label": "line segment", "kind": "type" },
            { "id": "curved-element", "label": "curved element", "kind": "type" },
            { "id": "polygon", "label": "polygon", "kind": "type" },
            { "id": "valid-shape", "label": "valid shape", "kind": "individual" },
            { "id": "open-shape", "label": "open shape", "kind": "individual" },
            { "id": "mixed-shape", "label": "mixed shape", "kind": "individual" },
            { "id": "curved-shape", "label": "curved shape", "kind": "individual" },
            { "id": "valid-boundary", "label": "valid boundary", "kind": "individual" },
            { "id": "open-boundary", "label": "open boundary", "kind": "individual" },
            { "id": "mixed-boundary", "label": "mixed boundary", "kind": "individual" },
            { "id": "curved-boundary", "label": "curved boundary", "kind": "individual" },
            { "id": "valid-cycle", "label": "valid cycle", "kind": "individual" },
            { "id": "mixed-cycle", "label": "mixed cycle", "kind": "individual" },
            { "id": "curved-cycle", "label": "curved cycle", "kind": "individual" },
            { "id": "valid-edge-one", "label": "valid edge one", "kind": "individual" },
            { "id": "valid-edge-two", "label": "valid edge two", "kind": "individual" },
            { "id": "open-edge", "label": "open edge", "kind": "individual" },
            { "id": "mixed-edge", "label": "mixed edge", "kind": "individual" },
            { "id": "mixed-curve", "label": "mixed curve", "kind": "individual" },
            { "id": "curved-edge", "label": "curved edge", "kind": "individual" }
          ],
          "lexemes": [],
          "relations": [
            { "id": "is-a", "verb": "is a", "order": 0, "comparisonPriority": 0, "subjectKind": "type", "objectKind": "type" },
            { "id": "instance-of", "verb": "is an instance of", "order": 0, "comparisonPriority": 0, "subjectKind": "individual", "objectKind": "type" },
            { "id": "has-boundary", "domain": "shape", "range": "boundary", "verb": "has", "order": 1, "comparisonPriority": 0 },
            { "id": "has-boundary-element", "domain": "boundary", "range": "geometric-element", "verb": "contains", "order": 2, "comparisonPriority": 0 },
            { "id": "has-topology", "domain": "boundary", "range": "closed-cycle", "verb": "has", "order": 3, "comparisonPriority": 0 }
          ],
          "definitions": [
            { "concept": "entity", "definitionKind": "primitive", "necessaryConditions": [], "sufficientExpression": null },
            { "concept": "shape", "definitionKind": "primitive", "necessaryConditions": [], "sufficientExpression": null },
            { "concept": "boundary", "definitionKind": "defined", "necessaryConditions": [], "sufficientExpression": { "kind": "some", "relation": "has-boundary-element", "targetType": "geometric-element" } },
            { "concept": "closed-cycle", "definitionKind": "primitive", "necessaryConditions": [], "sufficientExpression": null },
            { "concept": "closed-boundary", "definitionKind": "defined", "necessaryConditions": [], "sufficientExpression": { "kind": "and", "operands": [
              { "kind": "some", "relation": "has-boundary-element", "targetType": "geometric-element" },
              { "kind": "some", "relation": "has-topology", "targetType": "closed-cycle" }
            ] } },
            { "concept": "geometric-element", "definitionKind": "primitive", "necessaryConditions": [], "sufficientExpression": null },
            { "concept": "line-segment", "definitionKind": "primitive", "necessaryConditions": [], "sufficientExpression": null },
            { "concept": "curved-element", "definitionKind": "primitive", "necessaryConditions": [], "sufficientExpression": null },
            { "concept": "polygon", "definitionKind": "defined", "necessaryConditions": [], "sufficientExpression": { "kind": "some", "relation": "has-boundary", "targetType": "closed-boundary", "filler": { "kind": "and", "operands": [
              { "kind": "some", "relation": "has-boundary-element", "targetType": "line-segment" },
              { "kind": "all", "relation": "has-boundary-element", "targetType": "line-segment" }
            ] } } }
          ],
          "relationAssertions": [
            { "subject": "shape", "relation": "is-a", "object": "entity" },
            { "subject": "boundary", "relation": "is-a", "object": "entity" },
            { "subject": "closed-cycle", "relation": "is-a", "object": "entity" },
            { "subject": "closed-boundary", "relation": "is-a", "object": "boundary" },
            { "subject": "geometric-element", "relation": "is-a", "object": "entity" },
            { "subject": "line-segment", "relation": "is-a", "object": "geometric-element" },
            { "subject": "curved-element", "relation": "is-a", "object": "geometric-element" },
            { "subject": "polygon", "relation": "is-a", "object": "shape" },
            { "subject": "valid-shape", "relation": "instance-of", "object": "shape" },
            { "subject": "open-shape", "relation": "instance-of", "object": "shape" },
            { "subject": "mixed-shape", "relation": "instance-of", "object": "shape" },
            { "subject": "curved-shape", "relation": "instance-of", "object": "shape" },
            { "subject": "valid-boundary", "relation": "instance-of", "object": "boundary" },
            { "subject": "open-boundary", "relation": "instance-of", "object": "boundary" },
            { "subject": "mixed-boundary", "relation": "instance-of", "object": "boundary" },
            { "subject": "curved-boundary", "relation": "instance-of", "object": "boundary" },
            { "subject": "valid-cycle", "relation": "instance-of", "object": "closed-cycle" },
            { "subject": "mixed-cycle", "relation": "instance-of", "object": "closed-cycle" },
            { "subject": "curved-cycle", "relation": "instance-of", "object": "closed-cycle" },
            { "subject": "valid-edge-one", "relation": "instance-of", "object": "line-segment" },
            { "subject": "valid-edge-two", "relation": "instance-of", "object": "line-segment" },
            { "subject": "open-edge", "relation": "instance-of", "object": "line-segment" },
            { "subject": "mixed-edge", "relation": "instance-of", "object": "line-segment" },
            { "subject": "mixed-curve", "relation": "instance-of", "object": "curved-element" },
            { "subject": "curved-edge", "relation": "instance-of", "object": "curved-element" },
            { "subject": "valid-shape", "relation": "has-boundary", "object": "valid-boundary" },
            { "subject": "open-shape", "relation": "has-boundary", "object": "open-boundary" },
            { "subject": "mixed-shape", "relation": "has-boundary", "object": "mixed-boundary" },
            { "subject": "curved-shape", "relation": "has-boundary", "object": "curved-boundary" },
            { "subject": "valid-boundary", "relation": "has-boundary-element", "object": "valid-edge-one" },
            { "subject": "valid-boundary", "relation": "has-boundary-element", "object": "valid-edge-two" },
            { "subject": "open-boundary", "relation": "has-boundary-element", "object": "open-edge" },
            { "subject": "mixed-boundary", "relation": "has-boundary-element", "object": "mixed-edge" },
            { "subject": "mixed-boundary", "relation": "has-boundary-element", "object": "mixed-curve" },
            { "subject": "curved-boundary", "relation": "has-boundary-element", "object": "curved-edge" },
            { "subject": "valid-boundary", "relation": "has-topology", "object": "valid-cycle" },
            { "subject": "mixed-boundary", "relation": "has-topology", "object": "mixed-cycle" },
            { "subject": "curved-boundary", "relation": "has-topology", "object": "curved-cycle" }
          ],
          "dataProperties": [],
          "dataAssertions": []
        }
        """;
}
