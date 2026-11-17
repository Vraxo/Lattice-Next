using System.Text.RegularExpressions;
using Lattice.Core.Semantics;

namespace Lattice.Core.Language;

internal static class SemanticWorldModelRelationQuestionParser
{
    private static readonly Regex RelationPairPattern = new(
        @"^How is (?<subject>[A-Za-z][A-Za-z0-9'’! -]*?) related to (?<other>[A-Za-z][A-Za-z0-9'’! -]*?)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex RelationTargetPattern = new(
        @"^What does (?<subject>[A-Za-z][A-Za-z -]*?) (?<verb>[a-z]+)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ScopedRelationTargetPattern = new(
        @"^Which (?<target>[A-Za-z][A-Za-z -]*?) of (?<scope>[A-Za-z0-9][A-Za-z0-9'’ -]*?) did (?<subject>[A-Za-z][A-Za-z'’ -]*?) (?<verb>[a-z]+)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ScopedTypedRelationTargetPattern = new(
        @"^Which (?<targetType>[A-Za-z][A-Za-z -]*?) of (?<scope>[A-Za-z0-9][A-Za-z0-9'’ -]*?) did (?<subject>[A-Za-z][A-Za-z'’ -]*?) (?<verb>[a-z]+)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex TypedRelationTargetPattern = new(
        @"^Which (?<targetType>[A-Za-z][A-Za-z -]*?) did (?<subject>[A-Za-z][A-Za-z'’ -]*?) (?<verb>[a-z]+)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex TypedRelationSourcePattern = new(
        @"^Which (?<subjectType>(?!(?:is|are|was|were|can|do|does|did)\b)[A-Za-z][A-Za-z -]*?) (?<verb>[a-z]+) (?<object>[""“]?[A-Z][A-Za-z0-9'’ -]*?[""”]?)\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex RelationSourcePattern = new(
        @"^(?:Who|What) (?!(?:is|are|was|were|be|being|been|has|have|does|do|did)\b)(?<verb>[a-z]+s) (?<object>(?:(?:the|a|an) )?(?!(?:when|where|why|how|to|for|from|with|by|in|on|at|of|that|which|who|what)\b)[A-Za-z]+(?: [A-Za-z]+){0,2})\??$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);


    internal static bool TryParsePair(string source, string text, out SemanticUtterance? utterance)
    {
        utterance = null;
        var relationPair = RelationPairPattern.Match(text);
        if (relationPair.Success)
        {
            utterance = new SemanticUtterance(
                UtteranceKind.Question,
                new SemanticRelationQueryFormula(
                    SemanticWorldModelQuestionNormalization.Normalize(relationPair.Groups["subject"].Value),
                    "related",
                    OtherSubject: SemanticWorldModelQuestionNormalization.Normalize(relationPair.Groups["other"].Value)),
                source);
            return true;
        }

        return false;
    }

    internal static bool TryParse(string source, string text, out SemanticUtterance? utterance)
    {
        utterance = null;
        var scopedTypedRelationTarget = ScopedTypedRelationTargetPattern.Match(text);
        if (scopedTypedRelationTarget.Success)
        {
            utterance = new SemanticUtterance(
                UtteranceKind.Question,
                new SemanticRelationQueryFormula(
                    SemanticWorldModelQuestionNormalization.Normalize(scopedTypedRelationTarget.Groups["subject"].Value),
                    SemanticWorldModelQuestionNormalization.Normalize(scopedTypedRelationTarget.Groups["verb"].Value),
                    SemanticWorldModelQuestionNormalization.Normalize(scopedTypedRelationTarget.Groups["scope"].Value),
                    SemanticWorldModelQuestionNormalization.NormalizeType(scopedTypedRelationTarget.Groups["targetType"].Value)),
                source);
            return true;
        }

        var typedRelationTarget = TypedRelationTargetPattern.Match(text);
        if (typedRelationTarget.Success)
        {
            utterance = new SemanticUtterance(
                UtteranceKind.Question,
                new SemanticRelationQueryFormula(
                    SemanticWorldModelQuestionNormalization.Normalize(typedRelationTarget.Groups["subject"].Value),
                    SemanticWorldModelQuestionNormalization.Normalize(typedRelationTarget.Groups["verb"].Value),
                    TargetType: SemanticWorldModelQuestionNormalization.NormalizeType(typedRelationTarget.Groups["targetType"].Value)),
                source);
            return true;
        }

        var scopedRelationTarget = ScopedRelationTargetPattern.Match(text);
        if (scopedRelationTarget.Success)
        {
            utterance = new SemanticUtterance(
                UtteranceKind.Question,
                new SemanticRelationQueryFormula(
                    SemanticWorldModelQuestionNormalization.Normalize(scopedRelationTarget.Groups["subject"].Value),
                    SemanticWorldModelQuestionNormalization.Normalize(scopedRelationTarget.Groups["verb"].Value),
                    SemanticWorldModelQuestionNormalization.Normalize(scopedRelationTarget.Groups["scope"].Value)),
                source);
            return true;
        }

        var relationTarget = RelationTargetPattern.Match(text);
        if (relationTarget.Success)
        {
            utterance = new SemanticUtterance(
                UtteranceKind.Question,
                new SemanticRelationQueryFormula(
                    SemanticWorldModelQuestionNormalization.Normalize(relationTarget.Groups["subject"].Value),
                    SemanticWorldModelQuestionNormalization.Normalize(relationTarget.Groups["verb"].Value)),
                source);
            return true;
        }

        var typedRelationSource = TypedRelationSourcePattern.Match(text);
        if (typedRelationSource.Success)
        {
            utterance = new SemanticUtterance(
                UtteranceKind.Question,
                new SemanticRelationSourceQueryFormula(
                    SemanticWorldModelQuestionNormalization.NormalizeQuotedTerm(typedRelationSource.Groups["object"].Value),
                    SemanticWorldModelQuestionNormalization.Normalize(typedRelationSource.Groups["verb"].Value),
                    SemanticWorldModelQuestionNormalization.Normalize(typedRelationSource.Groups["subjectType"].Value)),
                source);
            return true;
        }

        var relationSource = RelationSourcePattern.Match(text);
        if (relationSource.Success)
        {
            utterance = new SemanticUtterance(
                UtteranceKind.Question,
                new SemanticRelationSourceQueryFormula(
                    SemanticWorldModelQuestionNormalization.Normalize(relationSource.Groups["object"].Value),
                    SemanticWorldModelQuestionNormalization.Normalize(relationSource.Groups["verb"].Value)),
                source);
            return true;
        }

        return false;
    }
}
