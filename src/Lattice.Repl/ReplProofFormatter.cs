using Lattice.Client;
using Spectre.Console;

namespace Lattice.Repl;

internal static class ReplProofFormatter
{
    public static void WriteProofGroup(
        string heading,
        IReadOnlyList<LatticeEvidence> proofs)
    {
        if (proofs.Count == 0)
        {
            return;
        }

        AnsiConsole.MarkupLine($"[dim]{heading}[/]");

        for (var index = 0; index < proofs.Count; index++)
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
            foreach (var premise in current.Premises)
            {
                foreach (var statement in Collect(premise, emitted))
                {
                    yield return statement;
                }
            }

            foreach (var source in current.Sources)
            {
                var referent = source.ResolvedReferent is { } resolved
                    ? $" [it resolved to {resolved}]"
                    : string.Empty;
                var origin = source.Kind == LatticeKnowledgeSourceKind.UserAssertion
                    ? string.Empty
                    : $" [source: {source.Kind}]";
                var pack = string.IsNullOrWhiteSpace(source.SourceId)
                    ? string.Empty
                    : $" [pack: {source.SourceId}]";
                var statement = $"{source.SourceText}{referent}{origin}{pack}";

                if (emitted.Add(statement))
                {
                    yield return statement;
                }
            }
        }
    }
}
