using System.Collections.Immutable;
using System.Globalization;
using Lattice.Client;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;

using static Lattice.Runtime.SemanticResponseFormatting;
namespace Lattice.Runtime;

internal static class SemanticRelationSourceResponder
{

    internal static bool TryRespond(
        SemanticRelationSourceQueryFormula request,
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
        var target = model.FindConcept(request.Object, SemanticConceptKind.Individual);
        if (target is null)
        {
            return false;
        }

        var matchingRelationIds = model.Relations
            .Where(relation => MatchesVerb(relation.Verb, request.Verb))
            .Select(relation => relation.Id)
            .ToHashSet(StringComparer.Ordinal);
        if (matchingRelationIds.Count == 0)
        {
            return false;
        }

        var matches = model.RelationAssertions
            .Where(assertion =>
                assertion.Object == target.Id &&
                matchingRelationIds.Contains(assertion.Relation))
            .Select(assertion => (
                Subject: model.FindConcept(assertion.Subject, SemanticConceptKind.Individual),
                Relation: model.FindRelation(assertion.Relation),
                assertion.Source))
            .Where(match =>
                match.Subject is not null &&
                match.Relation is not null &&
                (request.SubjectType is null ||
                 reasoner.IsInstanceOf(match.Subject.Id, request.SubjectType)))
            .ToArray();
        if (matches.Length == 0)
        {
            return false;
        }

        var subjects = matches
            .Select(match => match.Subject!)
            .DistinctBy(subject => subject.Id)
            .ToArray();
        var relation = matches[0].Relation!;
        var subjectPhrase = JoinNames(subjects.Select(subject =>
            request.SubjectType is null ? $"the {subject.Label}" : subject.Label).ToArray());
        var pluralSubject = subjects.Length > 1 ||
            subjects[0].Label.EndsWith("s", StringComparison.OrdinalIgnoreCase);
        var verb = pluralSubject
            ? NormalizeVerb(relation.Verb.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0])
            : relation.Verb;
        var objectPhrase = request.SubjectType is null ? $"the {target.Label}" : target.Label;
        message = $"{Capitalize(subjectPhrase)} {verb} {objectPhrase}.";
        var sources = matches
            .Select(match => match.Source)
            .Concat(request.SubjectType is null
                ? []
                : subjects.SelectMany(subject =>
                    reasoner.GetMembershipEvidence(subject.Id, request.SubjectType)))
            .Distinct()
            .ToImmutableArray();
        evidence = ImmutableArray.Create(ToEvidence(sources, LatticeEvidenceKind.Derived));
        return true;
    }
}
