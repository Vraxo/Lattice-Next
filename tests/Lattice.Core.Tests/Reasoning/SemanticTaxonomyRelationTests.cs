using Lattice.Core.Reasoning;
using Xunit;

namespace Lattice.Core.Tests.Reasoning;

public sealed class SemanticTaxonomyRelationTests
{
    private const string ValidModel = """
        {
          "formatVersion": 3,
          "id": "taxonomy-test",
          "name": "Taxonomy test",
          "concepts": [
            { "id": "entity", "label": "entity", "kind": "type" },
            { "id": "animal", "label": "animal", "kind": "type" },
            { "id": "fish", "label": "fish", "kind": "type" },
            { "id": "tope", "label": "Tope", "kind": "individual" }
          ],
          "lexemes": [],
          "relations": [
            {
              "id": "is-a",
              "verb": "is a",
              "order": 0,
              "comparisonPriority": 0,
              "subjectKind": "type",
              "objectKind": "type"
            },
            {
              "id": "instance-of",
              "verb": "is an instance of",
              "order": 0,
              "comparisonPriority": 0,
              "subjectKind": "individual",
              "objectKind": "type"
            }
          ],
          "definitions": [
            { "concept": "entity", "definitionKind": "primitive", "necessaryConditions": [], "sufficientExpression": null },
            { "concept": "animal", "definitionKind": "primitive", "necessaryConditions": [], "sufficientExpression": null },
            { "concept": "fish", "definitionKind": "primitive", "necessaryConditions": [], "sufficientExpression": null }
          ],
          "relationAssertions": [
            { "subject": "fish", "relation": "is-a", "object": "animal" },
            { "subject": "animal", "relation": "is-a", "object": "entity" },
            { "subject": "tope", "relation": "instance-of", "object": "fish" }
          ],
          "dataProperties": [],
          "dataAssertions": []
        }
        """;

    [Fact]
    public void Uses_one_relation_graph_for_taxonomy_and_individual_membership()
    {
        var loader = new SemanticWorldModelLoader();
        Assert.True(loader.TryLoad(ValidModel, out var model, out var error), error);

        Assert.Equal(3, model!.RelationAssertions.Length);
        var reasoner = new SemanticWorldModelReasoner(model);

        Assert.True(reasoner.IsInstanceOf("tope", "fish"));
        Assert.True(reasoner.IsInstanceOf("tope", "animal"));
        Assert.True(reasoner.IsInstanceOf("tope", "entity"));
        Assert.True(reasoner.IsSubtypeOf("fish", "entity"));
        Assert.Equal(new[] { "animal" }, reasoner.GetDirectSupertypes("fish"));
        Assert.Equal(new[] { "fish" }, reasoner.GetDirectSubtypes("animal"));

        var evidence = reasoner.GetMembershipEvidence("tope", "entity");
        Assert.Contains(evidence, source =>
            source.SourceId == "taxonomy-test:relation:tope:instance-of:fish");
        Assert.Contains(evidence, source =>
            source.SourceId == "taxonomy-test:relation:fish:is-a:animal");
        Assert.Contains(evidence, source =>
            source.SourceId == "taxonomy-test:relation:animal:is-a:entity");
    }

    [Fact]
    public void Rejects_type_assertions_with_the_wrong_relation_argument_kinds()
    {
        var json = ValidModel.Replace(
            "{ \"subject\": \"tope\", \"relation\": \"instance-of\", \"object\": \"fish\" }",
            "{ \"subject\": \"tope\", \"relation\": \"is-a\", \"object\": \"animal\" }",
            StringComparison.Ordinal);
        var loader = new SemanticWorldModelLoader();

        Assert.False(loader.TryLoad(json, out var model, out _));
        Assert.Null(model);
    }

    [Fact]
    public void Rejects_cyclic_is_a_relations()
    {
        var json = ValidModel.Replace(
            "\"object\": \"entity\"",
            "\"object\": \"fish\"",
            StringComparison.Ordinal);
        var loader = new SemanticWorldModelLoader();

        Assert.False(loader.TryLoad(json, out var model, out var error));
        Assert.Null(model);
        Assert.Contains("cyclic type hierarchy", error);
    }

    [Fact]
    public void Rejects_legacy_hierarchy_and_provenance_fields_in_version_three()
    {
        var withParents = ValidModel.Replace(
            "\"concept\": \"fish\", \"definitionKind\"",
            "\"concept\": \"fish\", \"parents\": [], \"definitionKind\"",
            StringComparison.Ordinal);
        var withTypeAssertions = ValidModel.Replace(
            "\"relationAssertions\": [",
            "\"typeAssertions\": [], \"relationAssertions\": [",
            StringComparison.Ordinal);
        var withProse = ValidModel.Replace(
            "{ \"subject\": \"tope\", \"relation\": \"instance-of\", \"object\": \"fish\" }",
            "{ \"subject\": \"tope\", \"relation\": \"instance-of\", \"object\": \"fish\", \"sourceText\": \"Tope is a fish.\" }",
            StringComparison.Ordinal);
        var loader = new SemanticWorldModelLoader();

        Assert.False(loader.TryLoad(withParents, out _, out _));
        Assert.False(loader.TryLoad(withTypeAssertions, out _, out _));
        Assert.False(loader.TryLoad(withProse, out _, out _));
    }
}
