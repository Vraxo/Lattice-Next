using System.Collections.Immutable;

namespace Lattice.Core.Generation;

public sealed record GenerationStringLiteralFormat(
    string OpeningDelimiter,
    string ClosingDelimiter,
    ImmutableArray<GenerationEscapeSequence> Escapes);
