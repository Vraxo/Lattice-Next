using Lattice.Client;
using Lattice.Runtime;
using Xunit;

namespace Lattice.Runtime.Tests;

public sealed class KnowledgeDrivenGenerationRuntimeTests
{
    [Fact]
    public async Task Generates_from_multiple_semantic_facts_before_legacy_request_parsing()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(
            "Create a Python program that prints \"Hello, world!\".");

        var response = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.TaskCompleted, response.Kind);
        Assert.Equal("```python\nprint(\"Hello, world!\")\n```", response.Message);
        Assert.Contains("language.python", response.Meaning, StringComparison.Ordinal);
        Assert.Contains("intent.program-generation", response.Meaning, StringComparison.Ordinal);
        Assert.Contains("value.output", response.Meaning, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Generates_hello_world_from_its_semantic_output_requirement()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(
            "Write a Hello World program in Python.");

        var response = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.TaskCompleted, response.Kind);
        Assert.Equal("\u0060\u0060\u0060python\nprint(\"Hello, world!\")\n\u0060\u0060\u0060", response.Message);
        Assert.Contains("program.output.start", response.Meaning, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Generates_unquoted_custom_output_from_semantic_value_facts()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(
            "Write a Python program that prints Greetings, traveler!");

        var response = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.TaskCompleted, response.Kind);
        Assert.Equal("```python\nprint(\"Greetings, traveler!\")\n```", response.Message);
        Assert.Contains("value.output", response.Meaning, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Generates_hello_world_from_a_script_print_request()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(
            "Create a simple Python script that prints Hello World");

        var response = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.TaskCompleted, response.Kind);
        Assert.Equal("```python\nprint(\"Hello, world!\")\n```", response.Message);
        Assert.Contains("language.python", response.Meaning, StringComparison.Ordinal);
        Assert.Contains("intent.program-generation", response.Meaning, StringComparison.Ordinal);
        Assert.Contains("value.output", response.Meaning, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Generates_hello_world_when_language_precedes_output_phrase()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(
            "write a python hello world program");

        var response = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.TaskCompleted, response.Kind);
        Assert.Equal("```python\nprint(\"Hello, world!\")\n```", response.Message);
        Assert.Contains("language.python", response.Meaning, StringComparison.Ordinal);
        Assert.Contains("intent.program-generation", response.Meaning, StringComparison.Ordinal);
        Assert.Contains("program.output.start", response.Meaning, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Generates_quoted_custom_output_from_semantic_value_facts()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(
            "Create a Python program that prints 'A second message!'.");

        var response = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.TaskCompleted, response.Kind);
        Assert.Equal("```python\nprint(\"A second message!\")\n```", response.Message);
        Assert.Contains("value.output", response.Meaning, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Generates_hello_world_from_a_says_request_using_semantic_facts()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(
            "Please build a script using Python that says hello world.");

        var response = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.TaskCompleted, response.Kind);
        Assert.Equal("```python\nprint(\"Hello, world!\")\n```", response.Message);
        Assert.Contains("language.python", response.Meaning, StringComparison.Ordinal);
        Assert.Contains("intent.program-generation", response.Meaning, StringComparison.Ordinal);
        Assert.Contains("value.output", response.Meaning, StringComparison.Ordinal);
        Assert.Contains("program.output.start", response.Meaning, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Generates_custom_output_from_a_show_request_using_semantic_facts()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(
            "Create a Python script that shows 'Status: ready'.");

        var response = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.TaskCompleted, response.Kind);
        Assert.Equal("```python\nprint(\"Status: ready\")\n```", response.Message);
        Assert.Contains("language.python", response.Meaning, StringComparison.Ordinal);
        Assert.Contains("intent.program-generation", response.Meaning, StringComparison.Ordinal);
        Assert.Contains("value.output", response.Meaning, StringComparison.Ordinal);
        Assert.Contains("program.output.start", response.Meaning, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Generates_custom_output_from_a_define_request_using_semantic_facts()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(
            "Define a Python program that prints 'Ready'.");

        var response = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.TaskCompleted, response.Kind);
        Assert.Equal("```python" + Environment.NewLine + "print(\"Ready\")" + Environment.NewLine + "```", response.Message);
        Assert.Contains("language.python", response.Meaning, StringComparison.Ordinal);
        Assert.Contains("intent.program-generation", response.Meaning, StringComparison.Ordinal);
        Assert.Contains("value.output", response.Meaning, StringComparison.Ordinal);
        Assert.Contains("program.output.start", response.Meaning, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("write a hello world program in python")]
    [InlineData("Can you generate a program in Python to display Hello, world?")]
    [InlineData("make me a Python program which outputs Hello World")]
    [InlineData("Please build a script using Python that says hello world.")]
    [InlineData("I want to create a Python program that prints Hello World.")]
    [InlineData("Could you write a simple Python script to display Hello, world?")]
    [InlineData("Can you make a Python program which outputs 'Hello, world!'?")]
    public async Task Generates_hello_world_across_alternate_request_phrasings(string input)
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(input);

        var response = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.TaskCompleted, response.Kind);
        Assert.Equal("```python\nprint(\"Hello, world!\")\n```", response.Message);
    }

    [Fact]
    public async Task Composes_assignment_and_output_in_one_runtime_request()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(
            "Create a Python program that sets total to 5 and prints 'Done'.");

        var response = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.TaskCompleted, response.Kind);
        Assert.Equal("\u0060\u0060\u0060python\ntotal = 5\nprint(\"Done\")\n\u0060\u0060\u0060", response.Message);
        Assert.Contains("program.assignment.target", response.Meaning, StringComparison.Ordinal);
        Assert.Contains("program.output", response.Meaning, StringComparison.Ordinal);
    }
    [Fact]
    public async Task Generates_quoted_output_with_punctuation_from_semantic_value_facts()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(
            "Write a script in Python to display 'Keep this: punctuation, please!'.");

        var response = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.TaskCompleted, response.Kind);
        Assert.Equal("\u0060\u0060\u0060python\nprint(\"Keep this: punctuation, please!\")\n\u0060\u0060\u0060", response.Message);
        Assert.Contains("value.output", response.Meaning, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Generates_escaped_multiline_output_from_semantic_value_facts()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(
            "Generate a Python program that outputs 'line one\\nline two'.");

        var response = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.TaskCompleted, response.Kind);
        Assert.Equal("```python\nprint(\"line one\\\\nline two\")\n```", response.Message);
        Assert.Contains("value.output", response.Meaning, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Generates_custom_output_from_a_displays_request_using_semantic_facts()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(
            "Create a Python program that displays 'Ready'.");

        var response = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.TaskCompleted, response.Kind);
        Assert.Equal("```python" + Environment.NewLine + "print(\"Ready\")" + Environment.NewLine + "```", response.Message);
        Assert.Contains("language.python", response.Meaning, StringComparison.Ordinal);
        Assert.Contains("intent.program-generation", response.Meaning, StringComparison.Ordinal);
        Assert.Contains("value.output", response.Meaning, StringComparison.Ordinal);
        Assert.Contains("program.output.start", response.Meaning, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Generates_custom_output_from_a_say_request_using_semantic_facts()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(
            "Write a Python program to say 'Ready'.");

        var response = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.TaskCompleted, response.Kind);
        Assert.Equal("```python" + Environment.NewLine + "print(\"Ready\")" + Environment.NewLine + "```", response.Message);
        Assert.Contains("language.python", response.Meaning, StringComparison.Ordinal);
        Assert.Contains("intent.program-generation", response.Meaning, StringComparison.Ordinal);
        Assert.Contains("value.output", response.Meaning, StringComparison.Ordinal);
        Assert.Contains("program.output.start", response.Meaning, StringComparison.Ordinal);
    }


    [Fact]
    public async Task Generates_custom_output_from_a_display_request_using_semantic_facts()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(
            "Create a Python program to display 'Ready'.");

        var response = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.TaskCompleted, response.Kind);
        Assert.Equal("\u0060\u0060\u0060python" + Environment.NewLine + "print(\"Ready\")" + Environment.NewLine + "\u0060\u0060\u0060", response.Message);
        Assert.Contains("language.python", response.Meaning, StringComparison.Ordinal);
        Assert.Contains("intent.program-generation", response.Meaning, StringComparison.Ordinal);
        Assert.Contains("value.output", response.Meaning, StringComparison.Ordinal);
        Assert.Contains("program.output.start", response.Meaning, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Generates_custom_output_from_a_construct_request_using_semantic_facts()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(
            "Construct a Python program that prints 'Ready'.");

        var response = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.TaskCompleted, response.Kind);
        Assert.Equal("\u0060\u0060\u0060python" + Environment.NewLine + "print(\"Ready\")" + Environment.NewLine + "\u0060\u0060\u0060", response.Message);
        Assert.Contains("language.python", response.Meaning, StringComparison.Ordinal);
        Assert.Contains("intent.program-generation", response.Meaning, StringComparison.Ordinal);
        Assert.Contains("value.output", response.Meaning, StringComparison.Ordinal);
        Assert.Contains("program.output.start", response.Meaning, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Generates_custom_output_from_a_develop_request_using_semantic_facts()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(
            "Develop a Python program that prints 'Ready'.");

        var response = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.TaskCompleted, response.Kind);
        Assert.Equal("```python" + Environment.NewLine + "print(\"Ready\")" + Environment.NewLine + "```", response.Message);
        Assert.Contains("language.python", response.Meaning, StringComparison.Ordinal);
        Assert.Contains("intent.program-generation", response.Meaning, StringComparison.Ordinal);
        Assert.Contains("value.output", response.Meaning, StringComparison.Ordinal);
        Assert.Contains("program.output.start", response.Meaning, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Generates_custom_output_from_an_implement_request_using_semantic_facts()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(
            "Implement a Python program that prints 'Ready'.");

        var response = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.TaskCompleted, response.Kind);
        Assert.Equal("```python" + Environment.NewLine + "print(\"Ready\")" + Environment.NewLine + "```", response.Message);
        Assert.Contains("language.python", response.Meaning, StringComparison.Ordinal);
        Assert.Contains("intent.program-generation", response.Meaning, StringComparison.Ordinal);
        Assert.Contains("value.output", response.Meaning, StringComparison.Ordinal);
        Assert.Contains("program.output.start", response.Meaning, StringComparison.Ordinal);
    }


    [Fact]
    public async Task Generates_custom_output_from_a_produce_request_using_semantic_facts()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(
            "Produce a Python program that prints 'Ready'.");

        var response = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.TaskCompleted, response.Kind);
        Assert.Equal("```python" + Environment.NewLine + "print(\"Ready\")" + Environment.NewLine + "```", response.Message);
        Assert.Contains("language.python", response.Meaning, StringComparison.Ordinal);
        Assert.Contains("intent.program-generation", response.Meaning, StringComparison.Ordinal);
        Assert.Contains("value.output", response.Meaning, StringComparison.Ordinal);
        Assert.Contains("program.output.start", response.Meaning, StringComparison.Ordinal);
    }


    [Fact]
    public async Task Generates_custom_output_from_an_emit_request_using_semantic_facts()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(
            "Create a Python program that emits 'Ready'.");

        var response = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.TaskCompleted, response.Kind);
        Assert.Equal("```python" + Environment.NewLine + "print(\"Ready\")" + Environment.NewLine + "```", response.Message);
        Assert.Contains("language.python", response.Meaning, StringComparison.Ordinal);
        Assert.Contains("intent.program-generation", response.Meaning, StringComparison.Ordinal);
        Assert.Contains("value.output", response.Meaning, StringComparison.Ordinal);
        Assert.Contains("program.output.start", response.Meaning, StringComparison.Ordinal);
    }
    [Fact]
    public async Task Generates_custom_output_from_an_echo_request_using_semantic_facts()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(
            "Create a Python program that echoes 'Ready'.");

        var response = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.TaskCompleted, response.Kind);
        Assert.Equal("```python" + Environment.NewLine + "print(\"Ready\")" + Environment.NewLine + "```", response.Message);
        Assert.Contains("language.python", response.Meaning, StringComparison.Ordinal);
        Assert.Contains("intent.program-generation", response.Meaning, StringComparison.Ordinal);
        Assert.Contains("value.output", response.Meaning, StringComparison.Ordinal);
        Assert.Contains("program.output.start", response.Meaning, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Generates_custom_output_from_an_echoes_request_using_semantic_facts()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(
            "Create a Python program that echoes 'Ready'.");

        var response = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.TaskCompleted, response.Kind);
        Assert.Equal("```python" + Environment.NewLine + "print(\"Ready\")" + Environment.NewLine + "```", response.Message);
        Assert.Contains("language.python", response.Meaning, StringComparison.Ordinal);
        Assert.Contains("intent.program-generation", response.Meaning, StringComparison.Ordinal);
        Assert.Contains("value.output", response.Meaning, StringComparison.Ordinal);
        Assert.Contains("program.output.start", response.Meaning, StringComparison.Ordinal);
    }
}
