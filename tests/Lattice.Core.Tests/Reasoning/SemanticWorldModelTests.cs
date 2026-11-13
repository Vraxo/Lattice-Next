using Lattice.Core.Reasoning;
using Xunit;

namespace Lattice.Core.Tests.Reasoning;

public sealed class SemanticWorldModelTests
{
    private readonly SemanticWorldModelLoader loader = new();

    [Fact]
    public void Derives_instrument_classification_from_a_composed_mechanism_hierarchy()
    {
        const string json = """
            {
              "formatVersion": 1,
              "id": "semantic-test",
              "name": "Semantic test",
              "concepts": [
                { "id": "entity", "label": "entity", "kind": "type" },
                { "id": "physical-object", "label": "physical object", "kind": "type" },
                { "id": "sound-production-mechanism", "label": "sound-production mechanism", "kind": "type" },
                { "id": "string-sound-mechanism", "label": "string-sound mechanism", "kind": "type" },
                { "id": "struck-string-mechanism", "label": "struck-string mechanism", "kind": "type" },
                { "id": "musical-instrument", "label": "musical instrument", "kind": "type" },
                { "id": "string-instrument", "label": "string instrument", "kind": "type" },
                { "id": "struck-string-instrument", "label": "struck-string instrument", "kind": "type" },
                { "id": "piano", "label": "piano", "kind": "individual" },
                { "id": "action", "label": "piano action", "kind": "individual" }
              ],
              "lexemes": [],
              "relations": [
                {
                  "id": "has-sound-production-mechanism",
                  "domain": "physical-object",
                  "range": "sound-production-mechanism",
                  "verb": "uses",
                  "objectRole": null,
                  "order": 0,
                  "comparisonPriority": 0
                }
              ],
              "definitions": [
                { "concept": "physical-object", "parents": ["entity"], "conditions": [], "sourceText": "Ontology source: physical object." },
                { "concept": "sound-production-mechanism", "parents": ["entity"], "conditions": [], "sourceText": "Ontology source: mechanism." },
                { "concept": "string-sound-mechanism", "parents": ["sound-production-mechanism"], "conditions": [], "sourceText": "Ontology source: string mechanism." },
                { "concept": "struck-string-mechanism", "parents": ["string-sound-mechanism"], "conditions": [], "sourceText": "Ontology source: struck-string mechanism." },
                {
                  "concept": "musical-instrument",
                  "parents": ["physical-object"],
                  "conditions": [{ "relation": "has-sound-production-mechanism", "targetType": "sound-production-mechanism" }],
                  "sourceText": "Definition source: musical instrument."
                },
                {
                  "concept": "string-instrument",
                  "parents": ["musical-instrument"],
                  "conditions": [{ "relation": "has-sound-production-mechanism", "targetType": "string-sound-mechanism" }],
                  "sourceText": "Definition source: string instrument."
                },
                {
                  "concept": "struck-string-instrument",
                  "parents": ["string-instrument"],
                  "conditions": [{ "relation": "has-sound-production-mechanism", "targetType": "struck-string-mechanism" }],
                  "sourceText": "Definition source: struck-string instrument."
                }
              ],
              "typeAssertions": [
                { "individual": "piano", "type": "physical-object", "sourceText": "Piano is a physical object." },
                { "individual": "action", "type": "struck-string-mechanism", "sourceText": "Piano action is a struck-string mechanism." }
              ],
              "relationAssertions": [
                { "subject": "piano", "relation": "has-sound-production-mechanism", "object": "action", "sourceText": "Piano uses its action." }
              ]
            }
            """;

        Assert.True(loader.TryLoad(json, out var model, out var error), error);
        var reasoner = new SemanticWorldModelReasoner(model!);

        Assert.True(reasoner.IsInstanceOf("piano", "musical-instrument"));
        Assert.True(reasoner.IsInstanceOf("piano", "string-instrument"));
        Assert.True(reasoner.IsInstanceOf("piano", "struck-string-instrument"));

        var evidence = reasoner.GetMembershipEvidence("piano", "struck-string-instrument");
        Assert.Contains(evidence, source => source.SourceText == "Piano uses its action.");
        Assert.Contains(evidence, source => source.SourceText == "Piano action is a struck-string mechanism.");
        Assert.Contains(evidence, source => source.SourceText == "Definition source: struck-string instrument.");
    }


