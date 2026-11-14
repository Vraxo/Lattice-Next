using System.Collections.Immutable;
using Lattice.Client;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;

namespace Lattice.Runtime;

internal static class FamilySetCompletionResponder
{
    internal static bool TryRespond(
        FamilySetCompletionQuestionFormula question,
        string sourceText,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;

        var requiredType = question.MemberType switch
        {
            "daughter" => "female-person",
            "son" => "male-person",
            "child" => "person",
            _ => null
        };

        if (question.OwnerRelation != "parents" ||
            requiredType is null ||
            question.ExpectedCount < 2 ||
            question.RequestedOrdinal != question.ExpectedCount ||
            question.KnownMembers.IsDefault ||
            question.KnownMembers.Length != question.ExpectedCount - 1)
        {
            return false;
        }

        var distinctMembers = question.KnownMembers
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (distinctMembers.Length != question.KnownMembers.Length ||
            distinctMembers.Contains(question.Anchor, StringComparer.Ordinal))
        {
            return false;
        }

        var anchor = model.FindConcept(question.Anchor, SemanticConceptKind.Individual);
        if (anchor is null || !reasoner.IsInstanceOf(anchor.Id, requiredType))
        {
            return false;
        }

        var membershipSources = reasoner.GetMembershipEvidence(anchor.Id, requiredType)
            .Select(source => new LatticeEvidenceSource(
                LatticeKnowledgeSourceKind.FoundationalKnowledge,
                $"{anchor.Label} is modeled as a {question.MemberType} candidate.",
                source.SourceId,
                null))
            .ToImmutableArray();
        var premises = ImmutableArray.Create(
            new LatticeEvidence(
                LatticeEvidenceKind.Asserted,
                LatticeEvidencePolarity.Positive,
                ImmutableArray.Create(new LatticeEvidenceSource(
                    LatticeKnowledgeSourceKind.UserAssertion,
                    sourceText,
                    "family-set-description",
                    null)),
                ImmutableArray<LatticeEvidence>.Empty),
            new LatticeEvidence(
                LatticeEvidenceKind.Asserted,
                LatticeEvidencePolarity.Positive,
                membershipSources,
                ImmutableArray<LatticeEvidence>.Empty));

        message = $"The {Ordinal(question.RequestedOrdinal)} {question.MemberType} is {anchor.Label}.";
        evidence = ImmutableArray.Create(new LatticeEvidence(
            LatticeEvidenceKind.Derived,
            LatticeEvidencePolarity.Positive,
            ImmutableArray<LatticeEvidenceSource>.Empty,
            premises));
        return true;
    }

    private static string Ordinal(int value) => value switch
    {
        1 => "first",
        2 => "second",
        3 => "third",
        4 => "fourth",
        5 => "fifth",
        6 => "sixth",
        7 => "seventh",
        8 => "eighth",
        9 => "ninth",
        10 => "tenth",
        _ => value.ToString(System.Globalization.CultureInfo.InvariantCulture)
    };
}
