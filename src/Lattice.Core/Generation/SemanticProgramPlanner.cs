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

    /// <summary>
    /// Request facts that express an output or arithmetic intent which the generic requirement model
    /// does not represent yet. A request that carries one of them is planned by another path until the
    /// matching migration group replaces the fact.
    /// </summary>
    private static readonly ImmutableArray<string> UnmappedScalarIntentFactIds =
    [
        "program.arithmetic",
        "program.output",
        "program.output.start"
    ];

    private static readonly ImmutableArray<string> UnmappedScalarIntentFactPrefixes =
    [
        "value.output"
    ];

    /// <summary>
    /// Request facts that describe capabilities planned by a later migration group (functions,
    /// collections, files, classes, loops, and the stored scalar task families). A request that carries
    /// one of them is left to its own path instead of being planned as a scalar program.
    /// </summary>
    private static readonly ImmutableArray<string> DeferredProgramFactPrefixes =
    [
        "program.assignment.classification",
        "program.assignment.first",
        "program.assignment.second",
        "program.assignment.result",
        "program.classification",
        "program.class",
        "program.collection",
        "program.dictionary",
        "program.directory",
        "program.even",
        "program.file",
        "program.for",
        "program.function",
        "program.greeting",
        "program.import",
        "program.json",
        "program.list",
        "program.loop",
        "program.multi-field",
        "program.non-empty",
        "program.record",
        "program.single-field",
        "program.try",
        "program.while",
        "program.with"
    ];

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
        var defined = new HashSet<string>(
            program.Collections.IsDefault
                ? Array.Empty<string>()
                : program.Collections.Select(collection => collection.Name),
            StringComparer.Ordinal);
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

    /// <summary>
    /// Plans and renders a program from extracted request facts. Returns false when the facts describe a
    /// capability that another path still owns, so that no partial program is ever emitted.
    /// </summary>
    public static bool TryGenerateProgram(
        ImmutableArray<SequenceSemanticFact> facts,
        GenerationLanguagePack pack,
        out string code,
        out ImmutableArray<PlannedProgramStatement> statements,
        out string? error)
    {
        code = string.Empty;
        statements = ImmutableArray<PlannedProgramStatement>.Empty;
        error = null;

        if (facts.IsDefaultOrEmpty)
        {
            error = "A generated program requires extracted semantic facts.";
            return false;
        }

        if (!SemanticProgramFactMapper.TryCreate(facts, out var program, out error) || program is null)
        {
            return false;
        }

        if (HasDeferredProgramIntent(facts))
        {
            error = "The request describes a capability that is not planned by the scalar program planner.";
            return false;
        }

        if (!ContainsOutputRequirement(program.Requirements) && HasUnmappedScalarIntent(facts))
        {
            error = "The request expresses an output or arithmetic intent that its requirements do not carry.";
            return false;
        }

        if (!TryPlan(program, pack, out statements, out error))
        {
            return false;
        }

        code = Render(statements);
        return code.Length > 0;
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

    private static bool HasDeferredProgramIntent(ImmutableArray<SequenceSemanticFact> facts) =>
        facts.Any(fact => DeferredProgramFactPrefixes.Any(prefix =>
            fact.Id.StartsWith(prefix, StringComparison.Ordinal)));

    private static bool HasUnmappedScalarIntent(ImmutableArray<SequenceSemanticFact> facts) =>
        facts.Any(fact =>
            UnmappedScalarIntentFactIds.Contains(fact.Id, StringComparer.Ordinal) ||
            UnmappedScalarIntentFactPrefixes.Any(prefix =>
                fact.Id.StartsWith(prefix, StringComparison.Ordinal)));

    private static bool ContainsOutputRequirement(ImmutableArray<ProgramRequirement> requirements) =>
        !requirements.IsDefaultOrEmpty &&
        requirements.Any(requirement => requirement switch
        {
            OutputRequirement => true,
            ConditionalRequirement conditional =>
                ContainsOutputRequirement(conditional.ThenRequirements) ||
                ContainsOutputRequirement(conditional.ElseRequirements),
            _ => false
        });

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
                case IterationRequirement iteration:
                    if (!TryPlanIteration(iteration, depth, defined, pack, pipeline, planned, out error))
                    {
                        return false;
                    }

                    break;
                case AccumulationRequirement accumulation:
                    if (!TryPlanAccumulation(accumulation, depth, defined, pack, pipeline, planned, out error))
                    {
                        return false;
                    }

                    break;
                case FilterRequirement filter:
                    if (!TryPlanCollectionExpression(
                            filter.ResultName,
                            filter.ItemName,
                            filter.Source,
                            filter.Predicate,
                            "predicate",
                            "filter-binding",
                            "filter",
                            depth,
                            defined,
                            pack,
                            pipeline,
                            planned,
                            out error))
                    {
                        return false;
                    }

                    break;
                case MapRequirement map:
                    if (!TryPlanCollectionExpression(
                            map.ResultName,
                            map.ItemName,
                            map.Source,
                            map.Value,
                            "expression",
                            "map-binding",
                            "map",
                            depth,
                            defined,
                            pack,
                            pipeline,
                            planned,
                            out error))
                    {
                        return false;
                    }

                    break;
                case SortRequirement sort:
                    if (!TryPlanSort(sort, depth, defined, pack, pipeline, planned, out error))
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

        if (!TryEmitAssignment(
                assignment.VariableName,
                rendered,
                depth,
                defined,
                pack,
                pipeline,
                planned,
                out error))
        {
            return false;
        }

        defined.Add(assignment.VariableName);
        return true;
    }

    private static bool TryEmitAssignment(
        string variableName,
        string renderedValue,
        int depth,
        IReadOnlySet<string> defined,
        GenerationLanguagePack pack,
        SequenceGenerationPipeline pipeline,
        ImmutableArray<PlannedProgramStatement>.Builder planned,
        out string? error)
    {
        error = null;
        if (!TryRequireIdentifier(variableName, pack, "assignment target", out error))
        {
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
            .Add("target", variableName)
            .Add("expression", renderedValue);
        return TryEmit(
            pack,
            pipeline,
            effect,
            argumentKind: "assignment",
            argumentValue: variableName,
            bindings,
            depth,
            defined,
            planned,
            out error);
    }

    private static bool TryPlanIteration(
        IterationRequirement iteration,
        int depth,
        HashSet<string> defined,
        GenerationLanguagePack pack,
        SequenceGenerationPipeline pipeline,
        ImmutableArray<PlannedProgramStatement>.Builder planned,
        out string? error)
    {
        error = null;
        if (!TryRequireAvailable(iteration.Source, defined, out error))
        {
            return false;
        }

        if (!SemanticExpressionRenderer.TryRender(iteration.Source, pack, out var renderedSource))
        {
            error = $"The '{pack.Language}' language pack cannot render the iterated collection.";
            return false;
        }

        if (!TryEmitLoopHeader(
                iteration.ItemName,
                renderedSource,
                depth,
                defined,
                pack,
                pipeline,
                planned,
                out error))
        {
            return false;
        }

        var bodyDefined = new HashSet<string>(defined, StringComparer.Ordinal) { iteration.ItemName };
        if (!TryPlanRequirements(iteration.Body, depth + 1, bodyDefined, pack, pipeline, planned, out error))
        {
            return false;
        }

        defined.Add(iteration.ItemName);
        return true;
    }

    private static bool TryPlanAccumulation(
        AccumulationRequirement accumulation,
        int depth,
        HashSet<string> defined,
        GenerationLanguagePack pack,
        SequenceGenerationPipeline pipeline,
        ImmutableArray<PlannedProgramStatement>.Builder planned,
        out string? error)
    {
        error = null;
        if (!TryRequireAvailable(accumulation.Source, defined, out error) ||
            !TryRequireAvailable(accumulation.InitialValue, defined, out error))
        {
            return false;
        }

        if (!TryRequireAvailable(
                accumulation.Update,
                defined,
                out error,
                accumulation.AccumulatorName,
                accumulation.ItemName))
        {
            return false;
        }

        if (!SemanticExpressionRenderer.TryRender(accumulation.Source, pack, out var renderedSource))
        {
            error = $"The '{pack.Language}' language pack cannot render the accumulated collection.";
            return false;
        }

        if (!SemanticExpressionRenderer.TryRender(accumulation.InitialValue, pack, out var renderedInitial))
        {
            error = $"The '{pack.Language}' language pack cannot render the accumulator's initial value.";
            return false;
        }

        if (!SemanticExpressionRenderer.TryRender(accumulation.Update, pack, out var renderedUpdate))
        {
            error = $"The '{pack.Language}' language pack cannot render the accumulator update.";
            return false;
        }

        if (!TryEmitAssignment(
                accumulation.AccumulatorName,
                renderedInitial,
                depth,
                defined,
                pack,
                pipeline,
                planned,
                out error))
        {
            return false;
        }

        var loopDefined = new HashSet<string>(defined, StringComparer.Ordinal)
        {
            accumulation.AccumulatorName
        };
        if (!TryEmitLoopHeader(
                accumulation.ItemName,
                renderedSource,
                depth,
                loopDefined,
                pack,
                pipeline,
                planned,
                out error))
        {
            return false;
        }

        var bodyDefined = new HashSet<string>(loopDefined, StringComparer.Ordinal)
        {
            accumulation.ItemName
        };
        if (!TryEmitAssignment(
                accumulation.AccumulatorName,
                renderedUpdate,
                depth + 1,
                bodyDefined,
                pack,
                pipeline,
                planned,
                out error))
        {
            return false;
        }

        defined.Add(accumulation.AccumulatorName);
        defined.Add(accumulation.ItemName);
        return true;
    }

    private static bool TryPlanCollectionExpression(
        string resultName,
        string itemName,
        SemanticExpression source,
        SemanticExpression itemExpression,
        string itemBindingName,
        string argumentKind,
        string operationWord,
        int depth,
        HashSet<string> defined,
        GenerationLanguagePack pack,
        SequenceGenerationPipeline pipeline,
        ImmutableArray<PlannedProgramStatement>.Builder planned,
        out string? error)
    {
        error = null;
        if (!TryRequireAvailable(source, defined, out error) ||
            !TryRequireAvailable(itemExpression, defined, out error, itemName))
        {
            return false;
        }

        if (!TryRequireIdentifier(resultName, pack, $"{operationWord} result", out error) ||
            !TryRequireIdentifier(itemName, pack, $"{operationWord} item", out error))
        {
            return false;
        }

        if (!SemanticExpressionRenderer.TryRender(source, pack, out var renderedSource))
        {
            error = $"The '{pack.Language}' language pack cannot render the {operationWord} source.";
            return false;
        }

        if (!SemanticExpressionRenderer.TryRender(itemExpression, pack, out var renderedItemExpression))
        {
            error = $"The '{pack.Language}' language pack cannot render the {operationWord} expression.";
            return false;
        }

        string effect;
        switch (depth)
        {
            case 0:
                effect = $"emit-{argumentKind}";
                break;
            case 1:
                effect = $"emit-indented-{argumentKind}";
                break;
            case 2:
                effect = $"emit-double-indented-{argumentKind}";
                break;
            default:
                error = $"Program statements support at most {MaximumDepth} levels of nested blocks.";
                return false;
        }

        var bindings = ImmutableDictionary<string, string>.Empty
            .WithComparers(StringComparer.Ordinal)
            .Add("target", resultName)
            .Add("variable", itemName)
            .Add("iterable", renderedSource)
            .Add(itemBindingName, renderedItemExpression);
        if (!TryEmit(
                pack,
                pipeline,
                effect,
                argumentKind,
                resultName,
                bindings,
                depth,
                defined,
                planned,
                out error))
        {
            return false;
        }

        defined.Add(resultName);
        return true;
    }

    private static bool TryPlanSort(
        SortRequirement sort,
        int depth,
        HashSet<string> defined,
        GenerationLanguagePack pack,
        SequenceGenerationPipeline pipeline,
        ImmutableArray<PlannedProgramStatement>.Builder planned,
        out string? error)
    {
        error = null;
        if (!TryRequireAvailable(sort.Source, defined, out error) ||
            !TryRequireAvailable(sort.Key, defined, out error, sort.ItemName))
        {
            return false;
        }

        if (!TryRequireIdentifier(sort.ResultName, pack, "sorted result", out error) ||
            !TryRequireIdentifier(sort.ItemName, pack, "sorted item", out error))
        {
            return false;
        }

        if (!SemanticExpressionRenderer.TryRender(sort.Source, pack, out var renderedSource) ||
            !SemanticExpressionRenderer.TryRender(sort.Key, pack, out var renderedKey))
        {
            error = $"The '{pack.Language}' language pack cannot render the requested sort.";
            return false;
        }

        const string argumentKind = "sort-binding";
        var direction = sort.Descending ? "reverse-sort" : "sort";
        string effect;
        switch (depth)
        {
            case 0:
                effect = $"emit-{direction}-binding";
                break;
            case 1:
                effect = $"emit-indented-{direction}-binding";
                break;
            case 2:
                effect = $"emit-double-indented-{direction}-binding";
                break;
            default:
                error = $"Program statements support at most {MaximumDepth} levels of nested blocks.";
                return false;
        }

        var bindings = ImmutableDictionary<string, string>.Empty
            .WithComparers(StringComparer.Ordinal)
            .Add("target", sort.ResultName)
            .Add("variable", sort.ItemName)
            .Add("iterable", renderedSource)
            .Add("key", renderedKey);
        if (!TryEmit(
                pack,
                pipeline,
                effect,
                argumentKind,
                sort.ResultName,
                bindings,
                depth,
                defined,
                planned,
                out error))
        {
            return false;
        }

        defined.Add(sort.ResultName);
        return true;
    }

    private static bool TryEmitLoopHeader(
        string itemName,
        string renderedSource,
        int depth,
        IReadOnlySet<string> defined,
        GenerationLanguagePack pack,
        SequenceGenerationPipeline pipeline,
        ImmutableArray<PlannedProgramStatement>.Builder planned,
        out string? error)
    {
        error = null;
        if (!TryRequireIdentifier(itemName, pack, "loop item", out error))
        {
            return false;
        }

        string effect;
        switch (depth)
        {
            case 0:
                effect = "emit-loop-header";
                break;
            case 1:
                effect = "emit-indented-loop-header";
                break;
            case 2:
                effect = "emit-double-indented-loop-header";
                break;
            default:
                error = $"Program statements support at most {MaximumDepth} levels of nested blocks.";
                return false;
        }

        var bindings = ImmutableDictionary<string, string>.Empty
            .WithComparers(StringComparer.Ordinal)
            .Add("variable", itemName)
            .Add("iterable", renderedSource);
        return TryEmit(
            pack,
            pipeline,
            effect,
            argumentKind: "loop-header",
            argumentValue: renderedSource,
            bindings,
            depth,
            defined,
            planned,
            out error);
    }

    private static bool TryRequireIdentifier(
        string name,
        GenerationLanguagePack pack,
        string description,
        out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(name) ||
            pack.InputSyntax is not { } syntax ||
            !GenerationInputSyntaxParser.IsIdentifier(name, syntax))
        {
            error = $"The {description} '{name}' is not an identifier of the '{pack.Language}' language pack.";
            return false;
        }

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
        out string? error,
        params string[] boundByRequirement)
    {
        error = null;
        foreach (var name in SemanticExpressionAnalysis.GetReferencedNames(expression))
        {
            if (!defined.Contains(name) &&
                !boundByRequirement.Contains(name, StringComparer.Ordinal))
            {
                error = $"The program uses the value '{name}' before any requirement defines it.";
                return false;
            }
        }

        return true;
    }
}
