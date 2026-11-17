using System.Collections.Immutable;
using System.Globalization;
using Lattice.Client;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;

using static Lattice.Runtime.SemanticResponseFormatting;
namespace Lattice.Runtime;

internal static class SemanticRelationResponder
{

    internal static bool TryRespond(
        SemanticRelationQueryFormula request,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(reasoner);

        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;
        if (request.OtherSubject is not null)
        {
            return TryRespondPairwise(request.Subject, request.OtherSubject, model, out message, out evidence);
        }

        var subject = model.FindConcept(request.Subject, SemanticConceptKind.Individual);
        if (subject is null)
        {
            return false;
        }

        var matchingRelations = model.Relations
            .Where(relation => MatchesVerb(relation.Verb, request.Verb))
            .ToArray();
        if (matchingRelations.Length == 0)
        {
            return false;
        }

        var targetType = request.TargetType is null
            ? null
            : model.FindConcept(request.TargetType, SemanticConceptKind.Type);
        if (request.TargetType is not null && targetType is null)
        {
            return false;
        }

        var matches = matchingRelations
            .SelectMany(relation => reasoner.GetRelations(subject.Id, relation.Id)
                .Select(assertion => (Relation: relation, Assertion: assertion)))
            .Select(match => (
                match.Relation,
                Target: model.FindConcept(match.Assertion.Object, SemanticConceptKind.Individual),
                match.Assertion.Source))
            .Where(match =>
                match.Target is not null &&
                (request.TargetType is null ||
                 reasoner.IsInstanceOf(match.Target.Id, targetType!.Id)))
            .ToArray();
        var scopeEvidence = ImmutableArray<LatticeEvidence>.Empty;
        if (request.Scope is not null)
        {
            var scope = model.FindConcept(request.Scope, SemanticConceptKind.Individual);
            if (scope is null)
            {
                return false;
            }

            var scopeAssertions = model.Relations
                .SelectMany(relation => reasoner.GetRelations(scope.Id, relation.Id))
                .Where(assertion =>
                {
                    if (request.TargetType is null)
                    {
                        return true;
                    }

                    var relation = model.FindRelation(assertion.Relation);
                    return relation is not null &&
                        relation.SubjectKind == SemanticRelationArgumentKind.Individual &&
                        relation.ObjectKind == SemanticRelationArgumentKind.Individual &&
                        relation.DomainType is not null &&
                        relation.RangeType is not null &&
                        reasoner.IsInstanceOf(scope.Id, relation.DomainType) &&
                        reasoner.IsInstanceOf(assertion.Object, relation.RangeType) &&
                        reasoner.IsInstanceOf(assertion.Object, targetType!.Id);
                })
                .ToArray();
            var scopedTargetIds = scopeAssertions
                .Select(assertion => assertion.Object)
                .ToHashSet(StringComparer.Ordinal);
            matches = matches
                .Where(match => scopedTargetIds.Contains(match.Target!.Id))
                .ToArray();
            if (matches.Length == 0)
            {
                return false;
            }

            scopeEvidence = ImmutableArray.Create(ToEvidence(
                scopeAssertions
                    .Where(assertion => matches.Any(match => match.Target!.Id == assertion.Object))
                    .Select(assertion => assertion.Source)
                    .Distinct()
                    .ToImmutableArray(),
                LatticeEvidenceKind.Derived));
        }

        if (matches.Length == 0)
        {
            return false;
        }

        var relation = matches[0].Relation;
        var targets = matches
            .Select(match => match.Target!)
            .DistinctBy(target => target.Id)
            .ToArray();
        if (request.TargetType is not null)
        {
            var typePhrase = targets.Length == 1 ? IndefinitePhrase(targetType!.Label) : PluralizeType(targetType!.Label);
            var names = JoinNames(targets.Select(target => target.Label).ToArray());
            message = targets.Length == 1
                ? $"{names} is {typePhrase} {relation.Verb} by {subject.Label}."
                : $"{names} are {typePhrase} {relation.Verb} by {subject.Label}.";
        }
        else
        {
            var targetPhrase = request.Scope is null
                ? JoinNames(targets.Select(target => $"the {target.Label}").ToArray())
                : JoinNames(targets.Select(target => target.Label).ToArray());
            message = request.Scope is null
                ? $"The {subject.Label} {relation.Verb} {targetPhrase}."
                : $"{Capitalize(subject.Label)} {relation.Verb} {targetPhrase}.";
        }
        var relationSources = matches.Select(match => match.Source);
        if (request.TargetType is not null)
        {
            relationSources = relationSources.Concat(targets.SelectMany(target =>
                reasoner.GetMembershipEvidence(target.Id, targetType!.Id)));
        }

        var relationEvidence = ToEvidence(
            relationSources.Distinct().ToImmutableArray(),
            LatticeEvidenceKind.Derived);
        evidence = scopeEvidence.IsDefaultOrEmpty
            ? ImmutableArray.Create(relationEvidence)
            : scopeEvidence.Add(relationEvidence);
        return true;
    }

