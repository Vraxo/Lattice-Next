using System.Text.Json;
using System.Text.RegularExpressions;
using Lattice.Core.Language;
using Lattice.Core.Semantics;

namespace Lattice.Core.Reasoning;

public sealed class KnowledgePackLoader
{
    private const int SupportedFormatVersion = 1;
    private const int MaximumClaims = 500;
    private static readonly Regex PackIdPattern = new(
        "^[a-z0-9]+(?:-[a-z0-9]+)*$",
        RegexOptions.CultureInvariant);

    private readonly LceParser parser = new();

    public bool TryLoadAndAdd(
        string? json,
        KnowledgeBase knowledgeBase,
        out KnowledgePack? pack,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(knowledgeBase);

        pack = null;
        error = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            error = "The knowledge pack is empty.";
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                error = "A knowledge pack must be a JSON object.";
                return false;
            }

            var properties = root.EnumerateObject().ToArray();
            var propertyNames = properties.Select(property => property.Name).ToArray();

            if (properties.Length != 4 ||
                propertyNames.Distinct(StringComparer.Ordinal).Count() != 4 ||
                propertyNames.Any(name => name is not ("formatVersion" or "id" or "name" or "claims")))
            {
                error = "A version 1 pack must contain exactly: formatVersion, id, name, and claims.";
                return false;
            }

            if (!root.TryGetProperty("formatVersion", out var versionElement) ||
                !versionElement.TryGetInt32(out var version))
            {
                error = "The pack formatVersion must be an integer.";
                return false;
            }

            if (version != SupportedFormatVersion)
            {
                error = $"Knowledge pack format version {version} is not supported. Supported version: {SupportedFormatVersion}.";
                return false;
            }

            if (!root.TryGetProperty("id", out var idElement) ||
                idElement.ValueKind != JsonValueKind.String)
            {
                error = "The pack id must be a string.";
                return false;
            }

            var id = idElement.GetString();

            if (string.IsNullOrWhiteSpace(id) ||
                id.Length > 64 ||
                !PackIdPattern.IsMatch(id))
            {
                error = "The pack id must contain lowercase letters, numbers, and single hyphens between segments.";
                return false;
            }

            if (!root.TryGetProperty("name", out var nameElement) ||
                nameElement.ValueKind != JsonValueKind.String)
            {
                error = "The pack name must be a string.";
                return false;
            }

            var name = nameElement.GetString();

            if (string.IsNullOrWhiteSpace(name) || name.Length > 120)
            {
                error = "The pack name must contain between 1 and 120 characters.";
                return false;
            }

            if (!root.TryGetProperty("claims", out var claimsElement) ||
                claimsElement.ValueKind != JsonValueKind.Array)
            {
                error = "The pack claims must be an array of LCE assertions.";
                return false;
            }

            var claimElements = claimsElement.EnumerateArray().ToArray();

            if (claimElements.Length is 0 or > MaximumClaims)
            {
                error = $"A pack must contain between 1 and {MaximumClaims} claims.";
                return false;
            }

            var utterances = new List<SemanticUtterance>(claimElements.Length);
            var validationKnowledgeBase = new KnowledgeBase();

            foreach (var claimElement in claimElements)
            {
                if (claimElement.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(claimElement.GetString()))
                {
                    error = "Every pack claim must be a non-empty string.";
                    return false;
                }

                var claim = parser.ParseText(claimElement.GetString());

                if (!claim.IsSuccess ||
                    claim.Utterances.Length != 1 ||
                    claim.Utterances[0].Kind != UtteranceKind.Assertion)
                {
                    error = $"Invalid pack claim: {claim.Error ?? "Each claim must be exactly one supported assertion."}";
                    return false;
                }

                var utterance = claim.Utterances[0];

                if (!validationKnowledgeBase.TryAdd(
                    utterance,
                    out _,
                    out var validationError,
                    KnowledgeSourceKind.ImportedClaim,
                    id))
                {
                    error = $"Unsupported pack claim '{utterance.SourceText}': {validationError}";
                    return false;
                }

                utterances.Add(utterance);
            }

            foreach (var utterance in utterances)
            {
                if (!knowledgeBase.TryAdd(
                    utterance,
                    out _,
                    out var addError,
                    KnowledgeSourceKind.ImportedClaim,
                    id))
                {
                    error = $"The validated pack could not be added: {addError}";
                    return false;
                }
            }

            pack = new KnowledgePack(version, id, name, utterances.Count);
            return true;
        }
        catch (JsonException)
        {
            error = "The knowledge pack is not valid JSON.";
            return false;
        }
    }
}
