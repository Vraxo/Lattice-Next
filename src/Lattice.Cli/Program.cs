using Lattice.Core.Language;
using Lattice.Core.Semantics;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var parser = new ControlledEnglishParser();

Console.WriteLine("Lattice");
Console.WriteLine("Symbolic intelligence — controlled-language foundation");
Console.WriteLine();
Console.WriteLine("LCE 0.1 accepts one sentence per input. It parses meaning but does not reason yet.");
Console.WriteLine("Type :help for examples, :status for implementation status, or :quit to exit.");
Console.WriteLine();

while (true)
{
    Console.Write("you> ");

    var line = Console.ReadLine();

    if (line is null || string.Equals(line.Trim(), ":quit", StringComparison.OrdinalIgnoreCase))
    {
        break;
    }

    var input = line.Trim();

    if (input.Length == 0)
    {
        continue;
    }

    if (string.Equals(input, ":help", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("Supported forms:");
        Console.WriteLine("  Every dog is an animal.");
        Console.WriteLine("  Fido is a dog.");
        Console.WriteLine("  Is Fido an animal?");
        Console.WriteLine("LCE 0.1 is a small controlled-language subset, not full ACE or unrestricted English.");
        continue;
    }

    if (string.Equals(input, ":status", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("Language: LCE 0.1 parser");
        Console.WriteLine("Semantic representation: terms, predicates, implication, universal quantification");
        Console.WriteLine("Reasoning: not implemented");
        Console.WriteLine("Conversation context: not implemented");
        Console.WriteLine("Knowledge packs: none loaded");
        Console.WriteLine("Tools: none registered");
        continue;
    }

    var result = parser.Parse(input);

    if (result.Utterance is not { } utterance)
    {
        Console.WriteLine($"lattice> {result.Error}");
        continue;
    }

    var kind = utterance.Kind == UtteranceKind.Question ? "question" : "assertion";
    Console.WriteLine($"lattice> Parsed {kind}.");
    Console.WriteLine($"meaning> {SemanticFormatter.Format(utterance)}");
}
