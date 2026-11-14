using Lattice.Core.Reasoning;
using Xunit;

namespace Lattice.Core.Tests.Reasoning;

public sealed class SemanticGeometryTopologyTests
{
    [Fact]
    public void Represents_ordered_boundary_elements_with_shared_endpoints_and_explicit_closure()
    {
        const string json = """
            {
              "formatVersion": 3,
              "id": "geometry-topology-test",
              "name": "Geometry topology test",
              "concepts": [
                { "id": "entity", "label": "entity", "kind": "type" },
                { "id": "geometric-element", "label": "geometric element", "kind": "type" },
                { "id": "geometric-point", "label": "geometric point", "kind": "type" },
                { "id": "edge-one", "label": "first edge", "kind": "individual" },
                { "id": "edge-two", "label": "second edge", "kind": "individual" },
                { "id": "edge-three", "label": "third edge", "kind": "individual" },
                { "id": "point-one", "label": "first point", "kind": "individual" },
                { "id": "point-two", "label": "second point", "kind": "individual" },
                { "id": "point-three", "label": "third point", "kind": "individual" }
              ],
              "lexemes": [],
              "relations": [
                { "id": "is-a", "verb": "is a", "order": 0, "comparisonPriority": 0, "subjectKind": "type", "objectKind": "type" },
                { "id": "instance-of", "verb": "is an instance of", "order": 0, "comparisonPriority": 0, "subjectKind": "individual", "objectKind": "type" },
                { "id": "next-element", "domain": "geometric-element", "range": "geometric-element", "verb": "is followed by", "order": 0, "comparisonPriority": 0 },
                { "id": "has-start-point", "domain": "geometric-element", "range": "geometric-point", "verb": "has start point", "order": 1, "comparisonPriority": 0 },
                { "id": "has-end-point", "domain": "geometric-element", "range": "geometric-point", "verb": "has end point", "order": 2, "comparisonPriority": 0 }
              ],
              "definitions": [
                { "concept": "entity", "definitionKind": "primitive", "necessaryConditions": [], "sufficientExpression": null },
                { "concept": "geometric-element", "definitionKind": "primitive", "necessaryConditions": [], "sufficientExpression": null },
                { "concept": "geometric-point", "definitionKind": "primitive", "necessaryConditions": [], "sufficientExpression": null }
              ],
              "relationAssertions": [
                { "subject": "geometric-element", "relation": "is-a", "object": "entity" },
                { "subject": "geometric-point", "relation": "is-a", "object": "geometric-element" },
                { "subject": "edge-one", "relation": "instance-of", "object": "geometric-element" },
                { "subject": "edge-two", "relation": "instance-of", "object": "geometric-element" },
                { "subject": "edge-three", "relation": "instance-of", "object": "geometric-element" },
                { "subject": "point-one", "relation": "instance-of", "object": "geometric-point" },
                { "subject": "point-two", "relation": "instance-of", "object": "geometric-point" },
                { "subject": "point-three", "relation": "instance-of", "object": "geometric-point" },
                { "subject": "edge-one", "relation": "next-element", "object": "edge-two" },
                { "subject": "edge-two", "relation": "next-element", "object": "edge-three" },
                { "subject": "edge-three", "relation": "next-element", "object": "edge-one" },
                { "subject": "edge-one", "relation": "has-start-point", "object": "point-one" },
                { "subject": "edge-one", "relation": "has-end-point", "object": "point-two" },
                { "subject": "edge-two", "relation": "has-start-point", "object": "point-two" },
                { "subject": "edge-two", "relation": "has-end-point", "object": "point-three" },
                { "subject": "edge-three", "relation": "has-start-point", "object": "point-three" },
                { "subject": "edge-three", "relation": "has-end-point", "object": "point-one" }
              ],
              "dataProperties": [],
              "dataAssertions": []
            }
            """;

        var loader = new SemanticWorldModelLoader();
        Assert.True(loader.TryLoad(json, out var model, out var error), error);

        var reasoner = new SemanticWorldModelReasoner(model!);

        Assert.Equal("edge-two", Assert.Single(reasoner.GetRelations("edge-one", "next-element")).Object);
        Assert.Equal("edge-three", Assert.Single(reasoner.GetRelations("edge-two", "next-element")).Object);
        Assert.Equal("edge-one", Assert.Single(reasoner.GetRelations("edge-three", "next-element")).Object);

        Assert.Equal(
            Assert.Single(reasoner.GetRelations("edge-one", "has-end-point")).Object,
            Assert.Single(reasoner.GetRelations("edge-two", "has-start-point")).Object);
        Assert.Equal(
            Assert.Single(reasoner.GetRelations("edge-two", "has-end-point")).Object,
            Assert.Single(reasoner.GetRelations("edge-three", "has-start-point")).Object);
        Assert.Equal(
            Assert.Single(reasoner.GetRelations("edge-three", "has-end-point")).Object,
            Assert.Single(reasoner.GetRelations("edge-one", "has-start-point")).Object);
    }
}
