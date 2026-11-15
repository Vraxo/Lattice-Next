namespace Lattice.Core.Semantics;

public enum SemanticWorldModelQuestionKind
{
    Definition,
    WhyClassification,
    Ability,
    Mechanism,
    Causes,
    Compare,
    WhyUse,
    Applications,
    Clarification,
    RequiredKnowledge,
    ContributionQuery,
    WhyHarmful,
    BeginnerGuidance,
    CreativeIdeas,
    ListExamples,
    MaximumDataValue,
    HighestDataValue,
    NotableExamples,
    RegionalName,
    Ingredients,
    EventOutcomes,
    ContextualRelationRoleQuery,
    ProductionLocationReasons
}

public sealed record SemanticWorldModelQuestionFormula(
    SemanticWorldModelQuestionKind Kind,
    string Subject,
    string? Concept = null,
    string? OtherSubject = null,
    string? TargetType = null,
    int Limit = 0) : SemanticFormula;
