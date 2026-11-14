namespace Lattice.Core.Semantics;

public enum SemanticWorldModelQuestionKind
{
    Definition,
    WhyClassification,
    CapabilityExplanation,
    Mechanism,
    Causes,
    Compare,
    Membership,
    InverseRelation,
    RelationQuery,
    ContributionQuery,
    ImpactExplanation,
    TypedObjectRelationQuery,
    EventOutcomeExplanation,
    HighestPointQuery,
    RegionalTerminologyQuery,
    CommonTypedRelationQuery,
    MediaExamplesByGenreAndPeriodQuery
}

public sealed record SemanticWorldModelQuestionFormula(
    SemanticWorldModelQuestionKind Kind,
    string Subject,
    string? Concept = null,
    string? OtherSubject = null,
    string? TargetType = null) : SemanticFormula;
