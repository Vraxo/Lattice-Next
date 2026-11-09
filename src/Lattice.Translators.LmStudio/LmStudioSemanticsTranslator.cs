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

        The "candidates" array is only for complete sentences that follow the grammar below.
        Never put explanations, refusals, or ordinary-language text in "candidates".
        If meaning is unsupported, return "candidates": [] and put a brief reason in "message".

        Examples:
        Input: Is Fido a dog?
        Output: {"candidates":["Is Fido a dog?"],"message":null}
        Input: Could Fido be a dog?
        Output: {"candidates":[],"message":"LCE 0.9 cannot express the possibility in 'could'."}
        Input: No dog is a cat.
        Output: {"candidates":[],"message":"LCE 0.9 does not support negation."}

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
        after the user accepts the candidate. If the meaning cannot be represented faithfully, return an empty "candidates" array
        and put the reason only in "message".
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
                input = sourceText,
                system_prompt = SystemPrompt,
                temperature = 0.0,
                max_output_tokens = MaximumOutputTokens,
                reasoning = configuration.Reasoning,
                store = false
            });

        using var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            timeout.Token).ConfigureAwait(false);
        var responseBody = await runtime.ReadChatResponseAsync(response, timeout.Token)
            .ConfigureAwait(false);

        using var completion = JsonDocument.Parse(responseBody);
        var content = GetAssistantMessageContent(completion.RootElement);
        return ParseTranslation(content);
    }

    private static string GetAssistantMessageContent(JsonElement response)
    {
        if (!response.TryGetProperty("output", out var output) ||
            output.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("LM Studio returned a native chat response without an output array.");
        }

        var messages = new List<string>();
        var outputTypes = new List<string>();

        foreach (var item in output.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object ||
                !item.TryGetProperty("type", out var typeElement) ||
                typeElement.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var type = typeElement.GetString();
            if (type is not null)
            {
                outputTypes.Add(type);
            }

            if (!string.Equals(type, "message", StringComparison.Ordinal))
            {
                continue;
            }

            if (!item.TryGetProperty("content", out var contentElement) ||
                contentElement.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var content = contentElement.GetString();
            if (!string.IsNullOrWhiteSpace(content))
            {
                messages.Add(content);
            }
        }

        var messageContent = string.Concat(messages);
        if (!string.IsNullOrWhiteSpace(messageContent))
        {
            return messageContent;
        }

        var outputDescription = outputTypes.Count == 0
            ? "none"
            : string.Join(", ", outputTypes.Distinct(StringComparer.Ordinal));
        var reasoningDescription = GetReasoningTokenCount(response) is { } reasoningTokens &&
            reasoningTokens > 0
                ? $" The model produced {reasoningTokens} reasoning token(s), but no message content."
                : string.Empty;

        throw new JsonException(
            $"LM Studio returned no assistant message. Output item types: {outputDescription}.{reasoningDescription}");
    }

    private static int? GetReasoningTokenCount(JsonElement response) =>
        response.TryGetProperty("stats", out var stats) &&
        stats.ValueKind == JsonValueKind.Object &&
        stats.TryGetProperty("reasoning_output_tokens", out var reasoningTokens) &&
        reasoningTokens.TryGetInt32(out var count)
            ? count
            : null;

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
                    $"The model returned a candidate that is not valid LCE 0.9 ({JsonSerializer.Serialize(lce)}): {parsed.Error}");
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
