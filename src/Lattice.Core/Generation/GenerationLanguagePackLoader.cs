using System.Collections.Immutable;
using System.Text.Json;

namespace Lattice.Core.Generation;

public sealed class GenerationLanguagePackLoader
{
    private const string ResourcePrefix = "Lattice.Core.GenerationKnowledge.";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow
    };

    public bool TryLoad(
        string? json,
        out GenerationLanguagePack? pack,
        out string? error)
    {
        pack = null;
        error = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            error = "The generation language pack is empty.";
            return false;
        }

        try
        {
            pack = JsonSerializer.Deserialize<GenerationLanguagePack>(json, JsonOptions);
        }
        catch (JsonException)
        {
            error = "The generation language pack is invalid JSON or contains an unsupported field.";
            return false;
        }

        if (pack is null ||
            pack.FormatVersion != 1 ||
            !IsIdentifier(pack.Language) ||
            !IsIdentifier(pack.StartSymbol) ||
            pack.Productions.IsDefaultOrEmpty ||
            pack.Productions.Length > 128 ||
            pack.Bindings.IsDefaultOrEmpty ||
            pack.Bindings.Length > 32 ||
            pack.Operations.IsDefaultOrEmpty ||
            pack.Operations.Length > 128 ||
            pack.StringLiteralFormat is null ||
            string.IsNullOrEmpty(pack.StringLiteralFormat.OpeningDelimiter) ||
            string.IsNullOrEmpty(pack.StringLiteralFormat.ClosingDelimiter) ||
            pack.StringLiteralFormat.OpeningDelimiter.Length > 4 ||
            pack.StringLiteralFormat.ClosingDelimiter.Length > 4 ||
            pack.StringLiteralFormat.Escapes.IsDefault ||
            (pack.CollectionLiteralFormat is { } collectionFormat &&
                !IsValidCollectionLiteralFormat(collectionFormat)) ||
            (pack.InputSyntax is { } inputSyntax &&
                !IsValidInputSyntax(inputSyntax)) ||
            (pack.InterpolationFormat is { } interpolationFormat &&
                !IsValidInterpolationFormat(interpolationFormat)) ||
            (!pack.ConversionFormats.IsDefaultOrEmpty &&
                !IsValidConversionFormats(pack.ConversionFormats)))
        {
            error = "The generation language pack metadata or collection sizes are invalid.";
            pack = null;
            return false;
        }

        var productions = new Dictionary<string, GenerationProduction>(StringComparer.Ordinal);
        foreach (var production in pack.Productions)
        {
            if (production is null ||
                !IsIdentifier(production.Head) ||
                production.Symbols.IsDefaultOrEmpty ||
                production.Symbols.Length > 64 ||
                !productions.TryAdd(production.Head, production))
            {
                error = "Every grammar production requires a unique head and a non-empty symbol sequence.";
                pack = null;
                return false;
            }

            foreach (var symbol in production.Symbols)
            {
                if (symbol is null ||
                    symbol.Kind is not ("terminal" or "nonterminal" or "binding") ||
                    string.IsNullOrEmpty(symbol.Value) ||
                    symbol.Value.Length > 2048)
                {
                    error = "A grammar symbol has an unsupported kind or invalid value.";
                    pack = null;
                    return false;
                }
            }
        }

        if (!productions.ContainsKey(pack.StartSymbol))
        {
            error = "The grammar start symbol has no production.";
            pack = null;
            return false;
        }

        var bindings = new Dictionary<string, GenerationBinding>(StringComparer.Ordinal);
        foreach (var binding in pack.Bindings)
        {
            if (binding is null ||
                !IsIdentifier(binding.Name) ||
                binding.Source is not ("selected-operation" or "goal-argument" or "goal-binding" or "goal-collection") ||
                binding.Encoding is not ("none" or "string-literal" or "collection-literal") ||
                ((binding.Source == "goal-binding" || binding.Source == "goal-collection") &&
                    !IsIdentifier(binding.Key)) ||
                (binding.Source != "goal-binding" && binding.Source != "goal-collection" &&
                    binding.Key is not null) ||
                (binding.Source == "goal-collection" && binding.Encoding != "collection-literal") ||
                (binding.Encoding == "collection-literal" && binding.Source != "goal-collection") ||
                !bindings.TryAdd(binding.Name, binding))
            {
                error = "Every generation binding requires a unique name and a supported source and encoding.";
                pack = null;
                return false;
            }

            if (binding.Encoding == "string-literal" && binding.Source is not ("goal-argument" or "goal-binding"))
            {
                error = "String-literal encoding can only be applied to a semantic goal argument or named goal binding.";
                pack = null;
                return false;
            }
        }

        if (bindings.Values.Any(binding => binding.Encoding == "collection-literal") &&
            pack.CollectionLiteralFormat is null)
        {
            error = "A collection-literal binding requires a declarative collection format in the language pack.";
            pack = null;
            return false;
        }

        foreach (var symbol in pack.Productions.SelectMany(production => production.Symbols))
        {
            if ((symbol.Kind == "nonterminal" && !productions.ContainsKey(symbol.Value)) ||
                (symbol.Kind == "binding" && !bindings.ContainsKey(symbol.Value)))
            {
                error = "The grammar references an undefined nonterminal or binding.";
                pack = null;
                return false;
            }
        }

        var grammarBindings = pack.Productions
            .SelectMany(production => production.Symbols)
            .Where(symbol => symbol.Kind == "binding")
            .Select(symbol => symbol.Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal);
        if (!grammarBindings.SequenceEqual(bindings.Keys.OrderBy(name => name, StringComparer.Ordinal)))
        {
            error = "Every declared generation binding must be used by the grammar, and every grammar binding must be declared.";
            pack = null;
            return false;
        }

        var operationIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var operation in pack.Operations)
        {
            if (operation is null ||
                !IsIdentifier(operation.Id) ||
                string.IsNullOrWhiteSpace(operation.Symbol) ||
                operation.Symbol.Length > 128 ||
                !IsIdentifier(operation.Effect) ||
                !IsIdentifier(operation.ArgumentKind) ||
                operation.Prerequisites.IsDefault ||
                operation.Prerequisites.Any(prerequisite => !IsCapabilityIdentifier(prerequisite)) ||
                (operation.StartSymbol is not null &&
                    (!IsIdentifier(operation.StartSymbol) || !productions.ContainsKey(operation.StartSymbol))) ||
                !operationIds.Add(operation.Id))
            {
                error = "Every generation operation requires a unique identifier, symbol, effect, argument kind, and valid prerequisite list.";
                pack = null;
                return false;
            }
        }

        var escapeCharacters = new HashSet<string>(StringComparer.Ordinal);
        foreach (var escape in pack.StringLiteralFormat.Escapes)
        {
            if (escape is null ||
                escape.Character is null ||
                escape.Character.Length != 1 ||
                escape.Replacement is null ||
                !escapeCharacters.Add(escape.Character))
            {
                error = "String-literal escape rules must map unique single characters to replacement text.";
                pack = null;
                return false;
            }
        }

        return true;
    }

    public bool TryLoadEmbedded(
        out ImmutableDictionary<string, GenerationLanguagePack> packs,
        out string? error)
    {
        packs = ImmutableDictionary.Create<string, GenerationLanguagePack>(StringComparer.OrdinalIgnoreCase);
        error = null;

        var assembly = typeof(GenerationLanguagePackLoader).Assembly;
        var resources = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal) &&
                name.EndsWith(".json", StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        if (resources.Length == 0)
        {
            error = "No embedded generation language packs were found.";
            return false;
        }

        var builder = ImmutableDictionary.CreateBuilder<string, GenerationLanguagePack>(StringComparer.OrdinalIgnoreCase);
        foreach (var resource in resources)
        {
            using var stream = assembly.GetManifestResourceStream(resource);
            if (stream is null)
            {
                error = $"The embedded generation language pack '{resource}' is missing.";
                return false;
            }

            using var reader = new StreamReader(stream);
            if (!TryLoad(reader.ReadToEnd(), out var pack, out error))
            {
                return false;
            }

            if (builder.ContainsKey(pack!.Language))
            {
                error = $"More than one embedded generation pack declares language '{pack.Language}'.";
                return false;
            }

            builder.Add(pack.Language, pack);
        }

        packs = builder.ToImmutable();
        return true;
    }

    private static bool IsValidCollectionLiteralFormat(GenerationCollectionLiteralFormat format)
    {
        if (string.IsNullOrWhiteSpace(format.OpeningDelimiter) ||
            format.OpeningDelimiter.Length > 32 ||
            string.IsNullOrWhiteSpace(format.ClosingDelimiter) ||
            format.ClosingDelimiter.Length > 32 ||
            string.IsNullOrWhiteSpace(format.ItemSeparator) ||
            format.ItemSeparator.Length > 32 ||
            format.ItemFormats.IsDefaultOrEmpty ||
            format.ItemFormats.Length > 8)
        {
            return false;
        }

        var itemEncodings = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var itemFormat in format.ItemFormats)
        {
            if (itemFormat is null ||
                itemFormat.Kind is not ("number" or "text") ||
                itemFormat.Encoding is not ("none" or "string-literal") ||
                (itemFormat.Kind == "number" && itemFormat.Encoding != "none") ||
                (itemFormat.Kind == "text" && itemFormat.Encoding != "string-literal") ||
                !itemEncodings.TryAdd(itemFormat.Kind, itemFormat.Encoding))
            {
                return false;
            }
        }

        return itemEncodings.ContainsKey("number") && itemEncodings.ContainsKey("text");
    }

    private static bool IsValidInputSyntax(GenerationInputSyntax syntax)
    {
        if (string.IsNullOrEmpty(syntax.IdentifierStartCharacters) ||
            syntax.IdentifierStartCharacters.Length > 128 ||
            string.IsNullOrEmpty(syntax.IdentifierContinuationCharacters) ||
            syntax.IdentifierContinuationCharacters.Length > 128 ||
            syntax.IdentifierStartCharacters.Any(char.IsWhiteSpace) ||
            syntax.IdentifierContinuationCharacters.Any(char.IsWhiteSpace) ||
            syntax.IdentifierStartCharacters.Distinct().Count() != syntax.IdentifierStartCharacters.Length ||
            syntax.IdentifierContinuationCharacters.Distinct().Count() != syntax.IdentifierContinuationCharacters.Length ||
            syntax.IdentifierStartCharacters.Any(character => !syntax.IdentifierContinuationCharacters.Contains(character)) ||
            !IsValidNumberLiteralFormat(syntax.NumberLiteralFormat) ||
            (syntax.BooleanLiteralFormat is { } booleanFormat && !IsValidBooleanLiteralFormat(booleanFormat, syntax)) ||
            syntax.StringLiteralFormats.IsDefaultOrEmpty ||
            syntax.StringLiteralFormats.Length > 8 ||
            !IsValidFunctionCallFormat(syntax.FunctionCallFormat) ||
            (syntax.MemberAccessFormat is { } memberAccessFormat &&
                !IsValidMemberAccessFormat(memberAccessFormat, syntax)) ||
            syntax.GroupingDelimiters.IsDefault ||
            syntax.GroupingDelimiters.Length > 16 ||
            syntax.ExpressionOperators.IsDefaultOrEmpty ||
            syntax.ExpressionOperators.Length > 32 ||
            !IsValidExpressionOperators(syntax.ExpressionOperators) ||
            !IsValidExpressionGroupingDelimiter(syntax.ExpressionGroupingDelimiter))
        {
            return false;
        }

        if (syntax.BooleanLiteralFormat is { } booleanLiteralFormat &&
            syntax.ExpressionOperators.Any(operation =>
                operation.Token == booleanLiteralFormat.TrueLiteral ||
                operation.Token == booleanLiteralFormat.FalseLiteral))
        {
            return false;
        }

        var literalKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var format in syntax.StringLiteralFormats)
        {
            if (format is null ||
                string.IsNullOrEmpty(format.OpeningDelimiter) ||
                format.OpeningDelimiter.Length > 8 ||
                string.IsNullOrEmpty(format.ClosingDelimiter) ||
                format.ClosingDelimiter.Length > 8 ||
                string.IsNullOrEmpty(format.EscapePrefix) ||
                format.EscapePrefix.Length > 4 ||
                format.EscapeSequences.IsDefault ||
                format.EscapeSequences.Length > 32 ||
                !literalKeys.Add($"{format.OpeningDelimiter}\\0{format.ClosingDelimiter}"))
            {
                return false;
            }

            var escapeSequences = new HashSet<string>(StringComparer.Ordinal);
            if (format.EscapeSequences.Any(sequence =>
                    sequence is null ||
                    string.IsNullOrEmpty(sequence.Sequence) ||
                    sequence.Sequence.Length > 16 ||
                    sequence.Value is null ||
                    sequence.Value.Length > 16 ||
                    !sequence.Sequence.StartsWith(format.EscapePrefix, StringComparison.Ordinal) ||
                    sequence.Sequence.Length == format.EscapePrefix.Length ||
                    !escapeSequences.Add(sequence.Sequence)))
            {
                return false;
            }
        }

        var delimiterOpenings = new HashSet<string>(StringComparer.Ordinal)
        {
            syntax.FunctionCallFormat.OpeningDelimiter
        };
        var delimiterClosings = new HashSet<string>(StringComparer.Ordinal)
        {
            syntax.FunctionCallFormat.ClosingDelimiter
        };
        foreach (var delimiter in syntax.GroupingDelimiters)
        {
            if (delimiter is null ||
                string.IsNullOrEmpty(delimiter.OpeningDelimiter) ||
                delimiter.OpeningDelimiter.Length > 16 ||
                string.IsNullOrEmpty(delimiter.ClosingDelimiter) ||
                delimiter.ClosingDelimiter.Length > 16 ||
                delimiter.OpeningDelimiter == delimiter.ClosingDelimiter ||
                !delimiterOpenings.Add(delimiter.OpeningDelimiter) ||
                !delimiterClosings.Add(delimiter.ClosingDelimiter))
            {
                return false;
            }
        }

        var expressionGroupingDelimiter = syntax.ExpressionGroupingDelimiter!;
        var declaredDelimiters = syntax.GroupingDelimiters.Add(new GenerationDelimiterPair(
            syntax.FunctionCallFormat.OpeningDelimiter,
            syntax.FunctionCallFormat.ClosingDelimiter));
        if (declaredDelimiters.Any(delimiter =>
                delimiter != expressionGroupingDelimiter &&
                (delimiter.OpeningDelimiter == expressionGroupingDelimiter.OpeningDelimiter ||
                 delimiter.ClosingDelimiter == expressionGroupingDelimiter.ClosingDelimiter ||
                 delimiter.OpeningDelimiter == expressionGroupingDelimiter.ClosingDelimiter ||
                 delimiter.ClosingDelimiter == expressionGroupingDelimiter.OpeningDelimiter)))
        {
            return false;
        }

        return true;
    }

    private static bool IsValidInterpolationFormat(GenerationInterpolationFormat format) =>
        format is not null &&
        !string.IsNullOrEmpty(format.Opening) &&
        format.Opening.Length <= 8 &&
        !string.IsNullOrEmpty(format.PlaceholderOpening) &&
        format.PlaceholderOpening.Length <= 4 &&
        !string.IsNullOrEmpty(format.PlaceholderClosing) &&
        format.PlaceholderClosing.Length <= 4 &&
        !string.IsNullOrEmpty(format.Closing) &&
        format.Closing.Length <= 8;

    private static bool IsValidConversionFormats(ImmutableArray<GenerationConversionFormat> formats) =>
        formats.Length <= 8 &&
        formats.All(format =>
            format is not null &&
            Enum.TryParse<SemanticValueKind>(format.Target, ignoreCase: false, out var target) &&
            Enum.IsDefined(target) &&
            target is SemanticValueKind.Number or SemanticValueKind.Text &&
            !string.IsNullOrEmpty(format.Format) &&
            format.Format.Length <= 64 &&
            format.Format.Contains("{0}", StringComparison.Ordinal)) &&
        formats
            .Select(format => format.Target)
            .Distinct(StringComparer.Ordinal)
            .Count() == formats.Length;

    private static bool IsValidMemberAccessFormat(
        GenerationMemberAccessFormat format,
        GenerationInputSyntax syntax) =>
        !string.IsNullOrEmpty(format.Separator) &&
        format.Separator.Length <= 4 &&
        !format.Separator.Any(char.IsWhiteSpace) &&
        !format.Separator.Any(character =>
            syntax.IdentifierStartCharacters.Contains(character) ||
            syntax.IdentifierContinuationCharacters.Contains(character));

    private static bool IsValidExpressionGroupingDelimiter(GenerationDelimiterPair? delimiter) =>
        delimiter is not null &&
        !string.IsNullOrEmpty(delimiter.OpeningDelimiter) &&
        delimiter.OpeningDelimiter.Length <= 16 &&
        !string.IsNullOrEmpty(delimiter.ClosingDelimiter) &&
        delimiter.ClosingDelimiter.Length <= 16 &&
        delimiter.OpeningDelimiter != delimiter.ClosingDelimiter;

    private static bool IsValidBooleanLiteralFormat(
        GenerationBooleanLiteralFormat format,
        GenerationInputSyntax syntax) =>
        !string.IsNullOrWhiteSpace(format.TrueLiteral) &&
        format.TrueLiteral.Length <= 32 &&
        GenerationInputSyntaxParser.IsIdentifier(format.TrueLiteral, syntax) &&
        !string.IsNullOrWhiteSpace(format.FalseLiteral) &&
        format.FalseLiteral.Length <= 32 &&
        GenerationInputSyntaxParser.IsIdentifier(format.FalseLiteral, syntax) &&
        !string.Equals(format.TrueLiteral, format.FalseLiteral, StringComparison.Ordinal);

    private static bool IsValidNumberLiteralFormat(GenerationNumberLiteralFormat format)
    {
        return format is not null &&
            !string.IsNullOrEmpty(format.Digits) &&
            format.Digits.Length == 10 &&
            format.Digits.Distinct().Count() == format.Digits.Length &&
            !format.Digits.Any(char.IsWhiteSpace) &&
            !string.IsNullOrEmpty(format.DecimalSeparator) &&
            format.DecimalSeparator.Length <= 8 &&
            !string.IsNullOrEmpty(format.PositiveSign) &&
            format.PositiveSign.Length <= 4 &&
            !string.IsNullOrEmpty(format.NegativeSign) &&
            format.NegativeSign.Length <= 4 &&
            format.PositiveSign != format.NegativeSign &&
            !format.Digits.Contains(format.DecimalSeparator, StringComparison.Ordinal) &&
            !format.Digits.Contains(format.PositiveSign, StringComparison.Ordinal) &&
            !format.Digits.Contains(format.NegativeSign, StringComparison.Ordinal) &&
            !format.DecimalSeparator.Contains(format.PositiveSign, StringComparison.Ordinal) &&
            !format.DecimalSeparator.Contains(format.NegativeSign, StringComparison.Ordinal);
    }

    private static bool IsValidFunctionCallFormat(GenerationFunctionCallFormat format) =>
        format is not null &&
        !string.IsNullOrEmpty(format.OpeningDelimiter) && format.OpeningDelimiter.Length <= 16 &&
        !string.IsNullOrEmpty(format.ClosingDelimiter) && format.ClosingDelimiter.Length <= 16 &&
        !string.IsNullOrEmpty(format.ArgumentSeparator) && format.ArgumentSeparator.Length <= 16 &&
        (format.RenderingArgumentSeparator is null ||
         format.RenderingArgumentSeparator.Length is > 0 and <= 16) &&
        format.OpeningDelimiter != format.ClosingDelimiter &&
        format.OpeningDelimiter != format.ArgumentSeparator &&
        format.ClosingDelimiter != format.ArgumentSeparator;

    private static bool IsValidExpressionOperators(
        ImmutableArray<GenerationInputExpressionOperatorFormat> operators)
    {
        var supportedKinds = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["add"] = "infix",
            ["subtract"] = "infix",
            ["multiply"] = "infix",
            ["divide"] = "infix",
            ["remainder"] = "infix",
            ["equal"] = "infix",
            ["notEqual"] = "infix",
            ["lessThan"] = "infix",
            ["lessThanOrEqual"] = "infix",
            ["greaterThan"] = "infix",
            ["greaterThanOrEqual"] = "infix",
            ["and"] = "infix",
            ["or"] = "infix",
            ["not"] = "prefix",
            ["positive"] = "prefix",
            ["negative"] = "prefix"
        };
        var uniqueKinds = new HashSet<string>(StringComparer.Ordinal);
        var uniqueTokens = new HashSet<string>(StringComparer.Ordinal);
        foreach (var format in operators)
        {
            if (format is null ||
                string.IsNullOrEmpty(format.Kind) ||
                string.IsNullOrEmpty(format.Fixity) ||
                !supportedKinds.TryGetValue(format.Kind, out var expectedFixity) ||
                format.Fixity != expectedFixity ||
                string.IsNullOrEmpty(format.Token) ||
                format.Token.Length > 8 ||
                format.Token.Any(char.IsWhiteSpace) ||
                format.Precedence is < 1 or > 100 ||
                !uniqueKinds.Add($"{format.Kind}:{format.Fixity}") ||
                !uniqueTokens.Add($"{format.Fixity}:{format.Token}"))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsCapabilityIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 120)
        {
            return false;
        }

        var segments = value.Split(':');
        return segments.Length is 1 or 2 && segments.All(IsIdentifier);
    }

    private static bool IsIdentifier(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= 80 &&
        value.All(character =>
            char.IsAsciiLetterOrDigit(character) ||
            character is '-' or '_');
}
