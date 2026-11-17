using System.Collections.Immutable;
using System.Globalization;
using Lattice.Client;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;

namespace Lattice.Runtime;

internal static class SemanticResponseFormatting
{

internal static string PluralizeType(string value)
    {
        if (value.EndsWith('y') && value.Length > 1 &&
            !"aeiou".Contains(char.ToLowerInvariant(value[^2])))
        {
            return $"{value[..^1]}ies";
        }

        return value.EndsWith("s", StringComparison.OrdinalIgnoreCase)
            ? $"{value}es"
            : $"{value}s";
    }

internal static bool MatchesVerb(string relationVerb, string requestedVerb)
    {
        var relationHead = relationVerb.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        return string.Equals(
            NormalizeVerb(relationHead),
            NormalizeVerb(requestedVerb),
            StringComparison.Ordinal);
    }

internal static string NormalizeVerb(string value)
    {
        var verb = SemanticWorldModel.Normalize(value);
        return verb switch
        {
            "has" => "have",
            "does" => "do",
            _ when verb.EndsWith("ies", StringComparison.Ordinal) => $"{verb[..^3]}y",
            _ when verb.EndsWith("ed", StringComparison.Ordinal) => verb[..^2],
            _ when verb.EndsWith("s", StringComparison.Ordinal) => verb[..^1],
            _ => verb
        };
    }

internal static string JoinAlternatives(IReadOnlyList<string> phrases) =>
        phrases.Count switch
        {
            1 => phrases[0],
            2 => $"{phrases[0]} or {phrases[1]}",
            _ => $"{string.Join(", ", phrases.Take(phrases.Count - 1))}, or {phrases[^1]}"
        };

internal static string? FormatCondition(SemanticRestriction condition, SemanticWorldModel model)
    {
        var relation = model.FindRelation(condition.Relation);
        var target = model.FindConcept(condition.TargetType, SemanticConceptKind.Type);
        if (relation is null || target is null)
        {
            return null;
        }

        var phrase = $"{relation.Verb} {IndefinitePhrase(target.Label)}";
        return relation.ObjectRole is null
            ? phrase
            : $"{phrase} {relation.ObjectRole}";
    }

internal static string FormatRelation(
        SemanticConcept subject,
        SemanticConcept target,
        SemanticRelationDefinition relation,
        bool first)
    {
        var prefix = first ? Capitalize(IndefinitePhrase(subject.Label)) : $"The {subject.Label}";
        var objectPhrase = relation.ObjectRole is null
            ? $"the {target.Label}"
            : $"{IndefinitePhrase(target.Label)} {relation.ObjectRole}";
        var verb = subject.Label.EndsWith('s') && subject.Label.Length > 1
            ? InflectPluralSubjectVerb(relation.Verb)
            : relation.Verb;
        return $"{prefix} {verb} {objectPhrase}.";
    }

internal static string FormatRelationTarget(SemanticRelationDefinition relation, string targetLabel) =>
        relation.ObjectRole is null
            ? $"{relation.Verb} {Indefinite(targetLabel)} {targetLabel}"
            : $"{relation.Verb} {Indefinite(targetLabel)} {targetLabel} {relation.ObjectRole}";

internal static string InflectPluralSubjectVerb(string verbPhrase)
    {
        var separator = verbPhrase.IndexOf(' ');
        var verb = separator < 0 ? verbPhrase : verbPhrase[..separator];
        var remainder = separator < 0 ? string.Empty : verbPhrase[separator..];
        var baseVerb = verb switch
        {
            "has" => "have",
            "does" => "do",
            _ when verb.EndsWith("ies", StringComparison.Ordinal) => $"{verb[..^3]}y",
            _ when verb.EndsWith("s", StringComparison.Ordinal) => verb[..^1],
            _ => verb
        };
        return $"{baseVerb}{remainder}";
    }

internal static string JoinConditions(IReadOnlyList<string> phrases) =>
        phrases.Count switch
        {
            1 => phrases[0],
            2 => $"{phrases[0]} and {phrases[1]}",
            _ => $"{string.Join(", ", phrases.Take(phrases.Count - 1))}, and {phrases[^1]}"
        };

internal static string JoinNames(IReadOnlyList<string> names) =>
        names.Count switch
        {
            0 => string.Empty,
            1 => names[0],
            2 => $"{names[0]} and {names[1]}",
            _ => $"{string.Join(", ", names.Take(names.Count - 1))}, and {names[^1]}"
        };

internal static string Indefinite(string phrase)
    {
        if (IsMassNoun(phrase) || StartsWithPluralNoun(phrase))
        {
            return string.Empty;
        }

        return phrase.StartsWith("useful", StringComparison.OrdinalIgnoreCase) ||
               phrase.StartsWith("user", StringComparison.OrdinalIgnoreCase) ||
               phrase.StartsWith("university", StringComparison.OrdinalIgnoreCase)
            ? "a"
            : phrase.Length > 0 && "aeiou".Contains(char.ToLowerInvariant(phrase[0]))
                ? "an"
                : "a";
    }

internal static bool StartsWithPluralNoun(string phrase)
    {
        var firstWord = phrase.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return firstWord is not null &&
            firstWord.EndsWith('s') &&
            firstWord is not ("business" or "status" or "analysis" or "news" or "series" or "species");
    }

internal static string IndefinitePhrase(string phrase)
    {
        var article = Indefinite(phrase);
        return article.Length == 0 ? phrase : $"{article} {phrase}";
    }

internal static bool IsMassNoun(string phrase) =>
        phrase is "monopsony power" or "buyer market power" or "market power" or "software" or "straightness";

internal static string Capitalize(string value) =>
        value.Length == 0 ? value : char.ToUpperInvariant(value[0]) + value[1..];

internal static string FormatScientific(double value)
    {
        if (value == 0)
        {
            return "0";
        }

        var exponent = (int)Math.Floor(Math.Log10(Math.Abs(value)));
        var coefficient = value / Math.Pow(10, exponent);
        return $"{coefficient.ToString("0.00", CultureInfo.InvariantCulture)} × 10^{exponent}";
    }

internal static string FormatPercentage(double probability) =>
        $"{(probability * 100).ToString("0.0", CultureInfo.InvariantCulture)}%";

internal static LatticeEvidence ToEvidence(
        ImmutableArray<SemanticWorldModelSource> sources,
        LatticeEvidenceKind kind) =>
        new(
            kind,
            LatticeEvidencePolarity.Positive,
            sources.Select(source => new LatticeEvidenceSource(
                LatticeKnowledgeSourceKind.FoundationalKnowledge,
                source.SourceId,
                source.SourceId,
                null,
                null)).ToImmutableArray(),
            ImmutableArray<LatticeEvidence>.Empty);

internal static bool Fail(
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;
        return false;
    }
}
}
