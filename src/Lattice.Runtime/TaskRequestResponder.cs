using System.Collections.Immutable;
using System.Text;
using Lattice.Client;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;

namespace Lattice.Runtime;

internal static class TaskRequestResponder
{
    internal static bool TryRespond(
        TaskRequestFormula request,
        KnowledgeBase knowledgeBase,
        ReasoningEngine reasoningEngine,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(knowledgeBase);
        ArgumentNullException.ThrowIfNull(reasoningEngine);

        return request.Action switch
        {
            TaskRequestAction.Write => TryWrite(
                request,
                knowledgeBase,
                reasoningEngine,
                out message,
                out evidence),
            TaskRequestAction.Recommend => TryRecommend(
                request,
                knowledgeBase,
                out message,
                out evidence),
            _ => Fail(out message, out evidence)
        };
    }

    private static bool TryWrite(
        TaskRequestFormula request,
        KnowledgeBase knowledgeBase,
        ReasoningEngine reasoningEngine,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;

        if (!ConceptKnowledgeFormatter.TryCreate(
            knowledgeBase,
            reasoningEngine,
            request.Subject,
            out var explanation))
        {
            return false;
        }

        var mechanisms = GetTextValues(knowledgeBase, request.Subject, "mechanism");
        var examples = GetTextValues(knowledgeBase, request.Subject, "example");
        var references = GetTextValues(knowledgeBase, request.Subject, "research-reference");

        if (mechanisms.IsDefaultOrEmpty ||
            (request.Requirements.Contains("examples") && examples.IsDefaultOrEmpty) ||
            (request.Requirements.Contains("research-citations") && references.IsDefaultOrEmpty))
        {
            return false;
        }

        var paragraphs = new List<string> { explanation!.Message };
        paragraphs.AddRange(mechanisms);

        if (request.Requirements.Contains("examples"))
        {
            paragraphs.AddRange(examples.Select(example => $"For example, {example}"));
        }

        if (request.Requirements.Contains("research-citations"))
        {
            paragraphs.Add($"Relevant research includes: {string.Join("; ", references)}.");
        }

        message = string.Join(" ", paragraphs);
        evidence = explanation!.Evidence
            .AddRange(GetAttributeEvidence(
                knowledgeBase,
                request.Subject,
                ["mechanism", "example", "research-reference"]))
            .DistinctBy(GetEvidenceKey)
            .ToImmutableArray();
        return true;
    }

