using Lattice.Core.Generation;
using Xunit;

namespace Lattice.Core.Tests.Generation;

public sealed class SemanticProgramGenerationTests
{
    [Theory]
    [InlineData(
        "Write a Python program that stores two numbers in variables, adds them, and prints the result.",
        "first_number = 10\nsecond_number = 20\nresult = first_number + second_number\nprint(result)")]
    [InlineData(
        "Write a Python program that stores three numbers in variables, adds them, and prints the result.",
        "first_number = 10\nsecond_number = 20\nthird_number = 30\nresult = first_number + second_number + third_number\nprint(result)")]
    [InlineData(
        "Write a Python program that reports whether an integer is positive, negative, or zero.",
        "number = -5\nif number > 0:\n    print(\"Positive\")\nif number < 0:\n    print(\"Negative\")\nif number == 0:\n    print(\"Zero\")")]
    [InlineData(
        "Write a Python program that classifies an integer as positive, negative, or zero.",
        "number = -5\nif number > 0:\n    print(\"Positive\")\nif number < 0:\n    print(\"Negative\")\nif number == 0:\n    print(\"Zero\")")]
    [InlineData(
        "Create a Python program that prints each number in [1, 2, 3, 4, 5, 6] only if it is even.",
        "for number in [1, 2, 3, 4, 5, 6]:\n    if number % 2 == 0:\n        print(number)")]
    [InlineData(
        "Build a Python script that displays each value from [-3, -2, -1, 0, 1, 2, 3] only if it is odd.",
        "for value in [-3, -2, -1, 0, 1, 2, 3]:\n    if value % 2 != 0:\n        print(value)")]
    [InlineData(
        "Write a Python program that prints only the even numbers from a list.",
        "for number in [1, 2, 3, 4, 5]:\n    if number % 2 == 0:\n        print(number)")]
    [InlineData(
        "Write a Python program that, given a list of names, creates a dictionary that maps each name to the number of characters in it.",
        "def map_values(names):\n    mapped_values = {name: len(name) for name in names}\n    return mapped_values\nprint(map_values([\"Ada\", \"Grace\", \"Linus\"]))")]
    [InlineData(
        "Create a Python program that, from a list of numbers, builds a dictionary named squares that maps each number to the square of it.",
        "def map_values(numbers):\n    squares = {number: number * number for number in numbers}\n    return squares\nprint(map_values([1, 2, 3]))")]
    [InlineData(
        "Build a Python program that, given a list of values, makes a dictionary that maps each value to value + 10.",
        "def map_values(values):\n    mapped_values = {value: value + 10 for value in values}\n    return mapped_values\nprint(map_values([1, 2, 3]))")]
    [InlineData(
        "Build a Python script that stores {'Ada': 3, 'Grace': 5, 'Linus': 6} as a dictionary named counts, then displays each name and its count.",
        "counts = {'Ada': 3, 'Grace': 5, 'Linus': 6}\nfor name, count in counts.items():\n    print(name, count)")]
    [InlineData(
        "Create a Python program that stores {'apple': 1.5, 'banana': 0.75} in a dictionary named prices, then prints each product and its price.",
        "prices = {'apple': 1.5, 'banana': 0.75}\nfor product, price in prices.items():\n    print(product, price)")]
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
        "Define a Python class representing a bank account with a balance, a deposit method, and a withdrawal method that refuses withdrawals larger than the balance.",
        "class BankAccount:\n    def __init__(self, balance):\n        self.balance = balance\n    def deposit(self, amount):\n        self.balance += amount\n    def withdraw(self, amount):\n        if amount > self.balance:\n            return False\n        self.balance -= amount\n        return True")]
    [InlineData(
        "Write a Python program that reads a text file and counts how many non-empty lines it contains.",
        "line_count = 0\nwith open(\"input.txt\", \"r\", encoding=\"utf-8\") as input_file:\n    for line in input_file:\n        if line.strip():\n            line_count += 1\nprint(line_count)")]
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

    [Fact]
    public void Generates_a_serialized_round_trip_from_request_derived_facts()
    {
        const string input =
            "Write a Python program that saves a list of dictionaries to a JSON file and then loads it back.";
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var loadError), loadError);
        Assert.True(SequenceInputFactExtractor.TryExtract(knowledge!, input, out var facts, out var extractionError), extractionError);
        Assert.Contains(facts, fact => fact.Id == "program.requirement.serialization-round-trip");

        Assert.True(
            SemanticProgramPlanner.TryGenerateProgram(facts, LoadPythonPack(), out var code, out _, out var error),
            error);

        Assert.Equal(
            string.Join(
                '\n',
                "import json",
                "records = [{\"name\": \"Ada\", \"age\": 36}, {\"name\": \"Grace\", \"age\": 85}]",
                "with open(\"records.json\", \"w\", encoding=\"utf-8\") as output_file:",
                "    json.dump(records, output_file, indent=2)",
                "with open(\"records.json\", \"r\", encoding=\"utf-8\") as input_file:",
                "    loaded_records = json.load(input_file)",
                "print(loaded_records)"),
            code);
    }

    [Fact]
    public void Generates_a_directory_listing_from_request_derived_facts()
    {
        const string input =
            "Write a Python program that accepts a directory path and reports the names and sizes of all " +
            "files directly inside it, handling a missing directory cleanly.";
        Assert.True(SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out var loadError), loadError);
        Assert.True(SequenceInputFactExtractor.TryExtract(knowledge!, input, out var facts, out var extractionError), extractionError);
        Assert.Contains(facts, fact => fact.Id == "program.requirement.directory-listing");

        Assert.True(
            SemanticProgramPlanner.TryGenerateProgram(facts, LoadPythonPack(), out var code, out _, out var error),
            error);

        Assert.Equal(
            string.Join(
                '\n',
                "import os",
                "import sys",
                "directory = sys.argv[1]",
                "try:",
                "    for entry in sorted(os.scandir(directory), key=lambda entry: entry.name):",
                "        if entry.is_file():",
                "            print(f\"{entry.name}: {entry.stat().st_size} bytes\")",
                "except FileNotFoundError:",
                "    print(f\"Directory not found: {directory}\")"),
            code);
        Assert.DoesNotContain("directory-file-listing", code);
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
