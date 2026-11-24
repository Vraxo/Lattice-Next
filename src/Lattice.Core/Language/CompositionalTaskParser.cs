using System.Collections.Immutable;
using Lattice.Core.Generation;
using Lattice.Core.Semantics;

namespace Lattice.Core.Language;

internal static class CompositionalTaskParser
{
    internal static bool TryParse(
        string source,
        string sourceText,
        out SemanticUtterance? utterance)
    {
        utterance = null;
        if (!SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out _) ||
            !SequenceInputFactExtractor.TryExtract(knowledge!, source, out var facts, out _) ||
            facts.IsDefaultOrEmpty ||
            !facts.Any(fact => fact.Id == "intent.program-generation"))
        {
            return false;
        }

        var languages = facts
            .Where(fact => fact.Id.StartsWith("language.", StringComparison.Ordinal))
            .ToArray();
        if (languages.Length != 1)
        {
            return false;
        }

        var language = languages[0].Id["language.".Length..];
        if (language.Length == 0)
        {
            return false;
        }

        var languagePackLoader = new GenerationLanguagePackLoader();
        if (!languagePackLoader.TryLoadEmbedded(out var languagePacks, out _) ||
            !languagePacks.TryGetValue(language, out var languagePack))
        {
            return false;
        }

        var request = new KnowledgeDrivenSequenceGenerationRequest(
            ImmutableArray<string>.Empty,
            facts.Select(fact => fact.Id).ToImmutableArray())
        {
            FactValues = facts,
            LanguagePack = languagePack
        };
        if (!KnowledgeDrivenSequenceGenerator.TryGenerate(
            knowledge!,
            null,
            request,
            out _,
            out _))
        {
            return false;
        }

        var formula = new TaskRequestFormula(
            TaskRequestAction.Code,
            "program-generation",
            language,
            "source-code",
            "program-generation",
            facts.Select(fact => fact.Id).ToImmutableArray());

        utterance = new SemanticUtterance(
            UtteranceKind.TaskRequest,
            formula,
            sourceText.Trim());
        return true;
    }
}
