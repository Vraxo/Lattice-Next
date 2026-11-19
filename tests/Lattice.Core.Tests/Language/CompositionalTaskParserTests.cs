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
        Assert.Equal("program", request.Subject);
        Assert.Equal("python", request.TargetLanguage);
        Assert.Null(request.Context);
        Assert.Equal("source-code", request.OutputFormat);
        Assert.Null(request.Scope);
        var output = Assert.IsType<ProgramOutputStatement>(Assert.Single(request.Program!.Statements));
        Assert.Equal(new ProgramStringLiteral("Hello, world!"), output.Value);
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

        Assert.Equal("python", request.TargetLanguage);
        var output = Assert.IsType<ProgramOutputStatement>(Assert.Single(request.Program!.Statements));
        Assert.Equal(new ProgramStringLiteral(expected), output.Value);
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

    [Fact]
    public void Represents_variable_assignment_and_arithmetic_output_without_target_language_syntax()
    {
        var result = new LceParser().ParseText(
            "Create a Python program that sets total to 5 and prints total + 3.");

        Assert.True(result.IsSuccess, result.Error);
        var request = Assert.IsType<TaskRequestFormula>(Assert.Single(result.Utterances).Meaning);
        Assert.Equal("python", request.TargetLanguage);
        var statements = request.Program!.Statements;
        Assert.Equal(2, statements.Length);

        var assignment = Assert.IsType<ProgramAssignmentStatement>(statements[0]);
        Assert.Equal("total", assignment.Target);
        Assert.Equal(new ProgramNumberLiteral("5"), assignment.Value);

        var output = Assert.IsType<ProgramOutputStatement>(statements[1]);
        var expression = Assert.IsType<ProgramBinaryExpression>(output.Value);
        Assert.Equal(ProgramBinaryOperator.Add, expression.Operator);
        Assert.Equal(new ProgramNameExpression("total"), expression.Left);
        Assert.Equal(new ProgramNumberLiteral("3"), expression.Right);
    }

    [Fact]
    public void Preserves_expression_grouping_in_the_language_neutral_program()
    {
        var result = new LceParser().ParseText(
            "Generate a Python program that sets total to 5 and prints (total + 3) * 2.");

        Assert.True(result.IsSuccess, result.Error);
        var request = Assert.IsType<TaskRequestFormula>(Assert.Single(result.Utterances).Meaning);
        var output = Assert.IsType<ProgramOutputStatement>(request.Program!.Statements[1]);
        var multiply = Assert.IsType<ProgramBinaryExpression>(output.Value);

        Assert.Equal(ProgramBinaryOperator.Multiply, multiply.Operator);
        Assert.IsType<ProgramBinaryExpression>(multiply.Left);
        Assert.Equal(new ProgramNumberLiteral("2"), multiply.Right);
    }

    [Fact]
    public void Formats_the_neutral_program_meaning_without_emitting_target_source()
    {
        var result = new LceParser().ParseText("Write a Python Hello World program.");

        Assert.True(result.IsSuccess, result.Error);
        var formatted = SemanticFormatter.Format(Assert.Single(result.Utterances));

        Assert.Contains("target=python", formatted, StringComparison.Ordinal);
        Assert.Contains("program(output(text(", formatted, StringComparison.Ordinal);
        Assert.DoesNotContain("print(", formatted, StringComparison.Ordinal);
    }

}
