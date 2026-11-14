namespace Lattice.Core.Semantics;

public enum SemanticWorldModelQuestionKind
{
    Definition,
    WhyClassification,
    Mechanism,
    Causes,
    Compare,
    Membership,
    InverseRelation,
    RelationQuery
}

public sealed record SemanticWorldModelQuestionFormula(
    SemanticWorldModelQuestionKind Kind,
    string Subject,
    string? Concept = null,
    string? OtherSubject = null,
    string? TargetType = null) : SemanticFormula;
