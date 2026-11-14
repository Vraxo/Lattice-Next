using Lattice.Core.Reasoning;
using Xunit;

namespace Lattice.Core.Tests.Reasoning;

public sealed class SemanticMeasurementTests
{
    [Fact]
    public void Loads_decimal_measurements_and_normalizes_them_to_si_units()
    {
        const string json = """
        {
          "formatVersion": 4, "id": "measurement-test", "name": "Measurement test",
          "concepts": [
            { "id": "entity", "label": "entity", "kind": "Type" },
            { "id": "measured-component", "label": "measured component", "kind": "Type" },
            { "id": "sample", "label": "sample", "kind": "Individual" }
          ],
          "lexemes": [],
          "relations": [
            { "id": "is-a", "domain": null, "range": null, "verb": "is a", "objectRole": null, "order": 0, "comparisonPriority": 0, "subjectKind": "Type", "objectKind": "Type" },
            { "id": "instance-of", "domain": null, "range": null, "verb": "is an instance of", "objectRole": null, "order": 0, "comparisonPriority": 0, "subjectKind": "Individual", "objectKind": "Type" }
          ],
          "definitions": [
            { "concept": "entity", "definitionKind": "primitive", "necessaryConditions": [], "sufficientExpression": null },
            { "concept": "measured-component", "definitionKind": "primitive", "necessaryConditions": [], "sufficientExpression": null }
          ],
          "relationAssertions": [
            { "subject": "measured-component", "relation": "is-a", "object": "entity" },
            { "subject": "sample", "relation": "instance-of", "object": "measured-component" }
          ],
          "dataProperties": [
            { "id": "length", "domainType": "measured-component", "valueType": "decimal", "dimensionUnit": "m" }
          ],
          "dataAssertions": [
            { "subject": "sample", "property": "length", "value": "25", "unit": "mm" }
          ]
        }
        """;
        var loader = new SemanticWorldModelLoader();
        Assert.True(loader.TryLoad(json, out var model, out var error), error);
        var assertion = Assert.Single(model!.DataAssertions);
        Assert.Equal("0.025", assertion.Value);
        Assert.Equal("m", assertion.Unit);
    }

    [Fact]
    public void Rejects_measurements_with_a_dimension_that_does_not_match_the_property()
    {
        const string json = """
        {
          "formatVersion": 4, "id": "invalid-measurement-test", "name": "Invalid measurement test",
          "concepts": [
            { "id": "entity", "label": "entity", "kind": "Type" },
            { "id": "measured-component", "label": "measured component", "kind": "Type" },
            { "id": "sample", "label": "sample", "kind": "Individual" }
          ],
          "lexemes": [],
          "relations": [
            { "id": "is-a", "domain": null, "range": null, "verb": "is a", "objectRole": null, "order": 0, "comparisonPriority": 0, "subjectKind": "Type", "objectKind": "Type" },
            { "id": "instance-of", "domain": null, "range": null, "verb": "is an instance of", "objectRole": null, "order": 0, "comparisonPriority": 0, "subjectKind": "Individual", "objectKind": "Type" }
          ],
          "definitions": [
            { "concept": "entity", "definitionKind": "primitive", "necessaryConditions": [], "sufficientExpression": null },
            { "concept": "measured-component", "definitionKind": "primitive", "necessaryConditions": [], "sufficientExpression": null }
          ],
          "relationAssertions": [
            { "subject": "measured-component", "relation": "is-a", "object": "entity" },
            { "subject": "sample", "relation": "instance-of", "object": "measured-component" }
          ],
          "dataProperties": [
            { "id": "length", "domainType": "measured-component", "valueType": "decimal", "dimensionUnit": "m" }
          ],
          "dataAssertions": [
            { "subject": "sample", "property": "length", "value": "10", "unit": "N" }
          ]
        }
        """;
        var loader = new SemanticWorldModelLoader();
        Assert.False(loader.TryLoad(json, out _, out _));
    }
}
