namespace Lattice.Core.Semantics;

public enum SemanticWorldModelQuestionKind
{
    Definition,
    WhyClassification,
    Mechanism,
    Causes,
    Compare,
    RelationPath
}

public sealed record SemanticWorldModelQuestionFormula(
    SemanticWorldModelQuestionKind Kind,
    string Subject,
    string? Concept = null,
    string? OtherSubject = null) : SemanticFormula;
