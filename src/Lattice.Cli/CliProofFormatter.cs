using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;
using Spectre.Console;

namespace Lattice.Cli;

internal static class CliProofFormatter
{
    static void WriteProofGroup(string heading, IReadOnlyList<ProofNode> proofs)
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
    
    static IEnumerable<string> GetProofStatements(ProofNode proof)
    {
        var emitted = new HashSet<string>(StringComparer.Ordinal);
        return Collect(proof, emitted);
    
        static IEnumerable<string> Collect(ProofNode current, HashSet<string> emitted)
        {
            if (current.Premise is not null)
            {
                foreach (var statement in Collect(current.Premise, emitted))
                {
                    yield return statement;
                }
            }
    
            if (!current.AdditionalPremises.IsDefaultOrEmpty)
            {
                foreach (var additionalPremise in current.AdditionalPremises)
                {
                    foreach (var statement in Collect(additionalPremise, emitted))
                    {
                        yield return statement;
                    }
                }
            }
    
            foreach (var source in current.Sources)
            {
                var referent = source.ResolvedReferent is { } resolved
                    ? $" [it resolved to {resolved.Value}]"
                    : string.Empty;
                var origin = source.Kind == KnowledgeSourceKind.UserAssertion
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
