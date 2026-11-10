using Lattice.Core.Language;
using Xunit;

namespace Lattice.Core.Tests.Language;

public sealed class EnglishLexiconTests
{
    private const string ValidDocument = """
        {
          "schemaVersion": 1,
          "reservedTerms": ["a"],
          "irregularPluralNouns": { "criteria": "criterion" },
          "invariantNouns": ["deer"],
          "singularNounsEndingInS": ["analysis"],
          "irregularThirdPersonVerbs": { "have": "has" },
          "baseRelationVerbs": ["have", "like"],
          "pastParticiples": { "liked": "like" }
        }
        """;

    [Fact]
    public void Loads_the_embedded_lexicon_into_case_insensitive_immutable_collections()
    {
        Assert.True(EnglishLexicon.ReservedTerms.Contains("THE"));
        Assert.Equal("criterion", EnglishLexicon.ExplicitPluralNounForms["CRITERIA"]);
        Assert.True(EnglishLexicon.InvariantNouns.Contains("Deer"));
        Assert.True(EnglishLexicon.SingularNounsEndingInS.Contains("ANALYSIS"));
        Assert.Equal("has", EnglishLexicon.IrregularThirdPersonVerbs["HAVE"]);
        Assert.True(EnglishLexicon.BaseRelationVerbs.Contains("WRITE"));
        Assert.Equal("write", EnglishLexicon.PastParticiples["WRITTEN"]);
    }

    [Fact]
    public void Rejects_a_duplicate_token_even_when_case_differs()
    {
        var json = ValidDocument.Replace(
            "\"reservedTerms\": [\"a\"]",
            "\"reservedTerms\": [\"a\", \"A\"]");

        AssertInvalid(json);
    }

    [Fact]
    public void Rejects_overlapping_noun_classifications()
    {
        var json = ValidDocument.Replace(
            "\"invariantNouns\": [\"deer\"]",
            "\"invariantNouns\": [\"deer\", \"criteria\"]");

        AssertInvalid(json);
    }

    [Fact]
    public void Rejects_a_past_participle_that_maps_to_an_unknown_base_verb()
    {
        var json = ValidDocument.Replace(
            "\"pastParticiples\": { \"liked\": \"like\" }",
            "\"pastParticiples\": { \"liked\": \"invent\" }");

        AssertInvalid(json);
    }

    [Fact]
    public void Rejects_an_irregular_third_person_form_for_an_unknown_base_verb()
    {
        var json = ValidDocument.Replace(
            "\"irregularThirdPersonVerbs\": { \"have\": \"has\" }",
            "\"irregularThirdPersonVerbs\": { \"invent\": \"invents\" }");

        AssertInvalid(json);
    }

    [Fact]
    public void Rejects_an_unsupported_schema_version()
    {
        var json = ValidDocument.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 2");

        AssertInvalid(json);
    }

    [Fact]
    public void Rejects_duplicate_object_keys()
    {
        var json = ValidDocument.Replace(
            "\"irregularPluralNouns\": { \"criteria\": \"criterion\" }",
            "\"irregularPluralNouns\": { \"criteria\": \"criterion\", \"CRITERIA\": \"criterion\" }");

        AssertInvalid(json);
    }

    [Fact]
    public void Rejects_an_empty_lexicon_collection()
    {
        var json = ValidDocument.Replace(
            "\"baseRelationVerbs\": [\"have\", \"like\"]",
            "\"baseRelationVerbs\": []");

        AssertInvalid(json);
    }

    [Fact]
    public void Requires_singular_plural_exceptions_to_be_classified_as_singular()
    {
        var json = ValidDocument.Replace(
            "\"irregularPluralNouns\": { \"criteria\": \"criterion\" }",
            "\"irregularPluralNouns\": { \"analyses\": \"analysis\" }");

        AssertInvalid(json);
    }

    private static void AssertInvalid(string json)
    {
        Assert.False(EnglishLexicon.TryLoad(json, out var lexicon, out var error));
        Assert.Null(lexicon);
        Assert.NotEmpty(error);
    }
}
