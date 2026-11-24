using System.Collections.Immutable;
using Lattice.Core.Generation;
using Xunit;

namespace Lattice.Core.Tests.Generation;

public sealed class SemanticExpressionTests
{
    [Fact]
    public void Semantic_model_preserves_typed_literals_references_nesting_and_operand_order()
    {
        var literal = new SemanticBooleanExpression(true);
        var reference = new SemanticNameExpression("enabled");
        var expression = new SemanticBinaryExpression(
            SemanticExpressionOperator.And,
            reference,
            new SemanticBinaryExpression(
                SemanticExpressionOperator.Equal,
                new SemanticBooleanExpression(false),
                new SemanticBooleanExpression(true)));

        Assert.Equal(true, literal.Value);
        Assert.Equal("enabled", reference.Name);
        Assert.Same(reference, expression.Left);
        var nested = Assert.IsType<SemanticBinaryExpression>(expression.Right);
        Assert.Equal(SemanticExpressionOperator.Equal, nested.Operator);
        Assert.Equal(new SemanticBooleanExpression(false), nested.Left);
        Assert.Equal(new SemanticBooleanExpression(true), nested.Right);
        Assert.True(SemanticExpressionAnalysis.IsValid(expression));
        Assert.True(SemanticExpressionAnalysis.IsBoolean(expression));
    }

    [Fact]
    public void Parses_and_renders_expression_operators_numbers_calls_and_strings_from_selected_syntax()
    {
        var pack = CreateCustomPack();
        var syntax = pack.InputSyntax!;

        Assert.True(SemanticExpressionParser.TryParse(
            "arithmetic", "x ⊕ y × −٢٫٥", syntax, out var arithmetic));
        Assert.Equal(
            new SemanticBinaryExpression(
                SemanticExpressionOperator.Add,
                new SemanticNameExpression("x"),
                new SemanticBinaryExpression(
                    SemanticExpressionOperator.Multiply,
                    new SemanticNameExpression("y"),
                    new SemanticUnaryExpression(
                        SemanticExpressionOperator.Negative,
                        new SemanticNumberExpression("2.5")))),
            arithmetic);
        Assert.True(SemanticExpressionRenderer.TryRender(arithmetic!, pack, out var renderedArithmetic));
        Assert.Equal("x ⊕ y × −٢٫٥", renderedArithmetic);

        Assert.True(SemanticExpressionParser.TryParse(
            "functionCall", "combine(١٫٥; \"a\\n b\")", syntax, out var call));
        var parsedCall = Assert.IsType<SemanticFunctionCallExpression>(call);
        Assert.Equal("combine", parsedCall.Name);
        Assert.Equal(
            new SemanticExpression[]
            {
                new SemanticNumberExpression("1.5"),
                new SemanticTextExpression("a\n b")
            },
            parsedCall.Arguments);
        Assert.True(SemanticExpressionRenderer.TryRender(call!, pack, out var renderedCall));
        Assert.Equal("combine(١٫٥; \"a\\n b\")", renderedCall);
    }

    [Theory]
    [InlineData("True", true)]
    [InlineData("False", false)]
    public void Parses_and_renders_boolean_literals_from_language_pack(string source, bool value)
    {
        var pack = LoadPythonPack();
        Assert.True(SemanticExpressionParser.TryParse(
            "value", source, pack.InputSyntax, out var expression));
        Assert.Equal(new SemanticBooleanExpression(value), expression);
        Assert.True(SemanticExpressionRenderer.TryRender(expression!, pack, out var rendered));
        Assert.Equal(source, rendered);
    }

