using System.Collections.Immutable;
using Lattice.Core.Semantics;

namespace Lattice.Core.Reasoning;

public sealed record UniversalRule(
    ImmutableArray<PredicateApplication> Premises,
    PredicateApplication Conclusion,
    string SourceText);
