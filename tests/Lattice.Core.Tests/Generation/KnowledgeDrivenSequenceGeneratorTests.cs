using System.Collections.Immutable;
using Lattice.Core.Generation;
using Xunit;

namespace Lattice.Core.Tests.Generation;

public sealed class KnowledgeDrivenSequenceGeneratorTests
{
    [Fact]
    public void Extracts_repeated_captures_as_ordered_semantic_collections()
    {
        var knowledge = new SequenceKnowledgeBase(
            Rules: [],
            InputRules:
            [
                new SequenceInputFactRule(
                    "capture-definitions",
                    ["function"],
                    "definitions")
                {
                    RepeatMatches = true,
                    Captures =
                    [
                        new SequenceInputFactCapture(
                            "definitions",
                            "names",
                            ["named"],
                            ["returns"]),
                        new SequenceInputFactCapture(
                            "definitions",
                            "expressions",
                            ["returns"],
                            ["function"])
                        {
                            TrimSuffixCharacters = ","
                        }
                    ]
                }
            ]);

        Assert.True(
            SequenceInputFactExtractor.TryExtract(
                knowledge,
                "Define function named first returns value + 1, function named second returns first(2) + 3.",
                out var facts,
                out var error),
            error);

        var definitions = Assert.Single(facts);
        Assert.Equal(["first", "second"], definitions.Collections["names"]);
        Assert.Equal(["value + 1", "first(2) + 3"], definitions.Collections["expressions"]);
    }

