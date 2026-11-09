using System.Collections.Immutable;
using Lattice.Core.Semantics;

namespace Lattice.Core.Intelligence;

public sealed class SemanticTranslationService
{
    private readonly ISemanticsTranslator translator;
    private readonly SemanticUtteranceValidator validator;

    public SemanticTranslationService(
        ISemanticsTranslator translator,
        SemanticUtteranceValidator? validator = null)
    {
        ArgumentNullException.ThrowIfNull(translator);
        this.translator = translator;
        this.validator = validator ?? new SemanticUtteranceValidator();
    }

    public async ValueTask<SemanticTranslationResult> TranslateAsync(
        string? sourceText,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceText))
        {
            return Rejected("Input is empty.");
        }

        SemanticTranslationResponse? response;

        try
        {
            response = await translator
                .TranslateAsync(sourceText, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return new SemanticTranslationResult(
                SemanticTranslationStatus.Failed,
                ImmutableArray<SemanticUtterance>.Empty,
                "The semantics translator failed.");
        }

        if (response is null || response.Candidates.IsDefault)
        {
            return Rejected("The translator returned an invalid response.");
        }

        if (response.Candidates.IsEmpty)
        {
            return new SemanticTranslationResult(
                SemanticTranslationStatus.NoInterpretation,
                ImmutableArray<SemanticUtterance>.Empty,
                response.Message ?? "No interpretation was produced.");
        }

        var candidates = ImmutableArray.CreateBuilder<SemanticUtterance>(
            response.Candidates.Length);

        foreach (var candidate in response.Candidates)
        {
            if (!validator.TryValidate(candidate, out var error))
            {
                return Rejected(error ?? "A semantic candidate was rejected.");
            }

            candidates.Add(new SemanticUtterance(
                candidate!.Kind,
                candidate.Meaning,
                sourceText));
        }

        var validatedCandidates = candidates.ToImmutable();
        var status = validatedCandidates.Length == 1
            ? SemanticTranslationStatus.ReadyForReview
            : SemanticTranslationStatus.Ambiguous;

        return new SemanticTranslationResult(
            status,
            validatedCandidates,
            response.Message);
    }

    private static SemanticTranslationResult Rejected(string message) =>
        new(
            SemanticTranslationStatus.Rejected,
            ImmutableArray<SemanticUtterance>.Empty,
            message);
}
