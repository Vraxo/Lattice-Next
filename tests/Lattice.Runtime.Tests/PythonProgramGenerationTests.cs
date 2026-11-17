using System.Diagnostics;
using Lattice.Client;
using Lattice.Runtime;
using Xunit;

namespace Lattice.Runtime.Tests;

public sealed class PythonProgramGenerationTests
{
    [Theory]
    [InlineData(
        "Create a Python program with a function named classify that takes age and returns 'senior' when age is at least 65, returns 'adult' when age is at least 18, returns 'teen' when age is at least 13, and otherwise returns 'child', then print classify(70).",
        "def classify(age):\n    if age >= 65:\n        return \"senior\"\n    elif age >= 18:\n        return \"adult\"\n    elif age >= 13:\n        return \"teen\"\n    return \"child\"\nprint(classify(70))",
        "senior")]
    [InlineData(
        "Build a Python script with a function named classify that takes age and returns 'senior' when age is at least 65, returns 'adult' when age is at least 18, returns 'teen' when age is at least 13, and otherwise returns 'child', then print classify(30).",
        "def classify(age):\n    if age >= 65:\n        return \"senior\"\n    elif age >= 18:\n        return \"adult\"\n    elif age >= 13:\n        return \"teen\"\n    return \"child\"\nprint(classify(30))",
        "adult")]
    [InlineData(
        "Generate a Python program with a function named classify that takes age and returns 'senior' when age is at least 65, returns 'adult' when age is at least 18, returns 'teen' when age is at least 13, and otherwise returns 'child', then print classify(15).",
        "def classify(age):\n    if age >= 65:\n        return \"senior\"\n    elif age >= 18:\n        return \"adult\"\n    elif age >= 13:\n        return \"teen\"\n    return \"child\"\nprint(classify(15))",
        "teen")]
    [InlineData(
        "Create a Python program with a function named classify that takes age and returns 'senior' when age is at least 65, returns 'adult' when age is at least 18, returns 'teen' when age is at least 13, and otherwise returns 'child', then print classify(8).",
        "def classify(age):\n    if age >= 65:\n        return \"senior\"\n    elif age >= 18:\n        return \"adult\"\n    elif age >= 13:\n        return \"teen\"\n    return \"child\"\nprint(classify(8))",
        "child")]
    [InlineData(
        "Create a Python program with a function named classify that takes score and returns 'high' when score is at least 90, returns 'pass' when score is at least 60, and otherwise returns 'fail', then print classify(95).",
        "def classify(score):\n    if score >= 90:\n        return \"high\"\n    elif score >= 60:\n        return \"pass\"\n    return \"fail\"\nprint(classify(95))",
        "high")]
    [InlineData(
        "Build a Python script with a function named classify that takes score and returns 'high' when score is at least 90, returns 'pass' when score is at least 60, and otherwise returns 'fail', then print classify(75).",
        "def classify(score):\n    if score >= 90:\n        return \"high\"\n    elif score >= 60:\n        return \"pass\"\n    return \"fail\"\nprint(classify(75))",
        "pass")]
    [InlineData(
        "Generate a Python program with a function named classify that takes score and returns 'high' when score is at least 90, returns 'pass' when score is at least 60, and otherwise returns 'fail', then print classify(40).",
        "def classify(score):\n    if score >= 90:\n        return \"high\"\n    elif score >= 60:\n        return \"pass\"\n    return \"fail\"\nprint(classify(40))",
        "fail")]
    [InlineData(
        "Create a Python program with a function named classify that takes score and returns 'accepted' when not (score is at least 10 or score is less than 5) and otherwise returns 'rejected', then print classify(7).",
        "def classify(score):\n    if not (score >= 10 or score < 5):\n        return \"accepted\"\n    return \"rejected\"\nprint(classify(7))",
        "accepted")]
    [InlineData(
        "Build a Python script with a function named classify that takes score and returns 'accepted' when not (score is at least 10 or score is less than 5) and otherwise returns 'rejected', then print classify(3).",
        "def classify(score):\n    if not (score >= 10 or score < 5):\n        return \"accepted\"\n    return \"rejected\"\nprint(classify(3))",
        "rejected")]
    [InlineData(
        "Create a Python program with a function named access that takes role and returns 'allowed' when role equals 'admin' and otherwise returns 'denied', then print access('admin').",
        "def access(role):\n    if role == \"admin\":\n        return \"allowed\"\n    return \"denied\"\nprint(access('admin'))",
        "allowed")]
    [InlineData(
        "Build a Python script with a function named access that takes role and returns 'allowed' when role is not equal to 'admin' and otherwise returns 'denied', then print access('guest').",
        "def access(role):\n    if role != \"admin\":\n        return \"allowed\"\n    return \"denied\"\nprint(access('guest'))",
        "allowed")]
    [InlineData(
        "Create a Python program with a function named access that takes role and returns 'allowed' when role equals 'admin' and otherwise returns 'denied', then print access('guest').",
        "def access(role):\n    if role == \"admin\":\n        return \"allowed\"\n    return \"denied\"\nprint(access('guest'))",
        "denied")]
    [InlineData(
        "Create a Python program with a function named classify that takes score and returns 'accepted' when score is at least 10 or (score is less than 5 and score is not equal to 0) and otherwise returns 'rejected', then print classify(3).",
        "def classify(score):\n    if score >= 10 or (score < 5 and score != 0):\n        return \"accepted\"\n    return \"rejected\"\nprint(classify(3))",
        "accepted")]
    [InlineData(
        "Build a Python script with a function named classify that takes score and returns 'accepted' when score is at least 10 or (score is less than 5 and score is not equal to 0) and otherwise returns 'rejected', then print classify(0).",
        "def classify(score):\n    if score >= 10 or (score < 5 and score != 0):\n        return \"accepted\"\n    return \"rejected\"\nprint(classify(0))",
        "rejected")]
    [InlineData(
        "Create a Python program with a function named nonzero that takes value and returns 'nonzero' when value is not equal to 0 and otherwise returns 'zero', then print nonzero(4).",
        "def nonzero(value):\n    if value != 0:\n        return \"nonzero\"\n    return \"zero\"\nprint(nonzero(4))",
        "nonzero")]
    [InlineData(
        "Build a Python script with a function named nonzero that takes value and returns 'nonzero' when value is not equal to 0 and otherwise returns 'zero', then print nonzero(0).",
        "def nonzero(value):\n    if value != 0:\n        return \"nonzero\"\n    return \"zero\"\nprint(nonzero(0))",
        "zero")]
    [InlineData(
        "Create a Python program with a function named discount that takes price and limit and returns price * 0.9 when price is at least limit and price is less than limit + 50 and otherwise returns price, then print discount(120, 100).",
        "def discount(price, limit):\n    if price >= limit and price < limit + 50:\n        return price * 0.9\n    return price\nprint(discount(120, 100))",
        "108.0")]
    [InlineData(
        "Build a Python script with a function named classify that takes score and returns 'accepted' when score is at least 10 or score equals 0 and otherwise returns 'rejected', then print classify(0).",
        "def classify(score):\n    if score >= 10 or score == 0:\n        return \"accepted\"\n    return \"rejected\"\nprint(classify(0))",
        "accepted")]
    [InlineData(
        "Create a Python program with a function named discount that takes price and limit and returns price * 0.9 when price is at least limit + 5 and otherwise returns price, then print discount(120, 100).",
        "def discount(price, limit):\n    if price >= limit + 5:\n        return price * 0.9\n    return price\nprint(discount(120, 100))",
        "108.0")]
    [InlineData(
        "Build a Python script with a function named discount that takes price and limit and returns price * 0.9 when price is at least limit + 5 and otherwise returns price, then print discount(103, 100).",
        "def discount(price, limit):\n    if price >= limit + 5:\n        return price * 0.9\n    return price\nprint(discount(103, 100))",
        "103")]
    [InlineData(
        "Create a Python program with a function named discount that takes price and limit and returns price * 0.9 when price is at least limit and otherwise returns price, then print discount(120, 100).",
        "def discount(price, limit):\n    if price >= limit:\n        return price * 0.9\n    return price\nprint(discount(120, 100))",
        "108.0")]
    [InlineData(
        "Build a Python script with a function named discount that takes price and limit and returns price * 0.9 when price is at least limit and otherwise returns price, then print discount(80, 100).",
        "def discount(price, limit):\n    if price >= limit:\n        return price * 0.9\n    return price\nprint(discount(80, 100))",
        "80")]
    [InlineData(
        "Create a Python program with a function named reward that takes score and returns score * 2 when score is at least 10 and otherwise returns score + 1, then print reward(12).",
        "def reward(score):\n    if score >= 10:\n        return score * 2\n    return score + 1\nprint(reward(12))",
        "24")]
    [InlineData(
        "Build a Python script with a function named reward that takes score and returns score * 2 when score is at least 10 and otherwise returns score + 1, then print reward(5).",
        "def reward(score):\n    if score >= 10:\n        return score * 2\n    return score + 1\nprint(reward(5))",
        "6")]
    [InlineData(
        "Create a Python program with a function named classify that takes age and returns 'adult' when age is at least 18 and otherwise returns 'minor', then print classify(20).",
        "def classify(age):\n    if age >= 18:\n        return \"adult\"\n    return \"minor\"\nprint(classify(20))",
        "adult")]
    [InlineData(
        "Build a Python script with a function named classify that takes age and returns 'adult' when age is at least 18 and otherwise returns 'minor', then print classify(16).",
        "def classify(age):\n    if age >= 18:\n        return \"adult\"\n    return \"minor\"\nprint(classify(16))",
        "minor")]
    [InlineData(
        "Create a Python program that sets total to 5 and prints total + 3.",
        "total = 5\nprint(total + 3)",
        "8")]
    [InlineData(
        "Build a Python script that sets count to 12 and prints count * 2.",
        "count = 12\nprint(count * 2)",
        "24")]
    [InlineData(
        "Generate a Python program that sets result to 9 and prints result - 4.",
        "result = 9\nprint(result - 4)",
        "5")]
    [InlineData(
        "Create a Python program that prints 'adult' if 20 is at least 18.",
        "if 20 >= 18:\n    print(\"adult\")",
        "adult")]
    [InlineData(
        "Build a Python script that displays 'minor' if 16 is less than 18.",
        "if 16 < 18:\n    print(\"minor\")",
        "minor")]
    [InlineData(
        "Generate a Python program that outputs 'match' if 7 equals 7.",
        "if 7 == 7:\n    print(\"match\")",
        "match")]
    [InlineData(
        "Create a Python program that starts total at 0 and adds each number in [1, 2, 3] to total, then prints total.",
        "total = 0\nfor number in [1, 2, 3]:\n    total = total + number\nprint(total)",
        "6")]
    [InlineData(
        "Build a Python script that initializes result to 0 and adds each value in [-2, 4.5, 1.5] to result, then displays result.",
        "result = 0\nfor value in [-2, 4.5, 1.5]:\n    result = result + value\nprint(result)",
        "4.0")]
    [InlineData(
        "Create a Python program that prints each number in [1, 2, 3, 4, 5, 6] only if it is even.",
        "for number in [1, 2, 3, 4, 5, 6]:\n    if number % 2 == 0:\n        print(number)",
        "2\n4\n6")]
    [InlineData(
        "Build a Python script that displays each value from [-3, -2, -1, 0, 1, 2, 3] only if it is odd.",
        "for value in [-3, -2, -1, 0, 1, 2, 3]:\n    if value % 2 != 0:\n        print(value)",
        "-3\n-1\n1\n3")]
    [InlineData(
        "Create a Python program that prints each number in the list [2, 4, 6].",
        "for number in [2, 4, 6]:\n    print(number)",
        "2\n4\n6")]
    [InlineData(
        "Build a Python script that displays each value from [1, 3.5, -2].",
        "for value in [1, 3.5, -2]:\n    print(value)",
        "1\n3.5\n-2")]
    [InlineData(
        "Create a Python program that prints each name in the list ['Ada', 'Grace'].",
        "for name in ['Ada', 'Grace']:\n    print(name)",
        "Ada\nGrace")]
    [InlineData(
        "Build a Python script that displays each label from [\"red, blue\", 'green'].",
        "for label in [\"red, blue\", 'green']:\n    print(label)",
        "red, blue\ngreen")]
    [InlineData(
        "Create a Python program that prints each number from 1 to 5.",
        "for number in range(1, 6):\n    print(number)",
        "1\n2\n3\n4\n5")]
    [InlineData(
        "Build a Python script that displays each value from 2 through 4.",
        "for value in range(2, 5):\n    print(value)",
        "2\n3\n4")]
    [InlineData(
        "Generate a Python program that outputs each count from 3 to 6.",
        "for count in range(3, 7):\n    print(count)",
        "3\n4\n5\n6")]
    [InlineData(
        "Create a Python program with a function named double that takes value and returns value * 2, then print double(4).",
        "def double(value):\n    return value * 2\nprint(double(4))",
        "8")]
    [InlineData(
        "Build a Python script with a function named triple that takes number and returns number * 3, then print triple(3).",
        "def triple(number):\n    return number * 3\nprint(triple(3))",
        "9")]
    [InlineData(
        "Generate a Python program with a function named add_five that takes value and returns value + 5, then print add_five(7).",
        "def add_five(value):\n    return value + 5\nprint(add_five(7))",
        "12")]
    [InlineData(
        "Create a Python program with a function named add that takes left and right and returns left + right, then print add(3, 5).",
        "def add(left, right):\n    return left + right\nprint(add(3, 5))",
        "8")]
    [InlineData(
        "Build a Python script with a function named multiply that takes width and height and returns width * height, then print multiply(6, 7).",
        "def multiply(width, height):\n    return width * height\nprint(multiply(6, 7))",
        "42")]
    [InlineData(
        "Generate a Python program with a function named subtract that takes larger and smaller and returns larger - smaller, then print subtract(15, 4).",
        "def subtract(larger, smaller):\n    return larger - smaller\nprint(subtract(15, 4))",
        "11")]
    [InlineData(
        "Create a Python program with a function named sum_three that takes first and second and third and returns first + second + third, then print sum_three(2, 3, 4).",
        "def sum_three(first, second, third):\n    return first + second + third\nprint(sum_three(2, 3, 4))",
        "9")]
    [InlineData(
        "Build a Python script with a function named volume that takes width and height and depth and returns width * height * depth, then print volume(2, 3, 5).",
        "def volume(width, height, depth):\n    return width * height * depth\nprint(volume(2, 3, 5))",
        "30")]
    [InlineData(
        "Generate a Python program with a function named weighted_total that takes base and bonus and penalty and returns base + bonus - penalty, then print weighted_total(20, 7, 3).",
        "def weighted_total(base, bonus, penalty):\n    return base + bonus - penalty\nprint(weighted_total(20, 7, 3))",
        "24")]
    [InlineData(
        "Create a Python program with a function named scale that takes value and factor and returns (value + 3) * factor, then print scale(2, 4).",
        "def scale(value, factor):\n    return (value + 3) * factor\nprint(scale(2, 4))",
        "20")]
    [InlineData(
        "Build a Python script with a function named weighted that takes base and bonus and factor and returns (base + bonus) * (factor - 1), then print weighted(5, 2, 4).",
        "def weighted(base, bonus, factor):\n    return (base + bonus) * (factor - 1)\nprint(weighted(5, 2, 4))",
        "21")]
    [InlineData(
        "Create a Python program with a function named clamp_floor that takes value and returns max(abs(value), 10) + 2, then print clamp_floor(-4).",
        "def clamp_floor(value):\n    return max(abs(value), 10) + 2\nprint(clamp_floor(-4))",
        "12")]
    [InlineData(
        "Build a Python script with a function named rounded_ratio that takes total and count and returns round(total / count, 2), then print rounded_ratio(10, 3).",
        "def rounded_ratio(total, count):\n    return round(total / count, 2)\nprint(rounded_ratio(10, 3))",
        "3.33")]
    [InlineData(
        "Generate a Python program that sets total to 5 and prints (total + 3) * 2.",
        "total = 5\nprint((total + 3) * 2)",
        "16")]
    [InlineData(
        "Create a Python program with a function named double that takes value and returns value * 2, and a function named add_three that takes value and returns double(value) + 3, then print add_three(4).",
        "def double(value):\n    return value * 2\ndef add_three(value):\n    return double(value) + 3\nprint(add_three(4))",
        "11")]
    [InlineData(
        "Build a Python script with a function named sum_values that takes left and right and returns left + right, and a function named doubled_sum that takes left and right and returns sum_values(left, right) * 2, then print doubled_sum(3, 5).",
        "def sum_values(left, right):\n    return left + right\ndef doubled_sum(left, right):\n    return sum_values(left, right) * 2\nprint(doubled_sum(3, 5))",
        "16")]
    [InlineData(
        "Create a Python program with a function named double that takes value and returns value * 2, a function named add_three that takes value and returns double(value) + 3, and a function named finish that takes value and returns add_three(value) * 2, then print finish(4).",
        "def double(value):\n    return value * 2\ndef add_three(value):\n    return double(value) + 3\ndef finish(value):\n    return add_three(value) * 2\nprint(finish(4))",
        "22")]
    [InlineData(
        "Build a Python script with a function named add that takes left and right and returns left + right, a function named multiply that takes left and right and returns add(left, right) * 2, and a function named offset that takes left and right and returns multiply(left, right) + 5, then print offset(3, 4).",
        "def add(left, right):\n    return left + right\ndef multiply(left, right):\n    return add(left, right) * 2\ndef offset(left, right):\n    return multiply(left, right) + 5\nprint(offset(3, 4))",
        "19")]
    [InlineData(
        "Create a Python program with a function named calculate that takes value and sets doubled to value * 2 and returns doubled, then print calculate(4).",
        "def calculate(value):\n    doubled = value * 2\n    return doubled\nprint(calculate(4))",
        "8")]
    [InlineData(
        "Build a Python script with a function named adjusted that takes amount and sets result to amount + 7 and returns result * 2, then print adjusted(5).",
        "def adjusted(amount):\n    result = amount + 7\n    return result * 2\nprint(adjusted(5))",
        "24")]
    [InlineData(
        "Generate a Python program with a function named combine that takes left and right and sets total to left + right and returns total * 3, then print combine(4, 6).",
        "def combine(left, right):\n    total = left + right\n    return total * 3\nprint(combine(4, 6))",
        "30")]
    [InlineData(
        "Create a Python program with a function named calculate that takes value and sets doubled to value * 2 and sets tripled to doubled * 3 and returns tripled, then print calculate(4).",
        "def calculate(value):\n    doubled = value * 2\n    tripled = doubled * 3\n    return tripled\nprint(calculate(4))",
        "24")]
    [InlineData(
        "Build a Python script with a function named total_cost that takes price and quantity and sets subtotal to price * quantity and sets taxed to subtotal + 5 and returns taxed, then print total_cost(6, 4).",
        "def total_cost(price, quantity):\n    subtotal = price * quantity\n    taxed = subtotal + 5\n    return taxed\nprint(total_cost(6, 4))",
        "29")]
    [InlineData(
        "Generate a Python program with a function named transform that takes value and sets shifted to value + 2 and sets scaled to shifted * 4 and returns scaled - shifted, then print transform(3).",
        "def transform(value):\n    shifted = value + 2\n    scaled = shifted * 4\n    return scaled - shifted\nprint(transform(3))",
        "15")]
    public async Task Generates_and_executes_composed_python_programs(
        string input,
        string expectedCode,
        string expectedOutput)
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(input);

        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(LatticeUtteranceResultKind.TaskCompleted, utterance.Kind);

        var fence = new string((char)96, 3);
        var lines = utterance.Message.Split(Environment.NewLine, StringSplitOptions.None);
        var start = Array.IndexOf(lines, fence + "python");
        var end = Array.IndexOf(lines, fence, start + 1);
        Assert.True(start >= 0 && end > start, utterance.Message);

        var generatedCode = string.Join(Environment.NewLine, lines[(start + 1)..end]);
        Assert.Equal(expectedCode.Replace("\n", Environment.NewLine, StringComparison.Ordinal), generatedCode);
        await AssertPythonOutput(generatedCode, expectedOutput);
    }

    private static async Task AssertPythonOutput(string code, string expectedOutput)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "python" : "python3",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add(code);

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);

        var outputTask = process!.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        var output = await outputTask;
        var error = await errorTask;
        Assert.True(process.ExitCode == 0, error);
        Assert.Equal(expectedOutput.Replace("\n", Environment.NewLine, StringComparison.Ordinal) + Environment.NewLine, output);
    }
}
