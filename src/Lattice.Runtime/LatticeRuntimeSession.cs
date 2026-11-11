using System.Collections.Immutable;
using Lattice.Client;
using Lattice.Core.Context;
using Lattice.Core.Intelligence;
using Lattice.Core.Language;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;

namespace Lattice.Runtime;

internal sealed class LatticeRuntimeSession : ILatticeSession
{
    private readonly object gate = new();
    private readonly LceParser parser = new();
    private readonly ConversationContext conversationContext = new();
    private readonly KnowledgeBase knowledgeBase = new();
    private readonly KnowledgePackLoader knowledgePackLoader = new();
    private readonly List<KnowledgePack> knowledgePacks = [];
    private readonly ReasoningEngine reasoningEngine = new();
    private bool isDisposed;

    public Guid SessionId { get; } = Guid.NewGuid();

    public ValueTask<LatticeTurnResult> SubmitAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (isDisposed)
            {
                return ValueTask.FromResult(
                    LatticeTurnResult.Rejected("The session has been closed."));
            }

            return ValueTask.FromResult(Process(input));
        }
    }

    public ValueTask<LatticeTurnResult> SubmitSemanticAsync(
        LatticeSemanticUtterance utterance,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(utterance);

        lock (gate)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (isDisposed)
            {
                return ValueTask.FromResult(
                    LatticeTurnResult.Rejected("The session has been closed."));
            }

            var result = ProcessUtterance(Map(utterance));
            return ValueTask.FromResult(
                LatticeTurnResult.Completed(ImmutableArray.Create(result)));
        }
    }

    public ValueTask<LatticeKnowledgePackResult> LoadKnowledgePackAsync(
        string json,
        CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (isDisposed)
            {
                return ValueTask.FromResult(
                    LatticeKnowledgePackResult.Rejected("The session has been closed."));
            }

            if (!knowledgePackLoader.TryLoadAndAdd(
                json,
                knowledgeBase,
                out var pack,
                out var error))
            {
                return ValueTask.FromResult(
                    LatticeKnowledgePackResult.Rejected(
                        error ?? "The knowledge pack could not be loaded."));
            }

            knowledgePacks.Add(pack!);
            return ValueTask.FromResult(
                LatticeKnowledgePackResult.Loaded(
                    pack!.Name,
                    pack.Id,
                    pack.ClaimCount));
        }
    }

    public LatticeSessionSnapshot GetSnapshot()
    {
        lock (gate)
        {
            return new LatticeSessionSnapshot(
                knowledgeBase.Facts.Count,
                knowledgeBase.Rules.Count,
                conversationContext.KnownIndividualCount,
                knowledgePacks.Count);
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (gate)
        {
            isDisposed = true;
        }

        return ValueTask.CompletedTask;
    }

    private LatticeTurnResult Process(string input)
    {
        var parseResult = parser.ParseText(input);

        if (!parseResult.IsSuccess)
        {
            return LatticeTurnResult.Rejected(
                parseResult.Error ?? "Input could not be parsed.");
        }

        var results = ImmutableArray.CreateBuilder<LatticeUtteranceResult>(
            parseResult.Utterances.Length);

        foreach (var parsedUtterance in parseResult.Utterances)
        {
            results.Add(ProcessUtterance(parsedUtterance));
        }

        return LatticeTurnResult.Completed(results.ToImmutable());
    }

    private LatticeUtteranceResult ProcessUtterance(SemanticUtterance parsedUtterance)
    {
        var resolution = conversationContext.Resolve(parsedUtterance);

        if (!resolution.IsSuccess)
        {
            return new LatticeUtteranceResult(
                LatticeUtteranceResultKind.ClarificationRequired,
                GetClarificationMessage(resolution.Status),
                ReferenceStatus: resolution.Status switch
                {
                    ReferenceResolutionStatus.MissingReferent =>
                        LatticeReferenceStatus.MissingReferent,
                    ReferenceResolutionStatus.AmbiguousReferent =>
                        LatticeReferenceStatus.AmbiguousReferent,
                    _ => null
                });
        }

        var utterance = resolution.Utterance!;

        if (utterance.Kind == UtteranceKind.Assertion)
        {
            if (!knowledgeBase.TryAdd(utterance, out var kind, out var error))
            {
                return new LatticeUtteranceResult(
                    LatticeUtteranceResultKind.StatementRejected,
                    error ?? "The statement could not be stored.",
                    SemanticFormatter.Format(utterance));
            }

            conversationContext.Observe(utterance);

            return new LatticeUtteranceResult(
                LatticeUtteranceResultKind.StatementStored,
                Meaning: SemanticFormatter.Format(utterance),
                EntryKind: kind switch
                {
                    KnowledgeEntryKind.Fact => LatticeKnowledgeEntryKind.Fact,
                    KnowledgeEntryKind.Rule => LatticeKnowledgeEntryKind.Rule,
                    _ => null
                });
        }

        var answer = utterance.Meaning switch
        {
            PredicateApplication predicateQuery =>
                reasoningEngine.Ask(knowledgeBase, predicateQuery),
            Negation negatedQuery =>
                reasoningEngine.Ask(knowledgeBase, negatedQuery),
            ExistentialQuantification existentialQuery =>
                reasoningEngine.Ask(knowledgeBase, existentialQuery),
            UniversalQuantification universalQuery =>
                reasoningEngine.Ask(knowledgeBase, universalQuery),
            _ => null
        };

        if (answer is null)
        {
            return new LatticeUtteranceResult(
                LatticeUtteranceResultKind.UnsupportedQuestion,
                "Unsupported question form.",
                SemanticFormatter.Format(utterance));
        }

        conversationContext.Observe(utterance);

        return new LatticeUtteranceResult(
            LatticeUtteranceResultKind.QuestionAnswered,
            Meaning: SemanticFormatter.Format(utterance),
            TruthValue: answer.TruthValue switch
            {
                TruthValue.True => LatticeTruthValue.True,
                TruthValue.False => LatticeTruthValue.False,
                TruthValue.Both => LatticeTruthValue.Both,
                _ => LatticeTruthValue.Unknown
            },
            SupportingEvidence: answer.Proofs.Select(MapEvidence).ToImmutableArray(),
            OpposingEvidence: answer.ContradictingProofs.Select(MapEvidence).ToImmutableArray());
    }

    private static string GetClarificationMessage(ReferenceResolutionStatus status) =>
        status switch
        {
            ReferenceResolutionStatus.MissingReferent =>
                "I don't know who 'it' refers to yet. Please name the individual.",
            ReferenceResolutionStatus.AmbiguousReferent =>
                "More than one individual is known. Which one do you mean by 'it'? Please name them.",
            _ => "I couldn't resolve that reference. Please name the individual."
        };

    private static SemanticUtterance Map(LatticeSemanticUtterance utterance) =>
        new(
            utterance.Kind switch
            {
                LatticeSemanticUtteranceKind.Assertion => UtteranceKind.Assertion,
                LatticeSemanticUtteranceKind.Question => UtteranceKind.Question,
                _ => throw new ArgumentOutOfRangeException(nameof(utterance))
            },
            Map(utterance.Meaning),
            utterance.SourceText)
        {
            ResolvedReferent = utterance.ResolvedReferent is null
                ? null
                : new SemanticIdentifier(utterance.ResolvedReferent)
        };

    private static SemanticFormula Map(LatticeSemanticFormula formula) =>
        formula switch
        {
            LatticePredicateApplication predicate => new PredicateApplication(
                new SemanticIdentifier(predicate.Predicate),
                predicate.Arguments.Select(Map).ToImmutableArray()),
            LatticeNegation negation => new Negation(Map(negation.Formula)),
            LatticeImplication implication => new Implication(
                Map(implication.Premise),
                Map(implication.Conclusion)),
            LatticeConjunction conjunction => new Conjunction(
                conjunction.Conjuncts.Select(Map).ToImmutableArray()),
            LatticeExistentialQuantification existential => new ExistentialQuantification(
                new VariableTerm(new SemanticIdentifier(existential.Variable)),
                Map(existential.Body)),
            LatticeUniversalQuantification universal => new UniversalQuantification(
                new VariableTerm(new SemanticIdentifier(universal.Variable)),
                Map(universal.Body)),
            _ => throw new ArgumentOutOfRangeException(nameof(formula))
        };

    private static SemanticTerm Map(LatticeSemanticTerm term) =>
        term switch
        {
            LatticeIndividualTerm individual =>
                new IndividualTerm(new SemanticIdentifier(individual.Identifier)),
            LatticeAnonymousIndividualTerm anonymous =>
                new AnonymousIndividualTerm(new SemanticIdentifier(anonymous.Identity)),
            LatticeDescriptiveIndividualTerm description =>
                new DescriptiveIndividualTerm(
                    new SemanticIdentifier(description.Head),
                    description.Modifiers.Select(modifier => new SemanticIdentifier(modifier)).ToImmutableArray()),
            LatticeVariableTerm variable =>
                new VariableTerm(new SemanticIdentifier(variable.Identifier)),
            LatticeUnresolvedReferenceTerm reference =>
                new UnresolvedReferenceTerm(new SemanticIdentifier(reference.Reference)),
            _ => throw new ArgumentOutOfRangeException(nameof(term))
        };

    private static LatticeEvidence MapEvidence(ProofNode proof)
    {
        var premises = ImmutableArray.CreateBuilder<LatticeEvidence>();

        if (proof.Premise is not null)
        {
            premises.Add(MapEvidence(proof.Premise));
        }

        if (!proof.AdditionalPremises.IsDefaultOrEmpty)
        {
            foreach (var premise in proof.AdditionalPremises)
            {
                premises.Add(MapEvidence(premise));
            }
        }

        return new LatticeEvidence(
            proof.Kind switch
            {
                ProofNodeKind.Asserted => LatticeEvidenceKind.Asserted,
                _ => LatticeEvidenceKind.Derived
            },
            proof.Polarity switch
            {
                EvidencePolarity.Positive => LatticeEvidencePolarity.Positive,
                _ => LatticeEvidencePolarity.Negative
            },
            proof.Sources.Select(source => new LatticeEvidenceSource(
                source.Kind switch
                {
                    KnowledgeSourceKind.UserAssertion => LatticeKnowledgeSourceKind.UserAssertion,
                    KnowledgeSourceKind.ImportedClaim => LatticeKnowledgeSourceKind.ImportedClaim,
                    _ => LatticeKnowledgeSourceKind.ExternalObservation
                },
                source.SourceText,
                source.SourceId,
                source.ResolvedReferent?.Value)).ToImmutableArray(),
            premises.ToImmutable());
    }
}
