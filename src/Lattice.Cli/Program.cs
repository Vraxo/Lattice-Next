Console.OutputEncoding = System.Text.Encoding.UTF8;

Console.WriteLine("Lattice");
Console.WriteLine("Symbolic intelligence — clean-slate foundation");
Console.WriteLine();
Console.WriteLine("This build is only the console shell. Language understanding and reasoning are not implemented yet.");
Console.WriteLine("Type :help for commands or :quit to exit.");
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
        Console.WriteLine("Enter :quit to exit. Natural-language interpretation is not implemented in this scaffold.");
        continue;
    }

    if (string.Equals(input, ":status", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("Language: not implemented");
        Console.WriteLine("Semantics: not implemented");
        Console.WriteLine("Reasoning: not implemented");
        Console.WriteLine("Knowledge packs: none loaded");
        Console.WriteLine("Tools: none registered");
        continue;
    }

    Console.WriteLine("lattice> I received your input, but this build cannot interpret it yet.");
}
