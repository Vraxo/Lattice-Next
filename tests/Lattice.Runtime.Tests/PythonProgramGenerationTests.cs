using System.Diagnostics;
using Lattice.Client;
using Lattice.Runtime;
using Xunit;

namespace Lattice.Runtime.Tests;

public sealed class PythonProgramGenerationTests
{
    [Theory]
    [InlineData(
        "Write a Python program that, given a list of names, creates a dictionary that maps each name to the number of characters in it.",
        "def map_values(names):\n    mapped_values = {name: len(name) for name in names}\n    return mapped_values\nprint(map_values([\"Ada\", \"Grace\", \"Linus\"]))",
        "{'Ada': 3, 'Grace': 5, 'Linus': 5}")]
    [InlineData(
        "Create a Python program that, from a list of numbers, builds a dictionary named squares that maps each number to the square of it.",
        "def map_values(numbers):\n    squares = {number: number * number for number in numbers}\n    return squares\nprint(map_values([1, 2, 3]))",
        "{1: 1, 2: 4, 3: 9}")]
    [InlineData(
        "Build a Python program that, given a list of values, makes a dictionary that maps each value to value + 10.",
        "def map_values(values):\n    mapped_values = {value: value + 10 for value in values}\n    return mapped_values\nprint(map_values([1, 2, 3]))",
        "{1: 11, 2: 12, 3: 13}")]
    [InlineData(
        "Write a Python function that receives a list of numbers and returns a new list containing only values greater than the average.",
        "def above_average(values):\n    return [value for value in values if value > sum(values) / len(values)]\nprint(above_average([1, 2, 3, 4, 5]))",
        "[4, 5]")]
    [InlineData(
        "Create a Python function named keep_at_least that receives a list of numbers named readings and returns a new list containing only reading values at least 3.",
        "def keep_at_least(readings):\n    return [reading for reading in readings if reading >= 3]\nprint(keep_at_least([1, 2, 3, 4, 5]))",
        "[3, 4, 5]")]
    [InlineData(
        "Create a Python function named keep_at_most that receives a list of numbers named scores and returns a new list containing only score values less than or equal to 3.",
        "def keep_at_most(scores):\n    return [score for score in scores if score <= 3]\nprint(keep_at_most([1, 2, 3, 4, 5]))",
        "[1, 2, 3]")]
    [InlineData(
        "Create a Python function named omit_three that receives a list of numbers named scores and returns a new list containing only score values not equal to 3.",
        "def omit_three(scores):\n    return [score for score in scores if score != 3]\nprint(omit_three([1, 2, 3, 4, 5]))",
        "[1, 2, 4, 5]")]
    [InlineData(
        "Create a Python function named keep_above_expression that receives a list of numbers named scores and returns a new list containing only score values greater than 2 + 1.",
        "def keep_above_expression(scores):\n    return [score for score in scores if score > 2 + 1]\nprint(keep_above_expression([1, 2, 3, 4, 5]))",
        "[4, 5]")]
    [InlineData(
        "Create a Python function named keep_large that receives a list of numbers named readings and returns a new list containing only reading values greater than 10.",
        "def keep_large(readings):\n    return [reading for reading in readings if reading > 10]\nprint(keep_large([1, 2, 3, 4, 5]))",
        "[]")]
    [InlineData(
        "Build a Python function named keep_small that receives a list of numbers named scores and returns a new list containing only score values less than 4.",
        "def keep_small(scores):\n    return [score for score in scores if score < 4]\nprint(keep_small([1, 2, 3, 4, 5]))",
        "[1, 2, 3]")]
    [InlineData(
        "Create a Python function named filter_high that receives a list of numbers named scores and returns a new list containing only score values greater than the average of scores.",
        "def filter_high(scores):\n    return [score for score in scores if score > sum(scores) / len(scores)]\nprint(filter_high([1, 2, 3, 4, 5]))",
        "[4, 5]")]
    [InlineData(
        "Write a Python function that receives a list of numbers named measurements and returns a new list containing only measurement values less than the average of measurements.",
        "def above_average(measurements):\n    return [measurement for measurement in measurements if measurement < sum(measurements) / len(measurements)]\nprint(above_average([1, 2, 3, 4, 5]))",
        "[1, 2]")]
    [InlineData(
        "Create a Python function named below_mean that accepts a list of numbers named scores and returns a new list containing only score values less than the average of scores.",
        "def below_mean(scores):\n    return [score for score in scores if score < sum(scores) / len(scores)]\nprint(below_mean([1, 2, 3, 4, 5]))",
        "[1, 2]")]
    [InlineData(
        "Write a Python function that converts text to an integer and returns a clear result when the text is not a valid integer.",
        "def convert_to_integer(text):\n    try:\n        return int(text)\n    except ValueError:\n        return \"Invalid integer\"\nprint(convert_to_integer(\"42\"))\nprint(convert_to_integer(\"not-a-number\"))",
        "42\nInvalid integer")]
    [InlineData(
        "Create a Python function named parse_count that accepts raw_text and converts raw_text to an integer and returns a clear message if the input is not a valid integer.",
        "def parse_count(raw_text):\n    try:\n        return int(raw_text)\n    except ValueError:\n        return \"Invalid integer\"\nprint(parse_count(\"42\"))\nprint(parse_count(\"not-a-number\"))",
        "42\nInvalid integer")]
    [InlineData(
        "Create a Python program that stores {'apple': 1.5, 'banana': 0.75} in a dictionary named prices, then prints each product and its price.",
        "prices = {'apple': 1.5, 'banana': 0.75}\nfor product, price in prices.items():\n    print(product, price)",
        "apple 1.5\nbanana 0.75")]
    [InlineData(
        "Build a Python script that stores {'Ada': 3, 'Grace': 5, 'Linus': 6} as a dictionary named counts, then displays each name and its count.",
        "counts = {'Ada': 3, 'Grace': 5, 'Linus': 6}\nfor name, count in counts.items():\n    print(name, count)",
        "Ada 3\nGrace 5\nLinus 6")]
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

