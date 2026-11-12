using Lattice.Core.Semantics;

namespace Lattice.Core.Reasoning;

public sealed record ObjectiveEvidenceRequest(
    SemanticIdentifier Candidate,
    string ConditionName,
    string AttributeName,
    string Prompt,
    ObjectiveConditionValueKind ExpectedValueKind);
