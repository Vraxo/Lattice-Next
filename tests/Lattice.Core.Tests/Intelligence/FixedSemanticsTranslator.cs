using Lattice.Core.Intelligence;

namespace Lattice.Core.Tests.Intelligence;

internal sealed class FixedSemanticsTranslator : ISemanticsTranslator
{
    private readonly SemanticTranslationResponse response;

    public FixedSemanticsTranslator(SemanticTranslationResponse response)
    {
        this.response = response;
    }

    public string? ReceivedSourceText { get; private set; }

    public CancellationToken ReceivedCancellationToken { get; private set; }

    public ValueTask<SemanticTranslationResponse> TranslateAsync(
        string sourceText,
        CancellationToken cancellationToken = default)
    {
        ReceivedSourceText = sourceText;
        ReceivedCancellationToken = cancellationToken;
        return ValueTask.FromResult(response);
    }
}
