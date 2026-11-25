using System.Collections.Immutable;

namespace Lattice.Core.Generation;

/// <summary>One planned statement of a semantic program, realized through one declarative operation.</summary>
public sealed record PlannedProgramStatement(
    int Depth,
    string Effect,
    string OperationId,
    string Text);

/// <summary>
/// Plans language-neutral program requirements onto declarative language operations and realizes each
/// statement through the shared sequence pipeline. Every token comes from the selected language pack;
/// this type contains no target-language syntax, task identities, or fixed sample values.
/// </summary>
public static class SemanticProgramPlanner
{
    private const int MaximumDepth = 2;
    private const int MaximumRequirements = 64;
    private const int MaximumStatements = 256;

    /// <summary>Plans the ordered requirements of one program into realized statements.</summary>
    public static bool TryPlan(
        SemanticProgram program,
        GenerationLanguagePack pack,
        out ImmutableArray<PlannedProgramStatement> statements,
        out string? error)
    {
        statements = ImmutableArray<PlannedProgramStatement>.Empty;
        error = null;

        if (program is null || pack is null)
        {
            error = "A program plan requires a semantic program and a language pack.";
            return false;
        }

        if (program.Requirements.IsDefaultOrEmpty ||
            program.Requirements.Length > MaximumRequirements)
        {
            error = $"A program plan requires between one and {MaximumRequirements} requirements.";
            return false;
        }

        var planned = ImmutableArray.CreateBuilder<PlannedProgramStatement>();
        var defined = new HashSet<string>(StringComparer.Ordinal);
        if (!TryPlanRequirements(
                program.Requirements,
                depth: 0,
                defined,
                pack,
                new SequenceGenerationPipeline(),
                planned,
                out error))
        {
            return false;
        }

        statements = planned.ToImmutable();
        return true;
    }

    /// <summary>Renders planned statements as ordered source lines.</summary>
    public static string Render(ImmutableArray<PlannedProgramStatement> statements)
    {
        if (statements.IsDefaultOrEmpty)
        {
            return string.Empty;
        }

        return string.Join('\n', statements.Select(statement => statement.Text));
    }

    private static bool TryPlanRequirements(
        ImmutableArray<ProgramRequirement> requirements,
        int depth,
        HashSet<string> defined,
        GenerationLanguagePack pack,
        SequenceGenerationPipeline pipeline,
        ImmutableArray<PlannedProgramStatement>.Builder planned,
        out string? error)
    {
        error = null;
        foreach (var requirement in requirements)
        {
            if (planned.Count >= MaximumStatements)
            {
                error = $"A program plan supports at most {MaximumStatements} statements.";
                return false;
            }

            switch (requirement)
            {
                case AssignmentRequirement assignment:
                    if (!TryPlanAssignment(assignment, depth, defined, pack, pipeline, planned, out error))
                    {
                        return false;
                    }

                    break;
                case OutputRequirement output:
                    if (!TryPlanOutput(output, depth, defined, pack, pipeline, planned, out error))
                    {
                        return false;
                    }

                    break;
                case ConditionalRequirement conditional:
                    if (!TryPlanConditional(conditional, depth, defined, pack, pipeline, planned, out error))
                    {
                        return false;
                    }

                    break;
                default:
                    error = $"The program requirement '{requirement.GetType().Name}' has no known operation.";
                    return false;
            }
        }

        return true;
    }

    private static bool TryPlanAssignment(
        AssignmentRequirement assignment,
        int depth,
        HashSet<string> defined,
        GenerationLanguagePack pack,
        SequenceGenerationPipeline pipeline,
        ImmutableArray<PlannedProgramStatement>.Builder planned,
        out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(assignment.VariableName) ||
            pack.InputSyntax is not { } assignmentSyntax ||
            !GenerationInputSyntaxParser.IsIdentifier(assignment.VariableName, assignmentSyntax))
        {
            error = $"The assignment target '{assignment.VariableName}' is not an identifier of the " +
                $"'{pack.Language}' language pack.";
            return false;
        }

        if (!TryRequireAvailable(assignment.Value, defined, out error))
        {
            return false;
        }

        if (!SemanticExpressionRenderer.TryRender(assignment.Value, pack, out var rendered))
        {
            error = $"The '{pack.Language}' language pack cannot render the value assigned to " +
                $"'{assignment.VariableName}'.";
            return false;
        }

        string effect;
        switch (depth)
        {
            case 0:
                effect = "emit-assignment";
                break;
            case 1:
                effect = "emit-indented-assignment";
                break;
            case 2:
                effect = "emit-double-indented-assignment";
                break;
            default:
                error = $"Program statements support at most {MaximumDepth} levels of nested blocks.";
                return false;
        }

        var bindings = ImmutableDictionary<string, string>.Empty
            .WithComparers(StringComparer.Ordinal)
            .Add("target", assignment.VariableName)
            .Add("expression", rendered);
        if (!TryEmit(
                pack,
                pipeline,
                effect,
                argumentKind: "assignment",
                argumentValue: assignment.VariableName,
                bindings,
                depth,
                defined,
                planned,
                out error))
        {
            return false;
        }

