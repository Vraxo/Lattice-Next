using Lattice.Client;

namespace Lattice.Cli;

internal static class CliTextFormatter
{
    public static void WriteTurn(
        LatticeTurnResult turn,
        TextWriter standardOutput,
        TextWriter standardError)
    {
        if (!turn.IsSuccess)
        {
            standardError.WriteLine(turn.Error ?? "The turn was rejected.");
            return;
        }

        foreach (var utterance in turn.Utterances)
        {
            switch (utterance.Kind)
            {
                case LatticeUtteranceResultKind.StatementStored:
                    standardOutput.WriteLine(
                        $"Stored {(utterance.EntryKind ?? LatticeKnowledgeEntryKind.Fact).ToString().ToLowerInvariant()}: {utterance.Meaning}");
                    break;

                case LatticeUtteranceResultKind.QuestionAnswered:
                    standardOutput.WriteLine(utterance.TruthValue switch
                    {
                        LatticeTruthValue.True => "Yes.",
                        LatticeTruthValue.False => "No (explicit negative evidence).",
                        LatticeTruthValue.Both => "Contradiction: both the statement and its negation are supported.",
                        _ => "Unknown."
                    });
                    WriteEvidenceGroup(
                        "Supporting evidence:",
                        utterance.SupportingEvidence,
                        standardOutput);
                    WriteEvidenceGroup(
                        "Opposing evidence:",
                        utterance.OpposingEvidence,
                        standardOutput);
                    break;

                case LatticeUtteranceResultKind.StatementRejected:
                case LatticeUtteranceResultKind.ClarificationRequired:
                case LatticeUtteranceResultKind.UnsupportedQuestion:
                    standardError.WriteLine(
                        utterance.Message ?? "The utterance could not be processed.");
                    break;
            }
        }
    }

    private static void WriteEvidenceGroup(
        string heading,
        IReadOnlyList<LatticeEvidence> evidence,
        TextWriter output)
    {
        if (evidence.Count == 0)
        {
            return;
        }

        output.WriteLine(heading);

        foreach (var item in evidence)
        {
            foreach (var source in GetSources(item))
            {
                output.WriteLine($"  - {source}");
            }
        }
    }

    private static IEnumerable<string> GetSources(LatticeEvidence evidence)
    {
        foreach (var premise in evidence.Premises)
        {
            foreach (var source in GetSources(premise))
            {
                yield return source;
            }
        }

        foreach (var source in evidence.Sources)
        {
            var origin = source.Kind == LatticeKnowledgeSourceKind.UserAssertion
                ? string.Empty
                : $" [source: {source.Kind}]";
            var pack = string.IsNullOrWhiteSpace(source.SourceId)
                ? string.Empty
                : $" [pack: {source.SourceId}]";
            yield return $"{source.SourceText}{origin}{pack}";
        }
    }
}
