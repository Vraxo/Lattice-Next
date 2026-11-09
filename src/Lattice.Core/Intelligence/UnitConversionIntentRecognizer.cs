using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Lattice.Core.Tools;

namespace Lattice.Core.Intelligence;

public sealed class UnitConversionIntentRecognizer : IToolIntentRecognizer
{
    private const string Number = @"-?\d+(?:\.\d+)?";
    private const string Unit =
        @"millimeters?|millimetres?|mm|centimeters?|centimetres?|cm|kilometers?|kilometres?|km|meters?|metres?|m|milligrams?|mg|kilograms?|kg|grams?|g|milliliters?|millilitres?|ml|liters?|litres?|l|seconds?|s|minutes?|min|hours?|h";

    private static readonly Regex ConversionPattern = new(
        @"^(?:convert\s+)?(?<value>" + Number + @")\s*(?<from>" + Unit +
        @")\s+(?:to|in)\s+(?<to>" + Unit + @")[?.]?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex HowManyPattern = new(
        @"^how\s+many\s+(?<to>" + Unit + @")\s+is\s+(?<value>" + Number +
        @")\s+(?<from>" + Unit + @")[?.]?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly IReadOnlyDictionary<string, string> UnitAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["millimeter"] = "mm",
            ["millimeters"] = "mm",
            ["millimetre"] = "mm",
            ["millimetres"] = "mm",
            ["mm"] = "mm",
            ["centimeter"] = "cm",
            ["centimeters"] = "cm",
            ["centimetre"] = "cm",
            ["centimetres"] = "cm",
            ["cm"] = "cm",
            ["meter"] = "m",
            ["meters"] = "m",
            ["metre"] = "m",
            ["metres"] = "m",
            ["m"] = "m",
            ["kilometer"] = "km",
            ["kilometers"] = "km",
            ["kilometre"] = "km",
            ["kilometres"] = "km",
            ["km"] = "km",
            ["milligram"] = "mg",
            ["milligrams"] = "mg",
            ["mg"] = "mg",
            ["gram"] = "g",
            ["grams"] = "g",
            ["g"] = "g",
            ["kilogram"] = "kg",
            ["kilograms"] = "kg",
            ["kg"] = "kg",
            ["milliliter"] = "ml",
            ["milliliters"] = "ml",
            ["millilitre"] = "ml",
            ["millilitres"] = "ml",
            ["ml"] = "ml",
            ["liter"] = "l",
            ["liters"] = "l",
            ["litre"] = "l",
            ["litres"] = "l",
            ["l"] = "l",
            ["second"] = "s",
            ["seconds"] = "s",
            ["s"] = "s",
            ["minute"] = "min",
            ["minutes"] = "min",
            ["min"] = "min",
            ["hour"] = "h",
            ["hours"] = "h",
            ["h"] = "h"
        };

    public bool TryRecognize(
        string input,
        ToolObservation? latestObservation,
        out ToolIntent? intent)
    {
        ArgumentNullException.ThrowIfNull(input);
        intent = null;
        var source = input.Trim();
        var match = ConversionPattern.Match(source);

        if (!match.Success)
        {
            match = HowManyPattern.Match(source);
        }

        if (!match.Success ||
            !decimal.TryParse(
                match.Groups["value"].Value,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var value))
        {
            return false;
        }

        var from = NormalizeUnit(match.Groups["from"].Value);
        var to = NormalizeUnit(match.Groups["to"].Value);
        var arguments = JsonSerializer.Serialize(new { value, from, to });

        intent = new ToolIntent(
            "unit-converter",
            arguments,
            observation => $"{Format(value)} {from} is {observation.Content} {to}.");

        return true;
    }

    private static string NormalizeUnit(string value) =>
        UnitAliases.TryGetValue(value, out var normalized) ? normalized : value.ToLowerInvariant();

    private static string Format(decimal value) =>
        value.ToString("0.############################", CultureInfo.InvariantCulture);
}
