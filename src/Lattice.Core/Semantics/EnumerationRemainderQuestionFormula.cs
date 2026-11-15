using System.Collections.Immutable;

namespace Lattice.Core.Semantics;

/// <summary>
/// Represents a question asking for the one unnamed member in a stated enumeration.
/// </summary>
public sealed record EnumerationRemainderQuestionFormula(
    string Member,
    string Group,
    int ExpectedCount,
    int MissingPosition,
    ImmutableArray<string> NamedMembers) : SemanticFormula;
