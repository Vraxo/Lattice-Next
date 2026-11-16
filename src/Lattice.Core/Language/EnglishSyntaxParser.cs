using System.Collections.Immutable;

namespace Lattice.Core.Language;

/// <summary>
/// Builds a shallow, reusable constituent structure from lexical tokens.
/// It identifies clause predicates, noun phrases, and prepositional phrases
/// without assigning domain-specific meaning to them.
/// </summary>
internal static class EnglishSyntaxParser
{
    private static readonly HashSet<string> AdditionalVerbs = new(StringComparer.OrdinalIgnoreCase)
    {
        "create", "generate", "print", "display", "show", "output", "say",
        "explain", "recommend", "plan", "learn", "start", "operate"
    };

    private static readonly HashSet<string> Prepositions = new(StringComparer.OrdinalIgnoreCase)
    {
        "about", "as", "at", "by", "for", "from", "in", "into", "of", "on",
        "through", "to", "using", "with", "without"
    };

    private static readonly HashSet<string> RelativeMarkers = new(StringComparer.OrdinalIgnoreCase)
    {
        "that", "which", "who", "whom", "whose"
    };

    private static readonly HashSet<string> CoordinatingMarkers = new(StringComparer.OrdinalIgnoreCase)
    {
        "and", "but", "or"
    };

    private static readonly HashSet<string> SubordinatingMarkers = new(StringComparer.OrdinalIgnoreCase)
    {
        "because", "although", "while", "when", "if"
    };

    private static readonly HashSet<string> LeadingAuxiliaries = new(StringComparer.OrdinalIgnoreCase)
    {
        "can", "could", "would", "will", "should", "may", "might", "must",
        "please", "do", "does"
    };

    internal static bool TryParse(string source, out EnglishSentenceSyntax? syntax)
    {
        ArgumentNullException.ThrowIfNull(source);
        syntax = null;

        var tokens = EnglishLexer.Tokenize(source)
            .Where(token => !token.IsPunctuation)
            .ToImmutableArray();

        if (tokens.IsDefaultOrEmpty)
        {
            return false;
        }

        var verbIndexes = FindVerbIndexes(tokens);
        if (verbIndexes.Count == 0)
        {
            return false;
        }

        var clauses = ImmutableArray.CreateBuilder<EnglishClause>();

        for (var index = 0; index < verbIndexes.Count; index++)
        {
            var verbIndex = verbIndexes[index];
            var previousVerbIndex = index == 0 ? -1 : verbIndexes[index - 1];
            var markerIndex = index == 0
                ? -1
                : FindClauseMarker(tokens, verbIndex, previousVerbIndex);
            var kind = index == 0
                ? EnglishClauseKind.Main
                : GetClauseKind(tokens, markerIndex);

            var clauseStart = index == 0 ? 0 : markerIndex + 1;
            if (clauseStart < 0 || clauseStart > verbIndex)
            {
                continue;
            }

            var nextMarker = index + 1 < verbIndexes.Count
                ? FindClauseMarker(tokens, verbIndexes[index + 1], verbIndex)
                : tokens.Length;
            if (nextMarker < 0)
            {
                nextMarker = verbIndexes[index + 1] - 1;
            }

            var clauseEnd = Math.Max(verbIndex, nextMarker - 1);
            var subjectStart = clauseStart;
            var subjectEnd = verbIndex;

            if (index == 0)
            {
                while (subjectStart < subjectEnd &&
                    LeadingAuxiliaries.Contains(tokens[subjectStart].Value))
                {
                    subjectStart++;
                }
            }

            var subject = kind is EnglishClauseKind.Relative or EnglishClauseKind.Infinitive
                ? null
                : TryCreateNounPhrase(source, tokens, subjectStart, subjectEnd);

            var objectStart = verbIndex + 1;
            var firstPreposition = FindNextPreposition(tokens, objectStart, clauseEnd + 1);
            var objectEnd = firstPreposition >= 0 ? firstPreposition : clauseEnd + 1;
            var objectPhrase = TryCreateNounPhrase(source, tokens, objectStart, objectEnd);
            var prepositionalPhrases = ParsePrepositionalPhrases(
                source,
                tokens,
                firstPreposition,
                clauseEnd + 1);

            var subjectBinding = subject is not null
                ? EnglishSubjectBinding.Explicit
                : kind switch
                {
                    EnglishClauseKind.Relative => EnglishSubjectBinding.Antecedent,
                    EnglishClauseKind.Infinitive or EnglishClauseKind.Coordinated or EnglishClauseKind.Subordinate
                        => EnglishSubjectBinding.PreviousClause,
                    _ => EnglishSubjectBinding.Implicit
                };

            clauses.Add(new EnglishClause(
                kind,
                GetBaseVerb(tokens[verbIndex].Value),
                subject,
                objectPhrase,
                prepositionalPhrases,
                subjectBinding,
                verbIndex));
        }

        if (clauses.Count == 0)
        {
            return false;
        }

        syntax = new EnglishSentenceSyntax(tokens, clauses.ToImmutable());
        return true;
    }

    private static List<int> FindVerbIndexes(ImmutableArray<EnglishToken> tokens)
    {
        var indexes = new List<int>();

        for (var index = 0; index < tokens.Length; index++)
        {
            if (TryGetBaseVerb(tokens[index].Value, out _))
            {
                indexes.Add(index);
            }
        }

        return indexes;
    }

