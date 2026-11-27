using System.Collections.Immutable;
using Lattice.Client;
using Lattice.Core.Generation;

namespace Lattice.Runtime;

/// <summary>
/// Answers a program-generation request from the declarative sequence knowledge and the selected language
/// pack. One path realizes a program: the facts the request states are projected onto the language-neutral
/// requirement model and planned onto the pack's operations, so a request whose facts name no requirement
/// stays unanswered instead of being rendered by a second mechanism.
/// </summary>
internal static class KnowledgeDrivenGenerationResponder
{
    private static readonly Lazy<SequenceKnowledgeBase?> EmbeddedKnowledge = new(LoadKnowledge);
    private static readonly Lazy<ImmutableDictionary<string, GenerationLanguagePack>?> EmbeddedLanguagePacks =
        new(LoadLanguagePacks);

    internal static bool TryGenerate(
        string input,
        out LatticeUtteranceResult? result)
    {
        result = null;
        var knowledge = EmbeddedKnowledge.Value;
        if (knowledge is null ||
            !SequenceInputFactExtractor.TryExtract(
                knowledge,
                input,
                out var facts,
                out _) ||
            facts.IsDefaultOrEmpty)
        {
            return false;
        }

        var languagePack = SelectLanguagePack(facts);
        if (languagePack is null ||
            !SemanticProgramPlanner.TryGenerateProgram(
                facts,
                languagePack,
                out var programCode,
                out var plannedStatements,
                out _))
        {
            return false;
        }

        var fence = new string((char)96, 3);
        result = new LatticeUtteranceResult(
            LatticeUtteranceResultKind.TaskCompleted,
            $"{fence}{languagePack.Language}\n{programCode}\n{fence}",
            $"semantic-requirements({string.Join(", ", plannedStatements.Select(statement => statement.Effect).Distinct(StringComparer.Ordinal))})",
            SupportingEvidence: ImmutableArray<LatticeEvidence>.Empty,
            OpposingEvidence: ImmutableArray<LatticeEvidence>.Empty);
        return true;
    }

    private static SequenceKnowledgeBase? LoadKnowledge() =>
        SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out _)
            ? knowledge
            : null;

    private static ImmutableDictionary<string, GenerationLanguagePack>? LoadLanguagePacks()
    {
        var loader = new GenerationLanguagePackLoader();
        return loader.TryLoadEmbedded(out var packs, out _) ? packs : null;
    }

    private static GenerationLanguagePack? SelectLanguagePack(
        ImmutableArray<SequenceSemanticFact> facts)
    {
        var languageFactId = facts
            .Select(fact => fact.Id)
            .FirstOrDefault(id => id.StartsWith("language.", StringComparison.Ordinal));
        if (languageFactId is null || EmbeddedLanguagePacks.Value is not { } packs)
        {
            return null;
        }

        var language = languageFactId["language.".Length..];
        return packs.TryGetValue(language, out var pack) ? pack : null;
    }
}