    [Fact]
    public void Boolean_literal_spellings_are_selected_by_language_pack_metadata()
    {
        var pack = CreateCustomPack();
        var syntax = pack.InputSyntax! with
        {
            BooleanLiteralFormat = new GenerationBooleanLiteralFormat("yes", "no")
        };

        Assert.True(SemanticExpressionParser.TryParse("value", "yes", syntax, out var trueExpression));
        Assert.Equal(new SemanticBooleanExpression(true), trueExpression);
        Assert.True(SemanticExpressionParser.TryParse("value", "no", syntax, out var falseExpression));
        Assert.Equal(new SemanticBooleanExpression(false), falseExpression);
        Assert.True(SemanticExpressionRenderer.TryRender(trueExpression!, pack with { InputSyntax = syntax }, out var trueText));
        Assert.Equal("yes", trueText);
        Assert.True(SemanticExpressionRenderer.TryRender(falseExpression!, pack with { InputSyntax = syntax }, out var falseText));
        Assert.Equal("no", falseText);
    }

    [Fact]
    public void Parses_boolean_and_comparison_expressions_as_operator_trees()
    {
        var pack = CreateCustomPack();
        var syntax = pack.InputSyntax!;

        Assert.True(SemanticExpressionParser.TryParse(
            "boolean", "x ≤ ٢ && y ≠ ٣", syntax, out var expression));
        Assert.Equal(
            new SemanticBinaryExpression(
                SemanticExpressionOperator.And,
                new SemanticBinaryExpression(
                    SemanticExpressionOperator.LessThanOrEqual,
                    new SemanticNameExpression("x"),
                    new SemanticNumberExpression("2")),
                new SemanticBinaryExpression(
                    SemanticExpressionOperator.NotEqual,
                    new SemanticNameExpression("y"),
                    new SemanticNumberExpression("3"))),
            expression);
        Assert.True(SemanticExpressionAnalysis.IsBoolean(expression!));
        Assert.True(SemanticExpressionRenderer.TryRender(expression!, pack, out var rendered));
        Assert.Equal("x ≤ ٢ && y ≠ ٣", rendered);

        Assert.True(SemanticExpressionParser.TryParseComparisonOperator(
            "≤", syntax, out var comparisonOperator));
        Assert.Equal(SemanticExpressionOperator.LessThanOrEqual, comparisonOperator);
    }

    [Fact]
    public void Rejects_comparisons_between_incompatible_known_literal_types()
    {
        var mixed = new SemanticBinaryExpression(
            SemanticExpressionOperator.Equal,
            new SemanticNumberExpression("1"),
            new SemanticTextExpression("one"));
        var compatibleUnknown = new SemanticBinaryExpression(
            SemanticExpressionOperator.Equal,
            new SemanticNameExpression("value"),
            new SemanticTextExpression("one"));

        Assert.False(SemanticExpressionAnalysis.IsBoolean(mixed));
        Assert.True(SemanticExpressionAnalysis.IsBoolean(compatibleUnknown));
    }

    [Theory]
    [InlineData("x ⊕ × y")]
    [InlineData("x ≤")]
    [InlineData("combine(١٫٥;)")]
    [InlineData("(x ⊕ y")]
    public void Rejects_malformed_expression_syntax(string source)
    {
        var syntax = CreateCustomPack().InputSyntax!;
        Assert.False(SemanticExpressionParser.TryParseSyntax(source, syntax, out _));
    }

    [Fact]
    public void Parses_python_prefix_signs_when_the_tokens_are_also_infix_operators()
    {
        var pack = LoadPythonPack();
        var syntax = pack.InputSyntax!;

        Assert.True(SemanticExpressionParser.TryParse(
            "arithmetic", "value - -2 + +3", syntax, out var expression));
        Assert.Equal(
            new SemanticBinaryExpression(
                SemanticExpressionOperator.Add,
                new SemanticBinaryExpression(
                    SemanticExpressionOperator.Subtract,
                    new SemanticNameExpression("value"),
                    new SemanticUnaryExpression(
                        SemanticExpressionOperator.Negative,
                        new SemanticNumberExpression("2"))),
                new SemanticUnaryExpression(
                    SemanticExpressionOperator.Positive,
                    new SemanticNumberExpression("3"))),
            expression);
        Assert.True(SemanticExpressionRenderer.TryRender(expression!, pack, out var rendered));
        Assert.Equal("value - -2 + +3", rendered);
    }

