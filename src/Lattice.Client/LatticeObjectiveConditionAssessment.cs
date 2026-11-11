using System.Collections.Immutable;

/// <summary>The assessment and supporting source references for one objective condition.</summary>
public sealed record LatticeObjectiveConditionAssessment(string Name, string Role, string Status, ImmutableArray<LatticeObjectiveEvidenceSource> Evidence);
