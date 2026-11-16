using Lattice.Core.Language;
using Xunit;

namespace Lattice.Core.Tests.Language;

public sealed class EnglishLexerTests
{
    [Fact]
    public void Tokenizes_words_punctuation_and_quoted_text_while_preserving_source_positions()
    {
        const string input = "Create a Python script that prints 'Hello, world!'.";

        var tokens = EnglishLexer.Tokenize(input);

        Assert.Contains(tokens, token => token.Value == "Create" && !token.IsQuoted);
        Assert.Contains(tokens, token => token.Value == "Hello, world!" && token.IsQuoted);
        Assert.Contains(tokens, token => token.Value == "." && token.IsPunctuation);

        var quoted = Assert.Single(tokens.Where(token => token.IsQuoted));
        Assert.Equal(input.IndexOf("'"), quoted.Start);
        Assert.Equal("'Hello, world!'".Length, quoted.Length);
    }

    [Fact]
    public void Keeps_contractions_as_words_instead_of_treating_apostrophes_as_quotes()
    {
        var tokens = EnglishLexer.Tokenize("I'd like to create a Python script.");

        Assert.Contains(tokens, token => token.Value == "I'd" && !token.IsQuoted);
    }
}