    [Fact]
    public void Loads_typed_uri_properties_without_converting_them_to_text_attributes()
    {
        const string json = """
            {
              "formatVersion": 1,
              "id": "typed-uri-test",
              "name": "Typed URI test",
              "concepts": [
                { "id": "entity", "label": "entity", "kind": "type" },
                { "id": "software", "label": "software", "kind": "type" },
                { "id": "sample", "label": "sample software", "kind": "individual" }
              ],
              "lexemes": [],
              "relations": [],
              "definitions": [
                {
                  "concept": "software",
                  "parents": ["entity"],
                  "conditions": [],
                  "sourceText": "Curated software classification."
                }
              ],
              "typeAssertions": [
                { "individual": "sample", "type": "software", "sourceText": "Sample is software." }
              ],
              "relationAssertions": [],
              "dataProperties": [
                { "id": "documentation-url", "domainType": "software", "valueType": "uri" }
              ],
              "dataAssertions": [
                {
                  "subject": "sample",
                  "property": "documentation-url",
                  "value": "https://example.org/docs",
                  "sourceText": "Official documentation URL."
                }
              ]
            }
            """;

        Assert.True(loader.TryLoad(json, out var model, out var error), error);

        var property = Assert.Single(model!.DataProperties);
        var assertion = Assert.Single(model.DataAssertions);
        Assert.Equal(SemanticDataValueKind.Uri, property.ValueKind);
        Assert.Equal("https://example.org/docs", assertion.Value);
        Assert.Equal("documentation-url", assertion.Property);
    }

    [Fact]
    public void Rejects_prose_valued_semantic_data_and_free_text_definition_fields()
    {
        const string proseValue = """
            {
              "formatVersion": 1,
              "id": "prose-value-test",
              "name": "Prose value test",
              "concepts": [
                { "id": "entity", "label": "entity", "kind": "type" },
                { "id": "software", "label": "software", "kind": "type" },
                { "id": "sample", "label": "sample software", "kind": "individual" }
              ],
              "lexemes": [],
              "relations": [],
              "definitions": [
                {
                  "concept": "software",
                  "parents": ["entity"],
                  "conditions": [],
                  "sourceText": "Evidence only.",
                  "description": "A software object with an explanatory prose definition."
                }
              ],
              "typeAssertions": [
                { "individual": "sample", "type": "software", "sourceText": "Sample is software." }
              ],
              "relationAssertions": [],
              "dataProperties": [
                { "id": "description", "domainType": "software", "valueType": "text" }
              ],
              "dataAssertions": [
                {
                  "subject": "sample",
                  "property": "description",
                  "value": "A long explanatory sentence.",
                  "sourceText": "A prose definition."
                }
              ]
            }
            """;
        const string textProperty = """
            {
              "formatVersion": 1,
              "id": "text-property-test",
              "name": "Text property test",
              "concepts": [
                { "id": "entity", "label": "entity", "kind": "type" },
                { "id": "software", "label": "software", "kind": "type" },
                { "id": "sample", "label": "sample software", "kind": "individual" }
              ],
              "lexemes": [],
              "relations": [],
              "definitions": [
                {
                  "concept": "software",
                  "parents": ["entity"],
                  "conditions": [],
                  "sourceText": "Evidence only."
                }
              ],
              "typeAssertions": [
                { "individual": "sample", "type": "software", "sourceText": "Sample is software." }
              ],
              "relationAssertions": [],
              "dataProperties": [
                { "id": "description", "domainType": "software", "valueType": "text" }
              ],
              "dataAssertions": []
            }
            """;

        Assert.False(loader.TryLoad(proseValue, out var proseModel, out var proseError));
        Assert.Null(proseModel);
        Assert.Contains("unsupported field", proseError);

        Assert.False(loader.TryLoad(textProperty, out var textModel, out var textError));
        Assert.Null(textModel);
        Assert.Contains("typed value kind", textError);
    }

    [Fact]
    public void Rejects_relation_assertions_that_violate_declared_domain_types()
    {
        const string json = """
            {
              "formatVersion": 1,
              "id": "invalid-semantic-test",
              "name": "Invalid semantic test",
              "concepts": [
                { "id": "entity", "label": "entity", "kind": "type" },
                { "id": "physical-object", "label": "physical object", "kind": "type" },
                { "id": "sound-production-mechanism", "label": "sound-production mechanism", "kind": "type" },
                { "id": "piano", "label": "piano", "kind": "individual" },
                { "id": "action", "label": "action", "kind": "individual" }
              ],
              "lexemes": [],
              "relations": [
                {
                  "id": "has-sound-production-mechanism",
                  "domain": "physical-object",
                  "range": "sound-production-mechanism",
                  "verb": "uses",
                  "objectRole": null,
                  "order": 0,
                  "comparisonPriority": 0
                }
              ],
              "definitions": [
                { "concept": "physical-object", "parents": ["entity"], "conditions": [], "sourceText": "Ontology source." },
                { "concept": "sound-production-mechanism", "parents": ["entity"], "conditions": [], "sourceText": "Ontology source." }
              ],
              "typeAssertions": [
                { "individual": "piano", "type": "entity", "sourceText": "Piano is an entity." },
                { "individual": "action", "type": "sound-production-mechanism", "sourceText": "Action is a mechanism." }
              ],
              "relationAssertions": [
                { "subject": "piano", "relation": "has-sound-production-mechanism", "object": "action", "sourceText": "Piano uses an action." }
              ]
            }
            """;

        Assert.False(loader.TryLoad(json, out var model, out var error));
        Assert.Null(model);
        Assert.Contains("domain or range", error);
    }
}