    private static bool TryRespondPairwise(
        string subjectId,
        string otherSubjectId,
        SemanticWorldModel model,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;
        var subject = model.FindConcept(subjectId, SemanticConceptKind.Individual);
        var other = model.FindConcept(otherSubjectId, SemanticConceptKind.Individual);
        if (subject is null || other is null)
        {
            return false;
        }

        var directAssertions = model.RelationAssertions
            .Where(assertion =>
                (assertion.Subject == subject.Id && assertion.Object == other.Id) ||
                (assertion.Subject == other.Id && assertion.Object == subject.Id))
            .Where(assertion =>
            {
                var relation = model.FindRelation(assertion.Relation);
                return relation is not null &&
                    relation.SubjectKind == SemanticRelationArgumentKind.Individual &&
                    relation.ObjectKind == SemanticRelationArgumentKind.Individual;
            })
            .ToArray();
        if (directAssertions.Length > 0)
        {
            var sentences = FormatRelationAssertions(directAssertions, model);
            if (sentences.IsDefaultOrEmpty)
            {
                return false;
            }

            message = string.Join(" ", sentences);
            evidence = ImmutableArray.Create(ToEvidence(
                directAssertions.Select(assertion => assertion.Source).Distinct().ToImmutableArray(),
                LatticeEvidenceKind.Derived));
            return true;
        }

        if (!TryFindUniqueShortestRelationPath(subject.Id, other.Id, model, out var path))
        {
            return false;
        }

        var pathSentences = FormatRelationAssertions(path, model);
        if (pathSentences.Length != path.Length)
        {
            return false;
        }

        message = $"The shortest graph path is supported by these facts: {string.Join(" ", pathSentences)}";
        evidence = ImmutableArray.Create(ToEvidence(
            path.Select(assertion => assertion.Source).Distinct().ToImmutableArray(),
            LatticeEvidenceKind.Derived));
        return true;
    }

    private static ImmutableArray<string> FormatRelationAssertions(
        IReadOnlyCollection<SemanticRelationAssertion> assertions,
        SemanticWorldModel model) =>
        assertions.Select(assertion =>
            {
                var relation = model.FindRelation(assertion.Relation);
                var subject = model.FindConcept(assertion.Subject, SemanticConceptKind.Individual);
                var target = model.FindConcept(assertion.Object, SemanticConceptKind.Individual);
                return relation is null || subject is null || target is null
                    ? null
                    : FormatPairwiseRelation(subject, target, relation);
            })
            .Where(sentence => sentence is not null)
            .Select(sentence => sentence!)
            .Distinct(StringComparer.Ordinal)
            .ToImmutableArray();

    private static string FormatPairwiseRelation(
        SemanticConcept subject,
        SemanticConcept target,
        SemanticRelationDefinition relation)
    {
        var subjectPhrase = subject.Label.StartsWith("the ", StringComparison.OrdinalIgnoreCase) ||
            IsProperName(subject.Label)
            ? subject.Label
            : $"The {subject.Label}";
        var targetPhrase = target.Label.StartsWith("the ", StringComparison.OrdinalIgnoreCase) ||
            IsProperName(target.Label)
            ? target.Label
            : $"the {target.Label}";
        if (relation.ObjectRole is not null)
        {
            targetPhrase = $"{targetPhrase} {relation.ObjectRole}";
        }

        var verb = subject.Label.EndsWith('s') &&
            subject.Label.Length > 1 &&
            !IsProperName(subject.Label)
            ? InflectPluralSubjectVerb(relation.Verb)
            : relation.Verb;
        return $"{subjectPhrase} {verb} {targetPhrase}.";
    }

    private static bool IsProperName(string value)
    {
        var words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length > 0 && words.All(word => char.IsUpper(word[0]));
    }

    private static bool TryFindUniqueShortestRelationPath(
        string start,
        string target,
        SemanticWorldModel model,
        out ImmutableArray<SemanticRelationAssertion> path)
    {
        const int maximumDepth = 3;
        path = [];
        var pending = new Queue<(
            string Node,
            ImmutableArray<SemanticRelationAssertion> Path,
            HashSet<string> Visited)>();
        pending.Enqueue((start, [], new HashSet<string>(StringComparer.Ordinal) { start }));
        int? shortestDepth = null;
        var shortestPaths = new List<ImmutableArray<SemanticRelationAssertion>>();

        while (pending.TryDequeue(out var current))
        {
            if (current.Path.Length >= maximumDepth ||
                (shortestDepth.HasValue && current.Path.Length >= shortestDepth.Value))
            {
                continue;
            }

            foreach (var assertion in model.RelationAssertions.Where(assertion =>
                assertion.Subject == current.Node || assertion.Object == current.Node))
            {
                var relation = model.FindRelation(assertion.Relation);
                if (relation is null ||
                    relation.SubjectKind != SemanticRelationArgumentKind.Individual ||
                    relation.ObjectKind != SemanticRelationArgumentKind.Individual)
                {
                    continue;
                }

                var nextNode = assertion.Subject == current.Node
                    ? assertion.Object
                    : assertion.Subject;
                if (current.Visited.Contains(nextNode))
                {
                    continue;
                }

                var nextPath = current.Path.Add(assertion);
                if (nextNode == target)
                {
                    shortestDepth ??= nextPath.Length;
                    if (nextPath.Length == shortestDepth.Value)
                    {
                        shortestPaths.Add(nextPath);
                    }

                    continue;
                }

                if (!shortestDepth.HasValue || nextPath.Length < shortestDepth.Value)
                {
                    var visited = new HashSet<string>(current.Visited, StringComparer.Ordinal)
                    {
                        nextNode
                    };
                    pending.Enqueue((nextNode, nextPath, visited));
                }
            }
        }

        if (shortestPaths.Count != 1)
        {
            return false;
        }

        path = shortestPaths[0];
        return true;
    }
}
