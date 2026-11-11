using System.Collections.Immutable;

namespace Lattice.Core.Semantics;

public sealed record ObjectivePreferenceEdit(
    ObjectivePreferenceEditKind Kind,
    ImmutableArray<string> Values,
    string SourceText);
