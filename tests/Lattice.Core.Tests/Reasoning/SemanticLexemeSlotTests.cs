using Lattice.Core.Reasoning;
using Xunit;

namespace Lattice.Core.Tests.Reasoning;

public sealed class SemanticLexemeSlotTests
{
    private readonly SemanticWorldModelLoader loader = new();

    private const string Json = """
        {
          "formatVersion": 1,
          "id": "lexeme-slot-test",
          "name": "Lexeme slot test",
          "concepts": [
            { "id": "entity", "label": "entity", "kind": "type" },
            { "id": "task", "label": "task", "kind": "type" },
            { "id": "slot", "label": "slot", "kind": "type" },
            { "id": "recolour", "label": "recolour", "kind": "individual" },
            { "id": "slot-colour-name", "label": "YourColour", "kind": "individual" }
          ],
          "lexemes": [
            {
              "form": "recolours the object",
              "concept": "recolour",
              "partOfSpeech": "verb phrase"
            },
            {
              "form": "recolours the object {colour}",
              "concept": "recolour",
              "partOfSpeech": "verb phrase",
              "slots": [
                { "name": "colour", "concept": "slot-colour-name", "kind": "text" }
              ]
            }
          ],
          "relations": [],
          "definitions": [
            { "concept": "task", "parents": ["entity"], "conditions": [], "sourceText": "Task definition." },
            { "concept": "slot", "parents": ["entity"], "conditions": [], "sourceText": "Slot definition." }
          ],
          "typeAssertions": [
            { "individual": "recolour", "type": "task" },
            { "individual": "slot-colour-name", "type": "slot" }
          ],
          "relationAssertions": []
        }
        """;

    [Fact]
    public void Matches_an_exact_form_without_capturing_anything()
    {
        Assert.True(loader.TryLoad(Json, out var model, out var error), error);

        Assert.True(model!.TryMatchLexeme("recolours the object", out var concept, out var lexeme, out var captures));

        Assert.Equal("recolour", concept!.Id);
        Assert.Null(lexeme);
        Assert.Empty(captures);
    }

    [Fact]
    public void Captures_the_text_a_slot_bearing_form_leaves_open()
    {
        Assert.True(loader.TryLoad(Json, out var model, out var error), error);

        Assert.True(model!.TryMatchLexeme("Recolours the object  crimson ", out var concept, out var lexeme, out var captures));

        Assert.Equal("recolour", concept!.Id);
        Assert.Equal("recolours the object {colour}", lexeme!.Form);
        Assert.Equal("crimson", Assert.Single(captures).Value);
        Assert.Equal("slot-colour-name", Assert.Single(lexeme.Slots).Concept);
    }

    [Fact]
    public void Matches_nothing_when_the_phrase_fits_no_form()
    {
        Assert.True(loader.TryLoad(Json, out var model, out var error), error);

        Assert.False(model!.TryMatchLexeme("deletes every object", out var concept, out _, out var captures));

        Assert.Null(concept);
        Assert.Empty(captures);
    }

    [Fact]
    public void Rejects_a_slot_whose_marker_the_form_does_not_hold()
    {
        const string invalid = """
            {
              "formatVersion": 1,
              "id": "lexeme-slot-test",
              "name": "Lexeme slot test",
              "concepts": [
                { "id": "entity", "label": "entity", "kind": "type" },
                { "id": "task", "label": "task", "kind": "type" },
                { "id": "slot", "label": "slot", "kind": "type" },
                { "id": "recolour", "label": "recolour", "kind": "individual" },
                { "id": "slot-colour-name", "label": "YourColour", "kind": "individual" }
              ],
              "lexemes": [
                {
                  "form": "recolours the object",
                  "concept": "recolour",
                  "partOfSpeech": "verb phrase",
                  "slots": [
                    { "name": "colour", "concept": "slot-colour-name", "kind": "text" }
                  ]
                }
              ],
              "relations": [],
              "definitions": [
                { "concept": "task", "parents": ["entity"], "conditions": [], "sourceText": "Task definition." },
                { "concept": "slot", "parents": ["entity"], "conditions": [], "sourceText": "Slot definition." }
              ],
              "typeAssertions": [
                { "individual": "recolour", "type": "task" },
                { "individual": "slot-colour-name", "type": "slot" }
              ],
              "relationAssertions": []
            }
            """;

        Assert.False(loader.TryLoad(invalid, out _, out var error));

        Assert.Contains("slot", error, StringComparison.OrdinalIgnoreCase);
    }
}
