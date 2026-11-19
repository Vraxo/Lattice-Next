using System.Collections.Immutable;
using System.Text;
using Lattice.Core.Generation;
using Lattice.Core.Semantics;

namespace Lattice.Runtime.Generation;

internal sealed class PythonProgramBackend : IProgramBackend
{
    public string TargetLanguage => "python";

    public bool TryGenerate(
        ProgramSpecification program,
        out string source,
        out string? error)
    {
        source = string.Empty;
        error = null;

        if (program.Statements.IsDefaultOrEmpty || program.Statements.Length > 64)
        {
            error = "A program requires between one and 64 statements.";
            return false;
        }

        var goals = ImmutableArray.CreateBuilder<GenerationGoal>(program.Statements.Length);
        foreach (var statement in program.Statements)
        {
            switch (statement)
            {
                case ProgramAssignmentStatement assignment when IsIdentifier(assignment.Target):
                    goals.Add(new GenerationGoal(
                        "assign-variable",
                        "assignment",
                        $"{assignment.Target} = {RenderExpression(assignment.Value)}",
                        ImmutableArray<string>.Empty));
                    break;

                case ProgramOutputStatement { Value: ProgramStringLiteral text }:
                    goals.Add(new GenerationGoal(
                        "write-standard-output",
                        "text",
                        text.Value,
                        ImmutableArray<string>.Empty));
                    break;

                case ProgramOutputStatement output:
                    goals.Add(new GenerationGoal(
                        "write-standard-output",
                        "expression",
                        RenderExpression(output.Value),
                        ImmutableArray<string>.Empty));
                    break;

                default:
                    error = $"The Python backend does not support statement '{statement.GetType().Name}'.";
                    return false;
            }
        }

        var loader = new GenerationLanguagePackLoader();
        if (!loader.TryLoadEmbedded(out var packs, out error) ||
            !packs.TryGetValue(TargetLanguage, out var pack))
        {
            error ??= "The Python generation pack is unavailable.";
            return false;
        }

        if (!new SequenceGenerationPipeline().TryGenerateProgram(
            pack,
            goals.ToImmutable(),
            out var generated,
            out error))
        {
            return false;
        }

        source = generated!.Text;
        return true;
    }

    private static string RenderExpression(ProgramExpression expression) =>
        expression switch
        {
            ProgramNameExpression name => name.Name,
            ProgramNumberLiteral number => number.Value,
            ProgramStringLiteral text => $"\\"{EscapeString(text.Value)}\\"",
            ProgramUnaryExpression { Operator: ProgramUnaryOperator.Negate } unary =>
                $"-({RenderExpression(unary.Operand)})",
            ProgramBinaryExpression binary =>
                $"({RenderExpression(binary.Left)} {GetOperator(binary.Operator)} {RenderExpression(binary.Right)})",
            _ => throw new ArgumentOutOfRangeException(nameof(expression), expression, "Unsupported program expression.")
        };

    private static string GetOperator(ProgramBinaryOperator operation) =>
        operation switch
        {
            ProgramBinaryOperator.Add => "+",
            ProgramBinaryOperator.Subtract => "-",
            ProgramBinaryOperator.Multiply => "*",
            ProgramBinaryOperator.Divide => "/",
            ProgramBinaryOperator.Remainder => "%",
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "Unsupported program operator.")
        };

    private static string EscapeString(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            switch (character)
            {
                case '\\\\':
                    builder.Append("\\\\\\\\");
                    break;
                case '"':
                    builder.Append("\\\\\"");
                    break;
                case '\\r':
                    builder.Append("\\\\r");
                    break;
                case '\\n':
                    builder.Append("\\\\n");
                    break;
                case '\\t':
                    builder.Append("\\\\t");
                    break;
                default:
                    builder.Append(character);
                    break;
            }
        }

        return builder.ToString();
    }
    private static bool IsIdentifier(string value) =>
        value.Length > 0 &&
        (char.IsAsciiLetter(value[0]) || value[0] == '_') &&
        value.Skip(1).All(character => char.IsAsciiLetterOrDigit(character) || character == '_');
}