        defined.Add(assignment.VariableName);
        return true;
    }

    private static bool TryPlanOutput(
        OutputRequirement output,
        int depth,
        HashSet<string> defined,
        GenerationLanguagePack pack,
        SequenceGenerationPipeline pipeline,
        ImmutableArray<PlannedProgramStatement>.Builder planned,
        out string? error)
    {
        error = null;
        if (!TryRequireAvailable(output.Value, defined, out error))
        {
            return false;
        }

        if (!SemanticExpressionRenderer.TryRender(output.Value, pack, out var rendered))
        {
            error = $"The '{pack.Language}' language pack cannot render a requested output value.";
            return false;
        }

        string effect;
        switch (depth)
        {
            case 0:
                effect = "write-standard-output";
                break;
            case 1:
                effect = "write-standard-output-indented-expression";
                break;
            case 2:
                effect = "write-standard-output-double-indented-expression";
                break;
            default:
                error = $"Program statements support at most {MaximumDepth} levels of nested blocks.";
                return false;
        }

        return TryEmit(
            pack,
            pipeline,
            effect,
            argumentKind: "expression",
            argumentValue: rendered,
            ImmutableDictionary<string, string>.Empty.WithComparers(StringComparer.Ordinal),
            depth,
            defined,
            planned,
            out error);
    }

    private static bool TryPlanConditional(
        ConditionalRequirement conditional,
        int depth,
        HashSet<string> defined,
        GenerationLanguagePack pack,
        SequenceGenerationPipeline pipeline,
        ImmutableArray<PlannedProgramStatement>.Builder planned,
        out string? error)
    {
        error = null;
        if (depth >= MaximumDepth)
        {
            error = $"Program statements support at most {MaximumDepth} levels of nested blocks.";
            return false;
        }

        var thenRequirements = conditional.ThenRequirements.IsDefault
            ? ImmutableArray<ProgramRequirement>.Empty
            : conditional.ThenRequirements;
        var elseRequirements = conditional.ElseRequirements.IsDefault
            ? ImmutableArray<ProgramRequirement>.Empty
            : conditional.ElseRequirements;
        if (thenRequirements.IsEmpty && elseRequirements.IsEmpty)
        {
            error = "A conditional requirement needs at least one statement in a branch.";
            return false;
        }

        if (thenRequirements.IsEmpty)
        {
            error = "A conditional requirement with an else branch needs a true-branch statement.";
            return false;
        }

        if (!TryRequireAvailable(conditional.Condition, defined, out error))
        {
            return false;
        }

        if (!SemanticExpressionRenderer.TryRender(conditional.Condition, pack, out var renderedCondition))
        {
            error = $"The '{pack.Language}' language pack cannot render a requested condition.";
            return false;
        }

        string effect;
        switch (depth)
        {
            case 0:
                effect = "emit-conditional-header";
                break;
            case 1:
                effect = "emit-indented-conditional-header";
                break;
            default:
                effect = "emit-double-indented-conditional-header";
                break;
        }

        var conditionBindings = ImmutableDictionary<string, string>.Empty
            .WithComparers(StringComparer.Ordinal)
            .Add("condition", renderedCondition);
        if (!TryEmit(
                pack,
                pipeline,
                effect,
                argumentKind: "condition",
                argumentValue: renderedCondition,
                conditionBindings,
                depth,
                defined,
                planned,
                out error))
        {
            return false;
        }

        var thenDefined = new HashSet<string>(defined, StringComparer.Ordinal);
        if (!TryPlanRequirements(
                thenRequirements,
                depth + 1,
                thenDefined,
                pack,
                pipeline,
                planned,
                out error))
        {
            return false;
        }

        if (elseRequirements.IsEmpty)
        {
            defined.UnionWith(thenDefined);
            return true;
        }

        var elseEffect = depth switch
        {
            0 => "emit-else",
            1 => "emit-indented-else",
            _ => "emit-double-indented-else"
        };
        if (!TryEmit(
                pack,
                pipeline,
                elseEffect,
                argumentKind: "none",
                argumentValue: "else",
                ImmutableDictionary<string, string>.Empty.WithComparers(StringComparer.Ordinal),
                depth,
                defined,
                planned,
                out error))
        {
            return false;
        }

        var elseDefined = new HashSet<string>(defined, StringComparer.Ordinal);
        if (!TryPlanRequirements(
                elseRequirements,
                depth + 1,
                elseDefined,
                pack,
                pipeline,
                planned,
                out error))
        {
            return false;
        }

        defined.UnionWith(thenDefined.Intersect(elseDefined, StringComparer.Ordinal));
        return true;
    }

    private static bool TryEmit(
        GenerationLanguagePack pack,
        SequenceGenerationPipeline pipeline,
        string effect,
        string argumentKind,
        string argumentValue,
        ImmutableDictionary<string, string> bindings,
        int depth,
        IReadOnlySet<string> defined,
        ImmutableArray<PlannedProgramStatement>.Builder planned,
        out string? error)
    {
        var capabilities = defined
            .Select(name => $"variable:{name}")
            .Order(StringComparer.Ordinal)
            .ToImmutableArray();
        var goal = new GenerationGoal(effect, argumentKind, argumentValue, capabilities)
        {
            Bindings = bindings
        };

        if (!pipeline.TryGenerate(pack, goal, out var result, out error))
        {
            return false;
        }

        if (result!.SearchLimitReached)
        {
            error = $"The planned statement '{effect}' reached the sequence generation search limit.";
            return false;
        }

        planned.Add(new PlannedProgramStatement(
            depth,
            effect,
            result.SelectedOperationId,
            result.Text));
        return true;
    }

    private static bool TryRequireAvailable(
        SemanticExpression expression,
        IReadOnlySet<string> defined,
        out string? error)
    {
        error = null;
        foreach (var name in SemanticExpressionAnalysis.GetReferencedNames(expression))
        {
            if (!defined.Contains(name))
            {
                error = $"The program uses the value '{name}' before any requirement defines it.";
                return false;
            }
        }

        return true;
    }
}
