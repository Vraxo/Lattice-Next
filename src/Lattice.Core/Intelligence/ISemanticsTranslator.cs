using Lattice.Core.Semantics;

namespace Lattice.Core.Intelligence;

public interface ISemanticsTranslator
{
    ValueTask<SemanticTranslationResponse> TranslateAsync(
        string sourceText,
        CancellationToken cancellationToken = default);
}
