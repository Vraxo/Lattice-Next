using System.Collections.Immutable;
using System.Text.Json.Serialization;

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

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(LatticePredicateApplication), "predicate")]
[JsonDerivedType(typeof(LatticeNegation), "negation")]
[JsonDerivedType(typeof(LatticeImplication), "implication")]
[JsonDerivedType(typeof(LatticeConjunction), "conjunction")]
[JsonDerivedType(typeof(LatticeExistentialQuantification), "exists")]
[JsonDerivedType(typeof(LatticeUniversalQuantification), "forall")]
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

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(LatticeIndividualTerm), "individual")]
[JsonDerivedType(typeof(LatticeAnonymousIndividualTerm), "anonymous")]
[JsonDerivedType(typeof(LatticeDescriptiveIndividualTerm), "description")]
[JsonDerivedType(typeof(LatticeVariableTerm), "variable")]
[JsonDerivedType(typeof(LatticeUnresolvedReferenceTerm), "reference")]
public abstract record LatticeSemanticTerm;

public sealed record LatticeIndividualTerm(string Identifier) : LatticeSemanticTerm;

public sealed record LatticeAnonymousIndividualTerm(string Identity) : LatticeSemanticTerm;

public sealed record LatticeDescriptiveIndividualTerm(
    string Head,
    ImmutableArray<string> Modifiers) : LatticeSemanticTerm;

public sealed record LatticeVariableTerm(string Identifier) : LatticeSemanticTerm;

public sealed record LatticeUnresolvedReferenceTerm(string Reference) : LatticeSemanticTerm;
