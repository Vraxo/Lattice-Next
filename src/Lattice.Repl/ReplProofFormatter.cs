using System.Collections.Immutable;
using Lattice.Client;
using Spectre.Console;

namespace Lattice.Repl;

internal static class ReplProofFormatter
{
    public static void WriteProofGroup(
        string heading,
        ImmutableArray<LatticeEvidence> proofs)
    {
        if (proofs.IsDefaultOrEmpty)
        {
            return;
        }

        AnsiConsole.MarkupLine($"[dim]{heading}[/]");

        for (var index = 0; index < proofs.Length; index++)
        {
            if (index > 0)
            {
                AnsiConsole.MarkupLine("[dim]Another independent proof:[/]");
            }

            foreach (var statement in GetProofStatements(proofs[index]))
            {
                AnsiConsole.Markup("[dim]  • [/]");
                AnsiConsole.WriteLine(statement);
            }
        }
    }

    private static IEnumerable<string> GetProofStatements(LatticeEvidence proof)
    {
        var emitted = new HashSet<string>(StringComparer.Ordinal);
        return Collect(proof, emitted);

        static IEnumerable<string> Collect(
            LatticeEvidence current,
            HashSet<string> emitted)
        {
            foreach (var premise in current.Premises.IsDefault
                ? ImmutableArray<LatticeEvidence>.Empty
                : current.Premises)
            {
                foreach (var statement in Collect(premise, emitted))
                {
                    yield return statement;
                }
            }

            foreach (var source in current.Sources.IsDefault
                ? ImmutableArray<LatticeEvidenceSource>.Empty
                : current.Sources)
            {
                var referent = source.ResolvedReferent is { } resolved
                    ? $" [it resolved to {resolved}]"
                    : string.Empty;
                var origin = source.Kind == LatticeKnowledgeSourceKind.UserAssertion
                    ? string.Empty
                    : $" [source: {source.Kind}]";
                var reference = string.IsNullOrWhiteSpace(source.SourceId)
                    ? string.Empty
                    : source.Kind switch
                    {
                        LatticeKnowledgeSourceKind.FoundationalKnowledge => $" [fact: {source.SourceId}]",
                        LatticeKnowledgeSourceKind.ImportedClaim => $" [pack: {source.SourceId}]",
                        LatticeKnowledgeSourceKind.ExternalObservation => $" [observation: {source.SourceId}]",
                        _ => string.Empty
                    };
                var statement = $"{source.SourceText}{referent}{origin}{reference}";

                if (emitted.Add(statement))
                {
                    yield return statement;
                }
            }
        }
    }
}
