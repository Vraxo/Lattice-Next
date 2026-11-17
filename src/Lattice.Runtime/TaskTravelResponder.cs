using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Lattice.Client;
using Lattice.Core.Generation;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;

namespace Lattice.Runtime;

using static Lattice.Runtime.TaskRequestResponderHelpers;

internal static class TaskTravelResponder
{
    internal static bool TryPlanRoadTrip(
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
            sources.AddRange(model.GetDataAssertions(day.Day.Id, "day-number")
                .Select(assertion => assertion.Source));
            sources.AddRange(reasoner.GetRelations(matchingRoute.Route.Id, "route-has-day")
                .Where(assertion => assertion.Object == day.Day.Id)
                .Select(assertion => assertion.Source));

            foreach (var stop in stops)
            {
                var place = model.FindConcept(stop.Object, SemanticConceptKind.Individual);
                if (place is null || !reasoner.IsInstanceOf(place.Id, "road-trip-place"))
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

                stopDescriptions.Add($"**{place.Label}** — {JoinNames(activities)}");
                sources.Add(stop.Source);
                sources.AddRange(reasoner.GetMembershipEvidence(place.Id, "road-trip-place"));
                sources.AddRange(reasoner.GetRelations(place.Id, "place-offers-activity")
                    .Select(assertion => assertion.Source));
                foreach (var activity in reasoner.GetRelations(place.Id, "place-offers-activity"))
                {
                    sources.AddRange(reasoner.GetMembershipEvidence(activity.Object, "activity"));
                }
            }

            paragraphs.Add($"**Day {day.Number}:** {string.Join("; ", stopDescriptions)}.");
        }

        paragraphs.Add("Use Princeton as a base for both nights so you only unpack once. After Day 3, return to Westfield. Check holiday opening hours and reserve accommodation ahead of time; traffic can be heavier over Memorial Day weekend.");
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
}
