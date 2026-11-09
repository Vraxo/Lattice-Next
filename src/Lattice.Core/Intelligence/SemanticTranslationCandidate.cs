using Lattice.Core.Semantics;

namespace Lattice.Core.Intelligence;

public sealed record SemanticTranslationCandidate(
    UtteranceKind Kind,
    SemanticFormula Meaning);
