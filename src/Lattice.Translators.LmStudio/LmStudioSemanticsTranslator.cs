using System.Collections.Immutable;
using System.Text.Json;
using Lattice.Core.Intelligence;
using Lattice.Core.Language;
using Lattice.Core.Semantics;

namespace Lattice.Translators.LmStudio;

public sealed class LmStudioSemanticsTranslator : ISemanticsTranslator
{
    private const int MaximumCandidates = 4;
    private const int MaximumOutputTokens = 320;

    private const string SystemPrompt = """
        You translate one ordinary-language statement or question into Lattice Controlled English (LCE) 0.9.
        Return only a JSON object with exactly these properties:
        - "candidates": an array of zero to four strings. Each string must be exactly one complete LCE sentence.
        - "message": a short string explaining an abstention, or null.
        Do not put Markdown fences around the JSON.

        LCE 0.9 is a small, literal grammar, not unrestricted English. Use only these forms:
        - "Every <one or more lowercase class words> is a <lowercase concept>."
        - "Every <class words> <third-person singular verb ending in s> <one fixed object>."
        - "A <description> exists."
        - "A <description> is <property>." or "A <description> is a <concept>."
        - "<One capitalized name> is a <concept>."
        - "The <description> is <property>."
        - "It is a <concept>."
        - "<Name, definite description, or It> <third-person singular verb> <one object>."
        - Questions use "Is <name> a <concept>?", "Is it a <concept>?",
          "Is the <description> <property>?", "Does <name or it> <base-form verb> <one object>?",
          "Does <name> <base-form verb> it?", and "Does a <description> exist?"

        LCE words are single lowercase words or hyphenated words. Proper names are one capitalized token.
        Use only the grammar forms above. LCE has no negation, disjunction, comparisons, conditionals other
        than the stated universal rule, plural quantifiers, relational joins, or multi-sentence form.
        Do not silently drop, reverse, weaken, strengthen, or invent meaning. Do not replace a word with a
        synonym merely to force a translation. Never resolve pronouns in the translator. Preserve a supported
        singular "it" as an unresolved LCE reference. ConversationContext handles reference resolution only
        after the user accepts the candidate. If the meaning cannot be represented faithfully, return an empty
        candidates array.
        If the wording has multiple materially different readings, return a separate candidate for each
        defensible reading, or abstain when the readings cannot be stated reliably.
        A candidate must retain the original polarity, quantifier, predicate, arguments, and question/assertion
        distinction. Prefer abstention over guessing.
        """;

    private readonly HttpClient httpClient;
    private readonly LmStudioConfiguration configuration;
    private readonly LmStudioRuntime runtime;
    private readonly LceParser parser;
    private readonly Action<string>? reportStatus;

    public LmStudioSemanticsTranslator(
        HttpClient httpClient,
        LmStudioConfiguration configuration,
        LmStudioRuntime runtime,
        LceParser? parser = null,
        Action<string>? reportStatus = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(runtime);

        this.httpClient = httpClient;
        this.configuration = configuration;
        this.runtime = runtime;
        this.parser = parser ?? new LceParser();
        this.reportStatus = reportStatus;
    }

    public async ValueTask<SemanticTranslationResponse> TranslateAsync(
        string sourceText,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceText))
        {
            return new SemanticTranslationResponse(
                ImmutableArray<SemanticTranslationCandidate>.Empty,
                "Input is empty.");
        }

        if (string.IsNullOrWhiteSpace(configuration.ModelKey))
        {
            throw new InvalidOperationException("No LM Studio model is configured. Run :translator setup first.");
        }

        var modelIdentifier = await runtime.EnsureModelReadyAsync(
            configuration.ModelKey,
            configuration.ModelIdentifier,
            reportStatus,
            cancellationToken).ConfigureAwait(false);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(runtime.RequestTimeout);

        using var request = runtime.CreateChatRequest(
            new
            {
                model = modelIdentifier,
                messages = new[]
                {
                    new { role = "system", content = SystemPrompt },
                    new { role = "user", content = sourceText }
                },
                temperature = 0.0,
                max_tokens = MaximumOutputTokens,
                response_format = new { type = "json_object" }
            });

        using var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            timeout.Token).ConfigureAwait(false);
        var responseBody = await runtime.ReadChatResponseAsync(response, timeout.Token)
            .ConfigureAwait(false);

        using var completion = JsonDocument.Parse(responseBody);
        var content = completion.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new JsonException("LM Studio returned an empty assistant message.");
        }

        return ParseTranslation(content);
    }

    private SemanticTranslationResponse ParseTranslation(string content)
    {
        using var document = JsonDocument.Parse(content);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("candidates", out var candidatesElement) ||
            candidatesElement.ValueKind != JsonValueKind.Array ||
            candidatesElement.GetArrayLength() > MaximumCandidates)
        {
            throw new JsonException(
                "The translator response must contain a candidates array with no more than four entries.");
        }

        var message = root.TryGetProperty("message", out var messageElement)
            ? messageElement.ValueKind switch
            {
                JsonValueKind.String => messageElement.GetString(),
                JsonValueKind.Null => null,
                _ => throw new JsonException("The translator message must be a string or null.")
            }
            : throw new JsonException("The translator response is missing its message property.");
        var candidates = ImmutableArray.CreateBuilder<SemanticTranslationCandidate>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var candidateElement in candidatesElement.EnumerateArray())
        {
            if (candidateElement.ValueKind != JsonValueKind.String)
            {
                throw new JsonException("Every translation candidate must be an LCE string.");
            }

            var lce = candidateElement.GetString();

            if (string.IsNullOrWhiteSpace(lce))
            {
                throw new JsonException("Translation candidates cannot be empty.");
            }

            var parsed = parser.Parse(lce);

            if (!parsed.IsSuccess || parsed.Utterance is null)
            {
                return new SemanticTranslationResponse(
                    ImmutableArray<SemanticTranslationCandidate>.Empty,
                    $"The model output was outside LCE 0.9: {parsed.Error}");
            }

            var key = SemanticFormatter.Format(parsed.Utterance);

            if (seen.Add(key))
            {
                candidates.Add(new SemanticTranslationCandidate(
                    parsed.Utterance.Kind,
                    parsed.Utterance.Meaning));
            }
        }

        return new SemanticTranslationResponse(candidates.ToImmutable(), message);
    }
}