    [Fact]
    public void Preserves_left_associativity_when_rendering()
    {
        var pack = CreateCustomPack();
        var syntax = pack.InputSyntax!;
        Assert.True(SemanticExpressionParser.TryParse(
            "arithmetic", "(x ⊕ y) ⊖ z ⊖ ١", syntax, out var expression));

        Assert.Equal(
            new SemanticBinaryExpression(
                SemanticExpressionOperator.Subtract,
                new SemanticBinaryExpression(
                    SemanticExpressionOperator.Subtract,
                    new SemanticBinaryExpression(
                        SemanticExpressionOperator.Add,
                        new SemanticNameExpression("x"),
                        new SemanticNameExpression("y")),
                    new SemanticNameExpression("z")),
                new SemanticNumberExpression("1")),
            expression);
        Assert.True(SemanticExpressionRenderer.TryRender(expression!, pack, out var rendered));
        Assert.Equal("x ⊕ y ⊖ z ⊖ ١", rendered);
    }

    private static GenerationLanguagePack LoadPythonPack()
    {
        var loader = new GenerationLanguagePackLoader();
        Assert.True(loader.TryLoadEmbedded(out var packs, out var error), error);
        return packs["python"];
    }

    private static GenerationLanguagePack CreateCustomPack()
    {
        var syntax = new GenerationInputSyntax(
            "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ_",
            "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ_0123456789",
            new GenerationNumberLiteralFormat("٠١٢٣٤٥٦٧٨٩", "٫", "+", "−"),
            [
                new GenerationInputStringLiteralFormat(
                    "\"",
                    "\"",
                    "\\",
                    [
                        new GenerationInputEscapeSequence("\\\\", "\\"),
                        new GenerationInputEscapeSequence("\\\"", "\""),
                        new GenerationInputEscapeSequence("\\n", "\n")
                    ])
            ],
            new GenerationFunctionCallFormat("(", ")", ";")
            {
                RenderingArgumentSeparator = "; "
            })
        {
            ExpressionGroupingDelimiter = new GenerationDelimiterPair("(", ")"),
            GroupingDelimiters = [],
            ExpressionOperators = [
                new GenerationInputExpressionOperatorFormat("add", "⊕", "infix", 50),
                new GenerationInputExpressionOperatorFormat("subtract", "⊖", "infix", 50),
                new GenerationInputExpressionOperatorFormat("multiply", "×", "infix", 60),
                new GenerationInputExpressionOperatorFormat("divide", "÷", "infix", 60),
                new GenerationInputExpressionOperatorFormat("remainder", "%", "infix", 60),
                new GenerationInputExpressionOperatorFormat("equal", "≡", "infix", 40),
                new GenerationInputExpressionOperatorFormat("notEqual", "≠", "infix", 40),
                new GenerationInputExpressionOperatorFormat("lessThan", "<", "infix", 40),
                new GenerationInputExpressionOperatorFormat("lessThanOrEqual", "≤", "infix", 40),
                new GenerationInputExpressionOperatorFormat("greaterThan", ">", "infix", 40),
                new GenerationInputExpressionOperatorFormat("greaterThanOrEqual", "≥", "infix", 40),
                new GenerationInputExpressionOperatorFormat("and", "&&", "infix", 20),
                new GenerationInputExpressionOperatorFormat("or", "||", "infix", 10),
                new GenerationInputExpressionOperatorFormat("not", "!", "prefix", 30),
                new GenerationInputExpressionOperatorFormat("positive", "+", "prefix", 80),
                new GenerationInputExpressionOperatorFormat("negative", "−", "prefix", 80)
            ]
        };

        return new GenerationLanguagePack(
            1,
            "custom",
            "root",
            [],
            [],
            [],
            new GenerationStringLiteralFormat(
                "\"",
                "\"",
                [
                    new GenerationEscapeSequence("\\", "\\\\"),
                    new GenerationEscapeSequence("\"", "\\\""),
                    new GenerationEscapeSequence("\n", "\\n")
                ]))
        {
            InputSyntax = syntax
        };
    }
}
