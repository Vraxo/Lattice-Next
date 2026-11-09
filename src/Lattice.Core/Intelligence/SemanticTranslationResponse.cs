using System.Collections.Immutable;

namespace Lattice.Core.Intelligence;

public sealed record SemanticTranslationResponse(
    ImmutableArray<SemanticTranslationCandidate> Candidates,
    string? Message = null);
