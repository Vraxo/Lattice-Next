using Spectre.Console;

namespace Lattice.Repl;

internal static class ReplHelp
{
    public static void Write()
    {
        AnsiConsole.MarkupLine("[bold]Commands[/]");
        AnsiConsole.MarkupLine("  [green]:tools[/]  [dim]list registered capabilities[/]");
        AnsiConsole.MarkupLine("  [green]:permissions[/]  [dim]show remembered and session-only tool grants[/]");
        AnsiConsole.MarkupLine("  [green]:permissions revoke <tool-id>[/]  [dim]forget a tool's saved approval[/]");
        AnsiConsole.MarkupLine("  [green]:translator[/]  [dim]show local LM Studio configuration[/]");
        AnsiConsole.MarkupLine("  [green]:translator setup[/]  [dim]start LM Studio, discover installed models, and save a selection[/]");
        AnsiConsole.MarkupLine("  [green]:translate <text>[/]  [dim]translate one ordinary-language statement/question into LCE for review[/]");
        AnsiConsole.MarkupLine("  [dim]Translation is opt-in. Lattice never stores a model proposal without your explicit selection.[/]");
        AnsiConsole.MarkupLine("  [green]What is 12 multiplied by 3?[/] [dim]natural-language calculator request[/]");
        AnsiConsole.MarkupLine("  [green]Convert 150 cm to meters.[/] [dim]natural-language unit conversion[/]");
        AnsiConsole.MarkupLine("  [green]Search the web for Lattice symbolic reasoning.[/] [dim]public web search; no API key or server required[/]");
        AnsiConsole.MarkupLine("  [green]Multiply that by 5, then subtract 10.[/] [dim]chains operations over successive results[/]");
        AnsiConsole.MarkupLine("  [green]:tool calculator {\"left\":12,\"operation\":\"multiply\",\"right\":3}[/] [dim]explicit diagnostic invocation[/]");
        AnsiConsole.MarkupLine("  [dim]Natural-language selection uses exact phrase patterns declared by discovered tool manifests.[/]");
        AnsiConsole.MarkupLine("  [dim]Patterns support single-token numbers and words, multiword text captures, and optional literal phrases; they are not unrestricted English.[/]");
        AnsiConsole.MarkupLine("  [dim]The calculator and unit converter use Python's standard library. Web search also needs the pinned 'ddgs' package.[/]");
        AnsiConsole.MarkupLine("  [dim]Install it once with: python -m pip install -r tools/web-search/requirements.txt.[/]");
        AnsiConsole.MarkupLine("  [dim]Tool results remain sourced observations; they are not added to session knowledge.[/]");
        AnsiConsole.MarkupLine("  [dim]Tools are discovered from ./tools or LATTICE_TOOLS_DIR and start only when invoked.[/]");
        AnsiConsole.MarkupLine("  [dim]Tools ask before first use. Choose always to remember approval for that tool, or session-only.[/]");
        AnsiConsole.MarkupLine("  [dim]LATTICE_TOOL_PERMISSIONS can pre-grant read, write, or read,write for non-interactive runs.[/]");
        AnsiConsole.MarkupLine("[bold]Supported sentences[/]");
        AnsiConsole.MarkupLine("  [green]All dogs are animals.[/]       [dim]native plural universal[/]");
        AnsiConsole.MarkupLine("  [green]Only dogs are animals.[/]      [dim]native reverse quantifier[/]");
        AnsiConsole.MarkupLine("  [green]Fido belongs to the class of dogs.[/] [dim]native class membership[/]");
        AnsiConsole.MarkupLine("  [green]There is a red ball that is shiny.[/] [dim]native existential description[/]");
        AnsiConsole.MarkupLine("  [green]Every dog that is red likes milk.[/] [dim]native relative-clause rule[/]");
        AnsiConsole.MarkupLine("  [green]All red dogs are animals.[/]     [dim]shared noun-description grammar[/]");
        AnsiConsole.MarkupLine("  [green]All red and shiny dogs are animals.[/] [dim]coordinated modifiers[/]");
        AnsiConsole.MarkupLine("  [green]Some red dogs are animals.[/]   [dim]native existential quantifier[/]");
        AnsiConsole.MarkupLine("  [green]The ball that is red likes milk.[/] [dim]relative description as relation subject[/]");
        AnsiConsole.MarkupLine("  [green]Is milk liked by Fido?[/]       [dim]native passive relation question[/]");
        AnsiConsole.MarkupLine("  [green]Every dog is an animal.[/]  [dim]single-condition rule[/]");
        AnsiConsole.MarkupLine("  [green]Every red ball is a toy.[/] [dim]conjunctive class rule[/]");
        AnsiConsole.MarkupLine("  [green]A red ball exists.[/]     [dim]existential assertion[/]");
        AnsiConsole.MarkupLine("  [green]A red ball is shiny.[/]   [dim]indefinite property assertion[/]");
        AnsiConsole.MarkupLine("  [green]A red ball is a toy.[/]   [dim]existential class assertion[/]");
        AnsiConsole.MarkupLine("  [green]Does a red ball exist?[/] [dim]existence question[/]");
        AnsiConsole.MarkupLine("  [green]Fido is a dog.[/]            [dim]unary fact[/]");
        AnsiConsole.MarkupLine("  [green]It is a mammal.[/]          [dim]fact with reference[/]");
        AnsiConsole.MarkupLine("  [green]Fido likes milk.[/]         [dim]relation fact[/]");
        AnsiConsole.MarkupLine("  [green]It likes milk.[/]           [dim]relation fact with subject reference[/]");
        AnsiConsole.MarkupLine("  [green]Fido likes it.[/]           [dim]relation fact with object reference[/]");
        AnsiConsole.MarkupLine("  [green]Fido likes the red ball.[/] [dim]descriptive relation[/]");
        AnsiConsole.MarkupLine("  [green]The red ball is shiny.[/] [dim]property assertion[/]");
        AnsiConsole.MarkupLine("  [green]Is the red ball red?[/]    [dim]property question[/]");
        AnsiConsole.MarkupLine("  [green]Does Fido like the red ball?[/] [dim]descriptive relation query[/]");
        AnsiConsole.MarkupLine("  [green]Does Fido like milk?[/]     [dim]relation question[/]");
        AnsiConsole.MarkupLine("  [green]Does it like milk?[/]       [dim]relation question with subject reference[/]");
        AnsiConsole.MarkupLine("  [green]Does Fido like it?[/]       [dim]relation question with object reference[/]");
        AnsiConsole.MarkupLine("  [green]Is Fido an animal?[/]       [dim]unary question[/]");
        AnsiConsole.MarkupLine("  [green]Are all dogs animals?[/]    [dim]universal question[/]");
        AnsiConsole.MarkupLine("  [green]Is it a mammal?[/]          [dim]question with reference[/]");
        AnsiConsole.MarkupLine("[dim]Relation questions use the verb's base form; the parser maps it to third-person singular.[/]");
        AnsiConsole.MarkupLine("[dim]Universal rules infer unary or fixed-object relational conclusions from one or more unary conditions.[/]");
        AnsiConsole.MarkupLine("[dim]Combine complete sentences in one input; they are processed in order.[/]");
        AnsiConsole.MarkupLine("[dim]The word 'it' resolves only when exactly one individual is known in this session.[/]");
        AnsiConsole.MarkupLine("[dim]When the reference is missing or ambiguous, Lattice asks you to name the individual.[/]");
        AnsiConsole.MarkupLine("[dim]Input cleanup handles formatting artifacts only; spelling and grammar are not corrected.[/]");
        AnsiConsole.MarkupLine("[dim]Unknown means no proof was found; it does not mean false.[/]");
        AnsiConsole.MarkupLine("[dim]Definite descriptions become canonical identifiers (for example, 'the red ball' becomes red-ball).[/]");
        AnsiConsole.MarkupLine("[dim]Definite descriptions contribute head-noun and modifier facts to the knowledge base.[/]");
        AnsiConsole.MarkupLine("[dim]A rule such as 'Every red ball is a toy' requires both red(x) and ball(x).[/]");
        AnsiConsole.MarkupLine("[dim]Indefinite descriptions introduce unnamed witnesses; separate statements are not assumed to describe the same individual.[/]");
        AnsiConsole.MarkupLine("[dim]Existence questions can be supported by one matching witness or one already-known named individual.[/]");
        AnsiConsole.MarkupLine("[dim]All conditions must be supported for the same individual before a rule can fire.[/]");
        AnsiConsole.MarkupLine("[dim]Definite descriptions do not resolve synonyms; indefinite relational arguments remain unsupported.[/]");
        AnsiConsole.MarkupLine("[dim]LCE 0.9 is a small controlled-language subset, not full ACE or unrestricted English.[/]");
        AnsiConsole.WriteLine();
    }
}
