using System.Collections.Immutable;
using System.Text;
using Lattice.Core.Generation;
using Xunit;

namespace Lattice.Core.Tests.Generation;

public sealed class ScalarProgramInterpretationTests
{
    private static SequenceKnowledgeBase LoadKnowledge()
    {
        Assert.True(
            SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var loadError),
            loadError);
        return knowledge!;
    }

    private static SemanticProgram Interpret(string prompt)
    {
        Assert.True(
            SequenceInputFactExtractor.TryExtractProgram(LoadKnowledge(), prompt, out var program, out var error),
            error);
        return program!;
    }

    private static ImmutableArray<SequenceSemanticFact> ExtractFacts(string prompt)
    {
        Assert.True(
            SequenceInputFactExtractor.TryExtract(LoadKnowledge(), prompt, out var facts, out var error),
            error);
        return facts;
    }

    private static string Describe(SemanticProgram program) =>
        Describe(program.Requirements);

    private static string Describe(ImmutableArray<ProgramRequirement> requirements)
    {
        var builder = new StringBuilder();
        foreach (var requirement in requirements)
        {
            builder.Append(Describe(requirement));
        }

        return builder.ToString();
    }

    private static string Describe(ProgramRequirement requirement) => requirement switch
    {
        AssignmentRequirement assignment =>
            $"assign({assignment.VariableName},{Describe(assignment.Value)});",
        ConditionalRequirement conditional =>
            $"if({Describe(conditional.Condition)}){{{Describe(conditional.ThenRequirements)}}}" +
            $"else{{{Describe(conditional.ElseRequirements)}}};",
        OutputRequirement output => $"output({Describe(output.Value)});",
        _ => throw new InvalidOperationException($"Unsupported requirement '{requirement.GetType().Name}'.")
    };

    private static string Describe(SemanticExpression expression) => expression switch
    {
        SemanticNameExpression name => name.Name,
        SemanticNumberExpression number => number.Value,
        SemanticTextExpression text => $"'{text.Value}'",
        SemanticBooleanExpression boolean => boolean.Value ? "true" : "false",
        SemanticUnaryExpression unary => $"({SemanticExpressionParser.GetOperatorKind(unary.Operator)} {Describe(unary.Operand)})",
        SemanticBinaryExpression binary =>
            $"({Describe(binary.Left)} {SemanticExpressionParser.GetOperatorKind(binary.Operator)} {Describe(binary.Right)})",
        SemanticFunctionCallExpression call =>
            $"{call.Name}({string.Join(", ", call.Arguments.Select(Describe))})",
        _ => throw new InvalidOperationException($"Unsupported expression '{expression.GetType().Name}'.")
    };

    private static SemanticBinaryExpression AssertComparison(ConditionalRequirement conditional) =>
        Assert.IsType<SemanticBinaryExpression>(conditional.Condition);

    [Theory]
    [InlineData("sets age to 21 then when age is at least 18 then prints 'adult'.", "age", "18")]
    [InlineData("sets limit to 7 then when limit is at least 30 then prints 'go'.", "limit", "30")]
    [InlineData("assigns price to 99 then when price is less than 5 then prints 'cheap'.", "price", "5")]
    public void Projects_both_comparison_operands_from_the_request(
        string phrase,
        string expectedLeftName,
        string expectedRightValue)
    {
        var program = Interpret($"Create a Python program that {phrase}");

        var conditional = Assert.IsType<ConditionalRequirement>(program.Requirements[1]);
        var comparison = AssertComparison(conditional);
        Assert.Equal(new SemanticNameExpression(expectedLeftName), comparison.Left);
        Assert.Equal(new SemanticNumberExpression(expectedRightValue), comparison.Right);
    }

    [Theory]
    [InlineData("is at least", SemanticExpressionOperator.GreaterThanOrEqual)]
    [InlineData("is at most", SemanticExpressionOperator.LessThanOrEqual)]
    [InlineData("is greater than", SemanticExpressionOperator.GreaterThan)]
    [InlineData("is less than", SemanticExpressionOperator.LessThan)]
    [InlineData("is equal to", SemanticExpressionOperator.Equal)]
    [InlineData("is not equal to", SemanticExpressionOperator.NotEqual)]
    [InlineData("equals", SemanticExpressionOperator.Equal)]
    public void Projects_requested_comparison_operators_as_semantic_operators(
        string operatorPhrase,
        SemanticExpressionOperator expectedOperator)
    {
        var program = Interpret(
            $"Create a Python program that sets level to 9 then when level {operatorPhrase} 4 then prints 'yes'.");

        var conditional = Assert.IsType<ConditionalRequirement>(program.Requirements[1]);
        var comparison = AssertComparison(conditional);
        Assert.Equal(expectedOperator, comparison.Operator);
        Assert.Equal(new SemanticNameExpression("level"), comparison.Left);
        Assert.Equal(new SemanticNumberExpression("4"), comparison.Right);
    }

    [Theory]
    [InlineData("Create a Python program that prints 'adult' if 20 is at least 18.", "20", "18", "adult")]
    [InlineData("Create a Python program that displays 'minor' if 16 is less than 18.", "16", "18", "minor")]
    public void Projects_a_trailing_comparison_and_its_true_branch(
        string prompt,
        string expectedLeft,
        string expectedRight,
        string expectedOutput)
    {
        var program = Interpret(prompt);

        var conditional = Assert.IsType<ConditionalRequirement>(Assert.Single(program.Requirements));
        var comparison = AssertComparison(conditional);
        Assert.Equal(new SemanticNumberExpression(expectedLeft), comparison.Left);
        Assert.Equal(new SemanticNumberExpression(expectedRight), comparison.Right);
        Assert.Equal(
            new OutputRequirement(new SemanticTextExpression(expectedOutput)),
            Assert.Single(conditional.ThenRequirements));
        Assert.Empty(conditional.ElseRequirements);
    }

    [Theory]
    [InlineData("otherwise")]
    [InlineData("else")]
    public void Projects_condition_true_branch_and_false_branch_independently(string branchSeparator)
    {
        var program = Interpret(
            "Create a Python program that sets score to 42 then when score is at least 40 " +
            $"then prints 'pass' {branchSeparator} prints 'fail'.");

        Assert.Collection(
            program.Requirements,
            requirement => Assert.Equal(
                new AssignmentRequirement("score", new SemanticNumberExpression("42")),
                requirement),
            requirement =>
            {
                var conditional = Assert.IsType<ConditionalRequirement>(requirement);
                Assert.Equal(
                    new OutputRequirement(new SemanticTextExpression("pass")),
                    Assert.Single(conditional.ThenRequirements));
                Assert.Equal(
                    new OutputRequirement(new SemanticTextExpression("fail")),
                    Assert.Single(conditional.ElseRequirements));
            });
    }

    [Theory]
    [InlineData("assigns label to 'draft' then prints 'ready to send'.", "ready to send")]
    [InlineData("sets note to 'plain' then displays 'note: changed text'.", "note: changed text")]
    public void Projects_requested_output_text_instead_of_fixed_sample_strings(
        string phrase,
        string expectedOutput)
    {
        var program = Interpret($"Create a Python program that {phrase}");

        Assert.Collection(
            program.Requirements,
            _ => { },
            requirement => Assert.Equal(
                new OutputRequirement(new SemanticTextExpression(expectedOutput)),
                requirement));
    }

    [Fact]
    public void Projects_requested_output_expressions_from_earlier_assignments()
    {
        var program = Interpret(
            "Create a Python program that sets total to 5 then prints total + 1.");

        Assert.Collection(
            program.Requirements,
            requirement => Assert.Equal(
                new AssignmentRequirement("total", new SemanticNumberExpression("5")),
                requirement),
            requirement => Assert.Equal(
                new OutputRequirement(new SemanticBinaryExpression(
                    SemanticExpressionOperator.Add,
                    new SemanticNameExpression("total"),
                    new SemanticNumberExpression("1"))),
                requirement));
    }

    [Fact]
    public void Interprets_a_requested_output_after_then_without_requiring_a_function_call()
    {
        var program = Interpret("Create a Python program that sets total to 5 then print total.");

        Assert.Equal("assign(total,5);output(total);", Describe(program));
    }

    [Fact]
    public void Projects_multiple_scalar_requirements_from_one_request()
    {
        var program = Interpret(
            "Create a Python program that sets first_count to 2 then sets final_count to 9 " +
            "then prints first_count + final_count.");

        Assert.Collection(
            program.Requirements,
            requirement => Assert.Equal(
                new AssignmentRequirement("first_count", new SemanticNumberExpression("2")),
                requirement),
            requirement => Assert.Equal(
                new AssignmentRequirement("final_count", new SemanticNumberExpression("9")),
                requirement),
            requirement => Assert.Equal(
                new OutputRequirement(new SemanticBinaryExpression(
                    SemanticExpressionOperator.Add,
                    new SemanticNameExpression("first_count"),
                    new SemanticNameExpression("final_count"))),
                requirement));
    }

    [Fact]
    public void Preserves_requirement_dependencies_as_expression_references()
    {
        var program = Interpret(
            "Create a Python program that sets balance to 120 then when balance is at least 100 " +
            "then prints 'rich' otherwise prints 'poor'.");

        var assignment = Assert.IsType<AssignmentRequirement>(program.Requirements[0]);
        var conditional = Assert.IsType<ConditionalRequirement>(program.Requirements[1]);
        var comparison = AssertComparison(conditional);

        Assert.Empty(SemanticExpressionAnalysis.GetReferencedNames(assignment.Value));
        Assert.Equal(["balance"], SemanticExpressionAnalysis.GetReferencedNames(comparison));
        Assert.Empty(SemanticExpressionAnalysis.GetReferencedNames(
            Assert.IsType<OutputRequirement>(Assert.Single(conditional.ThenRequirements)).Value));
        Assert.Equal(
            ["balance"],
            SemanticExpressionAnalysis.GetReferencedNames(new SemanticBinaryExpression(
                SemanticExpressionOperator.Add,
                comparison.Left,
                new SemanticNumberExpression("1"))));
    }

    [Theory]
    [InlineData(
        "Create a Python program that prints 'start' then sets x to 1.",
        "output('start');assign(x,1);")]
    [InlineData(
        "Create a Python program that prints 'go' then sets x to 1 then when x is at least 1 then prints 'done'.",
        "output('go');assign(x,1);if((x greaterThanOrEqual 1)){output('done');}else{};")]
    [InlineData(
        "Create a Python program that sets second_count to 9 then sets first_count to 2.",
        "assign(second_count,9);assign(first_count,2);")]
    public void Preserves_source_request_order_of_scalar_requirements(
        string prompt,
        string expectedDescription)
    {
        Assert.Equal(expectedDescription, Describe(Interpret(prompt)));
    }

    [Theory]
    [InlineData("sets first_count to 2 then prints first_count.", "2")]
    [InlineData("sets first_count to 5 then prints first_count.", "5")]
    [InlineData("sets first_count to 42 then prints first_count.", "42")]
    public void Projects_changed_numeric_literals_exactly(string phrase, string expectedLiteral)
    {
        var program = Interpret($"Create a Python program that {phrase}");

        var assignment = Assert.IsType<AssignmentRequirement>(program.Requirements[0]);
        Assert.Equal(new SemanticNumberExpression(expectedLiteral), assignment.Value);
    }

    [Theory]
    [InlineData("sets draft_label to 'first text' then prints draft_label.", "draft_label", "first text")]
    [InlineData("sets final_label to 'second text!' then prints final_label.", "final_label", "second text!")]
    public void Projects_changed_text_literals_and_identifiers_exactly(
        string phrase,
        string expectedIdentifier,
        string expectedText)
    {
        var program = Interpret($"Create a Python program that {phrase}");

        Assert.Collection(
            program.Requirements,
            requirement => Assert.Equal(
                new AssignmentRequirement(expectedIdentifier, new SemanticTextExpression(expectedText)),
                requirement),
            requirement => Assert.Equal(
                new OutputRequirement(new SemanticNameExpression(expectedIdentifier)),
                requirement));
    }

    [Theory]
    [InlineData("sets", "prints")]
    [InlineData("assigns", "displays")]
    [InlineData("assign", "display")]
    public void Projects_equivalent_requirements_from_paraphrased_requests(
        string assignmentVerb,
        string outputVerb)
    {
        var first = Interpret(
            "Create a Python program that sets running_total to 5 then prints running_total.");
        var second = Interpret(
            "Make a Python program that " + assignmentVerb +
            " running_total to 5 then " + outputVerb + " running_total.");

        Assert.Equal(Describe(first), Describe(second));
        Assert.Equal("assign(running_total,5);output(running_total);", Describe(first));
    }

    [Fact]
    public void Projects_paraphrased_conditions_with_equivalent_meaning()
    {
        var first = Interpret(
            "Create a Python program that sets amount to 12 then when amount is at least 10 then prints 'large'.");
        var second = Interpret(
            "Write a Python program that assigns amount to 12 then if amount is at least 10 then displays 'large'.");

        Assert.Equal(Describe(first), Describe(second));
    }

    [Fact]
    public void Retains_complete_ordered_requirement_set_for_mixed_scalar_requests()
    {
        var program = Interpret(
            "Create a Python program that sets balance to 120 then when balance is at least 100 " +
            "then prints 'rich' otherwise prints 'poor'.");

        Assert.Collection(
            program.Requirements,
            requirement => Assert.Equal(
                new AssignmentRequirement("balance", new SemanticNumberExpression("120")),
                requirement),
            requirement =>
            {
                var conditional = Assert.IsType<ConditionalRequirement>(requirement);
                Assert.Equal(
                    new SemanticBinaryExpression(
                        SemanticExpressionOperator.GreaterThanOrEqual,
                        new SemanticNameExpression("balance"),
                        new SemanticNumberExpression("100")),
                    conditional.Condition);
                Assert.Equal(
                    new OutputRequirement(new SemanticTextExpression("rich")),
                    Assert.Single(conditional.ThenRequirements));
                Assert.Equal(
                    new OutputRequirement(new SemanticTextExpression("poor")),
                    Assert.Single(conditional.ElseRequirements));
            });
    }

    [Fact]
    public void Does_not_project_task_family_facts_for_generic_scalar_requests()
    {
        var facts = ExtractFacts(
            "Create a Python program that sets balance to 120 then when balance is at least 100 " +
            "then prints 'rich' otherwise prints 'poor'.");

        Assert.DoesNotContain(facts, fact =>
            fact.Id.StartsWith("program.conditional-output", StringComparison.Ordinal) ||
            fact.Id.StartsWith("program.classification", StringComparison.Ordinal) ||
            fact.Id.StartsWith("program.greeting", StringComparison.Ordinal));
        Assert.Contains(facts, fact => fact.Id == "program.requirement.condition");
        Assert.Contains(facts, fact => fact.Id == "program.requirement.output");
    }

    [Fact]
    public void Retires_fixed_comparison_sample_captures_and_fragments()
    {
        var knowledge = LoadKnowledge();

        // The knowledge now stores input rules and validations only, so the retired family has nowhere
        // left to live: no rule can capture it and no token rule can render it.
        Assert.DoesNotContain(knowledge.InputRules, rule =>
            rule.FactId.StartsWith("program.conditional-output", StringComparison.Ordinal));
        Assert.DoesNotContain(knowledge.Validations, validation =>
            validation.Id.Contains("conditional-output", StringComparison.Ordinal));

        var facts = ExtractFacts("Create a Python program that prints 'adult' if 20 is at least 18.");
        Assert.DoesNotContain(facts, fact =>
            fact.Id.StartsWith("program.conditional-output", StringComparison.Ordinal));
    }
}
