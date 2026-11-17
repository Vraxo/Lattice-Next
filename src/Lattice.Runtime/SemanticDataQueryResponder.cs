using System.Collections.Immutable;
using System.Globalization;
using Lattice.Client;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;

using static Lattice.Runtime.SemanticResponseFormatting;
namespace Lattice.Runtime;

internal static class SemanticDataQueryResponder
{

    internal static bool TryHighestDataValue(
        SemanticWorldModelQuestionFormula request,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;

        var featureType = model.FindConcept(request.Subject, SemanticConceptKind.Type);
        var property = model.FindDataProperty(request.Concept ?? string.Empty);
        var scope = model.FindConcept(request.TargetType ?? string.Empty, SemanticConceptKind.Individual);
        if (featureType is null ||
            property is null ||
            property.ValueKind != SemanticDataValueKind.Number ||
            property.DomainType != featureType.Id ||
            scope is null)
        {
            return false;
        }

        var interpretations = reasoner.GetRelations(scope.Id, "has-scope-interpretation");
        if (interpretations.IsDefaultOrEmpty)
        {
            return false;
        }

        var answers = new List<(SemanticConcept Scope, SemanticConcept Feature, decimal Value)>();
        var sources = new List<SemanticWorldModelSource>();
        sources.AddRange(interpretations.Select(assertion => assertion.Source));

        foreach (var interpretation in interpretations)
        {
            var interpretedScope = model.FindConcept(interpretation.Object, SemanticConceptKind.Individual);
            if (interpretedScope is null)
            {
                return false;
            }

            var highestAssertions = reasoner.GetRelations(interpretedScope.Id, "has-highest-feature");
            if (highestAssertions.Length != 1)
            {
                return false;
            }

            var feature = model.FindConcept(highestAssertions[0].Object, SemanticConceptKind.Individual);
            if (feature is null || !reasoner.IsInstanceOf(feature.Id, featureType.Id))
            {
                return false;
            }

            var featureValues = model.GetDataAssertions(feature.Id, property.Id);
            if (featureValues.Length != 1 ||
                !decimal.TryParse(featureValues[0].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
            {
                return false;
            }

            var scopedCandidates = model.Concepts
                .Where(concept =>
                    concept.Kind == SemanticConceptKind.Individual &&
                    reasoner.IsInstanceOf(concept.Id, featureType.Id))
                .SelectMany(concept =>
                {
                    var locations = reasoner.GetRelations(concept.Id, "located-in-geographic-region")
                        .Where(assertion => assertion.Object == interpretedScope.Id)
                        .ToArray();
                    var values = model.GetDataAssertions(concept.Id, property.Id);
                    return locations.SelectMany(location => values.Select(candidateValue => (
                        Concept: concept,
                        Location: location,
                        Assertion: candidateValue)));
                })
                .Where(candidate => decimal.TryParse(
                    candidate.Assertion.Value,
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out _))
                .Select(candidate => (
                    candidate.Concept,
                    candidate.Location,
                    candidate.Assertion,
                    Value: decimal.Parse(candidate.Assertion.Value, NumberStyles.Number, CultureInfo.InvariantCulture)))
                .ToArray();
            if (scopedCandidates.Length == 0 ||
                scopedCandidates.Max(candidate => candidate.Value) != value ||
                !scopedCandidates.Any(candidate => candidate.Concept.Id == feature.Id && candidate.Value == value))
            {
                return false;
            }

            answers.Add((interpretedScope, feature, value));
            sources.Add(highestAssertions[0].Source);
            sources.AddRange(reasoner.GetMembershipEvidence(feature.Id, featureType.Id));
            sources.AddRange(scopedCandidates.SelectMany(candidate => new[]
            {
                candidate.Location.Source,
                candidate.Assertion.Source
            }));
        }

        var answerText = answers.Select(answer =>
            $"For {answer.Scope.Label}, {answer.Feature.Label} is the tallest {featureType.Label}, at {answer.Value.ToString("N0", CultureInfo.InvariantCulture)} {request.OtherSubject}.");
        message = $"The phrase \"{scope.Label}\" can refer to different geographic scopes. {string.Join(" ", answerText)}";
        evidence = ImmutableArray.Create(ToEvidence(sources.Distinct().ToImmutableArray(), LatticeEvidenceKind.Derived));
        return true;
    }

    internal static bool TryNotableExamples(
        SemanticWorldModelQuestionFormula request,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;

        if (request.Limit is < 1 or > 12)
        {
            return false;
        }

        var filmType = model.FindConcept(request.Concept ?? string.Empty, SemanticConceptKind.Type);
        var yearProperty = model.FindDataProperty("film-release-year");
        if (filmType is null ||
            yearProperty is null ||
            yearProperty.ValueKind != SemanticDataValueKind.Number ||
            !reasoner.IsSubtypeOf(filmType.Id, "comedy-film"))
        {
            return false;
        }

        var films = new List<(SemanticConcept Film, int Year, string Genres, ImmutableArray<SemanticWorldModelSource> Sources)>();
        foreach (var film in model.Concepts.Where(concept =>
            concept.Kind == SemanticConceptKind.Individual &&
            reasoner.IsInstanceOf(concept.Id, filmType.Id)))
        {
            var yearAssertions = model.GetDataAssertions(film.Id, yearProperty.Id);
            var genreAssertions = reasoner.GetRelations(film.Id, "has-film-genre");
            if (yearAssertions.Length != 1 ||
                !int.TryParse(yearAssertions[0].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var year) ||
                genreAssertions.IsDefaultOrEmpty)
            {
                continue;
            }

            var genres = genreAssertions
                .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
                .Where(concept => concept is not null && reasoner.IsInstanceOf(concept.Id, "film-genre"))
                .Select(concept => concept!.Label)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (genres.Length == 0)
            {
                continue;
            }

            var sources = reasoner.GetMembershipEvidence(film.Id, filmType.Id)
                .Concat(yearAssertions.Select(assertion => assertion.Source))
                .Concat(genreAssertions.Select(assertion => assertion.Source))
                .ToImmutableArray();
            films.Add((film, year, JoinNames(genres), sources));
        }

        if (films.Count < request.Limit)
        {
            return false;
        }

        var selected = films
            .OrderBy(item => item.Year)
            .ThenBy(item => item.Film.Label, StringComparer.OrdinalIgnoreCase)
            .Take(request.Limit)
            .ToArray();
        var entries = selected.Select((item, index) =>
            $"{index + 1}. {item.Film.Label} ({item.Year}) — {item.Genres}");
        message = $"There is no single objective ranking, but here are {request.Limit} widely regarded comedy films from the 1980s, spanning several styles:\n{string.Join("\n", entries)}";
        evidence = ImmutableArray.Create(ToEvidence(
            selected.SelectMany(item => item.Sources).Distinct().ToImmutableArray(),
            LatticeEvidenceKind.Derived));
        return true;
    }

    internal static bool TryMaximumDataValue(
        SemanticWorldModelQuestionFormula request,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;

        var property = model.FindDataProperty(request.Concept ?? string.Empty);
        if (property is null || property.ValueKind != SemanticDataValueKind.Number)
        {
            return false;
        }

        var candidates = model.Concepts
            .Where(concept =>
                concept.Kind == SemanticConceptKind.Individual &&
                reasoner.IsInstanceOf(concept.Id, property.DomainType))
            .SelectMany(concept => model.GetDataAssertions(concept.Id, property.Id)
                .Where(assertion => decimal.TryParse(
                    assertion.Value,
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out _))
                .Select(assertion => (
                    Concept: concept,
                    Assertion: assertion,
                    Value: decimal.Parse(
                        assertion.Value,
                        NumberStyles.Number,
                        CultureInfo.InvariantCulture))))
            .ToArray();
        if (candidates.Length == 0)
        {
            return false;
        }

        var maximum = candidates.Max(candidate => candidate.Value);
        var winners = candidates
            .Where(candidate => candidate.Value == maximum)
            .ToArray();
        var names = JoinNames(winners.Select(winner => winner.Concept.Label).ToArray());
        var verb = winners.Length == 1 ? "has" : "have";
        var metric = string.IsNullOrWhiteSpace(request.OtherSubject)
            ? property.Id.Replace('-', ' ')
            : request.OtherSubject;
        message = $"{names} {verb} won the most {metric} ({maximum.ToString(CultureInfo.InvariantCulture)}).";
        evidence = ImmutableArray.Create(ToEvidence(
            winners.Select(winner => winner.Assertion.Source).Distinct().ToImmutableArray(),
            LatticeEvidenceKind.Derived));
        return true;
    }

    internal static bool TryListTypeInstances(
        string requestedType,
        string countText,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;
        if (!int.TryParse(countText, NumberStyles.None, CultureInfo.InvariantCulture, out var requestedCount) ||
            requestedCount is < 1 or > 20)
        {
            return false;
        }

        var type = model.FindConcept(requestedType, SemanticConceptKind.Type);
        if (type is null)
        {
            return false;
        }

        var instances = model.Concepts
            .Where(concept =>
                concept.Kind == SemanticConceptKind.Individual &&
                reasoner.IsInstanceOf(concept.Id, type.Id))
            .OrderBy(concept => concept.Label, StringComparer.OrdinalIgnoreCase)
            .Take(requestedCount)
            .ToArray();
        if (instances.Length == 0)
        {
            return false;
        }

        var sources = instances
            .SelectMany(instance => reasoner.GetMembershipEvidence(instance.Id, type.Id))
            .Distinct()
            .ToImmutableArray();
        if (sources.IsDefaultOrEmpty)
        {
            return false;
        }

        var pluralType = type.Label.EndsWith("y", StringComparison.OrdinalIgnoreCase)
            ? $"{type.Label[..^1]}ies"
            : type.Label.EndsWith("s", StringComparison.OrdinalIgnoreCase)
                ? type.Label
                : $"{type.Label}s";
        message = $"The model identifies {instances.Length} {pluralType}: {JoinNames(instances.Select(instance => instance.Label).ToArray())}. The model does not rank them.";
        evidence = ImmutableArray.Create(ToEvidence(sources, LatticeEvidenceKind.Derived));
        return true;
    }

    internal static bool TryListExamples(
        string requestedType,
        int limit,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;

        if (limit is < 1 or > 10)
        {
            return false;
        }

        var type = model.FindConcept(requestedType, SemanticConceptKind.Type);
        if (type is null)
        {
            return false;
        }

        var examples = model.Concepts
            .Where(concept =>
                concept.Kind == SemanticConceptKind.Individual &&
                reasoner.IsInstanceOf(concept.Id, type.Id))
            .OrderBy(concept => concept.Label, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToArray();
        if (examples.Length < limit)
        {
            return false;
        }

        var sources = examples
            .SelectMany(example => reasoner.GetMembershipEvidence(example.Id, type.Id))
            .Distinct()
            .ToImmutableArray();
        if (sources.IsDefaultOrEmpty)
        {
            return false;
        }

        var pluralLabel = type.Label.EndsWith("y", StringComparison.OrdinalIgnoreCase)
            ? $"{type.Label[..^1]}ies"
            : $"{type.Label}s";
        message = $"Here are {limit} prominent {pluralLabel}:\n{string.Join("\n", examples.Select((example, index) => $"{index + 1}. {example.Label}"))}";
        evidence = ImmutableArray.Create(ToEvidence(sources, LatticeEvidenceKind.Derived));
        return true;
    }
}
