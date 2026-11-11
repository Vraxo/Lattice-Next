using System.Collections.Immutable;

namespace Lattice.Core.Reasoning;

/// <summary>
/// The evidence and status for one independently stated objective preference.
/// </summary>
public sealed record ObjectivePreferenceAssessment(
    string Preference,
    ObjectiveConditionAssessmentStatus Status,
    ImmutableArray<KnowledgeAttribute> Evidence);
