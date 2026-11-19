using System.Collections.Immutable;
using Lattice.Core.Generation;
using Xunit;

namespace Lattice.Core.Tests.Generation;

public sealed class KnowledgeDrivenSequenceGeneratorTests
{
    [Fact]
    public void Continues_from_multiple_active_semantic_facts_using_contextual_next_tokens()
    {
        var trainingSequence = ImmutableArray.Create(
            "let ",
            "total",
            " = 0\n",
            "print",
            "(",
            "total",
            ")");
        Assert.True(SequenceTokenModel.TryTrain(
            ImmutableArray.Create(trainingSequence, trainingSequence, trainingSequence),
            out var model,
            out var modelError), modelError);

        var knowledge = new SequenceKnowledgeBase(ImmutableArray.Create(
            Rule("initialize-start", [], ["initialize-total"], [], "let ", []),
            Rule("initialize-name", ["let "], ["initialize-total"], [], "total", []),
            Rule("initialize-value", ["let ", "total"], ["initialize-total"], [], " = 0\n", ["initialize-total"]),
            Rule("display-start", [], ["display-total"], [], "print", []),
            Rule("display-open", ["print"], ["display-total"], [], "(", []),
            Rule("display-name", ["print", "("], ["display-total"], [], "total", []),
            Rule("display-close", ["print", "(", "total"], ["display-total"], [], ")", ["display-total"])));

        var request = new KnowledgeDrivenSequenceGenerationRequest(
            ImmutableArray<string>.Empty,
            ["initialize-start", "initialize-name", "initialize-value", "display-start", "display-open", "display-name", "display-close"]);

        var generated = KnowledgeDrivenSequenceGenerator.TryGenerate(
            knowledge,
            model,
            request,
            out var result,
            out var error);

        Assert.True(generated, error);
        Assert.Equal(string.Concat(trainingSequence), result!.Text);
        Assert.False(result.SearchLimitReached);
    }

    [Fact]
    public void Waits_for_required_facts_to_be_completed_before_emitting_dependent_tokens()
    {
        var modelSequences = ImmutableArray.Create(
            ImmutableArray.Create("display"),
            ImmutableArray.Create("display"),
            ImmutableArray.Create("declare"));
        Assert.True(SequenceTokenModel.TryTrain(
            modelSequences,
            out var model,
            out var modelError), modelError);

        var knowledge = new SequenceKnowledgeBase(ImmutableArray.Create(
            Rule("declare", [], ["declaration"], [], "declare", ["declaration"]),
            Rule("display", [], ["display"], ["declaration"], " display", ["display"])));
        var request = new KnowledgeDrivenSequenceGenerationRequest(
            ImmutableArray<string>.Empty,
            ["declaration", "display"]);

        Assert.True(KnowledgeDrivenSequenceGenerator.TryGenerate(
            knowledge,
            model,
            request,
            out var result,
            out var error), error);

        Assert.Equal("declare display", result!.Text);
    }

    [Fact]
    public void Uses_the_token_model_to_choose_between_applicable_knowledge_rules()
    {
        Assert.True(SequenceTokenModel.TryTrain(
            ImmutableArray.Create(
                ImmutableArray.Create("B"),
                ImmutableArray.Create("B"),
                ImmutableArray.Create("B"),
                ImmutableArray.Create("A")),
            out var model,
            out var modelError), modelError);

        var knowledge = new SequenceKnowledgeBase(ImmutableArray.Create(
            Rule("first-fact", [], ["first"], [], "A", ["first"]),
            Rule("second-fact", [], ["second"], [], "B", ["second"])));
        var request = new KnowledgeDrivenSequenceGenerationRequest(
            ImmutableArray<string>.Empty,
            ["first", "second"]);

        Assert.True(KnowledgeDrivenSequenceGenerator.TryGenerate(
            knowledge,
            model,
            request,
            out var result,
            out var error), error);

        Assert.Equal("BA", result!.Text);
    }

    [Fact]
    public void Loads_next_token_rules_from_declarative_knowledge()
    {
        const string json = """
        {
          "formatVersion": 1,
          "rules": [
            {
              "id": "emit-first",
              "contextSuffix": [],
              "requiredFacts": ["first"],
              "requiresCompletedFacts": [],
              "nextToken": "first-token",
              "satisfiesFacts": ["first"],
              "weight": 0.5
            },
            {
              "id": "emit-second",
              "contextSuffix": ["first-token"],
              "requiredFacts": ["second"],
              "requiresCompletedFacts": ["first"],
              "nextToken": "second-token",
              "satisfiesFacts": ["second"]
            }
          ]
        }
        """;

        Assert.True(SequenceKnowledgeBaseLoader.TryLoad(
            json,
            out var knowledge,
            out var error), error);

        Assert.Equal(2, knowledge!.Rules.Length);
        Assert.Equal("second-token", knowledge.Rules[1].NextToken);
        Assert.Equal(["first"], knowledge.Rules[1].RequiresCompletedFacts);
    }

    [Fact]
    public void Rejects_knowledge_with_duplicate_rule_identifiers()
    {
        const string json = """
        {
          "formatVersion": 1,
          "rules": [
            { "id": "same", "nextToken": "a" },
            { "id": "same", "nextToken": "b" }
          ]
        }
        """;

        Assert.False(SequenceKnowledgeBaseLoader.TryLoad(json, out var knowledge, out _));
        Assert.Null(knowledge);
    }

    [Fact]
    public void Rejects_generation_when_active_facts_cannot_be_satisfied()
    {
        var knowledge = new SequenceKnowledgeBase(ImmutableArray.Create(
            Rule("unrelated", [], ["other-fact"], [], "token", ["other-fact"])));
        var request = new KnowledgeDrivenSequenceGenerationRequest(
            ImmutableArray<string>.Empty,
            ["required-fact"]);

        Assert.False(KnowledgeDrivenSequenceGenerator.TryGenerate(
            knowledge,
            null,
            request,
            out var result,
            out var error));
        Assert.Null(result);
        Assert.Contains("could not produce", error, StringComparison.Ordinal);
    }

    private static SequenceTokenRule Rule(
        string id,
        ImmutableArray<string> context,
        ImmutableArray<string> required,
        ImmutableArray<string> completed,
        string nextToken,
        ImmutableArray<string> satisfies) =>
        new(id, context, required, completed, nextToken, satisfies);
}
