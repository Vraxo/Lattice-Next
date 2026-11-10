using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Lattice.Core.Tools;

namespace Lattice.Core.Intelligence;

public sealed class ManifestToolIntentRecognizer : IToolIntentRecognizer
{
    private const int MaximumChainLength = 8;
    private const int MaximumMatchCandidates = 1024;
    private const int MaximumMatchSteps = 100_000;

    private static readonly Regex ChainSeparator = new(
        @"\s*,?\s+then\s+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

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

        if (TryMatch(definition, input, out var single))
        {
            intent = CreateIntent(single, latestObservation, Array.Empty<MatchedToolIntent>());
            return intent is not null;
        }

        var segments = ChainSeparator.Split(input.Trim());

        if (segments.Length <= 1 || segments.Length > MaximumChainLength ||
            !TryMatch(definition, segments[0], out var first))
        {
            return false;
        }

        var remaining = new List<MatchedToolIntent>();

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
        MatchedToolIntent matched,
        ToolObservation? previous,
        IReadOnlyList<MatchedToolIntent> remaining)
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
        MatchedToolIntent matched,
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
        out MatchedToolIntent matched)
    {
        matched = new MatchedToolIntent(
            definition,
            new Dictionary<string, object?>(StringComparer.Ordinal));

        if (!ToolIntentPatternParser.TryParse(definition.Pattern, out var parts))
        {
            return false;
        }

        var inputTokens = input.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        if (inputTokens.Length == 0 || inputTokens.Length > 256)
        {
            return false;
        }

        var matches = new List<(IReadOnlyDictionary<string, object?> Slots, int OptionalMatches)>();
        var exceededCandidateLimit = false;
        var searchSteps = 0;
        CollectMatches(
            parts,
            inputTokens,
            0,
            0,
            new Dictionary<string, object?>(StringComparer.Ordinal),
            0,
            matches,
            ref exceededCandidateLimit,
            ref searchSteps);

        if (exceededCandidateLimit || matches.Count == 0)
        {
            return false;
        }

        var maximumOptionalMatches = matches.Max(match => match.OptionalMatches);
        var bestMatches = matches
            .Where(match => match.OptionalMatches == maximumOptionalMatches)
            .ToArray();

        if (bestMatches.Length != 1)
        {
            return false;
        }

        matched = new MatchedToolIntent(definition, bestMatches[0].Slots);
        return true;
    }

    private static void CollectMatches(
        IReadOnlyList<ToolIntentPatternPart> parts,
        IReadOnlyList<string> inputTokens,
        int partIndex,
        int inputIndex,
        Dictionary<string, object?> slots,
        int optionalMatches,
        List<(IReadOnlyDictionary<string, object?> Slots, int OptionalMatches)> matches,
        ref bool exceededCandidateLimit,
        ref int searchSteps)
    {
        if (exceededCandidateLimit)
        {
            return;
        }

        searchSteps++;

        if (searchSteps > MaximumMatchSteps)
        {
            exceededCandidateLimit = true;
            return;
        }

        if (partIndex == parts.Count)
        {
            if (inputIndex == inputTokens.Count)
            {
                AddUniqueMatch(matches, slots, optionalMatches, ref exceededCandidateLimit);
            }

            return;
        }

        var part = parts[partIndex];

        if (part.IsOptional)
        {
            if (MatchesLiteralTokens(part.LiteralTokens, inputTokens, inputIndex))
            {
                CollectMatches(
                    parts,
                    inputTokens,
                    partIndex + 1,
                    inputIndex + part.LiteralTokens.Length,
                    slots,
                    optionalMatches + 1,
                    matches,
                    ref exceededCandidateLimit,
                    ref searchSteps);
            }

            CollectMatches(
                parts,
                inputTokens,
                partIndex + 1,
                inputIndex,
                slots,
                optionalMatches,
                matches,
                ref exceededCandidateLimit,
                ref searchSteps);
            return;
        }

        if (part.SlotName is null)
        {
            if (MatchesLiteralTokens(part.LiteralTokens, inputTokens, inputIndex))
            {
                CollectMatches(
                    parts,
                    inputTokens,
                    partIndex + 1,
                    inputIndex + part.LiteralTokens.Length,
                    slots,
                    optionalMatches,
                    matches,
                    ref exceededCandidateLimit,
                    ref searchSteps);
            }

            return;
        }

        if (part.SlotType == "text")
        {
            for (var length = 1;
                length <= inputTokens.Count - inputIndex && !exceededCandidateLimit;
                length++)
            {
                var captured = string.Join(
                    " ",
                    inputTokens.Skip(inputIndex).Take(length))
                    .TrimEnd(',', '.', '?', '!', ';', ':');

                if (string.IsNullOrWhiteSpace(captured))
                {
                    continue;
                }

                slots[part.SlotName] = captured;
                CollectMatches(
                    parts,
                    inputTokens,
                    partIndex + 1,
                    inputIndex + length,
                    slots,
                    optionalMatches,
                    matches,
                    ref exceededCandidateLimit,
                    ref searchSteps);
                slots.Remove(part.SlotName);
            }

            return;
        }

        if (inputIndex >= inputTokens.Count ||
            !TryReadSlot(NormalizeToken(inputTokens[inputIndex]), part.SlotType!, out var value))
        {
            return;
        }

        slots[part.SlotName] = value;
        CollectMatches(
            parts,
            inputTokens,
            partIndex + 1,
            inputIndex + 1,
            slots,
            optionalMatches,
            matches,
            ref exceededCandidateLimit,
            ref searchSteps);
        slots.Remove(part.SlotName);
    }

    private static bool MatchesLiteralTokens(
        IReadOnlyList<string> expected,
        IReadOnlyList<string> actual,
        int inputIndex)
    {
        if (expected.Count > actual.Count - inputIndex)
        {
            return false;
        }

        for (var index = 0; index < expected.Count; index++)
        {
            var expectedToken = NormalizeToken(expected[index]);
            var actualToken = NormalizeToken(actual[inputIndex + index]);

            if (!string.Equals(expectedToken, actualToken, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static void AddUniqueMatch(
        List<(IReadOnlyDictionary<string, object?> Slots, int OptionalMatches)> matches,
        Dictionary<string, object?> slots,
        int optionalMatches,
        ref bool exceededCandidateLimit)
    {
        for (var index = 0; index < matches.Count; index++)
        {
            var existing = matches[index];

            if (existing.Slots.Count != slots.Count ||
                existing.Slots.Any(pair =>
                    !slots.TryGetValue(pair.Key, out var value) ||
                    !Equals(pair.Value, value)))
            {
                continue;
            }

            if (optionalMatches > existing.OptionalMatches)
            {
                matches[index] = (
                    new Dictionary<string, object?>(slots, StringComparer.Ordinal),
                    optionalMatches);
            }

            return;
        }

        if (matches.Count >= MaximumMatchCandidates)
        {
            exceededCandidateLimit = true;
            return;
        }

        matches.Add((
            new Dictionary<string, object?>(slots, StringComparer.Ordinal),
            optionalMatches));
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
}
