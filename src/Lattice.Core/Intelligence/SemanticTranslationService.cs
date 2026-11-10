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
        catch (Exception exception)
        {
            var message = string.IsNullOrWhiteSpace(exception.Message)
                ? "The semantics translator failed."
                : $"The semantics translator failed: {exception.Message}";

            return new SemanticTranslationResult(
                SemanticTranslationStatus.Failed,
                ImmutableArray<SemanticUtterance>.Empty,
                message);
        }

        if (response is null || response.Candidates.IsDefault)
        {
            return Rejected("The translator returned an invalid response.");
        }

        if (response.Outcome == SemanticTranslationOutcome.Unsupported)
        {
            return response.Candidates.IsEmpty
                ? new SemanticTranslationResult(SemanticTranslationStatus.NoInterpretation, ImmutableArray<SemanticUtterance>.Empty, response.Message ?? "The input is unsupported.")
                : Rejected("An unsupported translation response cannot contain candidates.");
        }

        if (response.Outcome == SemanticTranslationOutcome.Invalid)
        {
            return response.Candidates.IsEmpty
                ? Rejected(response.Message ?? "The translator returned an invalid candidate.")
                : Rejected("An invalid translation response cannot contain candidates.");
        }

        var candidateCountMatches = response.Outcome switch
        {
            SemanticTranslationOutcome.Translated => response.Candidates.Length == 1,
            SemanticTranslationOutcome.Ambiguous => response.Candidates.Length >= 2,
            _ => false
        };

        if (!candidateCountMatches)
        {
            return Rejected("The translator outcome does not match its candidate count.");
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
        var status = response.Outcome == SemanticTranslationOutcome.Translated
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
