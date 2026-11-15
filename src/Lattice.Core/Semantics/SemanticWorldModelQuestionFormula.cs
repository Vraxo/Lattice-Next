namespace Lattice.Core.Semantics;

public enum SemanticWorldModelQuestionKind
{
    Definition,
    WhyClassification,
    Ability,
    Mechanism,
    Causes,
    Compare,
    ContributionQuery,
    WhyHarmful,
    BeginnerGuidance
}

public sealed record SemanticWorldModelQuestionFormula(
    SemanticWorldModelQuestionKind Kind,
    string Subject,
    string? Concept = null,
    string? OtherSubject = null,
    string? TargetType = null) : SemanticFormula;
