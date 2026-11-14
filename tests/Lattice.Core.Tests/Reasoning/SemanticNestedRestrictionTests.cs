using Lattice.Core.Reasoning;
using Xunit;

namespace Lattice.Core.Tests.Reasoning;

public sealed class SemanticNestedRestrictionTests
{
    private const string ModelJson = """
        {
          "formatVersion": 3,
          "id": "nested-restriction-test",
          "name": "Nested restriction test",
          "concepts": [
            { "id": "entity", "label": "entity", "kind": "type" },
            { "id": "physical-component", "label": "physical component", "kind": "type" },
            { "id": "sound-production-mechanism", "label": "sound production mechanism", "kind": "type" },
            { "id": "mechanical-state", "label": "mechanical state", "kind": "type" },
            { "id": "tensioned-state", "label": "tensioned state", "kind": "type" },
            { "id": "sound-event", "label": "sound event", "kind": "type" },
            { "id": "resonator", "label": "resonator", "kind": "type" },
            { "id": "string-component", "label": "string component", "kind": "type" },
            { "id": "striking-component", "label": "striking component", "kind": "type" },
            { "id": "struck-string-mechanism", "label": "struck string mechanism", "kind": "type" },
            { "id": "piano-action", "label": "piano action", "kind": "individual" },
            { "id": "unrelated-action", "label": "unrelated action", "kind": "individual" },
            { "id": "hammer", "label": "hammer", "kind": "individual" },
            { "id": "loose-component", "label": "loose component", "kind": "individual" },
            { "id": "string", "label": "string", "kind": "individual" },
            { "id": "board", "label": "soundboard", "kind": "individual" },
            { "id": "tension", "label": "tension", "kind": "individual" },
            { "id": "sound", "label": "sound", "kind": "individual" }
          ],
          "lexemes": [],
          "relations": [
            { "id": "is-a", "verb": "is a", "order": 0, "comparisonPriority": 0, "subjectKind": "type", "objectKind": "type" },
            { "id": "instance-of", "verb": "is an instance of", "order": 0, "comparisonPriority": 0, "subjectKind": "individual", "objectKind": "type" },
            { "id": "actuates", "domain": "sound-production-mechanism", "range": "physical-component", "verb": "actuates", "order": 0, "comparisonPriority": 0 },
            { "id": "strikes", "domain": "physical-component", "range": "physical-component", "verb": "strikes", "order": 1, "comparisonPriority": 0 },
            { "id": "has-mechanical-state", "domain": "physical-component", "range": "mechanical-state", "verb": "has mechanical state", "order": 0, "comparisonPriority": 0 },
            { "id": "transmits-vibration-to", "domain": "physical-component", "range": "physical-component", "verb": "transmits vibrations to", "order": 1, "comparisonPriority": 0 },
            { "id": "receives-vibration-from", "domain": "physical-component", "range": "physical-component", "verb": "receives vibrations from", "order": 0, "comparisonPriority": 0 },
            { "id": "radiates", "domain": "physical-component", "range": "sound-event", "verb": "radiates", "order": 1, "comparisonPriority": 0 }
          ],
          "definitions": [
            { "concept": "entity", "definitionKind": "primitive", "necessaryConditions": [], "sufficientExpression": null },
            { "concept": "physical-component", "definitionKind": "primitive", "necessaryConditions": [], "sufficientExpression": null },
            { "concept": "sound-production-mechanism", "definitionKind": "primitive", "necessaryConditions": [], "sufficientExpression": null },
            { "concept": "mechanical-state", "definitionKind": "primitive", "necessaryConditions": [], "sufficientExpression": null },
            { "concept": "tensioned-state", "definitionKind": "primitive", "necessaryConditions": [], "sufficientExpression": null },
            { "concept": "sound-event", "definitionKind": "primitive", "necessaryConditions": [], "sufficientExpression": null },
            {
              "concept": "resonator",
              "definitionKind": "defined",
              "necessaryConditions": [],
              "sufficientExpression": {
                "kind": "and",
                "operands": [
                  { "kind": "some", "relation": "receives-vibration-from", "targetType": "physical-component" },
                  { "kind": "some", "relation": "radiates", "targetType": "sound-event" }
                ]
              }
            },
            {
              "concept": "string-component",
              "definitionKind": "defined",
              "necessaryConditions": [],
              "sufficientExpression": {
                "kind": "and",
                "operands": [
                  { "kind": "some", "relation": "has-mechanical-state", "targetType": "tensioned-state" },
                  { "kind": "some", "relation": "transmits-vibration-to", "targetType": "resonator" }
                ]
              }
            },
            {
              "concept": "striking-component",
              "definitionKind": "defined",
              "necessaryConditions": [],
              "sufficientExpression": { "kind": "some", "relation": "strikes", "targetType": "physical-component" }
            },
            {
              "concept": "struck-string-mechanism",
              "definitionKind": "defined",
              "necessaryConditions": [],
              "sufficientExpression": {
                "kind": "some",
                "relation": "actuates",
                "targetType": "physical-component",
                "filler": {
                  "kind": "some",
                  "relation": "strikes",
                  "targetType": "string-component"
                }
              }
            }
          ],
          "relationAssertions": [
            { "subject": "physical-component", "relation": "is-a", "object": "entity" },
            { "subject": "sound-production-mechanism", "relation": "is-a", "object": "entity" },
            { "subject": "mechanical-state", "relation": "is-a", "object": "entity" },
            { "subject": "tensioned-state", "relation": "is-a", "object": "mechanical-state" },
            { "subject": "sound-event", "relation": "is-a", "object": "entity" },
            { "subject": "resonator", "relation": "is-a", "object": "physical-component" },
            { "subject": "string-component", "relation": "is-a", "object": "physical-component" },
            { "subject": "striking-component", "relation": "is-a", "object": "physical-component" },
            { "subject": "struck-string-mechanism", "relation": "is-a", "object": "sound-production-mechanism" },
            { "subject": "piano-action", "relation": "instance-of", "object": "sound-production-mechanism" },
            { "subject": "unrelated-action", "relation": "instance-of", "object": "sound-production-mechanism" },
            { "subject": "hammer", "relation": "instance-of", "object": "physical-component" },
            { "subject": "loose-component", "relation": "instance-of", "object": "physical-component" },
            { "subject": "string", "relation": "instance-of", "object": "physical-component" },
            { "subject": "board", "relation": "instance-of", "object": "physical-component" },
            { "subject": "tension", "relation": "instance-of", "object": "tensioned-state" },
            { "subject": "sound", "relation": "instance-of", "object": "sound-event" },
            { "subject": "piano-action", "relation": "actuates", "object": "hammer" },
            { "subject": "unrelated-action", "relation": "actuates", "object": "loose-component" },
            { "subject": "hammer", "relation": "strikes", "object": "string" },
            { "subject": "string", "relation": "has-mechanical-state", "object": "tension" },
            { "subject": "string", "relation": "transmits-vibration-to", "object": "board" },
            { "subject": "board", "relation": "receives-vibration-from", "object": "string" },
            { "subject": "board", "relation": "radiates", "object": "sound" }
          ],
          "dataProperties": [],
          "dataAssertions": []
        }
        """;

