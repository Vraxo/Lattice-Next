using Lattice.Core.Semantics;

namespace Lattice.Core.Generation;

public interface IProgramBackend
{
    string TargetLanguage { get; }

    bool TryGenerate(
        ProgramSpecification program,
        out string source,
        out string? error);
}