    [Fact]
    public async Task Generates_and_executes_non_empty_file_line_counting_program()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(
            "Write a Python program that reads a text file and counts how many non-empty lines it contains.");

        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(LatticeUtteranceResultKind.TaskCompleted, utterance.Kind);

        var fence = new string((char)96, 3);
        var lines = utterance.Message.Split(Environment.NewLine, StringSplitOptions.None);
        var start = Array.IndexOf(lines, fence + "python");
        var end = Array.IndexOf(lines, fence, start + 1);
        Assert.True(start >= 0 && end > start, utterance.Message);

        var generatedCode = string.Join(Environment.NewLine, lines[(start + 1)..end]);
        var expectedCode = string.Join(Environment.NewLine,
            "line_count = 0",
            "with open(\"input.txt\", \"r\", encoding=\"utf-8\") as input_file:",
            "    for line in input_file:",
            "        if line.strip():",
            "            line_count += 1",
            "print(line_count)");
        Assert.Equal(expectedCode, generatedCode);
        await AssertPythonOutput(generatedCode, "2", "input.txt", "First line\n\n   \nSecond line\n");
    }

    [Fact]
    public async Task Generates_and_executes_json_file_round_trip_program()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(
            "Write a Python program that saves a list of dictionaries to a JSON file and then loads it back.");

        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(LatticeUtteranceResultKind.TaskCompleted, utterance.Kind);

        var fence = new string((char)96, 3);
        var lines = utterance.Message.Split(Environment.NewLine, StringSplitOptions.None);
        var start = Array.IndexOf(lines, fence + "python");
        var end = Array.IndexOf(lines, fence, start + 1);
        Assert.True(start >= 0 && end > start, utterance.Message);

        var generatedCode = string.Join(Environment.NewLine, lines[(start + 1)..end]);
        var expectedCode = string.Join(Environment.NewLine,
            "import json",
            "records = [{\"name\": \"Ada\", \"age\": 36}, {\"name\": \"Grace\", \"age\": 85}]",
            "with open(\"records.json\", \"w\", encoding=\"utf-8\") as output_file:",
            "    json.dump(records, output_file, indent=2)",
            "with open(\"records.json\", \"r\", encoding=\"utf-8\") as input_file:",
            "    loaded_records = json.load(input_file)",
            "print(loaded_records)");
        Assert.Equal(expectedCode, generatedCode);
        await AssertPythonOutput(
            generatedCode,
            "[{'name': 'Ada', 'age': 36}, {'name': 'Grace', 'age': 85}]",
            useTemporaryDirectory: true);
    }

    [Fact]
    public async Task Generalizes_json_round_trip_variable_and_file_names()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(
            "Write a Python program that saves a list of dictionaries named people to a JSON file named people-data.json and then loads it back.");

        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(LatticeUtteranceResultKind.TaskCompleted, utterance.Kind);

        var fence = new string((char)96, 3);
        var lines = utterance.Message.Split(Environment.NewLine, StringSplitOptions.None);
        var start = Array.IndexOf(lines, fence + "python");
        var end = Array.IndexOf(lines, fence, start + 1);
        Assert.True(start >= 0 && end > start, utterance.Message);

        var generatedCode = string.Join(Environment.NewLine, lines[(start + 1)..end]);
        var expectedCode = string.Join(Environment.NewLine,
            "import json",
            "people = [{\"name\": \"Ada\", \"age\": 36}, {\"name\": \"Grace\", \"age\": 85}]",
            "with open(\"people-data.json\", \"w\", encoding=\"utf-8\") as output_file:",
            "    json.dump(people, output_file, indent=2)",
            "with open(\"people-data.json\", \"r\", encoding=\"utf-8\") as input_file:",
            "    loaded_people = json.load(input_file)",
            "print(loaded_people)");
        Assert.Equal(expectedCode, generatedCode);
        await AssertPythonOutput(
            generatedCode,
            "[{'name': 'Ada', 'age': 36}, {'name': 'Grace', 'age': 85}]",
            useTemporaryDirectory: true);
    }

    [Fact]
    public async Task Generates_and_executes_reusable_record_sorting_function()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(
            "Create a Python program that defines a function that accepts a list of records and a field name, then returns the records sorted by that field.");

        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(LatticeUtteranceResultKind.TaskCompleted, utterance.Kind);

        var fence = new string((char)96, 3);
        var lines = utterance.Message.Split(Environment.NewLine, StringSplitOptions.None);
        var start = Array.IndexOf(lines, fence + "python");
        var end = Array.IndexOf(lines, fence, start + 1);
        Assert.True(start >= 0 && end > start, utterance.Message);

        var generatedCode = string.Join(Environment.NewLine, lines[(start + 1)..end]);
        var expectedCode = string.Join(Environment.NewLine,
            "def sort_records(records, field):",
            "    return sorted(records, key=lambda record: record[field])",
            "print(sort_records([{\"name\": \"Ada\", \"age\": 36}, {\"name\": \"Grace\", \"age\": 85}, {\"name\": \"Linus\", \"age\": 55}], \"age\"))");
        Assert.Equal(expectedCode, generatedCode);
        await AssertPythonOutput(
            generatedCode,
            "[{'name': 'Ada', 'age': 36}, {'name': 'Linus', 'age': 55}, {'name': 'Grace', 'age': 85}]");
    }

    [Fact]
    public async Task Generalizes_record_sorting_function_and_parameter_names()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(
            "Create a Python program that defines a function named order_records that accepts a list of records named people and a field name named sort_key, then returns the records sorted by that field.");

        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(LatticeUtteranceResultKind.TaskCompleted, utterance.Kind);

        var fence = new string((char)96, 3);
        var lines = utterance.Message.Split(Environment.NewLine, StringSplitOptions.None);
        var start = Array.IndexOf(lines, fence + "python");
        var end = Array.IndexOf(lines, fence, start + 1);
        Assert.True(start >= 0 && end > start, utterance.Message);

        var generatedCode = string.Join(Environment.NewLine, lines[(start + 1)..end]);
        var expectedCode = string.Join(Environment.NewLine,
            "def order_records(people, sort_key):",
            "    return sorted(people, key=lambda record: record[sort_key])",
            "print(order_records([{\"name\": \"Ada\", \"age\": 36}, {\"name\": \"Grace\", \"age\": 85}, {\"name\": \"Linus\", \"age\": 55}], \"age\"))");
        Assert.Equal(expectedCode, generatedCode);
        await AssertPythonOutput(
            generatedCode,
            "[{'name': 'Ada', 'age': 36}, {'name': 'Linus', 'age': 55}, {'name': 'Grace', 'age': 85}]");
    }

    [Fact]
    public async Task Generates_record_sorting_function_in_descending_order()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(
            "Create a Python program that defines a reusable function named sort_people that accepts a list of records named people and a field name named sort_key, then returns the records sorted by that field in descending order.");

        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(LatticeUtteranceResultKind.TaskCompleted, utterance.Kind);

        var fence = new string((char)96, 3);
        var lines = utterance.Message.Split(Environment.NewLine, StringSplitOptions.None);
        var start = Array.IndexOf(lines, fence + "python");
        var end = Array.IndexOf(lines, fence, start + 1);
        Assert.True(start >= 0 && end > start, utterance.Message);

        var generatedCode = string.Join(Environment.NewLine, lines[(start + 1)..end]);
        var expectedCode = string.Join(Environment.NewLine,
            "def sort_people(people, sort_key):",
            "    return sorted(people, key=lambda record: record[sort_key], reverse=True)",
            "print(sort_people([{\"name\": \"Ada\", \"age\": 36}, {\"name\": \"Grace\", \"age\": 85}, {\"name\": \"Linus\", \"age\": 55}], \"age\"))");
        Assert.Equal(expectedCode, generatedCode);
        await AssertPythonOutput(
            generatedCode,
            "[{'name': 'Grace', 'age': 85}, {'name': 'Linus', 'age': 55}, {'name': 'Ada', 'age': 36}]");
    }

    [Fact]
    public async Task Sorts_string_record_fields_case_insensitively()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(
            "Create a Python program that defines a function named sort_people that accepts a list of records named people and a field name named sort_key, then returns the records sorted case-insensitively by that field.");

        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(LatticeUtteranceResultKind.TaskCompleted, utterance.Kind);

        var fence = new string((char)96, 3);
        var lines = utterance.Message.Split(Environment.NewLine, StringSplitOptions.None);
        var start = Array.IndexOf(lines, fence + "python");
        var end = Array.IndexOf(lines, fence, start + 1);
        Assert.True(start >= 0 && end > start, utterance.Message);

        var generatedCode = string.Join(Environment.NewLine, lines[(start + 1)..end]);
        var expectedCode = string.Join(Environment.NewLine,
            "def sort_people(people, sort_key):",
            "    return sorted(people, key=lambda record: record[sort_key].casefold())",
            """print(sort_people([{'name': 'ada', 'age': 36}, {'name': 'BOB', 'age': 42}, {'name': 'Grace', 'age': 85}], 'name'))""");
        Assert.Equal(expectedCode, generatedCode);
        await AssertPythonOutput(
            generatedCode,
            "[{'name': 'ada', 'age': 36}, {'name': 'BOB', 'age': 42}, {'name': 'Grace', 'age': 85}]");
    }

    [Fact]
    public async Task Generates_and_executes_record_sorting_by_multiple_fields()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(
            "Create a Python program that defines a function named order_records that accepts a list of records named people and a list of field names named sort_keys, then returns the records sorted by those fields.");

        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(LatticeUtteranceResultKind.TaskCompleted, utterance.Kind);

        var fence = new string((char)96, 3);
        var lines = utterance.Message.Split(Environment.NewLine, StringSplitOptions.None);
        var start = Array.IndexOf(lines, fence + "python");
        var end = Array.IndexOf(lines, fence, start + 1);
        Assert.True(start >= 0 && end > start, utterance.Message);

        var generatedCode = string.Join(Environment.NewLine, lines[(start + 1)..end]);
        var expectedCode = string.Join(Environment.NewLine,
            "def order_records(people, sort_keys):",
            "    return sorted(people, key=lambda record: tuple(record[field] for field in sort_keys))",
            """print(order_records([{"department": "Engineering", "name": "Ada"}, {"department": "Design", "name": "Grace"}, {"department": "Engineering", "name": "Linus"}], ["department", "name"]))""");
        Assert.Equal(expectedCode, generatedCode);
        await AssertPythonOutput(
            generatedCode,
            "[{'department': 'Design', 'name': 'Grace'}, {'department': 'Engineering', 'name': 'Ada'}, {'department': 'Engineering', 'name': 'Linus'}]");
    }

    [Fact]
    public async Task Accepts_reverse_order_as_a_descending_sort_request()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(
            "Create a Python program that defines a function named sort_people that accepts a list of records named people and a field name named sort_key, then returns the records sorted by that field in reverse order.");

        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(LatticeUtteranceResultKind.TaskCompleted, utterance.Kind);

        var fence = new string((char)96, 3);
        var lines = utterance.Message.Split(Environment.NewLine, StringSplitOptions.None);
        var start = Array.IndexOf(lines, fence + "python");
        var end = Array.IndexOf(lines, fence, start + 1);
        Assert.True(start >= 0 && end > start, utterance.Message);

        var generatedCode = string.Join(Environment.NewLine, lines[(start + 1)..end]);
        Assert.Contains("reverse=True", generatedCode, StringComparison.Ordinal);
        await AssertPythonOutput(
            generatedCode,
            "[{'name': 'Grace', 'age': 85}, {'name': 'Linus', 'age': 55}, {'name': 'Ada', 'age': 36}]");
    }

    [Fact]
    public async Task Generates_two_number_addition_from_a_natural_language_request()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(
            "Write a Python program that stores two numbers in variables, adds them, and prints the result.");

        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(LatticeUtteranceResultKind.TaskCompleted, utterance.Kind);

        var fence = new string((char)96, 3);
        var lines = utterance.Message!.Split(Environment.NewLine, StringSplitOptions.None);
        var start = Array.IndexOf(lines, fence + "python");
        var end = Array.IndexOf(lines, fence, start + 1);
        Assert.True(start >= 0 && end > start, utterance.Message);

        var generatedCode = string.Join(Environment.NewLine, lines[(start + 1)..end]);
        Assert.Equal(
            string.Join(Environment.NewLine,
                "first_number = 10",
                "second_number = 20",
                "result = first_number + second_number",
                "print(result)"),
            generatedCode);
        await AssertPythonOutput(generatedCode, "30");
    }

    [Fact]
    public async Task Generates_even_number_filtering_from_a_natural_language_request()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(
            "Write a Python program that prints only the even numbers from a list.");

        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(LatticeUtteranceResultKind.TaskCompleted, utterance.Kind);

        var fence = new string((char)96, 3);
        var lines = utterance.Message!.Split(Environment.NewLine, StringSplitOptions.None);
        var start = Array.IndexOf(lines, fence + "python");
        var end = Array.IndexOf(lines, fence, start + 1);
        Assert.True(start >= 0 && end > start, utterance.Message);

        var generatedCode = string.Join(Environment.NewLine, lines[(start + 1)..end]);
        Assert.Equal(
            string.Join(Environment.NewLine,
                "for number in [1, 2, 3, 4, 5, 6]:",
                "    if number % 2 == 0:",
                "        print(number)"),
            generatedCode);
        await AssertPythonOutput(generatedCode, "2\n4\n6");
    }

    [Fact]
    public async Task Calculates_a_list_total_without_using_the_builtin_sum_function()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(
            "Given a list of numbers, write a Python program that calculates their total without using the built-in sum function.");

        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(LatticeUtteranceResultKind.TaskCompleted, utterance.Kind);

        var fence = new string((char)96, 3);
        var lines = utterance.Message!.Split(Environment.NewLine, StringSplitOptions.None);
        var start = Array.IndexOf(lines, fence + "python");
        var end = Array.IndexOf(lines, fence, start + 1);
        Assert.True(start >= 0 && end > start, utterance.Message);

        var generatedCode = string.Join(Environment.NewLine, lines[(start + 1)..end]);
        Assert.Equal(
            string.Join(Environment.NewLine,
                "total = 0",
                "for number in [1, 2, 3, 4, 5]:",
                "    total = total + number",
                "print(total)"),
            generatedCode);
        Assert.DoesNotContain("sum(", generatedCode, StringComparison.Ordinal);
        await AssertPythonOutput(generatedCode, "15");
    }

    [Fact]
    public async Task Classifies_positive_negative_and_zero_integers()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync();

        var result = await session.SubmitAsync(
            "Write a Python program that reports whether an integer is positive, negative, or zero.");

        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(LatticeUtteranceResultKind.TaskCompleted, utterance.Kind);

        var fence = new string((char)96, 3);
        var lines = utterance.Message!.Split(Environment.NewLine, StringSplitOptions.None);
        var start = Array.IndexOf(lines, fence + "python");
        var end = Array.IndexOf(lines, fence, start + 1);
        Assert.True(start >= 0 && end > start, utterance.Message);

        var generatedCode = string.Join(Environment.NewLine, lines[(start + 1)..end]);
        Assert.Equal(
            string.Join(Environment.NewLine,
                "number = -5",
                "if number > 0:",
                "    print(\"Positive\")",
                "elif number < 0:",
                "    print(\"Negative\")",
                "elif number == 0:",
                "    print(\"Zero\")"),
            generatedCode);

        await AssertPythonOutput(generatedCode, "Negative");
        await AssertPythonOutput(generatedCode.Replace("number = -5", "number = 8", StringComparison.Ordinal), "Positive");
        await AssertPythonOutput(generatedCode.Replace("number = -5", "number = 0", StringComparison.Ordinal), "Zero");
    }

    private static async Task AssertPythonOutput(string code, string expectedOutput, string? inputFileName = null, string? inputFileContent = null, bool useTemporaryDirectory = false)
    {
        string? workingDirectory = null;
        if (useTemporaryDirectory || (inputFileName is not null && inputFileContent is not null))
        {
            workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workingDirectory);
            if (inputFileName is not null && inputFileContent is not null)
            {
                await File.WriteAllTextAsync(Path.Combine(workingDirectory, inputFileName), inputFileContent);
            }
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "python" : "python3",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory
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
        if (workingDirectory is not null)
        {
            Directory.Delete(workingDirectory, recursive: true);
        }

        Assert.True(process.ExitCode == 0, error);
        Assert.Equal(expectedOutput.Replace("\n", Environment.NewLine, StringComparison.Ordinal) + Environment.NewLine, output);
    }
}
