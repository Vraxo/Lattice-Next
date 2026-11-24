using System.Collections.Immutable;
using Lattice.Core.Generation;
using Xunit;

namespace Lattice.Core.Tests.Generation;

public sealed class SequenceInputSyntaxTests
{
    [Fact]
    public void Loads_identifier_and_literal_syntax_from_the_language_pack()
    {
        var loader = new GenerationLanguagePackLoader();
        Assert.True(loader.TryLoadEmbedded(out var packs, out var error), error);
        var syntax = Assert.IsType<GenerationInputSyntax>(packs["python"].InputSyntax);

        Assert.True(GenerationInputSyntaxParser.IsIdentifier("_value2", syntax));
        Assert.False(GenerationInputSyntaxParser.IsIdentifier("2value", syntax));
        Assert.True(GenerationInputSyntaxParser.IsNumberLiteral("-1.5", syntax));
        Assert.True(GenerationInputSyntaxParser.IsStringLiteral("'text'", syntax));
        Assert.True(GenerationInputSyntaxParser.IsStringLiteral("\"a, \\\"quoted\\\" value\"", syntax));
        Assert.False(GenerationInputSyntaxParser.IsStringLiteral("'unterminated", syntax));
    }

    [Fact]
    public void Identifier_validation_uses_the_selected_pack_and_requires_its_syntax_metadata()
    {
        var fact = new SequenceSemanticFact(
            "callable",
            ImmutableDictionary<string, string>.Empty.Add("name", "$compute2"));
        var validation = new SequenceInputFactValidation(
            "callable-name-is-an-identifier",
            "identifierValue",
            "callable",
            "name");
        var pack = CreatePack(CreateSyntax());

        Assert.True(SequenceInputFactValidator.TryValidate([validation], [fact], pack, out var error), error);
        Assert.False(SequenceInputFactValidator.TryValidate([validation], [fact], out _));

        var invalidFact = fact with
        {
            Values = ImmutableDictionary<string, string>.Empty.Add("name", "2compute")
        };
        Assert.False(SequenceInputFactValidator.TryValidate([validation], [invalidFact], pack, out _));
    }

    [Fact]
    public void Function_call_validation_uses_pack_delimiters_separators_numbers_and_strings()
    {
        var syntax = CreateSyntax();
        const string source = "$compute[−1,5; 'red; blue']";
        Assert.True(SemanticExpressionParser.TryParse(
            "functionCall", source, syntax, out var parsedCall));
        var call = new SequenceSemanticFact(
            "call",
            ImmutableDictionary<string, string>.Empty)
        {
            Expressions = ImmutableDictionary<string, SemanticExpression>.Empty
                .Add("expression", parsedCall!)
        };
        var signature = new SequenceSemanticFact(
            "signature",
            ImmutableDictionary<string, string>.Empty.Add("name", "$compute"))
        {
            Collections = ImmutableDictionary<string, ImmutableArray<string>>.Empty
                .Add("parameters", ["value", "label"])
        };
        var validation = new SequenceInputFactValidation(
            "call-matches-signature",
            "matchingFunctionCall",
            "call",
            "expression",
            "signature",
            "name",
            "signature",
            "parameters");
        var pack = CreatePack(syntax);

        Assert.True(SequenceInputFactValidator.TryValidate([validation], [call, signature], pack, out var error), error);
        Assert.False(SemanticExpressionParser.TryParse(
            "functionCall", "$compute(1,5, 'red; blue')", syntax, out _));

        Assert.True(SemanticExpressionParser.TryParse(
            "functionCall", "$compute[1,5; 'red; blue'; 3]", syntax, out var parsedWrongArity));
        var wrongArity = call with
        {
            Expressions = ImmutableDictionary<string, SemanticExpression>.Empty
                .Add("expression", parsedWrongArity!)
        };
        Assert.False(SequenceInputFactValidator.TryValidate(
            [validation], [wrongArity, signature], pack, out _));

        Assert.False(SemanticExpressionParser.TryParse(
            "functionCall", "$compute[1.5; 'red; blue']", syntax, out _));
    }

    [Fact]
    public void Function_call_parser_keeps_nested_delimiters_and_string_separators_inside_arguments()
    {
        var syntax = CreateSyntax();

        Assert.True(GenerationInputSyntaxParser.TryParseFunctionCall(
            "$compute[$inner[1, 2]; 'red; blue']",
            syntax,
            out var functionName,
            out var arguments));

        Assert.Equal("$compute", functionName);
        Assert.Equal(ImmutableArray.Create("$inner[1, 2]", "'red; blue'"), arguments);
    }

    private static GenerationInputSyntax CreateSyntax() => new(
        "$_abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ",
        "$_abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789",
        new GenerationNumberLiteralFormat("0123456789", ",", "+", "−"),
        [
            new GenerationInputStringLiteralFormat(
                "\"", "\"", "\\", CreateEscapes(), AllowUnknownEscapes: true),
            new GenerationInputStringLiteralFormat(
                "'", "'", "\\", CreateEscapes(), AllowUnknownEscapes: true)
        ],
        new GenerationFunctionCallFormat("[", "]", ";") { RenderingArgumentSeparator = "; " })
    {
        GroupingDelimiters = [new GenerationDelimiterPair("(", ")"), new GenerationDelimiterPair("{", "}")],
        ExpressionGroupingDelimiter = new GenerationDelimiterPair("(", ")"),
        ExpressionOperators = [
            new GenerationInputExpressionOperatorFormat("add", "+", "infix", 50),
            new GenerationInputExpressionOperatorFormat("subtract", "-", "infix", 50),
            new GenerationInputExpressionOperatorFormat("multiply", "*", "infix", 60),
            new GenerationInputExpressionOperatorFormat("divide", "/", "infix", 60),
            new GenerationInputExpressionOperatorFormat("remainder", "%", "infix", 60),
            new GenerationInputExpressionOperatorFormat("equal", "==", "infix", 40),
            new GenerationInputExpressionOperatorFormat("notEqual", "!=", "infix", 40),
            new GenerationInputExpressionOperatorFormat("lessThan", "<", "infix", 40),
            new GenerationInputExpressionOperatorFormat("lessThanOrEqual", "<=", "infix", 40),
            new GenerationInputExpressionOperatorFormat("greaterThan", ">", "infix", 40),
            new GenerationInputExpressionOperatorFormat("greaterThanOrEqual", ">=", "infix", 40),
            new GenerationInputExpressionOperatorFormat("and", "and", "infix", 20),
            new GenerationInputExpressionOperatorFormat("or", "or", "infix", 10),
            new GenerationInputExpressionOperatorFormat("not", "not", "prefix", 30),
            new GenerationInputExpressionOperatorFormat("positive", "+", "prefix", 80),
            new GenerationInputExpressionOperatorFormat("negative", "−", "prefix", 80)
        ]
    };

    private static ImmutableArray<GenerationInputEscapeSequence> CreateEscapes() => [
        new GenerationInputEscapeSequence("\\\\", "\\"),
        new GenerationInputEscapeSequence("\\\"", "\""),
        new GenerationInputEscapeSequence("\\'", "'"),
        new GenerationInputEscapeSequence("\\n", "\n")
    ];

    private static GenerationLanguagePack CreatePack(GenerationInputSyntax inputSyntax) => new(
        1,
        "custom",
        "root",
        [],
        [],
        [],
        new GenerationStringLiteralFormat("\"", "\"", []))
    {
        InputSyntax = inputSyntax
    };
}
