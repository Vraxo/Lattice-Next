using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Lattice.Core.Tools;

namespace Lattice.Core.Intelligence;

public sealed class CalculatorIntentRecognizer : IToolIntentRecognizer
{
    private const string Number = @"-?\d+(?:\.\d+)?";

    private static readonly Regex ExpressionPattern = new(
        @"^(?:(?:what\s+is|calculate|compute)\s+)?(?<left>" + Number +
        @")\s+(?<operator>plus|added\s+to|minus|less|times|multiplied\s+by|divided\s+by|over)\s+(?<right>" +
        Number + @")[?.]?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex VerbPattern = new(
        @"^(?:(?:what\s+is|calculate|compute|please)\s+)?(?<verb>add|subtract|multiply|divide)\s+(?<first>" +
        Number + @")\s+(?<connector>and|to|from|by)\s+(?<second>" + Number + @")[?.]?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex FollowUpPattern = new(
        @"^(?:and\s+)?(?<verb>multiply|divide)\s+(?:that|it|the\s+result)\s+by\s+(?<right>" +
        Number + @")[?.]?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex AddFollowUpPattern = new(
        @"^(?:and\s+)?add\s+(?<right>" + Number + @")\s+to\s+(?:that|it|the\s+result)[?.]?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex SubtractFollowUpPattern = new(
        @"^(?:and\s+)?subtract\s+(?<right>" + Number + @")\s+from\s+(?:that|it|the\s+result)[?.]?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public bool TryRecognize(
        string input,
        ToolObservation? latestObservation,
        out ToolIntent? intent)
    {
        ArgumentNullException.ThrowIfNull(input);
        intent = null;
        var source = input.Trim();

        var expression = ExpressionPattern.Match(source);

        if (expression.Success &&
            TryDecimal(expression.Groups["left"].Value, out var expressionLeft) &&
            TryDecimal(expression.Groups["right"].Value, out var expressionRight))
        {
            var operation = expression.Groups["operator"].Value.ToLowerInvariant() switch
            {
                "plus" or "added to" => "add",
                "minus" or "less" => "subtract",
                "times" or "multiplied by" => "multiply",
                "divided by" or "over" => "divide",
                _ => null
            };

            if (operation is not null)
            {
                intent = CreateIntent(expressionLeft, operation, expressionRight);
                return true;
            }
        }

        var verb = VerbPattern.Match(source);

        if (verb.Success &&
            TryDecimal(verb.Groups["first"].Value, out var first) &&
            TryDecimal(verb.Groups["second"].Value, out var second))
        {
            var verbName = verb.Groups["verb"].Value.ToLowerInvariant();
            var connector = verb.Groups["connector"].Value.ToLowerInvariant();
            var operation = verbName switch
            {
                "add" when connector is "and" or "to" => "add",
                "subtract" when connector == "from" => "subtract",
                "multiply" when connector == "by" => "multiply",
                "divide" when connector == "by" => "divide",
                _ => null
            };

            if (operation is not null)
            {
                var left = operation == "subtract" ? second : first;
                var right = operation == "subtract" ? first : second;
                intent = CreateIntent(left, operation, right);
                return true;
            }
        }

        if (!TryGetPreviousNumber(latestObservation, out var previous))
        {
            return false;
        }

        var followUp = FollowUpPattern.Match(source);

        if (followUp.Success && TryDecimal(followUp.Groups["right"].Value, out var followUpRight))
        {
            var operation = followUp.Groups["verb"].Value.Equals("multiply", StringComparison.OrdinalIgnoreCase)
                ? "multiply"
                : "divide";
            intent = CreateIntent(previous, operation, followUpRight);
            return true;
        }

        var addFollowUp = AddFollowUpPattern.Match(source);

        if (addFollowUp.Success && TryDecimal(addFollowUp.Groups["right"].Value, out var addRight))
        {
            intent = CreateIntent(previous, "add", addRight);
            return true;
        }

        var subtractFollowUp = SubtractFollowUpPattern.Match(source);

        if (subtractFollowUp.Success && TryDecimal(subtractFollowUp.Groups["right"].Value, out var subtractRight))
        {
            intent = CreateIntent(previous, "subtract", subtractRight);
            return true;
        }

        return false;
    }

    private static ToolIntent CreateIntent(decimal left, string operation, decimal right)
    {
        var arguments = JsonSerializer.Serialize(new { left, operation, right });
        var operationText = operation switch
        {
            "add" => "plus",
            "subtract" => "minus",
            "multiply" => "multiplied by",
            "divide" => "divided by",
            _ => operation
        };

        return new ToolIntent(
            "calculator",
            arguments,
            observation => $"{Format(left)} {operationText} {Format(right)} is {observation.Content}.");
    }

    private static bool TryGetPreviousNumber(ToolObservation? observation, out decimal value)
    {
        value = 0;

        return observation is not null &&
            TryDecimal(observation.Content, out value);
    }

    private static bool TryDecimal(string value, out decimal result) =>
        decimal.TryParse(
            value,
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out result);

    private static string Format(decimal value) =>
        value.ToString("0.############################", CultureInfo.InvariantCulture);
}
