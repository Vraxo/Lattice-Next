using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Lattice.Client;
using Lattice.Core.Generation;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;

namespace Lattice.Runtime;

using static Lattice.Runtime.TaskRequestResponderHelpers;

internal static class TaskRecommendationResponder
{
    internal static bool TryRecommend(
        TaskRequestFormula request,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;

        var requiresOpenSource = request.Requirements.Contains("open-source");
        var requiresPython = request.Requirements.Contains("python");
        var requiresProcessSimulation = request.Requirements.Contains("process-simulation");
        var requiresOptimization = request.Requirements.Contains("optimization");

        var candidates = model.Concepts
            .Where(concept =>
                concept.Kind == SemanticConceptKind.Individual &&
                reasoner.IsInstanceOf(concept.Id, "software") &&
                (!requiresOpenSource || reasoner.IsInstanceOf(concept.Id, "open-source-software")) &&
                (!requiresPython || reasoner.IsInstanceOf(concept.Id, "python-package")))
            .Select(concept => CreateSoftwareProfile(concept, model, reasoner))
            .Where(profile => profile is not null)
            .Select(profile => profile!)
            .Where(profile =>
                (!requiresProcessSimulation && !requiresOptimization) ||
                (requiresProcessSimulation && profile.HasProcessModelingCapability) ||
                (requiresOptimization && profile.HasOptimizationCapability))
            .OrderBy(profile => profile.IsOptimizationFramework ? 1 : 0)
            .ThenBy(profile => profile.Name, StringComparer.Ordinal)
            .ToArray();

        if (candidates.Length == 0)
        {
            return false;
        }

        var builder = new StringBuilder();
        builder.AppendLine("Relevant open-source Python tools for the stated purpose:");

        foreach (var candidate in candidates)
        {
            builder.Append("- **")
                .Append(candidate.Name)
                .Append("** (")
                .Append(candidate.Role)
                .Append("): capabilities include ")
                .Append(string.Join(", ", candidate.Capabilities))
                .Append(". It is used for ")
                .Append(candidate.ApplicationDomains.Length == 1
                    ? candidate.ApplicationDomains[0]
                    : string.Join(", ", candidate.ApplicationDomains))
                .AppendLine(".");
            if (!string.IsNullOrWhiteSpace(candidate.DocumentationUrl))
            {
                builder.Append("Documentation: ").AppendLine(candidate.DocumentationUrl);
            }
        }

        if (candidates.Any(candidate => candidate.IsOptimizationFramework) &&
            candidates.Any(candidate => !candidate.IsOptimizationFramework))
        {
            builder.Append("Use the optimization framework alongside a process simulator; it does not replace the simulator's unit-operation and thermodynamic models.");
        }

        message = builder.ToString().Trim();
        evidence = ImmutableArray.Create(new LatticeEvidence(
            LatticeEvidenceKind.Derived,
            LatticeEvidencePolarity.Positive,
            candidates
                .SelectMany(candidate => candidate.Evidence)
                .DistinctBy(source => $"{source.Kind}:{source.SourceId}")
                .ToImmutableArray(),
            ImmutableArray<LatticeEvidence>.Empty));
        return true;
    }