    [Fact]
    public void Classifies_from_nested_relational_structure_and_retains_evidence()
    {
        var loader = new SemanticWorldModelLoader();
        Assert.True(loader.TryLoad(ModelJson, out var model, out var error), error);

        var reasoner = new SemanticWorldModelReasoner(model!);

        Assert.True(reasoner.IsInstanceOf("board", "resonator"));
        Assert.True(reasoner.IsInstanceOf("string", "string-component"));
        Assert.True(reasoner.IsInstanceOf("hammer", "striking-component"));
        Assert.True(reasoner.IsInstanceOf("piano-action", "struck-string-mechanism"));
        Assert.False(reasoner.IsInstanceOf("unrelated-action", "struck-string-mechanism"));

        var evidence = reasoner.GetMembershipEvidence("piano-action", "struck-string-mechanism");
        Assert.Contains(evidence, source => source.SourceId == "nested-restriction-test:definition:struck-string-mechanism");
        Assert.Contains(evidence, source => source.SourceId == "nested-restriction-test:relation:piano-action:actuates:hammer");
        Assert.Contains(evidence, source => source.SourceId == "nested-restriction-test:relation:hammer:strikes:string");
        Assert.Contains(evidence, source => source.SourceId == "nested-restriction-test:definition:string-component");
        Assert.Contains(evidence, source => source.SourceId == "nested-restriction-test:definition:resonator");
    }
}
