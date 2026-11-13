using Lattice.Core.Reasoning;
using Xunit;

namespace Lattice.Core.Tests.Reasoning;

public sealed class SemanticClassExpressionTests
{
    [Fact]
    public void Derives_striking_components_and_resonators_from_structural_relations()
    {
        const string json = """
            {
              "formatVersion": 2,
              "id": "instrument-axiom-test",
              "name": "Instrument axiom test",
              "concepts": [
                { "id": "entity", "label": "entity", "kind": "type" },
                { "id": "physical-component", "label": "physical component", "kind": "type" },
                { "id": "sound-event", "label": "sound event", "kind": "type" },
                { "id": "mechanical-state", "label": "mechanical state", "kind": "type" },
                { "id": "tensioned-state", "label": "tensioned state", "kind": "type" },
                { "id": "striking-component", "label": "striking component", "kind": "type" },
                { "id": "string-component", "label": "string component", "kind": "type" },
                { "id": "resonator", "label": "resonator", "kind": "type" },
                { "id": "sound-producing-component", "label": "sound producing component", "kind": "type" },
                { "id": "hammer", "label": "hammer", "kind": "individual" },
                { "id": "string", "label": "string", "kind": "individual" },
                { "id": "board", "label": "soundboard", "kind": "individual" },
                { "id": "sound", "label": "sound", "kind": "individual" },
                { "id": "tension", "label": "tension", "kind": "individual" }
              ],
              "lexemes": [],
              "relations": [
                { "id": "strikes", "domain": "physical-component", "range": "physical-component", "verb": "strikes", "objectRole": null, "order": 0, "comparisonPriority": 0 },
                { "id": "receives-vibration-from", "domain": "physical-component", "range": "physical-component", "verb": "receives vibration from", "objectRole": null, "order": 0, "comparisonPriority": 0 },
                { "id": "radiates", "domain": "physical-component", "range": "sound-event", "verb": "radiates", "objectRole": null, "order": 0, "comparisonPriority": 0 },
                { "id": "vibrates-to-produce", "domain": "physical-component", "range": "sound-event", "verb": "vibrates to produce", "objectRole": null, "order": 0, "comparisonPriority": 0 },
                { "id": "has-mechanical-state", "domain": "physical-component", "range": "mechanical-state", "verb": "has mechanical state", "objectRole": null, "order": 0, "comparisonPriority": 0 }
              ],
              "definitions": [
                { "concept": "entity", "parents": [], "definitionKind": "primitive", "necessaryConditions": [], "equivalentTo": null, "sourceText": "Ontology root." },
                { "concept": "physical-component", "parents": ["entity"], "definitionKind": "primitive", "necessaryConditions": [], "equivalentTo": null, "sourceText": "Primitive physical component." },
                { "concept": "sound-event", "parents": ["entity"], "definitionKind": "primitive", "necessaryConditions": [], "equivalentTo": null, "sourceText": "Primitive sound event." },
                { "concept": "mechanical-state", "parents": ["entity"], "definitionKind": "primitive", "necessaryConditions": [], "equivalentTo": null, "sourceText": "Primitive mechanical state." },
                { "concept": "tensioned-state", "parents": ["mechanical-state"], "definitionKind": "primitive", "necessaryConditions": [], "equivalentTo": null, "sourceText": "Primitive tensioned state." },
                { "concept": "striking-component", "parents": ["physical-component"], "definitionKind": "defined", "necessaryConditions": [], "equivalentTo": { "kind": "some", "relation": "strikes", "targetType": "physical-component" }, "sourceText": "A striking component acts on another physical component by striking it." },
                { "concept": "string-component", "parents": ["physical-component"], "definitionKind": "primitive", "necessaryConditions": [{ "relation": "has-mechanical-state", "targetType": "tensioned-state" }, { "relation": "vibrates-to-produce", "targetType": "sound-event" }], "equivalentTo": null, "sourceText": "Primitive string component with necessary mechanical and vibrational characteristics." },
                { "concept": "resonator", "parents": ["physical-component"], "definitionKind": "defined", "necessaryConditions": [], "equivalentTo": { "kind": "and", "operands": [{ "kind": "some", "relation": "receives-vibration-from", "targetType": "physical-component" }, { "kind": "some", "relation": "radiates", "targetType": "sound-event" }] }, "sourceText": "A resonator receives vibration and radiates sound." },
                { "concept": "sound-producing-component", "parents": ["physical-component"], "definitionKind": "defined", "necessaryConditions": [], "equivalentTo": { "kind": "or", "operands": [{ "kind": "some", "relation": "radiates", "targetType": "sound-event" }, { "kind": "some", "relation": "vibrates-to-produce", "targetType": "sound-event" }] }, "sourceText": "A sound-producing component radiates or directly produces a sound event." }
              ],
              "typeAssertions": [
                { "individual": "hammer", "type": "physical-component", "sourceText": "Hammer is a physical component." },
                { "individual": "string", "type": "string-component", "sourceText": "String is a string component." },
                { "individual": "board", "type": "physical-component", "sourceText": "Board is a physical component." },
                { "individual": "sound", "type": "sound-event", "sourceText": "Sound is a sound event." },
                { "individual": "tension", "type": "tensioned-state", "sourceText": "The string is under tension." }
              ],
              "relationAssertions": [
                { "subject": "hammer", "relation": "strikes", "object": "string", "sourceText": "Hammer strikes string." },
                { "subject": "board", "relation": "receives-vibration-from", "object": "string", "sourceText": "Board receives vibration from string." },
                { "subject": "board", "relation": "radiates", "object": "sound", "sourceText": "Board radiates sound." },
                { "subject": "string", "relation": "has-mechanical-state", "object": "tension", "sourceText": "String is tensioned." },
                { "subject": "string", "relation": "vibrates-to-produce", "object": "sound", "sourceText": "String vibrates to produce sound." }
              ],
              "dataProperties": [],
              "dataAssertions": []
            }
            """;

        var loader = new SemanticWorldModelLoader();
        Assert.True(loader.TryLoad(json, out var model, out var error), error);
        var reasoner = new SemanticWorldModelReasoner(model!);

        Assert.True(reasoner.IsInstanceOf("hammer", "striking-component"));
        Assert.True(reasoner.IsInstanceOf("board", "resonator"));
        Assert.True(reasoner.IsInstanceOf("string", "sound-producing-component"));
        Assert.False(reasoner.IsInstanceOf("string", "striking-component"));
        Assert.True(reasoner.IsInstanceOf("string", "string-component"));
    }

    [Fact]
    public void Rejects_incomplete_version_two_type_declarations()
    {
        const string json = """
            {
              "formatVersion": 2,
              "id": "incomplete-definition-test",
              "name": "Incomplete definition test",
              "concepts": [
                { "id": "entity", "label": "entity", "kind": "type" },
                { "id": "sample-type", "label": "sample type", "kind": "type" }
              ],
              "lexemes": [],
              "relations": [],
              "definitions": [
                { "concept": "entity", "parents": [], "definitionKind": "primitive", "necessaryConditions": [], "equivalentTo": null, "sourceText": "Root." },
                { "concept": "sample-type", "parents": ["entity"], "definitionKind": "defined", "necessaryConditions": [], "equivalentTo": null, "sourceText": "Missing sufficient definition." }
              ],
              "typeAssertions": [],
              "relationAssertions": [],
              "dataProperties": [],
              "dataAssertions": []
            }
            """;

        var loader = new SemanticWorldModelLoader();
        Assert.False(loader.TryLoad(json, out var model, out var error));
        Assert.Null(model);
        Assert.Contains("inconsistent primitive/defined", error);
    }
}