    [Fact]
    public void Extracts_conditional_function_branches_as_aligned_semantic_collections()
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var loadError), loadError);
        const string prompt = "Write a Python function named classify that takes score and returns \"High\" when score is greater than 90, returns \"Pass\" when score is at least 50, and otherwise returns \"Fail\", then print classify(75).";

        Assert.True(
            SequenceInputFactExtractor.TryExtract(knowledge!, prompt, out var facts, out var extractionError),
            extractionError);

        var branches = Assert.Single(facts, fact => fact.Id == "program.requirement.function.branches");
        Assert.Equal(
            new SemanticBinaryExpression(
                SemanticExpressionOperator.GreaterThan,
                new SemanticNameExpression("score"),
                new SemanticNumberExpression("90")),
            branches.ExpressionCollections["condition"][0]);
        Assert.Equal(
            new SemanticBinaryExpression(
                SemanticExpressionOperator.GreaterThanOrEqual,
                new SemanticNameExpression("score"),
                new SemanticNumberExpression("50")),
            branches.ExpressionCollections["condition"][1]);
        Assert.Equal(new SemanticTextExpression("High"), branches.ExpressionCollections["value"][0]);
        Assert.Equal(new SemanticTextExpression("Pass"), branches.ExpressionCollections["value"][1]);
    }

    [Fact]
    public void Rejects_conditional_function_branches_that_reference_unknown_parameters()
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var loadError), loadError);
        const string prompt = "Write a Python function named classify that takes score and returns \"High\" when other is greater than 90, returns \"Pass\" when score is at least 50, and otherwise returns \"Fail\", then print classify(75).";

        Assert.True(
            SequenceInputFactExtractor.TryExtract(knowledge!, prompt, out var facts, out var extractionError),
            extractionError);

        Assert.False(SemanticProgramPlanner.TryGenerateProgram(
            facts,
            LoadPythonLanguagePack(),
            out _,
            out _,
            out var generationError));
        Assert.Contains("before any requirement defines it", generationError, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(
        "Write a Python program that fulfills this request: Given a dictionary of product names and prices, print each product with its price.",
        "product", "price")]
    [InlineData(
        "Write a Python program that fulfills this request: Given a dictionary of employee names and salaries, display each employee with its salary.",
        "employee", "salary")]
    public void Extracts_and_generates_dictionary_iteration_from_key_value_language(
        string prompt,
        string expectedKey,
        string expectedValue)
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var loadError), loadError);
        Assert.True(SequenceInputFactExtractor.TryExtract(knowledge!, prompt, out var facts, out var extractionError), extractionError);

        var iteration = Assert.Single(facts, fact => fact.Id == "program.dictionary-iteration");
        Assert.Equal(expectedKey, iteration.Values["key"]);
        Assert.Equal(expectedValue, iteration.Values["value"]);
        Assert.Equal("items", iteration.Values["collection"]);

        var request = new KnowledgeDrivenSequenceGenerationRequest(
            ImmutableArray<string>.Empty,
            facts.Select(fact => fact.Id).ToImmutableArray())
        {
            FactValues = facts
        };

        Assert.True(
            KnowledgeDrivenSequenceGenerator.TryGenerate(knowledge!, null, request, out var generated, out var generationError),
            $"{generationError} Facts: {string.Join(", ", facts.Select(fact => fact.Id))}");
        Assert.Contains("items = {}", generated!.Text, StringComparison.Ordinal);
        Assert.Contains($"for {expectedKey}, {expectedValue} in items.items():", generated!.Text, StringComparison.Ordinal);
        Assert.Contains($"print({expectedKey}, {expectedValue})", generated.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Extracts_greeting_function_requests_as_generic_function_facts()
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var loadError), loadError);
        const string prompt = "Write a Python program that fulfills this request: Define a function that accepts a person's name and returns a greeting. Call it with two different names.";

        Assert.True(SequenceInputFactExtractor.TryExtract(knowledge!, prompt, out var facts, out var extractionError), extractionError);
        Assert.Contains(facts, fact => fact.Id == "program.requirement.function");
        Assert.Contains(facts, fact => fact.Id == "program.requirement.function.template");
        Assert.Contains(facts, fact => fact.Id == "program.requirement.function.samples");
        Assert.Contains(facts, fact => fact.Id == "language.python");
        Assert.Contains(facts, fact => fact.Id == "intent.program-generation");

        Assert.True(
            SemanticProgramPlanner.TryGenerateProgram(
                facts,
                LoadPythonLanguagePack(),
                out var code,
                out _,
                out var generationError),
            generationError);
        Assert.Equal(
            string.Join(
                '\n',
                "def greet(name):",
                "    return f\"Hello, {name}!\"",
                "print(greet(\"Ada\"))",
                "print(greet(\"Grace\"))"),
            code);
    }

    [Fact]
    public void Extracts_and_plans_json_round_trip_with_custom_names()
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var loadError), loadError);

        const string prompt = "Write a Python program that saves a list of dictionaries named people to a JSON file named people-data.json and then loads it back.";
        Assert.True(
            SequenceInputFactExtractor.TryExtract(knowledge!, prompt, out var facts, out var extractionError),
            extractionError);

        var roundTrip = Assert.Single(
            facts,
            fact => fact.Id == "program.requirement.serialization-round-trip");
        Assert.Equal("people", roundTrip.Values["records"]);
        Assert.Equal("people-data.json", roundTrip.Values["path"]);

        Assert.True(
            SemanticProgramPlanner.TryGenerateProgram(
                facts,
                LoadPythonLanguagePack(),
                out var code,
                out _,
                out var generationError),
            generationError);
        Assert.Contains("people-data.json", code, StringComparison.Ordinal);
        Assert.Contains("loaded_people = json.load(input_file)", code, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Write a small Python program that groups a list of purchase records by customer and calculates each customer's total spending.", "purchase", "customer", "spending")]
    [InlineData("Create a small Python program that groups a list of order records by buyer and sums each buyer's total amount.", "order", "buyer", "amount")]
    public void Extracts_and_generates_grouped_record_aggregation_from_declarative_facts(string prompt, string record, string group, string measure)
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var loadError), loadError);
        Assert.True(SequenceInputFactExtractor.TryExtract(knowledge!, prompt, out var facts, out var extractionError), extractionError);
        var aggregation = Assert.Single(facts, fact => fact.Id == "program.record-aggregation");
        Assert.Equal(record, aggregation.Values["record"]);
        Assert.Equal(group, aggregation.Values["group"]);
        Assert.Equal(measure, aggregation.Values["measure"]);
        Assert.Contains(facts, fact => fact.Id == "program.record-aggregation.intent");
        Assert.Contains(facts, fact => fact.Id == "intent.program-generation");
        Assert.Contains(facts, fact => fact.Id == "program.collection");
        Assert.Contains(facts, fact => fact.Id == "program.iteration");
        Assert.Contains(facts, fact => fact.Id == "program.output");
        var request = new KnowledgeDrivenSequenceGenerationRequest(ImmutableArray<string>.Empty, facts.Select(fact => fact.Id).ToImmutableArray()) { FactValues = facts };
        Assert.True(KnowledgeDrivenSequenceGenerator.TryGenerate(knowledge!, null, request, out var generated, out var generationError), generationError);
        Assert.Contains($"for {record} in records:", generated!.Text, StringComparison.Ordinal);
        Assert.Contains($"key = {record}[\"{group}\"]", generated.Text, StringComparison.Ordinal);
        Assert.Contains($"{record}[\"{measure}\"]", generated.Text, StringComparison.Ordinal);
    }


    [Theory]
    [InlineData(
        "Create a Python program with a function named classify that takes score and returns 'accepted' when score is at least 10 or (score is less than 5 and score is not equal to 0) and otherwise returns 'rejected', then print classify(3).",
        "score >= 10 or score < 5 and score != 0", "\"accepted\"", "\"rejected\"")]
    [InlineData(
        "Create a Python program with a function named classify that takes score and returns 'accepted' when not (score is at least 10 or score is less than 5) and otherwise returns 'rejected', then print classify(7).",
        "not (score >= 10 or score < 5)", "\"accepted\"", "\"rejected\"")]
    [InlineData(
        "Create a Python program with a function named classify that takes score and returns 'accepted' when score is greater than or equal to 10 and otherwise returns 'rejected', then print classify(12).",
        "score >= 10", "\"accepted\"", "\"rejected\"")]
    [InlineData(
        "Create a Python program with a function named discount that takes price and limit and returns price * 0.9 when price is at least limit and otherwise returns price, then print discount(120, 100).",
        "price >= limit", "price * 0.9", "price")]
    [InlineData(
        "Create a Python program with a function named discount that takes price and limit and returns price * 0.9 when price is at least limit and price is less than limit + 50 and otherwise returns price, then print discount(120, 100).",
        "price >= limit and price < limit + 50", "price * 0.9", "price")]
    [InlineData(
        "Create a Python program with a function named access that takes role and returns 'allowed' when role equals 'admin' and otherwise returns 'denied', then print access('admin').",
        "role == \"admin\"", "\"allowed\"", "\"denied\"")]
    public void Extracts_and_generates_boolean_conditions_from_declarative_semantics(
        string prompt,
        string expectedCondition,
        string expectedTrueReturn,
        string expectedFalseReturn)
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var loadError), loadError);
        Assert.True(SequenceInputFactExtractor.TryExtract(knowledge!, prompt, out var facts, out var extractionError), extractionError);

        var branches = Assert.Single(facts, fact => fact.Id == "program.requirement.function.branches");
        Assert.Equal(expectedCondition, RenderExpression(Assert.Single(branches.ExpressionCollections["condition"])));

        Assert.True(
            SemanticProgramPlanner.TryGenerateProgram(
                facts,
                LoadPythonLanguagePack(),
                out var generated,
                out _,
                out var generationError),
            $"{generationError} Facts: {string.Join(", ", facts.Select(fact => fact.Id))}");
        Assert.Contains($"if {expectedCondition}:", generated, StringComparison.Ordinal);
        Assert.Contains($"return {expectedTrueReturn}", generated, StringComparison.Ordinal);
        Assert.Contains($"return {expectedFalseReturn}", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void Captures_changed_nested_boolean_expression_as_typed_semantic_nodes()
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var loadError), loadError);
        const string prompt = "Create a Python program with a function named verify that takes amount and offset and returns 'yes' when amount is greater than 23 and (amount + offset) is not equal to 41, and otherwise returns 'no', then print verify(24, 2).";

        Assert.True(SequenceInputFactExtractor.TryExtract(knowledge!, prompt, out var facts, out var extractionError), extractionError);
        var branches = Assert.Single(facts, fact => fact.Id == "program.requirement.function.branches");
        var expression = Assert.IsType<SemanticBinaryExpression>(Assert.Single(branches.ExpressionCollections["condition"]));
        Assert.Equal(SemanticExpressionOperator.And, expression.Operator);
        Assert.Equal(
            new SemanticBinaryExpression(
                SemanticExpressionOperator.GreaterThan,
                new SemanticNameExpression("amount"),
                new SemanticNumberExpression("23")),
            expression.Left);
        Assert.Equal(
            new SemanticBinaryExpression(
                SemanticExpressionOperator.NotEqual,
                new SemanticBinaryExpression(
                    SemanticExpressionOperator.Add,
                    new SemanticNameExpression("amount"),
                    new SemanticNameExpression("offset")),
                new SemanticNumberExpression("41")),
            expression.Right);
    }

    [Fact]
    public void Rejects_boolean_conditions_that_reference_undeclared_parameters()
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var loadError), loadError);
        Assert.True(SequenceInputFactExtractor.TryExtract(
            knowledge!,
            "Create a Python program with a function named classify that takes score and returns 'accepted' when score is at least 10 or other is less than 5 and otherwise returns 'rejected', then print classify(3).",
            out var facts,
            out var extractionError),
            extractionError);

        Assert.False(SemanticProgramPlanner.TryGenerateProgram(
            facts,
            LoadPythonLanguagePack(),
            out _,
            out _,
            out var generationError));
        Assert.Contains("before any requirement defines it", generationError, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_grouped_record_aggregation_with_invalid_identifiers()
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var loadError), loadError);
        Assert.False(SequenceInputFactExtractor.TryExtract(
            knowledge!,
            "Write a program that groups a list of purchase records by customer name and sums each customer's total amount.",
            out _,
            out _));
    }

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
            Rule("initialize-start", [SequenceKnowledgeBase.StartToken], ["initialize-start"], [], "let ", ["initialize-start"]),
            Rule("initialize-name", ["let "], ["initialize-name"], [], "total", ["initialize-name"]),
            Rule("initialize-value", ["let ", "total"], ["initialize-value"], [], " = 0\n", ["initialize-value"]),
            Rule("display-start", [" = 0\n"], ["display-start"], [], "print", ["display-start"]),
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

    [Theory]
    [InlineData("comparisonOperandsReferences")]
    [InlineData("booleanExpressionReferences")]
    public void Rejects_removed_unused_expression_validation_kinds(string kind)
    {
        var json = $$"""
        {
          "formatVersion": 1,
          "validations": [
            { "id": "obsolete-expression-check", "kind": "{{kind}}", "factId": "condition", "valueId": "expression" }
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

    [Theory]
    [InlineData(
        "Create a Python program that starts total at 0 and adds each number in [1, 2, 3] to total, then prints total.",
        "total = 0\nfor number in [1, 2, 3]:\n    total = total + number\nprint(total)")]
    [InlineData(
        "Build a Python script that initializes result to 0 and adds each value in [-2, 4.5, 1.5] to result, then displays result.",
        "result = 0\nfor value in [-2, 4.5, 1.5]:\n    result = result + value\nprint(result)")]
    public void Plans_accumulation_requests_from_request_supplied_collections(string input, string expectedCode)
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var loadError), loadError);
        Assert.True(SequenceInputFactExtractor.TryExtract(knowledge!, input, out var facts, out var extractionError), extractionError);
        Assert.True(
            facts.Any(fact => fact.Id == "program.accumulation"),
            $"Extracted facts: {string.Join(", ", facts.Select(fact => fact.Id))}");
        Assert.DoesNotContain(facts, fact => fact.Id.StartsWith("program.loop", StringComparison.Ordinal));

        var packLoader = new GenerationLanguagePackLoader();
        Assert.True(packLoader.TryLoadEmbedded(out var packs, out var packError), packError);
        Assert.True(
            SemanticProgramPlanner.TryGenerateProgram(
                facts,
                packs["python"],
                out var code,
                out _,
                out var generationError),
            $"Generation failed: {generationError}. Facts: {string.Join(", ", facts.Select(fact => fact.Id))}");
        Assert.Equal(expectedCode, code);
    }

    [Theory]
    [InlineData(
        "Create a Python program that prints each number in the list [2, 4, 6].",
        "number",
        "number:2|number:4|number:6",
        "for number in [2, 4, 6]:\n    print(number)")]
    [InlineData(
        "Build a Python script that displays each value from [1, 3.5, -2].",
        "value",
        "number:1|number:3.5|number:-2",
        "for value in [1, 3.5, -2]:\n    print(value)")]
    [InlineData(
        "Create a Python program that prints each name in the list ['Ada', 'Grace'].",
        "name",
        "text:Ada|text:Grace",
        "for name in [\"Ada\", \"Grace\"]:\n    print(name)")]
    [InlineData(
        "Build a Python script that displays each label from [\"red, blue\", 'green'].",
        "label",
        "text:red, blue|text:green",
        "for label in [\"red, blue\", \"green\"]:\n    print(label)")]
    [InlineData(
        "Create a Python program that prints each item from [2, \"Ada\", 3.5, -1.5].",
        "item",
        "number:2|text:Ada|number:3.5|number:-1.5",
        "for item in [2, \"Ada\", 3.5, -1.5]:\n    print(item)")]
    [InlineData(
        "Create a Python program that prints each item in the list [].",
        "item",
        "",
        "for item in []:\n    print(item)")]
    public void Generates_collection_iteration_from_structured_semantic_facts(
        string input,
        string expectedItem,
        string expectedSemanticItems,
        string expectedCode)
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var loadError), loadError);
        Assert.True(SequenceInputFactExtractor.TryExtract(knowledge!, input, out var facts, out var extractionError), extractionError);

        var collectionLoop = Assert.Single(facts.Where(fact => fact.Id == "program.collection-loop"));
        Assert.Equal(expectedItem, collectionLoop.Values["item"]);
        Assert.False(collectionLoop.Values.ContainsKey("collection"));
        var expectedItems = string.IsNullOrEmpty(expectedSemanticItems)
            ? ImmutableArray<SemanticCollectionItem>.Empty
            : expectedSemanticItems.Split('|')
                .Select(item =>
                {
                    var separator = item.IndexOf(':');
                    return new SemanticCollectionItem(
                        Enum.Parse<SemanticCollectionItemKind>(item[..separator], ignoreCase: true),
                        item[(separator + 1)..]);
                })
                .ToImmutableArray();
        Assert.Equal(expectedItems, collectionLoop.SemanticCollections["collection"].Items);

        var languagePackLoader = new GenerationLanguagePackLoader();
        Assert.True(languagePackLoader.TryLoadEmbedded(out var languagePacks, out var packLoadError), packLoadError);
        var request = new KnowledgeDrivenSequenceGenerationRequest(
            ImmutableArray<string>.Empty,
            facts.Select(fact => fact.Id).ToImmutableArray())
        {
            FactValues = facts,
            LanguagePack = languagePacks["python"]
        };
        Assert.True(KnowledgeDrivenSequenceGenerator.TryGenerate(
            knowledge!, null, request, out var generated, out var generationError),
            $"Generation failed: {generationError}. Facts: {string.Join(", ", facts.Select(fact => fact.Id))}");

        var fence = new string((char)96, 3);
        Assert.Equal(
            string.Join(Environment.NewLine, fence + "python", expectedCode, fence),
            generated!.Text);
    }

    [Fact]
    public void Loads_declarative_semantic_collection_input_formats()
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var loadError), loadError);

        var captures = knowledge!.InputRules
            .SelectMany(rule => rule.Captures)
            .Where(capture => capture.SemanticCollectionFormat is not null)
            .ToArray();

        Assert.Equal(3, captures.Length);
        Assert.All(captures, capture =>
        {
            Assert.Equal("[", capture.SemanticCollectionFormat!.OpeningDelimiter);
            Assert.Equal("]", capture.SemanticCollectionFormat.ClosingDelimiter);
            Assert.Equal(",", capture.SemanticCollectionFormat.ItemSeparator);
            Assert.Contains(capture.SemanticCollectionFormat.ItemFormats,
                format => format.Kind == SemanticCollectionItemKind.Number);
            Assert.Contains(capture.SemanticCollectionFormat.ItemFormats,
                format => format.Kind == SemanticCollectionItemKind.Text);
        });
    }

    [Fact]
    public void Validates_semantic_collection_items_and_their_types()
    {
        var fact = new SequenceSemanticFact(
            "program.collection-loop",
            ImmutableDictionary<string, string>.Empty)
        {
            SemanticCollections = ImmutableDictionary<string, SemanticCollection>.Empty.Add(
                "collection",
                new SemanticCollection([
                    new SemanticCollectionItem(SemanticCollectionItemKind.Text, "red, blue"),
                    new SemanticCollectionItem(SemanticCollectionItemKind.Number, "3.5")
                ]))
        };
        var validation = new SequenceInputFactValidation(
            "collection-loop-has-semantic-values",
            "semanticCollection",
            "program.collection-loop",
            "collection");

        Assert.True(
            SequenceInputFactValidator.TryValidate([validation], [fact], out var error),
            error);
    }

    [Fact]
    public void Rejects_collection_items_that_do_not_match_the_declared_semantic_types()
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var loadError), loadError);

        Assert.False(SequenceInputFactExtractor.TryExtract(
            knowledge!,
            "Create a Python program that prints each item in the list [__import__('os')].",
            out var facts,
            out _));
        Assert.Empty(facts);
    }

    [Fact]
    public void Plans_a_manual_total_from_the_declared_example_data()
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var loadError), loadError);

        const string input = "Given a list of numbers, write a Python program that calculates their total without using the built-in sum function.";
        Assert.True(SequenceInputFactExtractor.TryExtract(
            knowledge!,
            input,
            out var facts,
            out var extractionError), extractionError);

        Assert.Contains(facts, fact => fact.Id == "program.accumulation");
        Assert.DoesNotContain(facts, fact => fact.Id == "program.manual-list-total");

        var packLoader = new GenerationLanguagePackLoader();
        Assert.True(packLoader.TryLoadEmbedded(out var packs, out var packError), packError);
        Assert.True(
            SemanticProgramPlanner.TryGenerateProgram(
                facts,
                packs["python"],
                out var code,
                out _,
                out var generationError),
            $"Generation failed: {generationError}. Facts: {string.Join(", ", facts.Select(fact => fact.Id))}");

        // The request names the total but no collection, so the declared example data supplies the numbers,
        // the item name, and the starting value.
        Assert.Equal(
            string.Join('\n', "total = 0", "for number in [1, 2, 3, 4, 5]:", "    total = total + number", "print(total)"),
            code);
    }

    [Fact]
    public void Keeps_request_supplied_collections_ahead_of_the_example_data()
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var loadError), loadError);

        const string input = "Create a Python program that starts total at 0 and adds each number in [4, 9] to total, then prints total.";
        Assert.True(SequenceInputFactExtractor.TryExtract(
            knowledge!,
            input,
            out var facts,
            out var extractionError), extractionError);

        var packLoader = new GenerationLanguagePackLoader();
        Assert.True(packLoader.TryLoadEmbedded(out var packs, out var packError), packError);
        Assert.True(
            SemanticProgramPlanner.TryGenerateProgram(
                facts,
                packs["python"],
                out var code,
                out _,
                out var generationError),
            generationError);

        Assert.Equal(
            string.Join('\n', "total = 0", "for number in [4, 9]:", "    total = total + number", "print(total)"),
            code);
    }

    [Theory]
    [InlineData("Build a Python script that stores {'Ada': 3, 'Grace': 5, 'Linus': 6} as a dictionary named counts, then displays each name and its count.", "program.dictionary-name-count-iteration", "counts = {'Ada': 3, 'Grace': 5, 'Linus': 6}\nfor name, count in counts.items():\n    print(name, count)")]
    [InlineData("Create a Python program that stores {'apple': 1.5, 'banana': 0.75} in a dictionary named prices, then prints each product and its price.", "program.dictionary-product-iteration", "prices = {'apple': 1.5, 'banana': 0.75}\nfor product, price in prices.items():\n    print(product, price)")]
    public void Generates_dictionary_iteration_from_declarative_semantic_facts(string input, string expectedFact, string expectedCode)
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var loadError), loadError);
        Assert.True(SequenceInputFactExtractor.TryExtract(knowledge!, input, out var facts, out var extractionError), extractionError);
        Assert.Contains(facts, fact => fact.Id == expectedFact);

        var request = new KnowledgeDrivenSequenceGenerationRequest(
            ImmutableArray<string>.Empty,
            facts.Select(fact => fact.Id).ToImmutableArray())
        {
            FactValues = facts
        };
        Assert.True(KnowledgeDrivenSequenceGenerator.TryGenerate(
            knowledge!, null, request, out var result, out var generationError),
            $"Generation failed: {generationError}. Facts: {string.Join(", ", facts.Select(fact => fact.Id))}");

        var fence = new string((char)96, 3);
        Assert.Equal(string.Join(Environment.NewLine, fence + "python", expectedCode.Replace("\n", Environment.NewLine), fence), result!.Text);
    }


    [Theory]
    [InlineData(
        "Create a Python program that defines a function named order_records that accepts a list of records named people and a list of field names named sort_keys, then returns the records sorted by those fields.",
        "program.multi-field-record-sorting")]
    [InlineData(
        "Create a Python program that defines a function named order_records that accepts a list of records named people and a list of field names named sort_keys, then returns the records sorted case-insensitively by those fields.",
        "program.multi-field-record-sorting.case-insensitive")]
    public void Generates_multi_field_record_sorting_from_declarative_facts(string input, string expectedFact)
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var loadError), loadError);
        Assert.True(SequenceInputFactExtractor.TryExtract(knowledge!, input, out var facts, out var extractionError), extractionError);
        Assert.Contains(facts, fact => fact.Id == expectedFact);
        Assert.Contains(facts, fact => fact.Id == "program.multi-field-record-sorting");
        Assert.Contains(facts, fact => fact.Id == "language.python");
        Assert.Contains(facts, fact => fact.Id == "intent.program-generation");
        Assert.Contains(facts, fact => fact.Id == "program.function");
        Assert.Contains(facts, fact => fact.Id == "program.collection");
        Assert.Contains(facts, fact => fact.Id == "program.output");

        const string sample = """[{"department": "Engineering", "name": "Ada"}, {"department": "Design", "name": "Grace"}, {"department": "Engineering", "name": "Linus"}]""";
        var key = expectedFact.EndsWith("case-insensitive", StringComparison.Ordinal)
            ? "tuple(record[field].casefold() for field in sort_keys)"
            : "tuple(record[field] for field in sort_keys)";
        var expectedCode = string.Join(Environment.NewLine,
            "def order_records(people, sort_keys):",
            $"    return sorted(people, key=lambda record: {key})",
            $"print(order_records({sample}, [\"department\", \"name\"]))");

        var request = new KnowledgeDrivenSequenceGenerationRequest(
            ImmutableArray<string>.Empty,
            facts.Select(fact => fact.Id).ToImmutableArray())
        {
            FactValues = facts
        };
        Assert.True(KnowledgeDrivenSequenceGenerator.TryGenerate(
            knowledge!, null, request, out var result, out var generationError),
            $"{generationError}. Active facts: {string.Join(", ", facts.Select(fact => fact.Id))}");

        var fence = new string((char)96, 3);
        Assert.Equal(string.Join(Environment.NewLine, fence + "python", expectedCode, fence), result!.Text);
    }

    [Theory]
    [InlineData("Create a Python program that defines a reusable function named sort_people that accepts a list of records named people and a field name named sort_key, then returns the records sorted by that field in descending order.", "sort_people", "people", "sort_key", "program.single-field-record-sorting.descending")]
    [InlineData("Create a Python program that defines a function named order_records that accepts a list of records named entries and a field name named order_by, then returns the records sorted case-insensitively by that field.", "order_records", "entries", "order_by", "program.single-field-record-sorting.case-insensitive")]
    public void Extracts_and_generates_single_field_record_sorting_declaratively(string prompt, string functionName, string recordsName, string fieldName, string expectedFact)
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var loadError), loadError);
        var extracted = SequenceInputFactExtractor.TryExtract(knowledge!, prompt, out var facts, out var extractionError);
        var captureSummary = string.Join(", ", facts.SelectMany(fact => fact.Values.Select(value => $"{fact.Id}.{value.Key}={value.Value}")));
        Assert.True(extracted, $"{extractionError}. Captures: {captureSummary}");
        Assert.Contains(facts, fact => fact.Id == "program.single-field-record-sorting");
        Assert.Contains(facts, fact => fact.Id == expectedFact);
        var languagePackLoader = new GenerationLanguagePackLoader();
        Assert.True(languagePackLoader.TryLoadEmbedded(out var languagePacks, out var languagePackError), languagePackError);
        Assert.True(SequenceInputFactValidator.TryValidate(
            knowledge!.Validations, facts, languagePacks["python"], out var validationError), validationError);
        Assert.True(KnowledgeDrivenSequenceGenerator.TryGenerate(
            knowledge!,
            null,
            new KnowledgeDrivenSequenceGenerationRequest(
                ImmutableArray<string>.Empty,
                facts.Select(fact => fact.Id).ToImmutableArray()) { FactValues = facts },
            out var generated,
            out var generationError), $"{generationError}. Active facts: {string.Join(", ", facts.Select(fact => fact.Id))}. Values: {captureSummary}");
        Assert.Contains($"def {functionName}({recordsName}, {fieldName}):", generated!.Text);
        Assert.Contains($"sorted({recordsName}, key=lambda record: record[{fieldName}]", generated.Text);
    }

    [Fact]
    public void Rejects_single_field_record_sorting_with_invalid_identifiers()
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var loadError), loadError);
        Assert.False(SequenceInputFactExtractor.TryExtract(
            knowledge!,
            "Create a Python program that defines a function named sort_people that accepts a list of records named people and a field name named sort-key, then returns the records sorted by that field.",
            out _,
            out _));
    }

    [Fact]
    public void Generates_ordered_collection_items_and_advances_only_at_declared_boundaries()
    {
        var fact = new SequenceSemanticFact(
            "locals",
            ImmutableDictionary<string, string>.Empty)
        {
            Collections = ImmutableDictionary<string, ImmutableArray<string>>.Empty
                .Add("assignments", ["first = left + right; ", "second = first * 2; "])
        };
        var binding = new SequenceTokenBinding("locals", "assignments", UseCollection: true);
        var knowledge = new SequenceKnowledgeBase([
            new SequenceTokenRule("emit-item", [], ["locals"], [], null, [], 1)
            {
                TokenBinding = binding,
                AdvanceCollections = ["locals:assignments"]
            }
        ]);
        var request = new KnowledgeDrivenSequenceGenerationRequest([], ["locals"])
        {
            FactValues = [fact]
        };

        Assert.True(KnowledgeDrivenSequenceGenerator.TryGenerate(
            knowledge, null, request, out var result, out var error), error);
        Assert.Equal(["first = left + right; ", "second = first * 2; "], result!.Tokens);
        Assert.False(result.SearchLimitReached);
    }

    [Fact]
    public void Loads_collection_bindings_and_explicit_cursor_advancement()
    {
        const string json = """
        {
          "formatVersion": 1,
          "rules": [{
            "id": "emit-item",
            "contextSuffix": [],
            "requiredFacts": ["locals"],
            "requiresCompletedFacts": [],
            "tokenBinding": { "factId": "locals", "valueId": "items", "collection": true },
            "satisfiesFacts": [],
            "advanceCollections": ["locals:items"]
          }]
        }
        """;

        Assert.True(SequenceKnowledgeBaseLoader.TryLoad(json, out var knowledge, out var error), error);
        Assert.True(knowledge!.Rules[0].TokenBinding!.UseCollection);
        Assert.Equal(["locals:items"], knowledge.Rules[0].AdvanceCollections);
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

        Assert.Equal(
            string.Join(Environment.NewLine,
                "```python",
                "print(\"Hello, \\\"world\\\"!\\nSecond line\")",
                "```"),
            result!.Text);
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
    public void Loads_ordered_collection_capture_metadata_from_knowledge()
    {
        const string json = """
        {
          "formatVersion": 1,
          "rules": [{ "id": "unused", "nextToken": "unused" }],
          "inputRules": [{
            "id": "capture-items",
            "matchTokens": ["variables"],
            "factId": "program.locals",
            "captures": [{
              "factId": "program.locals",
              "valueId": "names",
              "startAfterTokens": ["are"],
              "stopBeforeTokens": ["and"],
              "collectionDelimiter": ","
            }]
          }]
        }
        """;

        Assert.True(SequenceKnowledgeBaseLoader.TryLoad(json, out var knowledge, out var error), error);
        Assert.Equal(",", Assert.Single(knowledge!.InputRules[0].Captures).CollectionDelimiter);
    }

    [Fact]
    public void Extracts_an_ordered_collection_from_a_declarative_input_capture()
    {
        var knowledge = new SequenceKnowledgeBase(
            [Rule("unused", [], ["unused"], [], "unused", ["unused"])],
            InputRules: [
                new SequenceInputFactRule(
                    "capture-items",
                    ["variables"],
                    "program.locals",
                    Captures: [
                        new SequenceInputFactCapture(
                            "program.locals",
                            "names",
                            ["are"],
                            ["and", "then"],
                            LiteralValue: null)
                        {
                            CollectionDelimiter = ","
                        }
                    ])
            ]);

        Assert.True(SequenceInputFactExtractor.TryExtract(
            knowledge,
            "Variables are first, second, third and then return.",
            out var facts,
            out var error), error);

        var locals = Assert.Single(facts, fact => fact.Id == "program.locals");
        Assert.Equal(["first", "second", "third"], locals.Collections["names"]);
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

        Assert.Equal("total", Assert.Single(target.Collections["name"]));
        Assert.Equal(new SemanticNumberExpression("5"), Assert.Single(value.ExpressionCollections["value"]));
        Assert.Contains(facts, fact => fact.Id == "program.assignment.operator");
        Assert.Contains(facts, fact => fact.Id == "program.output");
        Assert.Contains(facts, fact => fact.Id == "value.output");
    }


    [Theory]
    [InlineData("assigns units to 17", "units", "number", "17")]
    [InlineData("sets title to 'New: value!'", "title", "text", "New: value!")]
    public void Captures_changed_assignment_names_and_literal_values(
        string assignmentPhrase,
        string expectedName,
        string expectedValueKind,
        string expectedValue)
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var loadError), loadError);
        Assert.True(SequenceInputFactExtractor.TryExtract(
            knowledge!,
            $"Create a Python program that {assignmentPhrase}.",
            out var facts,
            out var error), error);

        var target = Assert.Single(facts, fact => fact.Id == "program.assignment.target");
        var value = Assert.Single(facts, fact => fact.Id == "program.assignment.value");
        Assert.Equal(expectedName, Assert.Single(target.Collections["name"]));
        var expression = Assert.Single(value.ExpressionCollections["value"]);
        SemanticExpression expected = expectedValueKind == "number"
            ? new SemanticNumberExpression(expectedValue)
            : new SemanticTextExpression(expectedValue);
        Assert.Equal(expected, expression);
    }

    [Fact]
    public void Extracts_generic_scalar_requirements_without_task_specific_fact_ids()
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var loadError), loadError);
        const string prompt = "Create a Python program that assigns subtotal to 23.";

        Assert.True(SequenceInputFactExtractor.TryExtractProgram(knowledge!, prompt, out var program, out var error), error);

        var assignment = Assert.IsType<AssignmentRequirement>(program!.Requirements[0]);
        Assert.Equal("subtotal", assignment.VariableName);
        Assert.Equal(new SemanticNumberExpression("23"), assignment.Value);
        Assert.Single(program.Requirements);
    }

    [Fact]
    public void Extracts_multiple_generic_assignments_in_request_order()
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var loadError), loadError);
        const string prompt = "Create a Python program that sets first_count to 2 then sets final_count to 9.";

        Assert.True(SequenceInputFactExtractor.TryExtractProgram(knowledge!, prompt, out var program, out var error), error);

        Assert.Collection(
            program!.Requirements,
            requirement => Assert.Equal(
                new AssignmentRequirement("first_count", new SemanticNumberExpression("2")), requirement),
            requirement => Assert.Equal(
                new AssignmentRequirement("final_count", new SemanticNumberExpression("9")), requirement));
    }

    [Fact]
    public void Extracts_generic_assignment_and_output_requirements()
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var loadError), loadError);
        const string prompt = "Create a Python program that assigns subtotal to 23 then prints subtotal.";

        Assert.True(SequenceInputFactExtractor.TryExtractProgram(knowledge!, prompt, out var program, out var error), error);

        Assert.Collection(
            program!.Requirements,
            requirement => Assert.Equal(
                new AssignmentRequirement("subtotal", new SemanticNumberExpression("23")), requirement),
            requirement => Assert.Equal(
                new OutputRequirement(new SemanticNameExpression("subtotal")), requirement));
    }

    [Fact]
    public void Extracts_generic_condition_and_branch_output_after_assignment()
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var loadError), loadError);
        const string prompt = "Create a Python program that sets age to 21 then when age is at least 18 then prints 'adult'.";

        Assert.True(SequenceInputFactExtractor.TryExtractProgram(knowledge!, prompt, out var program, out var error), error);

        Assert.Collection(
            program!.Requirements,
            requirement => Assert.Equal(
                new AssignmentRequirement("age", new SemanticNumberExpression("21")), requirement),
            requirement =>
            {
                var conditional = Assert.IsType<ConditionalRequirement>(requirement);
                Assert.Equal(
                    new SemanticBinaryExpression(
                        SemanticExpressionOperator.GreaterThanOrEqual,
                        new SemanticNameExpression("age"),
                        new SemanticNumberExpression("18")),
                    conditional.Condition);
                Assert.Equal(
                    new OutputRequirement(new SemanticTextExpression("adult")),
                    Assert.Single(conditional.ThenRequirements));
                Assert.Empty(conditional.ElseRequirements);
            });
    }

    [Theory]
    [InlineData("Create a Python program that assigns balance to 41 then displays balance.", "balance", "41")]
    [InlineData("Make a Python program that sets greeting to 'Welcome, Ada!' then prints greeting.", "greeting", "Welcome, Ada!")]
    public void Extracts_changed_assignment_and_output_values(
        string prompt,
        string variableName,
        string expectedValue)
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var loadError), loadError);

        Assert.True(SequenceInputFactExtractor.TryExtractProgram(knowledge!, prompt, out var program, out var error), error);

        SemanticExpression assignedValue = expectedValue == "41"
            ? new SemanticNumberExpression(expectedValue)
            : new SemanticTextExpression(expectedValue);
        Assert.Collection(
            program!.Requirements,
            requirement => Assert.Equal(new AssignmentRequirement(variableName, assignedValue), requirement),
            requirement => Assert.Equal(
                new OutputRequirement(new SemanticNameExpression(variableName)), requirement));
    }

    [Fact]
    public void Extracts_assignments_in_request_order_across_different_capture_rules()
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var loadError), loadError);
        const string prompt = "Create a Python program that assigns second_count to 9 then sets first_count to 2.";

        Assert.True(SequenceInputFactExtractor.TryExtractProgram(knowledge!, prompt, out var program, out var error), error);

        Assert.Collection(
            program!.Requirements,
            requirement => Assert.Equal(
                new AssignmentRequirement("second_count", new SemanticNumberExpression("9")), requirement),
            requirement => Assert.Equal(
                new AssignmentRequirement("first_count", new SemanticNumberExpression("2")), requirement));
    }

    [Fact]
    public void Captures_assignment_references_and_arithmetic_operator_as_typed_tree()
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var loadError), loadError);
        Assert.True(SequenceInputFactExtractor.TryExtract(
            knowledge!,
            "Create a Python program that assigns subtotal to unit_price * quantity + 7.",
            out var facts,
            out var error), error);

        var target = Assert.Single(facts, fact => fact.Id == "program.assignment.target");
        var value = Assert.Single(facts, fact => fact.Id == "program.assignment.value");
        Assert.Equal("subtotal", Assert.Single(target.Collections["name"]));
        Assert.Empty(value.Values);
        Assert.Equal(
            new SemanticBinaryExpression(
                SemanticExpressionOperator.Add,
                new SemanticBinaryExpression(
                    SemanticExpressionOperator.Multiply,
                    new SemanticNameExpression("unit_price"),
                    new SemanticNameExpression("quantity")),
                new SemanticNumberExpression("7")),
            Assert.Single(value.ExpressionCollections["value"]));
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
    public void Composes_assignment_and_arithmetic_output_through_semantic_facts()
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(
            out var knowledge,
            out var loadError), loadError);

        Assert.True(SequenceInputFactExtractor.TryExtract(
            knowledge!,
            "Create a Python program that sets total to 5 and prints total + 3.",
            out var facts,
            out var extractionError), extractionError);

        Assert.Contains(facts, fact => fact.Id == "program.assignment.target");
        Assert.Contains(facts, fact => fact.Id == "program.assignment.value");
        Assert.Contains(facts, fact => fact.Id == "program.arithmetic");
        Assert.Contains(facts, fact => fact.Id == "value.output.expression");

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

        Assert.Equal("```python\ntotal = 5\nprint(total + 3)\n```", result!.Text);
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


    [Fact]
    public void Generates_two_number_addition_from_embedded_semantic_knowledge()
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var loadError), loadError);
        Assert.True(SequenceInputFactExtractor.TryExtract(
            knowledge!,
            "Write a Python program that stores two numbers in variables, adds them, and prints the result.",
            out var facts,
            out var extractError), extractError);

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
            out var generationError), generationError);

        var fence = new string((char)96, 3);
        Assert.Equal(
            fence + "python\nfirst_number = 10\nsecond_number = 20\nresult = first_number + second_number\nprint(result)\n" + fence,
            result!.Text);
    }

    [Theory]
    [InlineData("Write a Python program that reports whether an integer is positive, negative, or zero.")]
    [InlineData("Write a Python program that classifies an integer as positive, negative, or zero.")]
    public void Generates_integer_sign_classification_from_semantic_facts(string prompt)
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(
            out var knowledge,
            out var loadError), loadError);

        Assert.True(SequenceInputFactExtractor.TryExtract(
            knowledge!,
            prompt,
            out var facts,
            out var extractionError), extractionError);

        Assert.Contains(facts, fact => fact.Id == "program.assignment.classification");
        Assert.Contains(facts, fact => fact.Id == "program.classification.positive");
        Assert.Contains(facts, fact => fact.Id == "program.classification.negative");
        Assert.Contains(facts, fact => fact.Id == "program.classification.zero");

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
            out var generationError), generationError);

        var fence = new string((char)96, 3);
        Assert.Equal(
            string.Join(Environment.NewLine,
                fence + "python",
                "number = -5",
                "if number > 0:",
                "    print(\"Positive\")",
                "if number < 0:",
                "    print(\"Negative\")",
                "if number == 0:",
                "    print(\"Zero\")",
                fence),
            result!.Text);
    }

    [Theory]
    [InlineData("Define a Python class representing a bank account with a balance, a deposit method, and a withdrawal method that refuses withdrawals larger than the balance.")]
    [InlineData("Write a Python program that fulfills this request: Define a class representing a bank account with a balance, a deposit method, and a withdrawal method that refuses withdrawals larger than the balance.")]
    public void Extracts_and_generates_class_requests_as_generic_class_facts(string input)
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var loadError), loadError);
        Assert.True(SequenceInputFactExtractor.TryExtract(knowledge!, input, out var facts, out var extractionError), extractionError);

        var declaration = Assert.Single(facts, fact => fact.Id == "program.requirement.class");
        Assert.Equal(["bank account"], declaration.Collections["name"]);

        var fields = Assert.Single(facts, fact => fact.Id == "program.requirement.class.field");
        Assert.Equal(["balance"], fields.Collections["field"]);

        var methods = Assert.Single(facts, fact => fact.Id == "program.requirement.class.method");
        Assert.Equal(["deposit", "withdraw"], methods.Collections["method"]);
        Assert.Equal(["none", "Boolean"], methods.Collections["result"]);

        Assert.True(
            SemanticProgramPlanner.TryGenerateProgram(
                facts,
                LoadPythonLanguagePack(),
                out var code,
                out _,
                out var generationError),
            generationError);
        Assert.Equal(
            string.Join(
                '\n',
                "class BankAccount:",
                "    def __init__(self, balance):",
                "        self.balance = balance",
                "    def deposit(self, amount):",
                "        self.balance += amount",
                "    def withdraw(self, amount):",
                "        if amount > self.balance:",
                "            return False",
                "        self.balance -= amount",
                "        return True"),
            code);
    }

    [Fact]
    public void Directory_listing_facts_are_planned_by_the_language_neutral_pipeline()
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var loadError), loadError);
        const string input = "Write a Python program that accepts a directory path and reports the names and sizes of all files directly inside it, handling a missing directory cleanly.";
        Assert.True(SequenceInputFactExtractor.TryExtract(knowledge!, input, out var facts, out var extractionError), extractionError);
        Assert.Contains(facts, fact => fact.Id == "program.requirement.directory-listing");
        var packLoader = new GenerationLanguagePackLoader();
        Assert.True(packLoader.TryLoadEmbedded(out var packs, out var packError), packError);
        Assert.True(
            SemanticProgramPlanner.TryGenerateProgram(facts, packs["python"], out var code, out _, out var programError),
            programError);

        Assert.Equal(string.Join(Environment.NewLine,
            "import os",
            "import sys",
            "directory = sys.argv[1]",
            "try:",
            "    for entry in sorted(os.scandir(directory), key=lambda entry: entry.name):",
            "        if entry.is_file():",
            "            print(f\"{entry.name}: {entry.stat().st_size} bytes\")",
            "except FileNotFoundError:",
            "    print(f\"Directory not found: {directory}\")"), code);
    }

    [Theory]
    [InlineData(
        "Generate a Python program with a function named transform that takes value and set shifted to value + 2 and sets scaled to shifted * 4 and returns scaled - shifted, then print transform(3).",
        true)]
    [InlineData(
        "Create a Python program with a function named calculate that takes value and sets total to value + 1 and sets total to total * 2 and returns total, then print calculate(3).",
        false)]
    [InlineData(
        "Create a Python program with a function named calculate that takes value and sets value to value * 2 and returns value, then print calculate(3).",
        false)]
    [InlineData(
        "Create a Python program with a function named calculate that takes value and sets first to later + 1 and sets later to value * 2 and returns later, then print calculate(3).",
        false)]
    [InlineData(
        "Create a Python program with a function named calculate that takes value and sets doubled to value * 2 and returns missing + doubled, then print calculate(3).",
        false)]
    [InlineData(
        "Create a Python program with a function named calculate that takes value and sets doubled to value * 2 and returns value + 1, then print calculate(3).",
        false)]
    [InlineData(
        "Create a Python program with a function named calculate that takes value and sets doubled to value + * 2 and returns doubled, then print calculate(3).",
        false)]
    [InlineData(
        "Create a Python program with a function named calculate that takes value and sets doubled to value * 2 and returns doubled, then print calculate(3, 4).",
        false)]
    [InlineData(
        "Create a Python program with a function named calculate that takes value and sets doubled to helper(value) and returns doubled, then print calculate(3).",
        false)]
    [InlineData(
        "Create a Python program with a function named calculate that takes value and sets doubled to value * 2 and returns doubled, then print other(3).",
        false)]
    public void Extracts_and_validates_function_local_variables_declaratively(
        string input,
        bool expectedValid)
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var loadError), loadError);

        var extracted = SequenceInputFactExtractor.TryExtract(
            knowledge!,
            input,
            out var facts,
            out var extractionError);

        Assert.True(expectedValid == extracted, extractionError);
        if (!expectedValid)
        {
            Assert.Empty(facts);
            return;
        }

        Assert.True(
            SemanticProgramPlanner.TryGenerateProgram(
                facts,
                LoadPythonLanguagePack(),
                out var generated,
                out _,
                out var generationError),
            generationError);
        Assert.Equal(
            "def transform(value):\n" +
            "    shifted = value + 2\n" +
            "    scaled = shifted * 4\n" +
            "    return scaled - shifted\n" +
            "print(transform(3))",
            generated);
    }


    [Theory]
    [InlineData(
        "Create a Python program with a function named double that takes value and returns value * 2, then print double(4).",
        "def double(value):\n    return value * 2\nprint(double(4))")]
    [InlineData(
        "Build a Python script with a function named scale that takes value and factor and returns (value + 3) * factor, then print scale(2, 4).",
        "def scale(value, factor):\n    return (value + 3) * factor\nprint(scale(2, 4))")]
    [InlineData(
        "Create a Python program with a function named sum_three that takes first and second and third and returns first + second + third, then print sum_three(2, 3, 4).",
        "def sum_three(first, second, third):\n    return first + second + third\nprint(sum_three(2, 3, 4))")]
    [InlineData(
        "Create a Python program with a function named clamp_floor that takes value and returns max(abs(value), 10) + 2, then print clamp_floor(-4).",
        "def clamp_floor(value):\n    return max(abs(value), 10) + 2\nprint(clamp_floor(-4))")]
    public void Extracts_and_generates_arithmetic_functions_declaratively(
        string input,
        string expectedCode)
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var loadError), loadError);

        Assert.True(
            SequenceInputFactExtractor.TryExtract(
                knowledge!,
                input,
                out var facts,
                out var extractionError),
            extractionError);

        Assert.True(
            SemanticProgramPlanner.TryGenerateProgram(
                facts,
                LoadPythonLanguagePack(),
                out var generated,
                out _,
                out var generationError),
            generationError);
        Assert.Equal(expectedCode, generated);
    }

    [Theory]
    [InlineData("Create a Python program with a function named 2double that takes value and returns value * 2, then print 2double(4).")]
    [InlineData("Create a Python program with a function named double that takes value and returns value + * 2, then print double(4).")]
    [InlineData("Create a Python program with a function named double that takes value and returns value * 2, then print triple(4).")]
    [InlineData("Create a Python program with a function named double that takes value and returns value * 2, then print double(4, 5).")]
    [InlineData("Create a Python program with a function named double that takes value and returns value * 2.")]
    public void Rejects_invalid_arithmetic_function_requests_declaratively(string input)
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var loadError), loadError);

        var extracted = SequenceInputFactExtractor.TryExtract(
            knowledge!,
            input,
            out var facts,
            out _);

        Assert.False(
            extracted &&
            SemanticProgramPlanner.TryGenerateProgram(
                facts,
                LoadPythonLanguagePack(),
                out _,
                out _,
                out _));
    }


    [Theory]
    [InlineData("Create a Python program that prints each number from 1 to 5.", "number", "1", "6", "```python\nfor number in range(1, 6):\n    print(number)\n```")]
    [InlineData("Build a Python script that displays each value from 2 through 4.", "value", "2", "5", "```python\nfor value in range(2, 5):\n    print(value)\n```")]
    [InlineData("Generate a Python program that outputs each count from 3 to 6.", "count", "3", "7", "```python\nfor count in range(3, 7):\n    print(count)\n```")]
    [InlineData("Create a Python program that prints each item from 7 to 7.", "item", "7", "8", "```python\nfor item in range(7, 8):\n    print(item)\n```")]
    public void Generates_range_loops_from_declarative_semantic_facts(string input, string item, string start, string exclusiveEnd, string expectedCode)
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var loadError), loadError);
        Assert.True(SequenceInputFactExtractor.TryExtract(knowledge!, input, out var facts, out var extractionError), extractionError);
        Assert.Contains(facts, fact => fact.Id == "program.range-loop" &&
            fact.Values["item"] == item && fact.Values["start"] == start && fact.Values["end"] == exclusiveEnd);
        var request = new KnowledgeDrivenSequenceGenerationRequest(
            ImmutableArray<string>.Empty,
            facts.Select(fact => fact.Id).ToImmutableArray())
        {
            FactValues = facts
        };
        Assert.True(KnowledgeDrivenSequenceGenerator.TryGenerate(
            knowledge!, null, request, out var generated, out var generationError),
            $"{generationError} Active facts: {string.Join(", ", facts.Select(fact => fact.Id))}");
        Assert.Equal(expectedCode, generated!.Text);
    }

    [Theory]
    [InlineData("Create a Python program that prints each number from 5 to 1.")]
    [InlineData("Create a Python program that prints each number from 1 to 2147483647.")]
    [InlineData("Create a Python program that prints each number from 1.5 to 5.")]
    public void Rejects_invalid_range_loop_bounds_declaratively(string input)
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var loadError), loadError);
        Assert.False(SequenceInputFactExtractor.TryExtract(knowledge!, input, out var facts, out var extractionError),
            $"Expected invalid range to be rejected: {input}. Extraction error: {extractionError}. Facts: {string.Join(" | ", facts.Select(fact => $"{fact.Id}: {string.Join(", ", fact.Values.Select(value => $"{value.Key}={value.Value}"))}"))}");
        Assert.Empty(facts);
    }

    [Theory]
    [InlineData("value > 0", true)]
    [InlineData("value + offset <= 100", true)]
    [InlineData("(value * 2) != offset - 1", true)]
    [InlineData("value > missing", false)]
    [InlineData("value > 0 and offset < 10", false)]
    [InlineData("value > * 2", false)]
    [InlineData("value >", false)]
    [InlineData("value > 0 > offset", false)]
    public void Validates_comparison_conditions_against_declared_parameters(
        string condition,
        bool expectedValid)
    {
        var pack = LoadPythonLanguagePack();
        var expressions = ImmutableDictionary<string, SemanticExpression>.Empty;
        if (SemanticExpressionParser.TryParse(
                "boolean", condition, pack.InputSyntax, out var parsedExpression))
        {
            expressions = expressions.Add("expression", parsedExpression!);
        }

        var facts = ImmutableArray.Create(
            new SequenceSemanticFact(
                "program.condition",
                ImmutableDictionary<string, string>.Empty)
            {
                Expressions = expressions
            },
            new SequenceSemanticFact(
                "program.signature",
                ImmutableDictionary<string, string>.Empty)
            {
                Collections = ImmutableDictionary<string, ImmutableArray<string>>.Empty
                    .Add("parameters", ImmutableArray.Create("value", "offset"))
            });
        var validation = new SequenceInputFactValidation(
            "condition-references-declared-parameters",
            "comparisonValueReferences",
            "program.condition",
            "expression",
            "program.signature",
            "parameters");

        Assert.Equal(
            expectedValid,
            SequenceInputFactValidator.TryValidate(
                ImmutableArray.Create(validation),
                facts,
                out _));
    }

    [Fact]
    public void Extracts_and_generates_conditional_function_from_composable_facts()
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var loadError), loadError);

        const string prompt = "Write a Python program with a function named classify that takes score and returns score + 1 when score is at least 10 and otherwise returns score - 1, then print classify(12).";
        Assert.True(
            SequenceInputFactExtractor.TryExtract(knowledge!, prompt, out var facts, out var extractionError),
            extractionError);

        var branches = Assert.Single(facts, fact => fact.Id == "program.requirement.function.branches");
        Assert.Equal("score >= 10", RenderExpression(Assert.Single(branches.ExpressionCollections["condition"])));

        Assert.True(
            SemanticProgramPlanner.TryGenerateProgram(
                facts,
                LoadPythonLanguagePack(),
                out var code,
                out _,
                out var generationError),
            $"Facts: {string.Join(", ", facts.Select(fact => fact.Id))}. {generationError}");
        Assert.Equal(
            "def classify(score):\n    if score >= 10:\n        return score + 1\n    return score - 1\nprint(classify(12))",
            code);
    }


    [Fact]
    public void Extracts_and_generates_collection_filter_from_composable_facts()
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var loadError), loadError);

        const string prompt = "Write a Python function that receives a list of numbers named measurements and returns a new list containing only measurement values less than the average of measurements.";
        Assert.True(
            SequenceInputFactExtractor.TryExtract(knowledge!, prompt, out var facts, out var extractionError),
            extractionError);

        var signature = Assert.Single(facts, fact => fact.Id == "program.collection-filter.signature");
        Assert.Equal("above_average", signature.Values["name"]);
        Assert.Equal("measurements", signature.Values["parameter"]);
        Assert.Equal("measurement", signature.Values["item"]);
        Assert.Equal(
            SemanticExpressionOperator.LessThan,
            Assert.Single(facts, fact => fact.Id == "program.collection-filter.comparison")
                .ComparisonOperators["operator"]);

        var request = new KnowledgeDrivenSequenceGenerationRequest(
            ImmutableArray<string>.Empty,
            facts.Select(fact => fact.Id).ToImmutableArray())
        {
            FactValues = facts
        };

        Assert.True(
            KnowledgeDrivenSequenceGenerator.TryGenerate(knowledge!, null, request, out var result, out var generationError),
            $"Facts: {string.Join(", ", facts.Select(fact => fact.Id))}. {generationError}");
        Assert.Equal(
            "```python\ndef above_average(measurements):\n    return [measurement for measurement in measurements if measurement < sum(measurements) / len(measurements)]\nprint(above_average([1, 2, 3, 4, 5]))\n```",
            string.Join(string.Empty, result!.Tokens));
    }


    [Theory]
    [InlineData("is greater than", "value > 10")]
    [InlineData("is less than", "value < 10")]
    [InlineData("is at most", "value <= 10")]
    [InlineData("is equal to", "value == 10")]
    [InlineData("is not equal to", "value != 10")]
    public void Extracts_comparison_phrases_as_normalized_condition_expressions(
        string phrase,
        string expectedExpression)
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var loadError), loadError);
        var prompt = $"Write a Python program with a function named compare that takes value and returns value + 1 when value {phrase} 10 and otherwise returns value - 1, then print compare(12).";

        Assert.True(
            SequenceInputFactExtractor.TryExtract(knowledge!, prompt, out var facts, out var extractionError),
            extractionError);

        var branches = Assert.Single(facts, fact => fact.Id == "program.requirement.function.branches");
        Assert.Equal(
            expectedExpression,
            RenderExpression(Assert.Single(branches.ExpressionCollections["condition"])));
    }

    [Fact]
    public void Rejects_conditional_function_conditions_with_undeclared_names()
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var loadError), loadError);
        const string prompt = "Write a Python program with a function named compare that takes value and returns value + 1 when missing is greater than 10 and otherwise returns value - 1, then print compare(12).";

        Assert.True(
            SequenceInputFactExtractor.TryExtract(knowledge!, prompt, out var facts, out var extractionError),
            extractionError);

        Assert.False(SemanticProgramPlanner.TryGenerateProgram(
            facts,
            LoadPythonLanguagePack(),
            out _,
            out _,
            out var generationError));
        Assert.Contains("before any requirement defines it", generationError, StringComparison.Ordinal);
    }

    private static GenerationLanguagePack LoadPythonLanguagePack()
    {
        var loader = new GenerationLanguagePackLoader();
        Assert.True(loader.TryLoadEmbedded(out var packs, out var error), error);
        return packs["python"];
    }

    private static string RenderExpression(SemanticExpression expression)
    {
        Assert.True(SemanticExpressionRenderer.TryRender(
            expression, LoadPythonLanguagePack(), out var rendered));
        return rendered;
    }
}
