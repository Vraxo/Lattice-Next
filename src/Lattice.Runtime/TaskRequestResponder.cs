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
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(reasoner);

        return request.Action switch
        {
            TaskRequestAction.Write => TryWrite(request, model, reasoner, out message, out evidence),
            TaskRequestAction.Recommend => TryRecommend(request, model, reasoner, out message, out evidence),
            TaskRequestAction.Plan => TryPlanRoadTrip(request, model, reasoner, out message, out evidence),
            _ => Fail(out message, out evidence)
        };
    }

    private static bool TryPlanRoadTrip(
        TaskRequestFormula request,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;

        if (request.Subject != "road-trip" ||
            request.Context is null ||
            !int.TryParse(
                request.OutputFormat?.Split('-', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(),
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out var requestedDays) ||
            requestedDays is < 1 or > 7)
        {
            return false;
        }

        var origin = model.FindConcept(request.Context, SemanticConceptKind.Individual);
        if (origin is null)
        {
            return false;
        }

        var matchingRoute = model.Concepts
            .Where(concept => concept.Kind == SemanticConceptKind.Individual &&
                reasoner.IsInstanceOf(concept.Id, "road-trip-route"))
            .Select(route => new
            {
                Route = route,
                OriginAssertion = reasoner.GetRelations(route.Id, "route-starts-near")
                    .FirstOrDefault(assertion => assertion.Object == origin.Id)
            })
            .FirstOrDefault(candidate => candidate.OriginAssertion is not null);

        if (matchingRoute is null)
        {
            return false;
        }

        var days = reasoner.GetRelations(matchingRoute.Route.Id, "route-has-day")
            .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
            .Where(day => day is not null)
            .Select(day => new
            {
                Day = day!,
                Number = model.GetDataAssertions(day!.Id, "day-number").FirstOrDefault()?.Value
            })
            .Select(item => int.TryParse(
                item.Number,
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out var number)
                    ? new { item.Day, Number = number }
                    : null)
            .Where(item => item is not null)
            .OrderBy(item => item!.Number)
            .ToArray();

        if (days.Length != requestedDays)
        {
            return false;
        }

        var paragraphs = new List<string>
        {
            $"Assuming you mean the U.S. Memorial Day long weekend, here's a {requestedDays}-day road trip from {origin.Label}. The route stays in New Jersey and balances outdoor walks with relaxed sightseeing."
        };
        var sources = new List<SemanticWorldModelSource>
        {
            matchingRoute.OriginAssertion!.Source
        };
        sources.AddRange(reasoner.GetMembershipEvidence(origin.Id, "town"));
        sources.AddRange(reasoner.GetMembershipEvidence(matchingRoute.Route.Id, "road-trip-route"));

        foreach (var day in days)
        {
            var stops = reasoner.GetRelations(day!.Day.Id, "day-has-stop");
            if (stops.IsDefaultOrEmpty)
            {
                return false;
            }

            var stopDescriptions = new List<string>();
            sources.AddRange(reasoner.GetMembershipEvidence(day.Day.Id, "trip-day"));
            sources.AddRange(reasoner.GetRelations(matchingRoute.Route.Id, "route-has-day")
                .Where(assertion => assertion.Object == day.Day.Id)
                .Select(assertion => assertion.Source));

            foreach (var stop in stops)
            {
                var place = model.FindConcept(stop.Object, SemanticConceptKind.Individual);
                if (place is null || !reasoner.IsInstanceOf(place.Id, "place"))
                {
                    return false;
                }

                var activities = reasoner.GetRelations(place.Id, "place-offers-activity")
                    .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
                    .Where(activity => activity is not null)
                    .Select(activity => activity!.Label)
                    .ToArray();
                if (activities.Length == 0)
                {
                    return false;
                }

                stopDescriptions.Add($"**{place.Label}** for {JoinNames(activities)}");
                sources.Add(stop.Source);
                sources.AddRange(reasoner.GetMembershipEvidence(place.Id, "place"));
                sources.AddRange(reasoner.GetRelations(place.Id, "place-offers-activity")
                    .Select(assertion => assertion.Source));
                foreach (var activity in reasoner.GetRelations(place.Id, "place-offers-activity"))
                {
                    sources.AddRange(reasoner.GetMembershipEvidence(activity.Object, "activity"));
                }
            }

            paragraphs.Add($"**Day {day.Number} — {string.Join("; ", stopDescriptions)}.**");
        }

        paragraphs.Add("A practical base is Princeton for both nights, so you only unpack once. Check holiday opening hours and reserve accommodation ahead of time; traffic can be heavier over Memorial Day weekend.");
        message = string.Join(Environment.NewLine + Environment.NewLine, paragraphs);
        evidence = ImmutableArray.Create(new LatticeEvidence(
            LatticeEvidenceKind.Derived,
            LatticeEvidencePolarity.Positive,
            sources
                .DistinctBy(source => source.SourceId)
                .Select(ToEvidenceSource)
                .ToImmutableArray(),
            ImmutableArray<LatticeEvidence>.Empty));
        return true;
    }

    private static bool TryWrite(
        TaskRequestFormula request,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;

        if (SemanticWorldModel.Normalize(request.Subject) != "monopsony" ||
            !SemanticWorldModelResponder.TryDefinition(
                "monopsony",
                model,
                reasoner,
                out var definition,
                out var definitionEvidence))
        {
            return false;
        }

        if (!SemanticWorldModelResponder.TryRespond(
            new SemanticWorldModelQuestionFormula(
                SemanticWorldModelQuestionKind.Causes,
                "monopsony-power"),
            model,
            reasoner,
            out var causes,
            out var causesEvidence))
        {
            return false;
        }

        var buyerMarketStructure = model.FindConcept("buyer-market-structure", SemanticConceptKind.Type);
        var wageSettingPower = model.FindConcept("wage-setting-power", SemanticConceptKind.Type);
        var wageMarkdown = model.FindConcept("wage-markdown", SemanticConceptKind.Type);
        var powerDefinition = model.FindDefinition("monopsony-power");
        if (buyerMarketStructure is null ||
            wageSettingPower is null ||
            wageMarkdown is null ||
            powerDefinition is null ||
            !model.RelationAssertions.Any(assertion =>
                assertion.Subject == powerDefinition.Concept &&
                assertion.Relation == "is-a" &&
                assertion.Object == wageSettingPower.Id) ||
            !powerDefinition.SufficientConditions.Any(condition =>
                condition.Relation == "has-market-power-effect" &&
                condition.TargetType == wageMarkdown.Id))
        {
            return false;
        }

        var paragraphs = new List<string>
        {
            definition,
            $"The model distinguishes monopsony as a {buyerMarketStructure.Label} from monopsony power, a {wageSettingPower.Label} whose modeled effect is a {wageMarkdown.Label}."
        };
        var sources = definitionEvidence
            .Concat(causesEvidence)
            .SelectMany(item => item.Sources)
            .ToList();

        if (request.Requirements.Contains("examples"))
        {
            if (!TryDescribeRuralMarket(model, reasoner, out var ruralExample, out var ruralSources) ||
                !TryDescribeRegionalMarket(model, reasoner, out var regionalExample, out var regionalSources))
            {
                return false;
            }

            paragraphs.Add(ruralExample);
            paragraphs.Add(regionalExample);
            sources.AddRange(ruralSources.Select(ToEvidenceSource));
            sources.AddRange(regionalSources.Select(ToEvidenceSource));
        }

        paragraphs.Add(causes);

        if (request.Requirements.Contains("research-citations") &&
            !causes.Contains("Research references include", StringComparison.Ordinal))
        {
            return false;
        }

        message = string.Join(" ", paragraphs);
        evidence = ImmutableArray.Create(new LatticeEvidence(
            LatticeEvidenceKind.Derived,
            LatticeEvidencePolarity.Positive,
            sources
                .DistinctBy(source => $"{source.Kind}:{source.SourceId}")
                .ToImmutableArray(),
            ImmutableArray<LatticeEvidence>.Empty));
        return true;
    }

    private static bool TryRecommend(
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

    private static SoftwareProfile? CreateSoftwareProfile(
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

    private static bool TryDescribeRuralMarket(
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

    private static bool TryDescribeRegionalMarket(
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

    private static bool IsSubtype(string candidate, string expected, SemanticWorldModel model)
    {
        if (candidate == expected)
        {
            return true;
        }

        var pending = new Stack<string>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        pending.Push(candidate);

        while (pending.TryPop(out var current))
        {
            if (!visited.Add(current))
            {
                continue;
            }

            foreach (var parent in model.RelationAssertions
                .Where(assertion => assertion.Subject == current && assertion.Relation == "is-a")
                .Select(assertion => assertion.Object))
            {
                if (parent == expected)
                {
                    return true;
                }

                pending.Push(parent);
            }
        }

        return false;
    }

    private static LatticeEvidenceSource ToEvidenceSource(SemanticWorldModelSource source) =>
        new(
            LatticeKnowledgeSourceKind.FoundationalKnowledge,
            source.SourceId,
            source.SourceId,
            null,
            null);

    private static string IndefinitePhrase(string value) =>
        value.Length > 0 && "aeiou".Contains(char.ToLowerInvariant(value[0]))
            ? $"an {value}"
            : $"a {value}";

    private static string JoinNames(IReadOnlyList<string> names) =>
        names.Count switch
        {
            0 => string.Empty,
            1 => names[0],
            2 => $"{names[0]} and {names[1]}",
            _ => $"{string.Join(", ", names.Take(names.Count - 1))}, and {names[^1]}"
        };

    private static bool Fail(
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;
        return false;
    }

}
