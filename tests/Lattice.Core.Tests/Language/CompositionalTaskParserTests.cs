using Lattice.Core.Language;
using Lattice.Core.Semantics;
using Xunit;

namespace Lattice.Core.Tests.Language;

public sealed class CompositionalTaskParserTests
{
    [Theory]
    [InlineData("create a simple Python script that prints Hello World")]
    [InlineData("Can you generate a program in Python to display Hello, world?")]
    [InlineData("make me a Python program which outputs Hello World")]
    [InlineData("Please build a script using Python that says hello world.")]
    [InlineData("I want to create a Python program that prints Hello World.")]
    [InlineData("Could you write a simple Python script to display Hello, world?")]
    [InlineData("Can you make a Python program which outputs 'Hello, world!'?")]
    public void Parses_python_output_requests_with_different_word_orders(string input)
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
        Assert.Equal(input, utterance.SourceText);
    }

    [Theory]
    [InlineData("Create a Python program that prints 'A second message!'.")]
    [InlineData("Write a script in Python to display 'Keep this: punctuation, please!'.")]
    [InlineData("Generate a Python program that outputs 'line one\\nline two'.")]
    public void Preserves_output_text_in_the_original_request_without_copying_it_into_task_semantics(
        string input)
    {
        var result = new LceParser().ParseText(input);

        Assert.True(result.IsSuccess, result.Error);
        var utterance = Assert.Single(result.Utterances);
        var request = Assert.IsType<TaskRequestFormula>(utterance.Meaning);

        Assert.Equal(input, utterance.SourceText);
        Assert.DoesNotContain("value=", SemanticFormatter.Format(utterance), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Create a Python program that prints each number in the list [2, 4, 6].")]
    [InlineData("Build a Python script that displays each value from [1, 3.5, -2].")]
    public void Leaves_collection_iteration_to_declarative_sequence_generation(string input)
    {
        var result = new LceParser().ParseText(input);

        Assert.True(result.IsSuccess, result.Error);
        var utterance = Assert.Single(result.Utterances);
        var request = Assert.IsType<TaskRequestFormula>(utterance.Meaning);
        Assert.Equal(input, utterance.SourceText);
    }

    [Fact]
    public void Parses_python_greeting_function_requests_with_implicit_function_name()
    {
        const string input = "Write a Python program that fulfills this request: Define a function that accepts a person's name and returns a greeting. Call it with two different names.";

        var result = new LceParser().ParseText(input);

        Assert.True(result.IsSuccess, result.Error);
        var utterance = Assert.Single(result.Utterances);
        var request = Assert.IsType<TaskRequestFormula>(utterance.Meaning);

        Assert.Equal(TaskRequestAction.Code, request.Action);
        Assert.Equal("python-program", request.Subject);
        Assert.Equal(input, utterance.SourceText);
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
