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

        if (utterance.Meaning is not PredicateApplication predicate ||
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

    public void Observe(ToolObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        LatestToolObservation = observation;
    }

    public ReferenceResolutionResult Resolve(SemanticUtterance utterance)
    {
        ArgumentNullException.ThrowIfNull(utterance);

        if (utterance.Meaning is not PredicateApplication predicate ||
            predicate.Arguments.IsDefault ||
            predicate.Arguments.Length != 1 ||
            predicate.Arguments[0] is not UnresolvedReferenceTerm)
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
                0,
                new IndividualTerm(referent))
        };
        var resolvedUtterance = utterance with
        {
            Meaning = resolvedPredicate,
            ResolvedReferent = referent
        };

        return new ReferenceResolutionResult(
            ReferenceResolutionStatus.Resolved,
            resolvedUtterance);
    }
}
