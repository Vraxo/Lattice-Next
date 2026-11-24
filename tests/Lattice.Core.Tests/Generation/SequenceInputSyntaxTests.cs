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
        var call = new SequenceSemanticFact(
            "call",
            ImmutableDictionary<string, string>.Empty.Add("expression", "$compute[1,5; 'red; blue']"));
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
        var pack = CreatePack(CreateSyntax());

        Assert.True(SequenceInputFactValidator.TryValidate([validation], [call, signature], pack, out var error), error);

        var wrongSeparator = call with
        {
            Values = ImmutableDictionary<string, string>.Empty
                .Add("expression", "$compute(1,5, 'red; blue')")
        };
        Assert.False(SequenceInputFactValidator.TryValidate([validation], [wrongSeparator, signature], pack, out _));

        var wrongArity = call with
        {
            Values = ImmutableDictionary<string, string>.Empty
                .Add("expression", "$compute[1,5; 'red; blue'; 3]")
        };
        Assert.False(SequenceInputFactValidator.TryValidate([validation], [wrongArity, signature], pack, out _));

        var wrongNumberSyntax = call with
        {
            Values = ImmutableDictionary<string, string>.Empty
                .Add("expression", "$compute[1.5; 'red; blue']")
        };
        Assert.False(SequenceInputFactValidator.TryValidate([validation], [wrongNumberSyntax, signature], pack, out _));
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
                "\"", "\"", "\\", ["\\\\", "\\\"", "\\'", "\\n"], AllowUnknownEscapes: true),
            new GenerationInputStringLiteralFormat(
                "'", "'", "\\", ["\\\\", "\\\"", "\\'", "\\n"], AllowUnknownEscapes: true)
        ],
        new GenerationFunctionCallFormat("[", "]", ";"))
    {
        GroupingDelimiters = [new GenerationDelimiterPair("(", ")"), new GenerationDelimiterPair("{", "}")]
    };

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
