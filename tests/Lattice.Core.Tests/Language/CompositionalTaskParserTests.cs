using Lattice.Core.Language;
using Lattice.Core.Semantics;
using Xunit;

namespace Lattice.Core.Tests.Language;

public sealed class CompositionalTaskParserTests
{
    [Theory]
    [InlineData("write a python hello world program")]
    [InlineData("write a hello world program in python")]
    [InlineData("create a simple Python script that prints Hello World")]
    [InlineData("Can you generate a program in Python to display Hello, world?")]
    [InlineData("make me a Python program which outputs Hello World")]
    [InlineData("Please build a script using Python that says hello world.")]
    [InlineData("I want to create a Python program that prints Hello World.")]
    [InlineData("Could you write a simple Python script to display Hello, world?")]
    [InlineData("Can you make a Python program which outputs 'Hello, world!'?")]
    public void Resolves_different_word_orders_to_the_same_semantic_goal(string input)
    {
        var result = new LceParser().ParseText(input);

        Assert.True(result.IsSuccess, result.Error);
        var utterance = Assert.Single(result.Utterances);
        var request = Assert.IsType<TaskRequestFormula>(utterance.Meaning);

        Assert.Equal(TaskRequestAction.Code, request.Action);
        Assert.Equal("python-program", request.Subject);
        Assert.Equal("python", request.Context);
        Assert.Equal("python-source", request.OutputFormat);
        Assert.Equal("write-standard-output", request.Scope);
        Assert.Equal("Hello, world!", request.RequestedValue);
    }

    [Theory]
    [InlineData("Create a Python program that prints 'A second message!'.", "A second message!")]
    [InlineData("Write a script in Python to display 'Keep this: punctuation, please!'.", "Keep this: punctuation, please!")]
    [InlineData("Generate a Python program that outputs 'line one\\nline two'.", "line one\\nline two")]
    public void Extracts_user_supplied_output_as_a_value_not_as_program_text(
        string input,
        string expected)
    {
        var result = new LceParser().ParseText(input);

        Assert.True(result.IsSuccess, result.Error);
        var request = Assert.IsType<TaskRequestFormula>(Assert.Single(result.Utterances).Meaning);

        Assert.Equal(expected, request.RequestedValue);
    }
    [Fact]
    public void Does_not_invent_an_output_goal_for_an_unspecified_program()
    {
        var result = new LceParser().ParseText("Create a Python program.");

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Does_not_treat_a_python_mention_inside_the_requested_output_as_the_language()
    {
        var result = new LceParser().ParseText(
            "Create a script that prints 'Python'.");

        Assert.False(result.IsSuccess);
    }

}
