using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Lattice.Core.Semantics;

namespace Lattice.Core.Language;

internal static class TaskRequestParser
{
    private static readonly Regex WritingRequestPattern = new(
        @"^\s*(?:Can|Could) you write a (?<length>short|brief) (?<format>introduction|overview|summary) about (?<topicClause>[^?]+)\?\s*(?<instructions>.*)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex TopicPattern = new(
        @"\bterm\s+[""“”']?(?<topic>[A-Za-z][A-Za-z-]*)[""“”']?(?:\s+in\s+(?<context>[^,.!?]+))?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ExampleScopePattern = new(
        @"\bexamples?\s+related\s+to\s+(?<scope>.+?)(?:\s+and\s+cite\b|[.!?]|$)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex RecommendationRequestPattern = new(
        @"^\s*(?:I would like to|I'd like to)\s+(?<purpose>[^.!?]+)[.!?]\s*(?:Which|What)\s+(?<subject>[^?]+?)\s+(?:do|would)\s+you\s+recommend\s+for\s+this\s+purpose\?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static bool TryParse(
        string source,
        string sourceText,
        out SemanticUtterance? utterance)
    {
        utterance = null;

        if (TryParseWritingRequest(source, sourceText, out utterance))
        {
            return true;
        }

        return TryParseRecommendationRequest(source, sourceText, out utterance);
    }

    private static bool TryParseWritingRequest(
        string source,
        string sourceText,
        out SemanticUtterance? utterance)
    {
        utterance = null;
        var match = WritingRequestPattern.Match(source);

        if (!match.Success)
        {
            return false;
        }

        var topicMatch = TopicPattern.Match(match.Groups["topicClause"].Value);
        if (!topicMatch.Success)
        {
            return false;
        }

        var instructions = match.Groups["instructions"].Value;
        var requirements = ImmutableArray.CreateBuilder<string>();

        var exampleScope = ExampleScopePattern.Match(instructions);
        if (exampleScope.Success)
        {
            requirements.Add("examples");
        }

        var requiresResearch = instructions.Contains("cite", StringComparison.OrdinalIgnoreCase) ||
            instructions.Contains("research", StringComparison.OrdinalIgnoreCase);
        if (requiresResearch)
        {
            requirements.Add("research-citations");
        }

        var formula = new TaskRequestFormula(
            TaskRequestAction.Write,
            topicMatch.Groups["topic"].Value.ToLowerInvariant(),
            topicMatch.Groups["context"].Success
                ? topicMatch.Groups["context"].Value.Trim()
                : null,
            $"{match.Groups["length"].Value.ToLowerInvariant()}-{match.Groups["format"].Value.ToLowerInvariant()}",
            exampleScope.Success ? exampleScope.Groups["scope"].Value.Trim() : null,
            requirements.ToImmutable());

        utterance = new SemanticUtterance(
            UtteranceKind.TaskRequest,
            formula,
            sourceText.Trim());
        return true;
    }

    private static bool TryParseRecommendationRequest(
        string source,
        string sourceText,
        out SemanticUtterance? utterance)
    {
        utterance = null;
        var match = RecommendationRequestPattern.Match(source);

        if (!match.Success)
        {
            return false;
        }

        var purpose = match.Groups["purpose"].Value.Trim();
        var subject = match.Groups["subject"].Value.Trim().ToLowerInvariant();
        var requirements = ImmutableArray.CreateBuilder<string>();

        if (subject.Contains("open-source", StringComparison.Ordinal))
        {
            requirements.Add("open-source");
        }

        if (purpose.Contains("python", StringComparison.OrdinalIgnoreCase))
        {
            requirements.Add("python");
        }

        if (purpose.Contains("simulation", StringComparison.OrdinalIgnoreCase) ||
            purpose.Contains("unit operation", StringComparison.OrdinalIgnoreCase))
        {
            requirements.Add("process-simulation");
        }

        if (purpose.Contains("optim", StringComparison.OrdinalIgnoreCase))
        {
            requirements.Add("optimization");
        }

        var formula = new TaskRequestFormula(
            TaskRequestAction.Recommend,
            subject.Replace(' ', '-'),
            purpose,
            null,
            null,
            requirements.ToImmutable());

        utterance = new SemanticUtterance(
            UtteranceKind.TaskRequest,
            formula,
            sourceText.Trim());
        return true;
    }
}
