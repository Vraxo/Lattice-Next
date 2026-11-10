using Lattice.Core.Language;
using Xunit;

namespace Lattice.Core.Tests.Language;

public sealed class EnglishLexiconTests
{
    private const string ValidDocument = """
        {
          "schema-version": 2,
          "reserved-terms": [
            "a"
          ],
          "named-individual-property-adjectives": [
            "happy"
          ],
          "irregular-plural-nouns": {
            "criteria": "criterion"
          },
          "invariant-nouns": [
            "deer"
          ],
          "singular-nouns-ending-in-s": [
            "analysis"
          ],
          "irregular-third-person-verbs": {
            "have": "has"
          },
          "base-relation-verbs": [
            "have",
            "like"
          ],
          "past-participles": {
            "liked": "like"
          }
        }
        """;

    [Fact]
    public void Loads_the_embedded_lexicon_into_case_insensitive_immutable_collections()
    {
        Assert.True(EnglishLexicon.ReservedTerms.Contains("THE"));
        Assert.Contains("EACH", EnglishLexicon.ReservedTerms.ToArray());
        Assert.True(EnglishLexicon.NamedIndividualPropertyAdjectives.Contains("HAPPY"));
        Assert.Equal("criterion", EnglishLexicon.ExplicitPluralNounForms["CRITERIA"]);
        Assert.True(EnglishLexicon.InvariantNouns.Contains("Deer"));
        Assert.True(EnglishLexicon.InvariantNouns.Contains("AIRCRAFT"));
        Assert.True(EnglishLexicon.InvariantNouns.Contains("HEADQUARTERS"));
        Assert.True(EnglishLexicon.InvariantNouns.Contains("MEANS"));
        Assert.True(EnglishLexicon.InvariantNouns.Contains("REINDEER"));
        Assert.True(EnglishLexicon.SingularNounsEndingInS.Contains("NEWS"));
        Assert.True(EnglishLexicon.SingularNounsEndingInS.Contains("ANALYSIS"));
        Assert.Equal("has", EnglishLexicon.IrregularThirdPersonVerbs["HAVE"]);
        Assert.True(EnglishLexicon.BaseRelationVerbs.Contains("WRITE"));
        Assert.Equal("write", EnglishLexicon.PastParticiples["WRITTEN"]);
    }

    [Fact]
    public void Rejects_a_duplicate_token_even_when_case_differs()
    {
        var json = ValidDocument.Replace(
            "\"reserved-terms\": [\n    \"a\"\n  ]",
            "\"reserved-terms\": [\n    \"a\",\n    \"A\"\n  ]");

        AssertInvalid(json);
    }

    [Fact]
    public void Rejects_overlapping_noun_classifications()
    {
        var json = ValidDocument.Replace(
            "\"invariant-nouns\": [\n    \"deer\"\n  ]",
            "\"invariant-nouns\": [\n    \"deer\",\n    \"criteria\"\n  ]");

        AssertInvalid(json);
    }

    [Fact]
    public void Rejects_a_past_participle_that_maps_to_an_unknown_base_verb()
    {
        var json = ValidDocument.Replace(
            "\"past-participles\": {\n    \"liked\": \"like\"\n  }",
            "\"past-participles\": {\n    \"liked\": \"invent\"\n  }");

        AssertInvalid(json);
    }

    [Fact]
    public void Rejects_an_irregular_third_person_form_for_an_unknown_base_verb()
    {
        var json = ValidDocument.Replace(
            "\"irregular-third-person-verbs\": {\n    \"have\": \"has\"\n  }",
            "\"irregular-third-person-verbs\": {\n    \"invent\": \"invents\"\n  }");

        AssertInvalid(json);
    }

    [Fact]
    public void Rejects_an_unsupported_schema_version()
    {
        var json = ValidDocument.Replace("\"schema-version\": 2", "\"schema-version\": 3");

        AssertInvalid(json);
    }

    [Fact]
    public void Rejects_legacy_camel_case_property_names()
    {
        var json = ValidDocument.Replace("\"schema-version\"", "\"schemaVersion\"");

        AssertInvalid(json);
    }

    [Fact]
    public void Rejects_duplicate_object_keys()
    {
        var json = ValidDocument.Replace(
            "\"irregular-plural-nouns\": {\n    \"criteria\": \"criterion\"\n  }",
            "\"irregular-plural-nouns\": {\n    \"criteria\": \"criterion\",\n    \"CRITERIA\": \"criterion\"\n  }");

        AssertInvalid(json);
    }

    [Fact]
    public void Rejects_an_empty_lexicon_collection()
    {
        var json = ValidDocument.Replace(
            "\"base-relation-verbs\": [\n    \"have\",\n    \"like\"\n  ]",
            "\"base-relation-verbs\": []");

        AssertInvalid(json);
    }

    [Fact]
    public void Requires_singular_plural_exceptions_to_be_classified_as_singular()
    {
        var json = ValidDocument
            .Replace(
                "\"irregular-plural-nouns\": {\n    \"criteria\": \"criterion\"\n  }",
                "\"irregular-plural-nouns\": {\n    \"analyses\": \"analysis\"\n  }")
            .Replace(
                "\"singular-nouns-ending-in-s\": [\n    \"analysis\"\n  ]",
                "\"singular-nouns-ending-in-s\": [\n    \"basis\"\n  ]");

        AssertInvalid(json);
    }

    private static void AssertInvalid(string json)
    {
        Assert.False(EnglishLexicon.TryLoad(json, out var lexicon, out var error));
        Assert.Null(lexicon);
        Assert.NotEmpty(error);
    }
}
