using System.Collections.Immutable;

namespace Lattice.Core.Generation;

public sealed record GenerationOperation(
    string Id,
    string Symbol,
    string Effect,
    string ArgumentKind,
    ImmutableArray<string> Prerequisites)
{
    public string? StartSymbol { get; init; }

    /// <summary>
    /// The one argument value this operation realizes, when it realizes a named one, such as the name of a
    /// serialization format. The selection requires an exact match, so knowledge decides which call spells
    /// a request's format rather than shared code.
    /// </summary>
    public string? MatchesArgumentValue { get; init; }
}