    private static bool TryGetBaseVerb(string value, out string baseVerb)
    {
        foreach (var candidate in AdditionalVerbs)
        {
            if (MatchesVerbForm(value, candidate))
            {
                baseVerb = candidate;
                return true;
            }
        }

        foreach (var candidate in EnglishLexicon.BaseRelationVerbs)
        {
            if (MatchesVerbForm(value, candidate))
            {
                baseVerb = candidate;
                return true;
            }
        }

        baseVerb = string.Empty;
        return false;
    }

    private static bool MatchesVerbForm(string value, string baseVerb)
    {
        if (value.Equals(baseVerb, StringComparison.OrdinalIgnoreCase) ||
            value.Equals(EnglishMorphology.InflectThirdPersonSingular(baseVerb), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var gerund = baseVerb.EndsWith("e", StringComparison.OrdinalIgnoreCase)
            ? $"{baseVerb[..^1]}ing"
            : $"{baseVerb}ing";

        return value.Equals(gerund, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetBaseVerb(string value) =>
        TryGetBaseVerb(value, out var baseVerb) ? baseVerb : value.ToLowerInvariant();

    private static int FindClauseMarker(
        ImmutableArray<EnglishToken> tokens,
        int verbIndex,
        int previousVerbIndex)
    {
        for (var index = verbIndex - 1; index > previousVerbIndex; index--)
        {
            var marker = tokens[index].Value;
            var isMarker = RelativeMarkers.Contains(marker) ||
                CoordinatingMarkers.Contains(marker) ||
                SubordinatingMarkers.Contains(marker) ||
                marker.Equals("to", StringComparison.OrdinalIgnoreCase);

            if (!isMarker)
            {
                continue;
            }

            if (index == verbIndex - 1 || IsExplicitSubjectStart(tokens[index + 1]))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool IsExplicitSubjectStart(EnglishToken token) =>
        token.Value is "I" or "you" or "he" or "she" or "it" or "we" or "they" ||
        (token.Value.Length > 0 && char.IsUpper(token.Value[0])) ||
        token.Value.Equals("the", StringComparison.OrdinalIgnoreCase) ||
        token.Value.Equals("a", StringComparison.OrdinalIgnoreCase) ||
        token.Value.Equals("an", StringComparison.OrdinalIgnoreCase);

    private static EnglishClauseKind GetClauseKind(
        ImmutableArray<EnglishToken> tokens,
        int markerIndex)
    {
        if (markerIndex < 0)
        {
            return EnglishClauseKind.Coordinated;
        }

        var marker = tokens[markerIndex].Value;
        if (RelativeMarkers.Contains(marker))
        {
            return EnglishClauseKind.Relative;
        }

        if (marker.Equals("to", StringComparison.OrdinalIgnoreCase))
        {
            return EnglishClauseKind.Infinitive;
        }

        if (CoordinatingMarkers.Contains(marker))
        {
            return EnglishClauseKind.Coordinated;
        }

        return EnglishClauseKind.Subordinate;
    }

    private static int FindNextPreposition(
        ImmutableArray<EnglishToken> tokens,
        int start,
        int end)
    {
        for (var index = start; index < end; index++)
        {
            if (Prepositions.Contains(tokens[index].Value))
            {
                return index;
            }
        }

        return -1;
    }

    private static ImmutableArray<EnglishPrepositionalPhrase> ParsePrepositionalPhrases(
        string source,
        ImmutableArray<EnglishToken> tokens,
        int firstPreposition,
        int end)
    {
        if (firstPreposition < 0)
        {
            return ImmutableArray<EnglishPrepositionalPhrase>.Empty;
        }

        var phrases = ImmutableArray.CreateBuilder<EnglishPrepositionalPhrase>();
        var index = firstPreposition;

        while (index < end)
        {
            if (!Prepositions.Contains(tokens[index].Value))
            {
                index++;
                continue;
            }

            var objectStart = index + 1;
            var nextPreposition = FindNextPreposition(tokens, objectStart, end);
            var objectEnd = nextPreposition >= 0 ? nextPreposition : end;
            var objectPhrase = TryCreateNounPhrase(source, tokens, objectStart, objectEnd);

            if (objectPhrase is not null)
            {
                phrases.Add(new EnglishPrepositionalPhrase(
                    tokens[index].Value.ToLowerInvariant(),
                    objectPhrase,
                    index,
                    objectEnd));
            }

            index = objectEnd;
        }

        return phrases.ToImmutable();
    }

    private static EnglishNounPhrase? TryCreateNounPhrase(
        string source,
        ImmutableArray<EnglishToken> tokens,
        int start,
        int end)
    {
        if (start >= end)
        {
            return null;
        }

        var determiner = tokens[start].Value.ToLowerInvariant() is "a" or "an" or "the"
            ? tokens[start].Value.ToLowerInvariant()
            : null;
        var headToken = tokens[end - 1];
        var modifiers = tokens
            .Skip(start)
            .Take(end - start - 1)
            .Where((_, index) => !(index == 0 && determiner is not null))
            .Select(token => token.Value)
            .ToImmutableArray();

        var text = start + 1 == end && headToken.IsQuoted
            ? headToken.Value
            : source[tokens[start].Start..(headToken.Start + headToken.Length)].Trim();

        return new EnglishNounPhrase(
            text,
            headToken.Value,
            determiner,
            modifiers,
            start,
            end - 1);
    }
}
