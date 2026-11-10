using Lattice.Core.Semantics;
using Lattice.Core.Tools;

namespace Lattice.Core.Context;

public sealed class ConversationContext
{
    private readonly HashSet<SemanticIdentifier> individuals = [];

    public int KnownIndividualCount => individuals.Count;

    public ToolObservation? LatestToolObservation { get; private set; }

    public void Observe(SemanticUtterance utterance)
    {
        ArgumentNullException.ThrowIfNull(utterance);

        if (!TryGetPredicate(utterance.Meaning, out var predicate) ||
            predicate.Arguments.IsDefault)
        {
            return;
        }

        foreach (var argument in predicate.Arguments)
        {
            switch (argument)
            {
                case IndividualTerm individual:
                    individuals.Add(individual.Identifier);
                    break;

                case DescriptiveIndividualTerm description:
                    individuals.Add(description.Identifier);
                    break;
            }
        }
    }

    private static bool TryGetPredicate(
        SemanticFormula formula,
        out PredicateApplication predicate)
    {
        predicate = formula switch
        {
            PredicateApplication direct => direct,
            Negation { Formula: PredicateApplication negated } => negated,
            _ => null!
        };

        return predicate is not null;
    }

    public void Observe(ToolObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        LatestToolObservation = observation;
    }

    public ReferenceResolutionResult Resolve(SemanticUtterance utterance)
    {
        ArgumentNullException.ThrowIfNull(utterance);

        if (!TryGetPredicate(utterance.Meaning, out var predicate) ||
            predicate.Arguments.IsDefault)
        {
            return new ReferenceResolutionResult(
                ReferenceResolutionStatus.NotRequired,
                utterance);
        }

        var referenceIndices = predicate.Arguments
            .Select((argument, index) =>
                argument is UnresolvedReferenceTerm ? index : -1)
            .Where(index => index >= 0)
            .ToArray();

        if (referenceIndices.Length == 0)
        {
            return new ReferenceResolutionResult(
                ReferenceResolutionStatus.NotRequired,
                utterance);
        }

        if (referenceIndices.Length != 1 ||
            predicate.Arguments.Length is < 1 or > 2 ||
            predicate.Arguments
                .Where((_, index) => index != referenceIndices[0])
                .Any(argument =>
                    argument is not IndividualTerm &&
                    argument is not DescriptiveIndividualTerm))
        {
            return new ReferenceResolutionResult(
                ReferenceResolutionStatus.NotRequired,
                utterance);
        }

        if (individuals.Count == 0)
        {
            return new ReferenceResolutionResult(
                ReferenceResolutionStatus.MissingReferent,
                null);
        }

        if (individuals.Count > 1)
        {
            return new ReferenceResolutionResult(
                ReferenceResolutionStatus.AmbiguousReferent,
                null);
        }

        var referent = individuals.Single();
        var resolvedPredicate = predicate with
        {
            Arguments = predicate.Arguments.SetItem(
                referenceIndices[0],
                new IndividualTerm(referent))
        };
        var resolvedMeaning = utterance.Meaning is Negation
            ? new Negation(resolvedPredicate)
            : resolvedPredicate;
        var resolvedUtterance = utterance with
        {
            Meaning = resolvedMeaning,
            ResolvedReferent = referent
        };

        return new ReferenceResolutionResult(
            ReferenceResolutionStatus.Resolved,
            resolvedUtterance);
    }
}
