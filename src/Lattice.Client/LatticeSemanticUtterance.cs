using System.Collections.Immutable;

namespace Lattice.Client;

/// <summary>
/// A transport-neutral semantic utterance for submitting reviewed interpretations.
/// </summary>
public sealed record LatticeSemanticUtterance(
    LatticeSemanticUtteranceKind Kind,
    LatticeSemanticFormula Meaning,
    string SourceText,
    string? ResolvedReferent = null);

public enum LatticeSemanticUtteranceKind
{
    Assertion,
    Question
}

public abstract record LatticeSemanticFormula;

public sealed record LatticePredicateApplication(
    string Predicate,
    ImmutableArray<LatticeSemanticTerm> Arguments) : LatticeSemanticFormula;

public sealed record LatticeNegation(LatticeSemanticFormula Formula) : LatticeSemanticFormula;

public sealed record LatticeImplication(
    LatticeSemanticFormula Premise,
    LatticeSemanticFormula Conclusion) : LatticeSemanticFormula;

public sealed record LatticeConjunction(
    ImmutableArray<LatticeSemanticFormula> Conjuncts) : LatticeSemanticFormula;

public sealed record LatticeExistentialQuantification(
    string Variable,
    LatticeSemanticFormula Body) : LatticeSemanticFormula;

public sealed record LatticeUniversalQuantification(
    string Variable,
    LatticeSemanticFormula Body) : LatticeSemanticFormula;

public abstract record LatticeSemanticTerm;

public sealed record LatticeIndividualTerm(string Identifier) : LatticeSemanticTerm;

public sealed record LatticeAnonymousIndividualTerm(string Identity) : LatticeSemanticTerm;

public sealed record LatticeDescriptiveIndividualTerm(
    string Head,
    ImmutableArray<string> Modifiers) : LatticeSemanticTerm;

public sealed record LatticeVariableTerm(string Identifier) : LatticeSemanticTerm;

public sealed record LatticeUnresolvedReferenceTerm(string Reference) : LatticeSemanticTerm;
