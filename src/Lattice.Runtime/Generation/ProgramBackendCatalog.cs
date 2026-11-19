using Lattice.Core.Generation;
using Lattice.Core.Semantics;

namespace Lattice.Runtime.Generation;

internal static class ProgramBackendCatalog
{
    private static readonly IProgramBackend[] Backends =
    [
        new PythonProgramBackend()
    ];

    internal static bool TryGenerate(
        string targetLanguage,
        ProgramSpecification program,
        out string source,
        out string? error)
    {
        source = string.Empty;
        error = null;

        var backend = Backends.FirstOrDefault(candidate =>
            candidate.TargetLanguage.Equals(targetLanguage, StringComparison.OrdinalIgnoreCase));
        if (backend is null)
        {
            error = $"No program backend is available for target language '{targetLanguage}'.";
            return false;
        }

        return backend.TryGenerate(program, out source, out error);
    }
}
