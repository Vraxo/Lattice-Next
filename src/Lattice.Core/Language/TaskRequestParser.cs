using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Lattice.Core.Reasoning;
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

    private static readonly Regex BrainstormUsesPattern = new(
        @"^\s*What are (?<count>[0-9]+) potential uses for an empty (?<object>[^?]+?)\??\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ConceptExplanationPattern = new(
        @"^\s*(?:Can|Could) you explain (?<topic>.+?)(?:\s+in\s+(?<context>.+?))?(?:\s+in simple terms)?(?:\s+for someone new to (?:the field of )?.+?)?\??\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex LearningPathPattern = new(
        @"^\s*How can I learn to (?<skill>[A-Za-z][A-Za-z-]*) (?<target>.+?)\??\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex BlenderScriptPattern = new(
        @"^\s*How would I create a script for (?<application>blender) that (?<task>.+?)\??\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex OperationExplanationPattern = new(
        @"^\s*How do you operate (?:a|an|the) (?<vehicle>car|vehicle) with (?:a|an|the) (?<transmission>[A-Za-z][A-Za-z -]*?)\??\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex RoadTripPlanPattern = new(
        @"^\s*Plan\s+for\s+(?<duration>(?:a\s+)?(?:one|two|three|four|five|six|seven|[0-9]+)[ -]days?)\s+road trip(?:\s+for\s+(?<occasion>.+?))?(?:\s+from\s+(?<origin>[^.!?]+?))?[.!?]?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex HobbyExplorationPattern = new(
        @"^\s*(?:I want to start doing|I would like to start doing|I'd like to start doing|I want to take up)\s+(?<hobby>[^,.?]+?)(?:\s+as a hobby)?\s*,?\s*(?:any suggestions.*|what could I do.*|what can I do.*)?[?.!]*\s*$",
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

        if (TryParseHobbyExploration(source, sourceText, out utterance))
        {
            return true;
        } 

        if (TryParseBlenderScript(source, sourceText, out utterance))
        {
            return true;
        }

        if (TryParseBrainstormUses(source, sourceText, out utterance))
        {
            return true;
        } 

        if (TryParseConceptExplanation(source, sourceText, out utterance))
        {
            return true;
        }

        if (TryParseOperationExplanation(source, sourceText, out utterance))
        {
            return true;
        } 

        if (TryParseLearningPath(source, sourceText, out utterance))
        {
            return true;
        }

        if (TryParseRoadTripPlan(source, sourceText, out utterance))
        {
            return true;
        }

        if (TryParseWritingRequest(source, sourceText, out utterance))
        {
            return true;
        }

        return TryParseRecommendationRequest(source, sourceText, out utterance);
    }

    private static bool TryParseHobbyExploration( 
    private static bool TryParseBlenderScript(
        string source,
        string sourceText,
        out SemanticUtterance? utterance)
    {
        utterance = null;
        var match = BlenderScriptPattern.Match(source);

        if (!match.Success)
        {
            return false;
        }

        var task = match.Groups["task"].Value.Trim().TrimEnd('?');
        var supportsObjectReplacement =
            task.Contains("replac", StringComparison.OrdinalIgnoreCase) &&
            task.Contains("object", StringComparison.OrdinalIgnoreCase) &&
            task.Contains("specific name", StringComparison.OrdinalIgnoreCase) &&
            task.Contains("point light", StringComparison.OrdinalIgnoreCase);
        var formula = new TaskRequestFormula(
            TaskRequestAction.Code,
            supportsObjectReplacement ? "blender-object-replacement" : "unsupported-blender-script",
            "blender",
            "python-script",
            supportsObjectReplacement ? "object-to-point-light" : task,
            ImmutableArray.Create("python", "blender-api"));

        utterance = new SemanticUtterance(
            UtteranceKind.TaskRequest,
            formula,
            sourceText.Trim());
        return true;
    }


        string source,
        string sourceText,
        out SemanticUtterance? utterance)
    {
        utterance = null;
        var match = HobbyExplorationPattern.Match(source);

        if (!match.Success)
        {
            return false;
        }

        var hobby = SemanticWorldModel.Normalize(match.Groups["hobby"].Value);
        if (hobby.Length == 0)
        {
            return false;
        }

        var formula = new TaskRequestFormula(
            TaskRequestAction.Explore,
            hobby,
            "hobby",
            "starter-activities",
            null,
            ImmutableArray<string>.Empty);

        utterance = new SemanticUtterance(
            UtteranceKind.TaskRequest,
            formula,
            sourceText.Trim());
        return true;
    }

    private static bool TryParseBrainstormUses(
        string source,
        string sourceText,
        out SemanticUtterance? utterance)
    {
        utterance = null;
        var match = BrainstormUsesPattern.Match(source);

        if (!match.Success ||
            !int.TryParse(match.Groups["count"].Value, out var count) ||
            count is < 1 or > 10)
        {
            return false;
        }

        var objectPhrase = match.Groups["object"].Value.Trim();
        var subject = objectPhrase;
        string? context = null;

        if (objectPhrase.Equals("can", StringComparison.OrdinalIgnoreCase) ||
            objectPhrase.EndsWith(" can", StringComparison.OrdinalIgnoreCase))
        {
            subject = "beverage-can";
            var brand = objectPhrase[..^3].Trim();
            if (brand.Length > 0)
            {
                context = string.Join(
                    '-',
                    brand.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    .ToLowerInvariant();
            }
        }
        else
        {
            subject = string.Join(
                '-',
                objectPhrase.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                .ToLowerInvariant();
        }

        var formula = new TaskRequestFormula(
            TaskRequestAction.Brainstorm,
            subject,
            context,
            $"{count}-ideas",
            "potential-uses",
            ImmutableArray.Create("potential-uses"));

        utterance = new SemanticUtterance(
            UtteranceKind.TaskRequest,
            formula,
            sourceText.Trim());
        return true;
    }

    private static bool TryParseOperationExplanation( 
    private static bool TryParseConceptExplanation(
        string source,
        string sourceText,
        out SemanticUtterance? utterance)
    {
        utterance = null;
        var match = ConceptExplanationPattern.Match(source);

        if (!match.Success)
        {
            return false;
        }

        var subject = string.Join(
            '-',
            match.Groups["topic"].Value.Trim()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .ToLowerInvariant();
        if (subject.Length == 0)
        {
            return false;
        }

        var context = match.Groups["context"].Success
            ? match.Groups["context"].Value.Trim()
            : null;
        var formula = new TaskRequestFormula(
            TaskRequestAction.Explain,
            subject,
            context,
            "conceptual-overview",
            null,
            ImmutableArray.Create("beginner-friendly"));

        utterance = new SemanticUtterance(
            UtteranceKind.TaskRequest,
            formula,
            sourceText.Trim());
        return true;
    }


        string source,
        string sourceText,
        out SemanticUtterance? utterance)
    {
        utterance = null;
        var match = OperationExplanationPattern.Match(source);

        if (!match.Success)
        {
            return false;
        }

        var transmission = string.Join(
            '-',
            match.Groups["transmission"].Value.Trim()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .ToLowerInvariant();
        var formula = new TaskRequestFormula(
            TaskRequestAction.Explain,
            transmission,
            match.Groups["vehicle"].Value.ToLowerInvariant(),
            "procedure",
            null,
            ImmutableArray.Create("operating-procedure"));

        utterance = new SemanticUtterance(
            UtteranceKind.TaskRequest,
            formula,
            sourceText.Trim());
        return true;
    }

    private static bool TryParseRoadTripPlan( 
    private static bool TryParseLearningPath(
        string source,
        string sourceText,
        out SemanticUtterance? utterance)
    {
        utterance = null;
        var match = LearningPathPattern.Match(source);
        if (!match.Success ||
            !match.Groups["skill"].Value.StartsWith("optimiz", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var target = match.Groups["target"].Value.Trim().ToLowerInvariant();
        var supportsSeo = target.Contains("webpage for search engines", StringComparison.Ordinal);

        var formula = new TaskRequestFormula(
            TaskRequestAction.Plan,
            supportsSeo ? "search-engine-optimization" : "unsupported-learning-path",
            target,
            "learning-path",
            null,
            ImmutableArray.Create("beginner", "practical-exercises"));

        utterance = new SemanticUtterance(
            UtteranceKind.TaskRequest,
            formula,
            sourceText.Trim());
        return true;
    }


        string source,
        string sourceText,
        out SemanticUtterance? utterance)
    {
        utterance = null;
        var match = RoadTripPlanPattern.Match(source);

        if (!match.Success || !match.Groups["origin"].Success)
        {
            return false;
        }

        var durationToken = match.Groups["duration"].Value
            .Replace("a ", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" days", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" day", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();
        var dayCount = durationToken.ToLowerInvariant() switch
        {
            "one" => 1,
            "two" => 2,
            "three" => 3,
            "four" => 4,
            "five" => 5,
            "six" => 6,
            "seven" => 7,
            _ => int.TryParse(durationToken, out var numericDays) ? numericDays : 0
        };
        var duration = $"{dayCount}-day";
        var occasion = match.Groups["occasion"].Success
            ? match.Groups["occasion"].Value.Trim()
            : null;
        var requirements = ImmutableArray.Create("road-trip");

        var formula = new TaskRequestFormula(
            TaskRequestAction.Plan,
            "road-trip",
            match.Groups["origin"].Value.Trim(),
            duration,
            occasion,
            requirements);

        utterance = new SemanticUtterance(
            UtteranceKind.TaskRequest,
            formula,
            sourceText.Trim());
        return true;
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
