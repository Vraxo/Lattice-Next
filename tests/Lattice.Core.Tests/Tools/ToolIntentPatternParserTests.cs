using Lattice.Core.Tools;
using Xunit;

namespace Lattice.Core.Tests.Tools;

public sealed class ToolIntentPatternParserTests
{
    [Fact]
    public void Parses_literal_tokens_typed_slots_and_optional_phrases()
    {
        var parsed = ToolIntentPatternParser.TryParse(
            "[please do] echo {message:text} [to the console]",
            out var parts);

        Assert.True(parsed);
        Assert.Equal(4, parts.Length);
        Assert.Equal(new[] { "please", "do" }, parts[0].LiteralTokens);
        Assert.True(parts[0].IsOptional);
        Assert.Equal(new[] { "echo" }, parts[1].LiteralTokens);
        Assert.Equal("message", parts[2].SlotName);
        Assert.Equal("text", parts[2].SlotType);
        Assert.Equal(new[] { "to", "the", "console" }, parts[3].LiteralTokens);
        Assert.True(parts[3].IsOptional);
    }

    [Theory]
    [InlineData("")]
    [InlineData("plain literal phrase")]
    [InlineData("echo {message:unknown}")]
    [InlineData("echo {message:text} {message:number}")]
    [InlineData("echo [please {message:text}]")]
    [InlineData("echo [ ] {message:text}")]
    [InlineData("echo {message:text")]
    [InlineData("echo message}")]
    [InlineData("[one] [two] [three] [four] [five] [six] [seven] [eight] [nine] echo {message:text}")]
    public void Rejects_invalid_pattern_expressions(string pattern)
    {
        var parsed = ToolIntentPatternParser.TryParse(pattern, out var parts);

        Assert.False(parsed);
        Assert.Empty(parts);
    }
}
