namespace Lattice.Core.Semantics;

public sealed record ObjectiveCandidateInformationFormula(
    SemanticIdentifier Candidate,
    SemanticIdentifier Attribute,
    ObjectiveConditionValue Value) : SemanticFormula;