    internal static SoftwareProfile? CreateSoftwareProfile(
        SemanticConcept software,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner)
    {
        var types = reasoner.GetTypes(software.Id);
        var role = types
            .Where(type => type is not ("software" or "open-source-software" or "python-package"))
            .Where(type => !types.Any(other =>
                other != type && IsSubtype(other, type, model)))
            .Select(type => model.FindConcept(type, SemanticConceptKind.Type))
            .Where(type => type is not null)
            .OrderBy(type => type!.Label, StringComparer.Ordinal)
            .FirstOrDefault();

        if (role is null)
        {
            return null;
        }

        var capabilityAssertions = reasoner.GetRelations(software.Id, "supports-capability");
        var capabilities = capabilityAssertions
            .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
            .Where(concept => concept is not null)
            .Select(concept => concept!.Label)
            .Distinct(StringComparer.Ordinal)
            .ToImmutableArray();
        var domainAssertions = reasoner.GetRelations(software.Id, "used-for-domain");
        var domains = domainAssertions
            .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
            .Where(concept => concept is not null)
            .Select(concept => concept!.Label)
            .Distinct(StringComparer.Ordinal)
            .ToImmutableArray();
        var documentation = model.GetDataAssertions(software.Id, "documentation-url").FirstOrDefault();

        if (capabilities.IsDefaultOrEmpty || domains.IsDefaultOrEmpty)
        {
            return null;
        }

        var typeEvidence = types
            .Where(type => model.FindConcept(type, SemanticConceptKind.Type) is not null)
            .SelectMany(type => reasoner.GetMembershipEvidence(software.Id, type));
        var relationEvidence = capabilityAssertions
            .Select(assertion => assertion.Source)
            .Concat(domainAssertions.Select(assertion => assertion.Source))
            .ToList();
        if (documentation is not null)
        {
            relationEvidence.Add(documentation.Source);
        }

        var evidence = typeEvidence
            .Concat(relationEvidence)
            .Distinct()
            .Select(ToEvidenceSource)
            .ToImmutableArray();

        return new SoftwareProfile(
            software.Label,
            role.Label,
            capabilities,
            domains,
            documentation?.Value ?? string.Empty,
            role.Id == "optimization-modeling-framework" ||
                reasoner.IsInstanceOf(software.Id, "optimization-framework"),
            capabilities.Any(capability =>
                capability is "equation-oriented process modeling" or
                    "process modeling" or
                    "unit-operation modeling" or
                    "flowsheet optimization"),
            capabilities.Any(capability =>
                capability is "algebraic optimization modeling" or
                    "linear optimization" or
                    "nonlinear optimization" or
                    "mixed-integer optimization" or
                    "flowsheet optimization"),
            evidence);
    }

    internal static bool TryDescribeRuralMarket(
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<SemanticWorldModelSource> sources)
    {
        message = string.Empty;
        sources = ImmutableArray<SemanticWorldModelSource>.Empty;
        var market = model.FindConcept("rural-town-labor-market", SemanticConceptKind.Individual);
        if (market is null)
        {
            return false;
        }

        var dominantBuyer = reasoner.GetRelations(market.Id, "has-dominant-buyer").FirstOrDefault();
        var workers = reasoner.GetRelations(market.Id, "has-worker-group").FirstOrDefault();
        if (dominantBuyer is null || workers is null)
        {
            return false;
        }

        var buyer = model.FindConcept(dominantBuyer.Object, SemanticConceptKind.Individual);
        var workerGroup = model.FindConcept(workers.Object, SemanticConceptKind.Individual);
        if (buyer is null || workerGroup is null ||
            !reasoner.IsInstanceOf(workerGroup.Id, "limited-outside-options"))
        {
            return false;
        }

        message = $"For example, a labour market in a rural town has {IndefinitePhrase(buyer.Label)} as its dominant buyer and includes {workerGroup.Label}, whose outside options are limited.";
        sources = ImmutableArray.Create(dominantBuyer.Source, workers.Source)
            .AddRange(reasoner.GetMembershipEvidence(workerGroup.Id, "limited-outside-options"));
        return true;
    }

    internal static bool TryDescribeRegionalMarket(
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<SemanticWorldModelSource> sources)
    {
        message = string.Empty;
        sources = ImmutableArray<SemanticWorldModelSource>.Empty;
        var market = model.FindConcept("regional-healthcare-labor-market", SemanticConceptKind.Individual);
        if (market is null)
        {
            return false;
        }

        var employers = reasoner.GetRelations(market.Id, "has-employer");
        var features = reasoner.GetRelations(market.Id, "has-market-feature");
        if (employers.Length < 2 || features.IsDefaultOrEmpty)
        {
            return false;
        }

        var featureLabels = features
            .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
            .Where(concept => concept is not null)
            .Select(concept => concept!.Label)
            .ToArray();
        if (featureLabels.Length == 0)
        {
            return false;
        }

        message = $"A regional healthcare labour market can have several employers while still exhibiting {JoinNames(featureLabels)}.";
        sources = employers.Select(assertion => assertion.Source)
            .Concat(features.Select(assertion => assertion.Source))
            .ToImmutableArray();
        return true;
    }
}
