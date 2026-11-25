using Lattice.Core.Generation;
using Xunit;

namespace Lattice.Core.Tests.Generation;

public sealed class SemanticProgramGenerationTests
{
    [Theory]
    [InlineData(
        "Create a Python program that sets total to 5 then print total.",
        "total = 5\nprint(total)")]
    [InlineData(
        "Create a Python program that sets total to 7 then print total.",
        "total = 7\nprint(total)")]
    [InlineData(
        "Create a Python program that sets subtotal to 23 then prints subtotal.",
        "subtotal = 23\nprint(subtotal)")]
    [InlineData(
        "Create a Python program that prints 'adult' if 20 is at least 18.",
        "if 20 >= 18:\n    print(\"adult\")")]
    [InlineData(
        "Create a Python program that prints 'minor' if 16 is less than 18.",
        "if 16 < 18:\n    print(\"minor\")")]
    [InlineData(
        "Create a Python program that sets amount to 12 then if amount is at least 10 then displays 'large'.",
        "amount = 12\nif amount >= 10:\n    print(\"large\")")]
    [InlineData(
        "Create a Python program that sets score to 42 then when score is at least 40 then prints 'pass' otherwise prints 'fail'.",
        "score = 42\nif score >= 40:\n    print(\"pass\")\nelse:\n    print(\"fail\")")]
    [InlineData(
        "Create a Python program that sets score to 12 then when score is at least 40 then prints 'pass' otherwise prints 'fail'.",
        "score = 12\nif score >= 40:\n    print(\"pass\")\nelse:\n    print(\"fail\")")]
    [InlineData(
        "Create a Python program with a function named classify that takes age and returns 'adult' when age is at least 18 and otherwise returns 'minor', then print classify(20).",
        "def classify(age):\n    if age >= 18:\n        return \"adult\"\n    return \"minor\"\nprint(classify(20))")]
    [InlineData(
        "Create a Python program with a function named classify that takes age and returns 'senior' when age is at least 65, returns 'adult' when age is at least 18, returns 'teen' when age is at least 13, and otherwise returns 'child', then print classify(70).",
        "def classify(age):\n    if age >= 65:\n        return \"senior\"\n    elif age >= 18:\n        return \"adult\"\n    elif age >= 13:\n        return \"teen\"\n    return \"child\"\nprint(classify(70))")]
    [InlineData(
        "Write a Python function that converts text to an integer and returns a clear result when the text is not a valid integer.",
        "def convert_to_integer(text):\n    try:\n        return int(text)\n    except ValueError:\n        return \"Invalid integer\"\nprint(convert_to_integer(\"42\"))\nprint(convert_to_integer(\"not-a-number\"))")]
    [InlineData(
        "Create a Python function named parse_count that accepts raw_text and converts raw_text to an integer and returns a clear message if the input is not a valid integer.",
        "def parse_count(raw_text):\n    try:\n        return int(raw_text)\n    except ValueError:\n        return \"Invalid integer\"\nprint(parse_count(\"42\"))\nprint(parse_count(\"not-a-number\"))")]
    [InlineData(
        "Write a Python program that fulfills this request: Define a function that accepts a person's name and returns a greeting. Call it with two different names.",
        "def greet(name):\n    return f\"Hello, {name}!\"\nprint(greet(\"Ada\"))\nprint(greet(\"Grace\"))")]
    public void Generates_requested_scalar_programs_from_their_requirements(
        string input,
        string expectedCode)
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var loadError), loadError);
        Assert.True(SequenceInputFactExtractor.TryExtract(knowledge!, input, out var facts, out var extractError), extractError);

        var generated = SemanticProgramPlanner.TryGenerateProgram(
            facts,
            LoadPythonPack(),
            out var code,
            out var statements,
            out var error);

        Assert.True(generated, error);
        Assert.Equal(expectedCode, code);
        Assert.NotEmpty(statements);
    }

    [Theory]
    [InlineData("is at least", "20", "18", "if 20 >= 18:")]
    [InlineData("is at most", "20", "18", "if 20 <= 18:")]
    [InlineData("is greater than", "20", "18", "if 20 > 18:")]
    [InlineData("is less than", "20", "18", "if 20 < 18:")]
    [InlineData("equals", "20", "18", "if 20 == 18:")]
    [InlineData("is not equal to", "20", "18", "if 20 != 18:")]
    public void Generates_every_supported_comparison_family(
        string operatorPhrase,
        string left,
        string right,
        string expectedHeader)
    {
        var input =
            $"Create a Python program that prints 'match' if {left} {operatorPhrase} {right}.";

        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var loadError), loadError);
        Assert.True(SequenceInputFactExtractor.TryExtract(knowledge!, input, out var facts, out var extractError), extractError);
        Assert.True(
            SemanticProgramPlanner.TryGenerateProgram(facts, LoadPythonPack(), out var code, out _, out var error),
            error);

        Assert.Equal(
            string.Join('\n', expectedHeader, "    print(\"match\")"),
            code);
    }

    [Fact]
    public void Generates_a_program_whose_statements_depend_on_earlier_assignments()
    {
        const string input =
            "Create a Python program that sets total to 4 then sets doubled to total * 2 then prints doubled.";

        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var loadError), loadError);
        Assert.True(SequenceInputFactExtractor.TryExtract(knowledge!, input, out var facts, out var extractError), extractError);
        Assert.True(
            SemanticProgramPlanner.TryGenerateProgram(facts, LoadPythonPack(), out var code, out _, out var error),
            error);

        Assert.Equal(
            string.Join('\n', "total = 4", "doubled = total * 2", "print(doubled)"),
            code);
    }

    [Theory]
    [InlineData("Create a Python program that prints total + 3.")]
    [InlineData("Create a Python program that sets total to 5 and prints total + 3.")]
    [InlineData("Write a Python program that stores two numbers in variables, adds them, and prints the result.")]
    [InlineData("Write a Python program that reports whether an integer is positive, negative, or zero.")]
    public void Leaves_requests_with_deferred_capabilities_to_their_own_path(string input)
    {
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var loadError), loadError);
        if (!SequenceInputFactExtractor.TryExtract(knowledge!, input, out var facts, out _))
        {
            return;
        }

        Assert.False(SemanticProgramPlanner.TryGenerateProgram(
            facts,
            LoadPythonPack(),
            out var code,
            out _,
            out _));
        Assert.Equal(string.Empty, code);
    }

    [Fact]
    public void Declines_an_empty_fact_set()
    {
        Assert.False(SemanticProgramPlanner.TryGenerateProgram(
            [],
            LoadPythonPack(),
            out var code,
            out _,
            out var error));

        Assert.Equal(string.Empty, code);
        Assert.Contains("requires extracted semantic facts", error, StringComparison.Ordinal);
    }

    private static GenerationLanguagePack LoadPythonPack()
    {
        var loader = new GenerationLanguagePackLoader();
        Assert.True(loader.TryLoadEmbedded(out var packs, out var error), error);
        return packs["python"];
    }
}
