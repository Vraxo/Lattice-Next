using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Lattice.Core.Tools;

namespace Lattice.Core.Intelligence;

public sealed class ManifestToolIntentRecognizer : IToolIntentRecognizer
{
    private const int MaximumChainLength = 8;

    private static readonly Regex ChainSeparator = new(
        @"\s*,?\s+then\s+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex SlotPattern = new(
        @"^\{(?<name>[A-Za-z][A-Za-z0-9_]*):(?<type>number|word|text)\}$",
        RegexOptions.CultureInvariant);

    private readonly ToolDescriptor descriptor;
    private readonly ToolIntentDefinition definition;

    public ManifestToolIntentRecognizer(
        ToolDescriptor descriptor,
        ToolIntentDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(definition);

        this.descriptor = descriptor;
        this.definition = definition;
    }

    public bool TryRecognize(
        string input,
        ToolObservation? latestObservation,
        out ToolIntent? intent)
    {
        ArgumentNullException.ThrowIfNull(input);
        intent = null;

        var segments = ChainSeparator.Split(input.Trim());

        if (segments.Length == 0 || segments.Length > MaximumChainLength ||
            !TryMatch(definition, segments[0], out var first))
        {
            return false;
        }

        var remaining = new List<MatchedIntent>();

        foreach (var segment in segments.Skip(1))
        {
            var matches = descriptor.Intents
                .Where(candidate => TryMatch(candidate, segment, out _))
                .Select(candidate =>
                {
                    TryMatch(candidate, segment, out var matched);
                    return matched;
                })
                .ToArray();

            if (matches.Length != 1 ||
                !matches[0].Definition.Arguments.Values.Any(binding => binding.Reference == "previous"))
            {
                return false;
            }

            remaining.Add(matches[0]);
        }

        intent = CreateIntent(first, latestObservation, remaining);
        return intent is not null;
    }

    private ToolIntent? CreateIntent(
        MatchedIntent matched,
        ToolObservation? previous,
        IReadOnlyList<MatchedIntent> remaining)
    {
        if (!TryBuildArguments(matched, previous, out var arguments))
        {
            return null;
        }

        Func<ToolObservation, ToolIntent?>? continuation = null;

        if (remaining.Count > 0)
        {
            var next = remaining[0];
            var later = remaining.Skip(1).ToArray();
            continuation = observation => CreateIntent(next, observation, later);
        }

        return new ToolIntent(
            descriptor.Id,
            JsonSerializer.Serialize(arguments),
            observation => FormatResponse(matched.Definition, arguments, observation),
            continuation);
    }

    private static bool TryBuildArguments(
        MatchedIntent matched,
        ToolObservation? previous,
        out Dictionary<string, object?> arguments)
    {
        arguments = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var pair in matched.Definition.Arguments)
        {
            var binding = pair.Value;

            if (binding.Reference is null)
            {
                if (binding.Constant is not { } constant ||
                    !TryGetConstant(constant, out var constantValue))
                {
                    return false;
                }

                arguments.Add(pair.Key, constantValue);
                continue;
            }

            if (binding.Reference == "previous")
            {
                if (!TryGetPreviousNumber(previous, out var previousValue))
                {
                    return false;
                }

                arguments.Add(pair.Key, previousValue);
                continue;
            }

            if (!matched.Slots.TryGetValue(binding.Reference, out var slotValue))
            {
                return false;
            }

            arguments.Add(pair.Key, slotValue);
        }

        return true;
    }

    private static bool TryMatch(
        ToolIntentDefinition definition,
        string input,
        out MatchedIntent matched)
    {
        matched = new MatchedIntent(
            definition,
            new Dictionary<string, object?>(StringComparer.Ordinal));

        var patternTokens = definition.Pattern.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var inputTokens = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (patternTokens.Length == 0 || patternTokens.Length != inputTokens.Length)
        {
            return false;
        }

        var slots = new Dictionary<string, object?>(StringComparer.Ordinal);

        for (var index = 0; index < patternTokens.Length; index++)
        {
            var patternToken = NormalizeToken(patternTokens[index]);
            var inputToken = NormalizeToken(inputTokens[index]);

            if (patternToken.Length == 0 || inputToken.Length == 0)
            {
                return false;
            }

            var slot = SlotPattern.Match(patternToken);

            if (!slot.Success)
            {
                if (!string.Equals(patternToken, inputToken, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                continue;
            }

            var name = slot.Groups["name"].Value;
            var type = slot.Groups["type"].Value;

            if (!TryReadSlot(inputToken, type, out var value) ||
                !slots.TryAdd(name, value))
            {
                return false;
            }
        }

        matched = new MatchedIntent(definition, slots);
        return true;
    }

    private static bool TryReadSlot(
        string token,
        string type,
        out object? value)
    {
        value = null;

        switch (type)
        {
            case "number":
                if (!decimal.TryParse(
                    token,
                    NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture,
                    out var number))
                {
                    return false;
                }

                value = number;
                return true;

            case "word":
                if (!token.All(char.IsLetter))
                {
                    return false;
                }

                value = token;
                return true;

            case "text":
                value = token;
                return true;

            default:
                return false;
        }
    }

    private static string NormalizeToken(string token) =>
        token.Trim(',', '.', '?', '!', ';', ':');

    private static bool TryGetConstant(JsonElement value, out object? result)
    {
        result = value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number when value.TryGetDecimal(out var number) => number,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };

        return result is not null;
    }

    private static bool TryGetPreviousNumber(
        ToolObservation? observation,
        out decimal value)
    {
        value = 0;

        return observation is not null &&
            decimal.TryParse(
                observation.Content,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out value);
    }

    private static string FormatResponse(
        ToolIntentDefinition definition,
        IReadOnlyDictionary<string, object?> arguments,
        ToolObservation observation)
    {
        var values = arguments.ToDictionary(
            pair => pair.Key,
            pair => FormatValue(pair.Value),
            StringComparer.Ordinal);
        values["result"] = FormatObservation(observation.Content);

        return Regex.Replace(
            definition.ResponseTemplate,
            @"\{(?<name>[A-Za-z][A-Za-z0-9_]*)\}",
            match => values.TryGetValue(match.Groups["name"].Value, out var value)
                ? value
                : match.Value,
            RegexOptions.CultureInvariant);
    }

    private static string FormatObservation(string content)
    {
        try
        {
            using var document = JsonDocument.Parse(content);
            var value = document.RootElement;

            return value.ValueKind switch
            {
                JsonValueKind.Number when value.TryGetDecimal(out var number) => FormatValue(number),
                JsonValueKind.String => value.GetString() ?? string.Empty,
                _ => content
            };
        }
        catch (JsonException)
        {
            return content;
        }
    }

    private static string FormatValue(object? value) =>
        value switch
        {
            decimal number => number.ToString("0.############################", CultureInfo.InvariantCulture),
            null => string.Empty,
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
        };

    private sealed record MatchedIntent(
        ToolIntentDefinition Definition,
        IReadOnlyDictionary<string, object?> Slots);
}
