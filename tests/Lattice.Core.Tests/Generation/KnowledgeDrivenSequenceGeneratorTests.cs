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
            " = 0\\n",
            "print",
            "(",
            "total",
            ")");
        Assert.True(SequenceTokenModel.TryTrain(
            ImmutableArray.Create(trainingSequence, trainingSequence, trainingSequence),
            out var model,
            out var modelError), modelError);

        var knowledge = new SequenceKnowledgeBase(ImmutableArray.Create(
            Rule("initialize-start", [SequenceKnowledgeBase.StartToken], ["initialize-start"], [], "let ", ["initialize-start"]),
            Rule("initialize-name", ["let "], ["initialize-name"], [], "total", ["initialize-name"]),
            Rule("initialize-value", ["let ", "total"], ["initialize-value"], [], " = 0\\n", ["initialize-value"]),
            Rule("display-start", [" = 0\\n"], ["display-start"], [], "print", ["display-start"]),
            Rule("display-open", ["print"], ["display-open"], [], "(", ["display-open"]),
            Rule("display-name", ["print", "("], ["display-name"], [], "total", ["display-name"]),
            Rule("display-close", ["print", "(", "total"], ["display-close"], [], ")", ["display-close"])));

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
                ImmutableArray.Create("seed", SequenceKnowledgeBase.StartToken, "B", "A"),
                ImmutableArray.Create("seed", SequenceKnowledgeBase.StartToken, "B", "A"),
                ImmutableArray.Create("seed", SequenceKnowledgeBase.StartToken, "B", "A"),
                ImmutableArray.Create("seed", SequenceKnowledgeBase.StartToken, "A", "B")),
            out var model,
            out var modelError), modelError);

        var knowledge = new SequenceKnowledgeBase(ImmutableArray.Create(
            Rule("first-fact", [], ["first"], [], "A", ["first"]),
            Rule("second-fact", [], ["second"], [], "B", ["second"])));
        var request = new KnowledgeDrivenSequenceGenerationRequest(
            ["seed"],
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

    [Fact]
    public void Ranks_complete_sequences_by_their_accumulated_scores()
    {
        var knowledge = new SequenceKnowledgeBase(ImmutableArray.Create(
            Rule("choose-a", [SequenceKnowledgeBase.StartToken], ["first"], [], "A", ["first"]) with { Weight = 10 },
            Rule("choose-b", [SequenceKnowledgeBase.StartToken], ["first"], [], "B", ["first"]),
            Rule("finish-a", ["A"], ["second"], ["first"], "X", ["second"]),
            Rule("finish-b", ["B"], ["second"], ["first"], "Y", ["second"]) with { Weight = 5 }));
        var request = new KnowledgeDrivenSequenceGenerationRequest(
            ImmutableArray<string>.Empty,
            ["first", "second"],
            BeamWidth: 2);

        Assert.True(KnowledgeDrivenSequenceGenerator.TryGenerate(
            knowledge,
            null,
            request,
            out var result,
            out var error), error);

        Assert.Equal("AX", result!.Text);
        Assert.Equal(10, result.Score);
    }

    private static SequenceTokenRule Rule(
        string id,
        ImmutableArray<string> context,
        ImmutableArray<string> required,
        ImmutableArray<string> completed,
        string nextToken,
        ImmutableArray<string> satisfies) =>
        new(id, context, required, completed, nextToken, satisfies);
    [Fact]
    public void Generates_arbitrary_values_from_embedded_knowledge_using_data_defined_encoding()
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(
            out var knowledge,
            out var loadError), loadError);

        var content = "Hello, \"world\"!\nSecond line";
        var request = new KnowledgeDrivenSequenceGenerationRequest(
            ImmutableArray<string>.Empty,
            ["language.python", "intent.program-generation", "program.output", "program.output.start", "value.output"])
        {
            FactValues = ImmutableArray.Create(
                new SequenceSemanticFact(
                    "value.output",
                    ImmutableDictionary<string, string>.Empty.Add("content", content)))
        };

        Assert.True(KnowledgeDrivenSequenceGenerator.TryGenerate(
            knowledge!,
            null,
            request,
            out var result,
            out var error), error);

        Assert.Equal("```python\nprint(\"Hello, \\\"world\\\"!\\nSecond line\")\n```", result!.Text);
    }

    [Fact]
    public void Rejects_value_bindings_that_reference_inactive_facts()
    {
        var knowledge = new SequenceKnowledgeBase(
            ImmutableArray.Create(
                new SequenceTokenRule(
                    "emit-value",
                    [SequenceKnowledgeBase.StartToken],
                    ["value"],
                    [],
                    null,
                    ["value"])
                {
                    TokenBinding = new SequenceTokenBinding("value", "content")
                }));

        var request = new KnowledgeDrivenSequenceGenerationRequest(
            ImmutableArray<string>.Empty,
            ["value"])
        {
            FactValues = ImmutableArray.Create(
                new SequenceSemanticFact(
                    "other",
                    ImmutableDictionary<string, string>.Empty.Add("content", "not active")))
        };

        Assert.False(KnowledgeDrivenSequenceGenerator.TryGenerate(
            knowledge,
            null,
            request,
            out var result,
            out var error));
        Assert.Null(result);
        Assert.Contains("reference one active fact", error, StringComparison.Ordinal);
    }



    [Fact]
    public void Extracts_multiple_active_semantic_facts_and_generates_from_one_request()
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(
            out var knowledge,
            out var loadError), loadError);

        var input = "Create a Python program that prints \"Hello, world!\".";
        Assert.True(SequenceInputFactExtractor.TryExtract(
            knowledge!,
            input,
            out var facts,
            out var extractionError), extractionError);

        Assert.Contains(facts, fact => fact.Id == "language.python");
        Assert.Contains(facts, fact => fact.Id == "program.output");
        var valueFact = Assert.Single(facts, fact => fact.Id == "value.output");
        Assert.Equal("Hello, world!", valueFact.Values["content"]);

        var request = new KnowledgeDrivenSequenceGenerationRequest(
            ImmutableArray<string>.Empty,
            facts.Select(fact => fact.Id).ToImmutableArray())
        {
            FactValues = facts
        };

        Assert.True(KnowledgeDrivenSequenceGenerator.TryGenerate(
            knowledge!,
            null,
            request,
            out var result,
            out var error), error);

        Assert.Equal("```python\nprint(\"Hello, world!\")\n```", result!.Text);
    }

    [Fact]
    public void Captures_unquoted_text_after_a_knowledge_matched_cue()
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(
            out var knowledge,
            out var loadError), loadError);

        Assert.True(SequenceInputFactExtractor.TryExtract(
            knowledge!,
            "Write a Python program that prints Greetings, traveler!",
            out var facts,
            out var error), error);

        var valueFact = Assert.Single(facts, fact => fact.Id == "value.output");
        Assert.Equal("Greetings, traveler!", valueFact.Values["content"]);
    }

    [Fact]
    public void Rejects_input_that_matches_no_semantic_fact_rule()
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(
            out var knowledge,
            out var loadError), loadError);

        Assert.False(SequenceInputFactExtractor.TryExtract(
            knowledge!,
            "Tell me something unrelated.",
            out var facts,
            out var error));
        Assert.Empty(facts);
        Assert.Contains("No semantic input rule", error, StringComparison.Ordinal);
    }


    [Fact]
    public void Extracts_multiple_named_values_from_one_assignment_phrase()
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(
            out var knowledge,
            out var loadError), loadError);

        Assert.True(SequenceInputFactExtractor.TryExtract(
            knowledge!,
            "Create a Python program that sets total to 5 and prints total + 3.",
            out var facts,
            out var error), error);

        var target = Assert.Single(facts, fact => fact.Id == "program.assignment.target");
        var value = Assert.Single(facts, fact => fact.Id == "program.assignment.value");

        Assert.Equal("total", target.Values["name"]);
        Assert.Equal("5", value.Values["value"]);
        Assert.Contains(facts, fact => fact.Id == "program.assignment.operator");
        Assert.Contains(facts, fact => fact.Id == "program.output");
        Assert.Contains(facts, fact => fact.Id == "value.output");
    }


    [Fact]
    public void Composes_assignment_and_output_facts_in_one_generated_sequence()
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(
            out var knowledge,
            out var loadError), loadError);

        Assert.True(SequenceInputFactExtractor.TryExtract(
            knowledge!,
            "Create a Python program that sets total to 5 and prints 'Done'.",
            out var facts,
            out var extractionError), extractionError);

        var request = new KnowledgeDrivenSequenceGenerationRequest(
            ImmutableArray<string>.Empty,
            facts.Select(fact => fact.Id).ToImmutableArray())
        {
            FactValues = facts
        };

        Assert.True(KnowledgeDrivenSequenceGenerator.TryGenerate(
            knowledge!,
            null,
            request,
            out var result,
            out var error), error);

        Assert.Equal("\u0060\u0060\u0060python\ntotal = 5\nprint(\"Done\")\n\u0060\u0060\u0060", result!.Text);
    }

    [Fact]
    public void Generates_a_write_script_request_through_semantic_facts()
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(
            out var knowledge,
            out var loadError), loadError);

        Assert.True(SequenceInputFactExtractor.TryExtract(
            knowledge!,
            "Write a Python script that prints 'Ready'.",
            out var facts,
            out var extractionError), extractionError);

        Assert.Contains(facts, fact => fact.Id == "intent.program-generation");
        Assert.Contains(facts, fact => fact.Id == "language.python");
        var output = Assert.Single(facts, fact => fact.Id == "value.output");
        Assert.Equal("Ready", output.Values["content"]);

        var request = new KnowledgeDrivenSequenceGenerationRequest(
            ImmutableArray<string>.Empty,
            facts.Select(fact => fact.Id).ToImmutableArray())
        {
            FactValues = facts
        };

        Assert.True(KnowledgeDrivenSequenceGenerator.TryGenerate(
            knowledge!,
            null,
            request,
            out var result,
            out var error), error);

        Assert.Equal("```python\nprint(\"Ready\")\n```", result!.Text);
    }
    [Fact]
    public void Does_not_recognize_generation_intent_for_an_ordinary_writing_request()
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(
            out var knowledge,
            out var loadError), loadError);

        Assert.True(SequenceInputFactExtractor.TryExtract(
            knowledge!,
            "Write a short note about Python.",
            out var facts,
            out var error), error);

        Assert.DoesNotContain(facts, fact => fact.Id == "intent.program-generation");
    }

    [Fact]
    public void Does_not_recognize_generation_intent_inside_quoted_output()
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(
            out var knowledge,
            out var loadError), loadError);

        Assert.True(SequenceInputFactExtractor.TryExtract(
            knowledge!,
            "Prepare a Python program that prints 'author a program that prints Ready'.",
            out var facts,
            out var error), error);

        Assert.DoesNotContain(facts, fact => fact.Id == "intent.program-generation");
        var output = Assert.Single(facts, fact => fact.Id == "value.output");
        Assert.Equal("author a program that prints Ready", output.Values["content"]);
    }


    [Fact]
    public void Preserves_punctuation_and_escapes_quotes_across_request_generation()
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(
            out var knowledge,
            out var loadError), loadError);

        Assert.True(SequenceInputFactExtractor.TryExtract(
            knowledge!,
            "Write a Python script that prints 'He said \"ready\" — 100%!'.",
            out var facts,
            out var extractionError), extractionError);

        var output = Assert.Single(facts, fact => fact.Id == "value.output");
        Assert.Equal("He said \"ready\" — 100%!", output.Values["content"]);

        var request = new KnowledgeDrivenSequenceGenerationRequest(
            ImmutableArray<string>.Empty,
            facts.Select(fact => fact.Id).ToImmutableArray())
        {
            FactValues = facts
        };

        Assert.True(KnowledgeDrivenSequenceGenerator.TryGenerate(
            knowledge!,
            null,
            request,
            out var result,
            out var error), error);

        Assert.Equal("```python\nprint(\"He said \\\"ready\\\" — 100%!\")\n```", result!.Text);
    }


    [Fact]
    public void Rejects_conflicting_values_from_equally_specific_input_rules()
    {
        var knowledge = new SequenceKnowledgeBase(
            ImmutableArray<SequenceTokenRule>.Empty,
            InputRules: ImmutableArray.Create(
                new SequenceInputFactRule(
                    "first-value",
                    ["make"],
                    "intent.make",
                    Captures: [
                        new SequenceInputFactCapture("value.output", "content", [], [], "First")
                    ]),
                new SequenceInputFactRule(
                    "second-value",
                    ["program"],
                    "intent.program",
                    Captures: [
                        new SequenceInputFactCapture("value.output", "content", [], [], "Second")
                    ])));

        Assert.False(SequenceInputFactExtractor.TryExtract(
            knowledge,
            "Make a program.",
            out var facts,
            out var error));

        Assert.Empty(facts);
        Assert.Contains("conflicting values", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Prefers_more_specific_captured_values_independent_of_rule_order(bool reverseRules)
    {
        var rules = new[]
        {
            new SequenceInputFactRule(
                "general-value",
                ["make"],
                "intent.make",
                Captures: [
                    new SequenceInputFactCapture("value.output", "content", [], [], "General")
                ]),
            new SequenceInputFactRule(
                "specific-value",
                ["make", "program"],
                "intent.program",
                Captures: [
                    new SequenceInputFactCapture("value.output", "content", [], [], "Specific")
                ])
        };
        if (reverseRules)
        {
            Array.Reverse(rules);
        }

        var knowledge = new SequenceKnowledgeBase(
            ImmutableArray<SequenceTokenRule>.Empty,
            InputRules: rules.ToImmutableArray());

        Assert.True(SequenceInputFactExtractor.TryExtract(
            knowledge,
            "Make a program.",
            out var facts,
            out var error), error);

        var output = Assert.Single(facts, fact => fact.Id == "value.output");
        Assert.Equal("Specific", output.Values["content"]);
    }

    [Theory]
    [InlineData(".")]
    [InlineData("?")]
    [InlineData("!")]
    public void Does_not_match_semantic_phrases_across_sentence_boundaries(string terminator)
    {
        var knowledge = new SequenceKnowledgeBase(
            ImmutableArray<SequenceTokenRule>.Empty,
            InputRules: [
                new SequenceInputFactRule(
                    "cross-sentence",
                    ["write", "program"],
                    "intent.program-generation")
            ]);

        Assert.False(SequenceInputFactExtractor.TryExtract(
            knowledge,
            $"Write a note{terminator} The program is ready.",
            out var facts,
            out var error));

        Assert.Empty(facts);
        Assert.Contains("No semantic input rule", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(".", "hello")]
    [InlineData("?", "hello")]
    [InlineData("!", "hello!")]
    public void Stops_captured_values_at_sentence_boundaries(string terminator, string expectedValue)
    {
        var knowledge = new SequenceKnowledgeBase(
            ImmutableArray<SequenceTokenRule>.Empty,
            InputRules: [
                new SequenceInputFactRule(
                    "capture-after-say",
                    ["say"],
                    "value.output",
                    "content")
            ]);

        Assert.True(SequenceInputFactExtractor.TryExtract(
            knowledge,
            $"Say hello{terminator} Then leave.",
            out var facts,
            out var error), error);

        var output = Assert.Single(facts, fact => fact.Id == "value.output");
        Assert.Equal(expectedValue, output.Values["content"]);
    }

    [Fact]
    public void Captures_values_when_stop_tokens_are_default()
    {
        var knowledge = new SequenceKnowledgeBase(
            ImmutableArray<SequenceTokenRule>.Empty,
            InputRules: [
                new SequenceInputFactRule(
                    "capture-after-say",
                    ["say"],
                    "value.output",
                    Captures: [
                        new SequenceInputFactCapture(
                            "value.output",
                            "content",
                            default,
                            default)
                    ])
            ]);

        Assert.True(SequenceInputFactExtractor.TryExtract(
            knowledge,
            "Say hello there.",
            out var facts,
            out var error), error);

        var output = Assert.Single(facts, fact => fact.Id == "value.output");
        Assert.Equal("hello there", output.Values["content"]);
    }


    [Fact]
    public void Generates_expression_valued_output_without_quoting_it()
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(
            out var knowledge,
            out var loadError), loadError);

        Assert.True(SequenceInputFactExtractor.TryExtract(
            knowledge!,
            "Create a Python program that prints total + 3.",
            out var facts,
            out var extractionError), extractionError);

        Assert.Contains(facts, fact => fact.Id == "value.output.expression");
        var output = Assert.Single(facts, fact => fact.Id == "value.output");
        Assert.Equal("total + 3", output.Values["content"]);

        var request = new KnowledgeDrivenSequenceGenerationRequest(
            ImmutableArray<string>.Empty,
            facts.Select(fact => fact.Id).ToImmutableArray())
        {
            FactValues = facts
        };

        Assert.True(KnowledgeDrivenSequenceGenerator.TryGenerate(
            knowledge!,
            null,
            request,
            out var result,
            out var error), error);

        Assert.Equal("\u0060\u0060\u0060python\nprint(total + 3)\n\u0060\u0060\u0060", result!.Text);
    }


    [Fact]
    public void Does_not_generate_assignment_without_program_generation_intent()
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(
            out var knowledge,
            out var loadError), loadError);

        Assert.True(SequenceInputFactExtractor.TryExtract(
            knowledge!,
            "In Python, set total to 5.",
            out var facts,
            out var extractionError), extractionError);

        Assert.Contains(facts, fact => fact.Id == "program.assignment.prefix");
        Assert.DoesNotContain(facts, fact => fact.Id == "intent.program-generation");

        var request = new KnowledgeDrivenSequenceGenerationRequest(
            ImmutableArray<string>.Empty,
            facts.Select(fact => fact.Id).ToImmutableArray())
        {
            FactValues = facts
        };

        Assert.False(KnowledgeDrivenSequenceGenerator.TryGenerate(
            knowledge!,
            null,
            request,
            out var result,
            out _));
        Assert.Null(result);
    }


    [Fact]
    public void Does_not_generate_python_output_without_program_generation_intent()
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(
            out var knowledge,
            out var loadError), loadError);

        var request = new KnowledgeDrivenSequenceGenerationRequest(
            ["print"],
            ["program.output", "value.output"])
        {
            FactValues = ImmutableArray.Create(
                new SequenceSemanticFact(
                    "value.output",
                    ImmutableDictionary<string, string>.Empty.Add("content", "Ready")))
        };

        Assert.False(KnowledgeDrivenSequenceGenerator.TryGenerate(
            knowledge!,
            null,
            request,
            out var result,
            out _));
        Assert.Null(result);
    }


    [Fact]
    public void Does_not_treat_creating_a_poem_about_python_as_program_generation()
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(
            out var knowledge,
            out var loadError), loadError);

        Assert.True(SequenceInputFactExtractor.TryExtract(
            knowledge!,
            "Create a poem about Python and say 'Ready'.",
            out var facts,
            out var extractionError), extractionError);

        Assert.Contains(facts, fact => fact.Id == "language.python");
        Assert.Contains(facts, fact => fact.Id == "program.output.start");
        Assert.DoesNotContain(facts, fact => fact.Id == "intent.program-generation");

        var request = new KnowledgeDrivenSequenceGenerationRequest(
            ImmutableArray<string>.Empty,
            facts.Select(fact => fact.Id).ToImmutableArray())
        {
            FactValues = facts
        };

        Assert.False(KnowledgeDrivenSequenceGenerator.TryGenerate(
            knowledge!,
            null,
            request,
            out var result,
            out _));
        Assert.Null(result);
    }


    [Fact]
    public void Does_not_apply_a_rule_when_a_forbidden_fact_is_active()
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(
            out var knowledge,
            out var loadError), loadError);

        var rules = knowledge!.Rules
            .Select(rule => rule.Id == "python-output-value"
                ? rule with { ForbiddenFacts = ["program.arithmetic"] }
                : rule)
            .ToImmutableArray();
        var constrainedKnowledge = knowledge with { Rules = rules };
        var request = new KnowledgeDrivenSequenceGenerationRequest(
            ["print", "("],
            ["program.output", "value.output", "program.arithmetic", "intent.program-generation"])
        {
            FactValues = ImmutableArray.Create(
                new SequenceSemanticFact(
                    "value.output",
                    ImmutableDictionary<string, string>.Empty.Add("content", "total + 3")))
        };

        Assert.False(KnowledgeDrivenSequenceGenerator.TryGenerate(
            constrainedKnowledge,
            null,
            request,
            out var result,
            out _));
        Assert.Null(result);
    }

}