    private static bool TryRecommend(
        TaskRequestFormula request,
        KnowledgeBase knowledgeBase,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;

        var requiresOpenSource = request.Requirements.Contains("open-source");
        var requiresPython = request.Requirements.Contains("python");
        var requiresProcessSimulation = request.Requirements.Contains("process-simulation");
        var requiresOptimization = request.Requirements.Contains("optimization");

        var candidates = knowledgeBase.Attributes
            .Where(attribute =>
                attribute.Name.Value == "category" &&
                attribute.Value is ObjectiveTextSetValue categories &&
                (!requiresOpenSource || categories.Values.Contains("open-source", StringComparer.OrdinalIgnoreCase)) &&
                (!requiresPython || categories.Values.Contains("python", StringComparer.OrdinalIgnoreCase)) &&
                ((!requiresProcessSimulation && !requiresOptimization) ||
                    (requiresProcessSimulation && categories.Values.Contains("process-simulation", StringComparer.OrdinalIgnoreCase)) ||
                    (requiresOptimization && categories.Values.Contains("optimization", StringComparer.OrdinalIgnoreCase))))
            .Select(attribute => attribute.Subject.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(subject => new
            {
                Subject = subject,
                DisplayName = GetTextValue(knowledgeBase, subject, "display-name"),
                Role = GetTextValue(knowledgeBase, subject, "role"),
                PrimaryUse = GetTextValue(knowledgeBase, subject, "primary-use"),
                Capabilities = GetTextSetValue(knowledgeBase, subject, "capabilities"),
                DocumentationUrl = GetTextValue(knowledgeBase, subject, "documentation-url")
            })
            .Where(candidate =>
                candidate.DisplayName is not null &&
                candidate.Role is not null &&
                candidate.PrimaryUse is not null &&
                !candidate.Capabilities.IsDefaultOrEmpty &&
                candidate.DocumentationUrl is not null)
            .OrderBy(candidate =>
                candidate.Role == "optimization-framework" ? 1 : 0)
            .ThenBy(candidate => candidate.DisplayName, StringComparer.Ordinal)
            .ToArray();

        if (candidates.Length == 0)
        {
            return false;
        }

        var builder = new StringBuilder();
        builder.AppendLine("Relevant open-source Python tools for the stated purpose:");

        foreach (var candidate in candidates)
        {
            var role = candidate.Role!.Replace('-', ' ');
            var capabilities = string.Join(", ", candidate.Capabilities);
            builder.Append("- **")
                .Append(candidate.DisplayName)
                .Append("** (")
                .Append(role)
                .Append("): ")
                .Append(candidate.PrimaryUse)
                .Append(". Capabilities include ")
                .Append(capabilities)
                .Append(". [Documentation](")
                .Append(candidate.DocumentationUrl)
                .AppendLine(").");
        }

        if (candidates.Any(candidate => candidate.Role == "optimization-framework") &&
            candidates.Any(candidate => candidate.Role != "optimization-framework"))
        {
            builder.Append("Use the optimization framework alongside a process simulator; it does not replace the simulator's unit-operation and thermodynamic models.");
        }

        message = builder.ToString().Trim();
        evidence = candidates
            .SelectMany(candidate => GetAttributeEvidence(
                knowledgeBase,
                candidate.Subject,
                ["category", "display-name", "role", "primary-use", "capabilities", "documentation-url"]))
            .DistinctBy(GetEvidenceKey)
            .ToImmutableArray();
        return true;
    }

    private static ImmutableArray<string> GetTextValues(
        KnowledgeBase knowledgeBase,
        string subject,
        string name) =>
        knowledgeBase.Attributes
            .Where(attribute =>
                string.Equals(attribute.Subject.Value, subject, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(attribute.Name.Value, name, StringComparison.OrdinalIgnoreCase) &&
                attribute.Value is ObjectiveTextValue)
            .Select(attribute => ((ObjectiveTextValue)attribute.Value).Value)
            .ToImmutableArray();

    private static string? GetTextValue(
        KnowledgeBase knowledgeBase,
        string subject,
        string name) =>
        knowledgeBase.Attributes
            .Where(attribute =>
                string.Equals(attribute.Subject.Value, subject, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(attribute.Name.Value, name, StringComparison.OrdinalIgnoreCase))
            .Select(attribute => attribute.Value)
            .OfType<ObjectiveTextValue>()
            .Select(value => value.Value)
            .FirstOrDefault();

    private static ImmutableArray<string> GetTextSetValue(
        KnowledgeBase knowledgeBase,
        string subject,
        string name) =>
        knowledgeBase.Attributes
            .Where(attribute =>
                string.Equals(attribute.Subject.Value, subject, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(attribute.Name.Value, name, StringComparison.OrdinalIgnoreCase))
            .Select(attribute => attribute.Value)
            .OfType<ObjectiveTextSetValue>()
            .SelectMany(value => value.Values)
            .ToImmutableArray();

    private static ImmutableArray<LatticeEvidence> GetAttributeEvidence(
        KnowledgeBase knowledgeBase,
        string subject,
        IReadOnlyCollection<string> names) =>
        knowledgeBase.Attributes
            .Where(attribute =>
                string.Equals(attribute.Subject.Value, subject, StringComparison.OrdinalIgnoreCase) &&
                names.Contains(attribute.Name.Value, StringComparer.OrdinalIgnoreCase))
            .Select(ToEvidence)
            .ToImmutableArray();

    private static LatticeEvidence ToEvidence(KnowledgeAttribute attribute) =>
        new(
            LatticeEvidenceKind.Asserted,
            LatticeEvidencePolarity.Positive,
            attribute.Sources.Select(source => new LatticeEvidenceSource(
                source.Kind switch
                {
                    KnowledgeSourceKind.UserAssertion => LatticeKnowledgeSourceKind.UserAssertion,
                    KnowledgeSourceKind.ImportedClaim => LatticeKnowledgeSourceKind.ImportedClaim,
                    KnowledgeSourceKind.ExternalObservation => LatticeKnowledgeSourceKind.ExternalObservation,
                    _ => throw new ArgumentOutOfRangeException(nameof(source), source.Kind, "Unsupported knowledge source kind.")
                },
                source.SourceText,
                source.SourceId,
                source.ResolvedReferent?.Value))
                .ToImmutableArray(),
            ImmutableArray<LatticeEvidence>.Empty);

    private static string GetEvidenceKey(LatticeEvidence evidence) =>
        $"{evidence.Kind}:{evidence.Polarity}:{string.Join(";", evidence.Sources.Select(source => $"{source.Kind}:{source.SourceId}:{source.SourceText}"))}";

    private static bool Fail(
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;
        return false;
    }
}
