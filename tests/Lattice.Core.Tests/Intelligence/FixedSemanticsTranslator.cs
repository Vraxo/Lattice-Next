using Lattice.Core.Intelligence;

namespace Lattice.Core.Tests.Intelligence;

internal sealed class FixedSemanticsTranslator : ISemanticsTranslator
{
    private readonly SemanticTranslationResponse response;
    private readonly Exception? failure;

    public FixedSemanticsTranslator(
        SemanticTranslationResponse response,
        Exception? failure = null)
    {
        this.response = response;
        this.failure = failure;
    }

    public string? ReceivedSourceText { get; private set; }

    public CancellationToken ReceivedCancellationToken { get; private set; }

    public ValueTask<SemanticTranslationResponse> TranslateAsync(
        string sourceText,
        CancellationToken cancellationToken = default)
    {
        ReceivedSourceText = sourceText;
        ReceivedCancellationToken = cancellationToken;

        return failure is null
            ? ValueTask.FromResult(response)
            : ValueTask.FromException<SemanticTranslationResponse>(failure);
    }
}
