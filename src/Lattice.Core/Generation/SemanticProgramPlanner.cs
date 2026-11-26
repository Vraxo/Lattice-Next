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
    private const int MaximumDepth = 4;
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
    /// Request facts that describe capabilities planned by a later migration group. A request that carries
    /// one of them is left to its own path instead of being planned from its generic requirements. Every
    /// prefix here still matches a fact that extraction can produce; `SequenceKnowledgeGuardTests` fails when
    /// one stops doing so, so the list shrinks as each family moves onto generic requirements.
    /// </summary>
    internal static readonly ImmutableArray<string> DeferredProgramFactPrefixes =
    [
        // 9.23 — explicit loops over a named collection.
        "program.loop"
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

        if (!SemanticProgramFactMapper.TryCreate(facts, pack.ExampleData, out var program, out error) ||
            program is null)
        {
            return false;
        }

        if (HasDeferredProgramIntent(facts, program))
        {
            error = "The request describes a capability that is not planned by the scalar program planner.";
            return false;
        }

        if (!CarriesDeclaredIntent(program.Requirements) && HasUnmappedScalarIntent(facts))
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

    private static bool HasDeferredProgramIntent(
        ImmutableArray<SequenceSemanticFact> facts,
        SemanticProgram program) =>
        facts.Any(fact => DeferredProgramFactPrefixes.Any(prefix =>
            fact.Id.StartsWith(prefix, StringComparison.Ordinal)) &&
            !IsCarriedByRequirements(fact.Id, program.Requirements));

    /// <summary>
    /// Whether the planned requirements already carry the structure a deferred fact names, so that a request
    /// which holds a collection of records is planned rather than refused.
    /// </summary>
    private static bool IsCarriedByRequirements(
        string factId,
        ImmutableArray<ProgramRequirement> requirements) =>
        factId.StartsWith("program.collection", StringComparison.Ordinal) &&
        CarriesCollection(requirements);

    private static bool CarriesCollection(ImmutableArray<ProgramRequirement> requirements) =>
        !requirements.IsDefaultOrEmpty &&
        requirements.Any(requirement => requirement switch
        {
            AssignmentRequirement assignment =>
                assignment.Value is SemanticListLiteralExpression or
                    SemanticRecordLiteralExpression or
                    SemanticMapLiteralExpression,
            IterationRequirement => true,
            DictionaryIterationRequirement => true,
            FilterRequirement => true,
            DictionaryMapRequirement => true,
            MapRequirement => true,
            SortRequirement => true,
            AccumulationRequirement => true,
            FunctionRequirement function => CarriesCollection(function.Body),
            ReturnRequirement returned =>
                returned.Value is SemanticSortExpression or SemanticFilterExpression or
                    SemanticListLiteralExpression or SemanticRecordLiteralExpression or
                    SemanticMapLiteralExpression,
            ConditionalRequirement conditional =>
                CarriesCollection(conditional.ThenRequirements) ||
                CarriesCollection(conditional.ElseRequirements),
            FileReadRequirement fileRead => CarriesCollection(fileRead.Body),
            FileWriteRequirement fileWrite => CarriesCollection(fileWrite.Body),
            DirectoryListingRequirement listing => CarriesCollection(listing.Body),
            TryRequirement attempt =>
                CarriesCollection(attempt.Body) ||
                attempt.Handlers.Any(handler => CarriesCollection(handler.Body)),
            _ => false
        });

    private static bool HasUnmappedScalarIntent(ImmutableArray<SequenceSemanticFact> facts) =>
        facts.Any(fact =>
            UnmappedScalarIntentFactIds.Contains(fact.Id, StringComparer.Ordinal) ||
            UnmappedScalarIntentFactPrefixes.Any(prefix =>
                fact.Id.StartsWith(prefix, StringComparison.Ordinal)));

    /// <summary>
    /// Whether the planned requirements already carry the intent the request expresses: either a scalar
    /// program that prints something, or a class declaration whose members and results carry it.
    /// </summary>
    private static bool CarriesDeclaredIntent(ImmutableArray<ProgramRequirement> requirements) =>
        !requirements.IsDefaultOrEmpty &&
        requirements.Any(requirement => requirement switch
        {
            OutputRequirement => true,
            ClassRequirement => true,
            SignClassificationRequirement => true,
            ConditionalRequirement conditional =>
                CarriesDeclaredIntent(conditional.ThenRequirements) ||
                CarriesDeclaredIntent(conditional.ElseRequirements),
            IterationRequirement iteration => CarriesDeclaredIntent(iteration.Body),
            DictionaryIterationRequirement dictionaryIteration =>
                CarriesDeclaredIntent(dictionaryIteration.Body),
            DirectoryListingRequirement listing => CarriesDeclaredIntent(listing.Body),
            FileReadRequirement fileRead => CarriesDeclaredIntent(fileRead.Body),
            TryRequirement attempt =>
                CarriesDeclaredIntent(attempt.Body) ||
                attempt.Handlers.Any(handler => CarriesDeclaredIntent(handler.Body)),
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
                case AugmentedAssignmentRequirement update:
                    if (!TryPlanAugmentedAssignment(update, depth, defined, pack, pipeline, planned, out error))
                    {
                        return false;
                    }

                    break;
                case ElementAssignmentRequirement element:
                    if (!TryPlanElementAssignment(element, depth, defined, pack, pipeline, planned, out error))
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
                case SignClassificationRequirement classification:
                    if (!TryPlanRequirements(
                        ExpandSignClassification(classification),
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
                case ConditionalRequirement conditional:
                    if (!TryPlanConditional(conditional, depth, defined, pack, pipeline, planned, out error))
                    {
                        return false;
                    }

                    break;
                case ConditionalChainRequirement chain:
                    if (!TryPlanChain(chain, depth, defined, pack, pipeline, planned, out error))
                    {
                        return false;
                    }

                    break;
                case FileWriteRequirement fileWrite:
                    if (!TryPlanFileWrite(fileWrite, depth, defined, pack, pipeline, planned, out error))
                    {
                        return false;
                    }

                    break;
                case SerializeRequirement serialize:
                    if (!TryPlanSerialize(serialize, depth, defined, pack, pipeline, planned, out error))
                    {
                        return false;
                    }

                    break;
                case DeserializeRequirement deserialize:
                    if (!TryPlanDeserialize(deserialize, depth, defined, pack, pipeline, planned, out error))
                    {
                        return false;
                    }

                    break;
                case DirectoryListingRequirement listing:
                    if (!TryPlanDirectoryListing(listing, depth, defined, pack, pipeline, planned, out error))
                    {
                        return false;
                    }

                    break;
                case ImportRequirement import:
                    if (!TryPlanImport(import, depth, defined, pack, pipeline, planned, out error))
                    {
                        return false;
                    }

                    break;
                case FileReadRequirement fileRead:
                    if (!TryPlanFileRead(fileRead, depth, defined, pack, pipeline, planned, out error))
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
                case FunctionRequirement function:
                    if (!TryPlanFunction(function, depth, defined, pack, pipeline, planned, out error))
                    {
                        return false;
                    }

                    break;
                case ReturnRequirement returned:
                    if (!TryPlanReturn(returned, depth, defined, pack, pipeline, planned, out error))
                    {
                        return false;
                    }

                    break;
                case ClassRequirement declaredClass:
                    if (!TryPlanClass(declaredClass, depth, defined, pack, pipeline, planned, out error))
                    {
                        return false;
                    }

                    break;
                case CallRequirement call:
                    if (!TryPlanCall(call, depth, defined, pack, pipeline, planned, out error))
                    {
                        return false;
                    }

                    break;
                case DictionaryIterationRequirement dictionaryIteration:
                    if (!TryPlanDictionaryIteration(
                            dictionaryIteration,
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
                case DictionaryMapRequirement dictionaryMap:
                    if (!TryPlanDictionaryMap(
                            dictionaryMap,
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
                case RaiseRequirement raise:
                    if (!TryPlanRaise(raise, depth, defined, pack, pipeline, planned, out error))
                    {
                        return false;
                    }

                    break;
                case TryRequirement attempt:
                    if (!TryPlanTry(attempt, depth, defined, pack, pipeline, planned, out error))
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

    /// <summary>
    /// Plans an update of one element: the target is an ordinary expression the pack already spells, so the
    /// same emit operations serve a name and an element alike.
    /// </summary>
    private static bool TryPlanElementAssignment(
        ElementAssignmentRequirement element,
        int depth,
        HashSet<string> defined,
        GenerationLanguagePack pack,
        SequenceGenerationPipeline pipeline,
        ImmutableArray<PlannedProgramStatement>.Builder planned,
        out string? error)
    {
        error = null;
        if (!TryRequireAvailable(element.Target, defined, out error) ||
            !TryRequireAvailable(element.Value, defined, out error))
        {
            return false;
        }

        if (!SemanticExpressionRenderer.TryRender(element.Target, pack, out var renderedTarget) ||
            !SemanticExpressionRenderer.TryRender(element.Value, pack, out var renderedValue))
        {
            error = "The language pack cannot render the requested element update.";
            return false;
        }

        return TryEmitAssignment(
            renderedTarget,
            renderedValue,
            depth,
            defined,
            pack,
            pipeline,
            planned,
            out error,
            targetIsIdentifier: false);
    }

    private static bool TryEmitAssignment(
        string variableName,
        string renderedValue,
        int depth,
        IReadOnlySet<string> defined,
        GenerationLanguagePack pack,
        SequenceGenerationPipeline pipeline,
        ImmutableArray<PlannedProgramStatement>.Builder planned,
        out string? error,
        bool targetIsIdentifier = true)
    {
        error = null;
        if (targetIsIdentifier &&
            !TryRequireIdentifier(variableName, pack, "assignment target", out error))
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

    /// <summary>
    /// Plans an in-place update: the pack supplies the compound operator symbol, so no target-language
    /// punctuation is decided here.
    /// </summary>
    private static bool TryPlanAugmentedAssignment(
        AugmentedAssignmentRequirement update,
        int depth,
        HashSet<string> defined,
        GenerationLanguagePack pack,
        SequenceGenerationPipeline pipeline,
        ImmutableArray<PlannedProgramStatement>.Builder planned,
        out string? error)
    {
        error = null;
        if (!TryRequireAvailable(update.Target, defined, out error) ||
            !TryRequireAvailable(update.Value, defined, out error))
        {
            return false;
        }

        if (!SemanticExpressionRenderer.TryRender(update.Target, pack, out var renderedTarget) ||
            !SemanticExpressionRenderer.TryRender(update.Value, pack, out var renderedValue) ||
            !SemanticExpressionRenderer.TryRenderOperator(update.Operator, pack, out var renderedOperator))
        {
            error = "The language pack cannot render the requested in-place update.";
            return false;
        }

        string effect;
        switch (depth)
        {
            case 0:
                effect = "emit-augmented-assignment";
                break;
            case 1:
                effect = "emit-indented-augmented-assignment";
                break;
            case 2:
                effect = "emit-double-indented-augmented-assignment";
                break;
            case 3:
                effect = "emit-triple-indented-augmented-assignment";
                break;
            default:
                error = $"Program statements support at most {MaximumDepth} levels of nested blocks.";
                return false;
        }

        var bindings = ImmutableDictionary<string, string>.Empty
            .WithComparers(StringComparer.Ordinal)
            .Add("target", renderedTarget)
            .Add("operator", renderedOperator)
            .Add("expression", renderedValue);
        return TryEmit(
            pack,
            pipeline,
            effect,
            argumentKind: "augmented-assignment",
            argumentValue: renderedTarget,
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

    private static bool TryPlanTry(
        TryRequirement attempt,
        int depth,
        HashSet<string> defined,
        GenerationLanguagePack pack,
        SequenceGenerationPipeline pipeline,
        ImmutableArray<PlannedProgramStatement>.Builder planned,
        out string? error)
    {
        error = null;
        if (depth > MaximumDepth)
        {
            error = $"Program statements support at most {MaximumDepth} levels of nested blocks.";
            return false;
        }

        string effect;
        switch (depth)
        {
            case 0:
                effect = "emit-try-header";
                break;
            case 1:
                effect = "emit-indented-try-header";
                break;
            default:
                effect = "emit-double-indented-try-header";
                break;
        }

        if (!TryEmit(
                pack,
                pipeline,
                effect,
                argumentKind: "try-header",
                argumentValue: "try",
                ImmutableDictionary<string, string>.Empty.WithComparers(StringComparer.Ordinal),
                depth,
                defined,
                planned,
                out error))
        {
            return false;
        }

        var attemptDefined = new HashSet<string>(defined, StringComparer.Ordinal);
        if (!TryPlanRequirements(
                attempt.Body,
                depth + 1,
                attemptDefined,
                pack,
                pipeline,
                planned,
                out error))
        {
            return false;
        }

        foreach (var handler in attempt.Handlers)
        {
            if (!TryPlanHandler(handler, depth, defined, pack, pipeline, planned, out error))
            {
                return false;
            }
        }

        if (attempt.ResultName is not { } resultName)
        {
            return true;
        }

        if (!attemptDefined.Contains(resultName))
        {
            error = $"The handled attempt never binds its result '{resultName}' inside the attempted body.";
            return false;
        }

        defined.Add(resultName);
        return true;
    }

    private static bool TryPlanHandler(
        HandlerRequirement handler,
        int depth,
        HashSet<string> defined,
        GenerationLanguagePack pack,
        SequenceGenerationPipeline pipeline,
        ImmutableArray<PlannedProgramStatement>.Builder planned,
        out string? error)
    {
        error = null;
        if (!TryRequireQualifiedName(handler.ErrorType, pack, "handled error type", out var errorType, out error))
        {
            return false;
        }

        string effect;
        switch (depth)
        {
            case 0:
                effect = "emit-handler-header";
                break;
            case 1:
                effect = "emit-indented-handler-header";
                break;
            default:
                effect = "emit-double-indented-handler-header";
                break;
        }

        var bindings = ImmutableDictionary<string, string>.Empty
            .WithComparers(StringComparer.Ordinal)
            .Add("error-type", errorType);
        if (!TryEmit(
                pack,
                pipeline,
                effect,
                argumentKind: "handler-header",
                argumentValue: errorType,
                bindings,
                depth,
                defined,
                planned,
                out error))
        {
            return false;
        }

        return TryPlanRequirements(
            handler.Body,
            depth + 1,
            new HashSet<string>(defined, StringComparer.Ordinal),
            pack,
            pipeline,
            planned,
            out error);
    }

    private static bool TryPlanRaise(
        RaiseRequirement raise,
        int depth,
        HashSet<string> defined,
        GenerationLanguagePack pack,
        SequenceGenerationPipeline pipeline,
        ImmutableArray<PlannedProgramStatement>.Builder planned,
        out string? error)
    {
        error = null;
        if (!TryRequireIdentifier(raise.ErrorType, pack, "raised error type", out error))
        {
            return false;
        }

        if (raise.Condition is not { } condition)
        {
            return TryEmitRaise(raise.ErrorType, depth, defined, pack, pipeline, planned, out error);
        }

        if (depth >= MaximumDepth)
        {
            error = $"Program statements support at most {MaximumDepth} levels of nested blocks.";
            return false;
        }

        if (!TryRequireAvailable(condition, defined, out error))
        {
            return false;
        }

        if (!SemanticExpressionRenderer.TryRender(condition, pack, out var renderedCondition))
        {
            error = $"The '{pack.Language}' language pack cannot render a raise condition.";
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

        var bindings = ImmutableDictionary<string, string>.Empty
            .WithComparers(StringComparer.Ordinal)
            .Add("condition", renderedCondition);
        if (!TryEmit(
                pack,
                pipeline,
                effect,
                argumentKind: "condition",
                argumentValue: renderedCondition,
                bindings,
                depth,
                defined,
                planned,
                out error))
        {
            return false;
        }

        return TryEmitRaise(raise.ErrorType, depth + 1, defined, pack, pipeline, planned, out error);
    }

    private static bool TryEmitRaise(
        string errorType,
        int depth,
        IReadOnlySet<string> defined,
        GenerationLanguagePack pack,
        SequenceGenerationPipeline pipeline,
        ImmutableArray<PlannedProgramStatement>.Builder planned,
        out string? error)
    {
        string effect;
        switch (depth)
        {
            case 0:
                effect = "emit-raise";
                break;
            case 1:
                effect = "emit-indented-raise";
                break;
            case 2:
                effect = "emit-double-indented-raise";
                break;
            default:
                error = $"Program statements support at most {MaximumDepth} levels of nested blocks.";
                return false;
        }

        var bindings = ImmutableDictionary<string, string>.Empty
            .WithComparers(StringComparer.Ordinal)
            .Add("error-type", errorType);
        return TryEmit(
            pack,
            pipeline,
            effect,
            argumentKind: "raise",
            argumentValue: errorType,
            bindings,
            depth,
            defined,
            planned,
            out error);
    }

    private static bool TryPlanClass(
        ClassRequirement declaredClass,
        int depth,
        HashSet<string> defined,
        GenerationLanguagePack pack,
        SequenceGenerationPipeline pipeline,
        ImmutableArray<PlannedProgramStatement>.Builder planned,
        out string? error)
    {
        error = null;
        if (depth != 0)
        {
            error = "A class declaration is planned only at the top level of a program.";
            return false;
        }

        if (!TryRequireIdentifier(declaredClass.Name, pack, "class name", out error) ||
            !TryRequireIdentifier(declaredClass.InstanceName, pack, "instance name", out error))
        {
            return false;
        }

        if (!TryEmit(
                pack,
                pipeline,
                "emit-class-header",
                argumentKind: "class-header",
                argumentValue: declaredClass.Name,
                ImmutableDictionary<string, string>.Empty.WithComparers(StringComparer.Ordinal),
                depth,
                defined,
                planned,
                out error))
        {
            return false;
        }

        if (!declaredClass.Fields.IsDefaultOrEmpty)
        {
            // The constructor receives whatever the fields start from, in field order, so a field that
            // starts at a request-declared value is initialized from the argument of that name.
            var initializedFrom = string.Join(
                ", ",
                declaredClass.Fields
                    .SelectMany(field => SemanticExpressionAnalysis.GetReferencedNames(field.InitialValue))
                    .Where(name => !string.Equals(name, declaredClass.InstanceName, StringComparison.Ordinal))
                    .Distinct(StringComparer.Ordinal)
                    .Prepend(declaredClass.InstanceName));
            if (!TryEmit(
                    pack,
                    pipeline,
                    "emit-indented-function-header",
                    argumentKind: "function-header",
                    argumentValue: "__init__",
                    ImmutableDictionary<string, string>.Empty
                        .WithComparers(StringComparer.Ordinal)
                        .Add("function", "__init__")
                        .Add("arguments", initializedFrom),
                    depth: 1,
                    defined,
                    planned,
                    out error))
            {
                return false;
            }

            foreach (var field in declaredClass.Fields)
            {
                if (!TryRequireIdentifier(field.Name, pack, "class field", out error))
                {
                    return false;
                }

                if (!SemanticExpressionRenderer.TryRender(field.InitialValue, pack, out var renderedInitial))
                {
                    error = $"The '{pack.Language}' language pack cannot render the initial value of " +
                        $"'{declaredClass.Name}.{field.Name}'.";
                    return false;
                }

                var fieldBindings = ImmutableDictionary<string, string>.Empty
                    .WithComparers(StringComparer.Ordinal)
                    .Add("instance", declaredClass.InstanceName)
                    .Add("target", field.Name)
                    .Add("expression", renderedInitial);
                if (!TryEmit(
                        pack,
                        pipeline,
                        "emit-double-indented-field-assignment",
                        argumentKind: "field-assignment",
                        argumentValue: field.Name,
                        fieldBindings,
                        depth: 2,
                        defined,
                        planned,
                        out error))
                {
                    return false;
                }
            }
        }

        foreach (var method in declaredClass.Methods)
        {
            if (!TryRequireIdentifier(method.Name, pack, "method name", out error))
            {
                return false;
            }

            var parameterNames = new List<string>(method.Parameters.Length);
            foreach (var parameter in method.Parameters)
            {
                if (!TryRequireIdentifier(parameter.Name, pack, "method parameter", out error))
                {
                    return false;
                }

                parameterNames.Add(parameter.Name);
            }

            var arguments = string.Join(
                ", ",
                parameterNames.Prepend(declaredClass.InstanceName));
            var methodBindings = ImmutableDictionary<string, string>.Empty
                .WithComparers(StringComparer.Ordinal)
                .Add("function", method.Name)
                .Add("arguments", arguments);
            if (!TryEmit(
                    pack,
                    pipeline,
                    "emit-indented-function-header",
                    argumentKind: "function-header",
                    argumentValue: method.Name,
                    methodBindings,
                    depth: 1,
                    defined,
                    planned,
                    out error))
            {
                return false;
            }

            var bodyDefined = new HashSet<string>(defined, StringComparer.Ordinal)
            {
                declaredClass.InstanceName
            };
            foreach (var name in parameterNames)
            {
                bodyDefined.Add(name);
            }

            if (!TryPlanRequirements(method.Body, depth + 2, bodyDefined, pack, pipeline, planned, out error))
            {
                return false;
            }
        }

        defined.Add(declaredClass.Name);
        return true;
    }

    private static bool TryPlanFunction(
        FunctionRequirement function,
        int depth,
        HashSet<string> defined,
        GenerationLanguagePack pack,
        SequenceGenerationPipeline pipeline,
        ImmutableArray<PlannedProgramStatement>.Builder planned,
        out string? error)
    {
        error = null;
        if (depth != 0)
        {
            error = "A function declaration is planned only at the top level of a program.";
            return false;
        }

        if (!TryRequireIdentifier(function.Name, pack, "function name", out error))
        {
            return false;
        }

        foreach (var parameter in function.Parameters)
        {
            if (!TryRequireIdentifier(parameter.Name, pack, "function parameter", out error))
            {
                return false;
            }
        }

        var parameters = string.Join(
            ", ",
            function.Parameters.Select(parameter => parameter.Name));
        var bindings = ImmutableDictionary<string, string>.Empty
            .WithComparers(StringComparer.Ordinal)
            .Add("function", function.Name);
        string effect;
        if (parameters.Length == 0)
        {
            effect = "emit-nullary-function-header";
        }
        else
        {
            effect = "emit-function-header";
            bindings = bindings.Add("arguments", parameters);
        }

        if (!TryEmit(
                pack,
                pipeline,
                effect,
                argumentKind: "function-header",
                argumentValue: function.Name,
                bindings,
                depth,
                defined,
                planned,
                out error))
        {
            return false;
        }

        var bodyDefined = new HashSet<string>(defined, StringComparer.Ordinal);
        foreach (var parameter in function.Parameters)
        {
            bodyDefined.Add(parameter.Name);
        }

        if (!TryPlanRequirements(function.Body, depth + 1, bodyDefined, pack, pipeline, planned, out error))
        {
            return false;
        }

        defined.Add(function.Name);
        return true;
    }

    private static bool TryPlanReturn(
        ReturnRequirement returned,
        int depth,
        HashSet<string> defined,
        GenerationLanguagePack pack,
        SequenceGenerationPipeline pipeline,
        ImmutableArray<PlannedProgramStatement>.Builder planned,
        out string? error)
    {
        error = null;
        if (!TryRequireAvailable(returned.Value, defined, out error))
        {
            return false;
        }

        if (!SemanticExpressionRenderer.TryRender(returned.Value, pack, out var rendered))
        {
            error = $"The '{pack.Language}' language pack cannot render the returned value.";
            return false;
        }

        string effect;
        switch (depth)
        {
            case 1:
                effect = "return-value-indented-expression";
                break;
            case 2:
                effect = "return-value-double-indented-expression";
                break;
            case 3:
                effect = "return-value-triple-indented-expression";
                break;
            default:
                error = "A return is planned inside a function or method body, one to three levels deep.";
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

    private static bool TryPlanCall(
        CallRequirement call,
        int depth,
        HashSet<string> defined,
        GenerationLanguagePack pack,
        SequenceGenerationPipeline pipeline,
        ImmutableArray<PlannedProgramStatement>.Builder planned,
        out string? error)
    {
        error = null;
        if (!TryRequireIdentifier(call.FunctionName, pack, "called function", out error))
        {
            return false;
        }

        string callee;
        if (call.Receiver is { } receiver)
        {
            if (!TryRequireIdentifier(receiver, pack, "call receiver", out error))
            {
                return false;
            }

            if (pack.InputSyntax?.MemberAccessFormat is not { } memberAccessFormat)
            {
                error = $"The '{pack.Language}' language pack cannot render a method call.";
                return false;
            }

            if (!defined.Contains(receiver))
            {
                error = $"The program uses the value '{receiver}' before any requirement defines it.";
                return false;
            }

            callee = receiver + memberAccessFormat.Separator + call.FunctionName;
        }
        else
        {
            callee = call.FunctionName;
        }

        var arguments = new List<string>(call.Arguments.Length);
        foreach (var argument in call.Arguments)
        {
            if (!TryRequireAvailable(argument, defined, out error))
            {
                return false;
            }

            if (!SemanticExpressionRenderer.TryRender(argument, pack, out var renderedArgument))
            {
                error = $"The '{pack.Language}' language pack cannot render an argument of '{call.FunctionName}'.";
                return false;
            }

            arguments.Add(renderedArgument);
        }

        string effect;
        var prefix = depth switch
        {
            0 => string.Empty,
            1 => "indented-",
            2 => "double-indented-",
            _ => null
        };
        if (prefix is null)
        {
            error = $"Program statements support at most {MaximumDepth} levels of nested blocks.";
            return false;
        }

        var bindings = ImmutableDictionary<string, string>.Empty
            .WithComparers(StringComparer.Ordinal)
            .Add("function", callee);
        if (arguments.Count == 0)
        {
            effect = $"emit-{prefix}nullary-call";
        }
        else
        {
            effect = $"emit-{prefix}call";
            bindings = bindings.Add("arguments", string.Join(", ", arguments));
        }

        return TryEmit(
            pack,
            pipeline,
            effect,
            argumentKind: "call",
            argumentValue: callee,
            bindings,
            depth,
            defined,
            planned,
            out error);
    }

    private static bool TryPlanDictionaryIteration(
        DictionaryIterationRequirement iteration,
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

        if (!TryRequireIdentifier(iteration.KeyName, pack, "dictionary key item", out error) ||
            !TryRequireIdentifier(iteration.ValueName, pack, "dictionary value item", out error))
        {
            return false;
        }

        if (!SemanticExpressionRenderer.TryRender(iteration.Source, pack, out var renderedSource))
        {
            error = $"The '{pack.Language}' language pack cannot render the iterated dictionary.";
            return false;
        }

        string effect;
        switch (depth)
        {
            case 0:
                effect = "emit-dictionary-loop-header";
                break;
            case 1:
                effect = "emit-indented-dictionary-loop-header";
                break;
            case 2:
                effect = "emit-double-indented-dictionary-loop-header";
                break;
            default:
                error = $"Program statements support at most {MaximumDepth} levels of nested blocks.";
                return false;
        }

        var bindings = ImmutableDictionary<string, string>.Empty
            .WithComparers(StringComparer.Ordinal)
            .Add("key", iteration.KeyName)
            .Add("value", iteration.ValueName)
            .Add("iterable", renderedSource);
        if (!TryEmit(
                pack,
                pipeline,
                effect,
                argumentKind: "dictionary-loop-header",
                argumentValue: renderedSource,
                bindings,
                depth,
                defined,
                planned,
                out error))
        {
            return false;
        }

        var bodyDefined = new HashSet<string>(defined, StringComparer.Ordinal)
        {
            iteration.KeyName,
            iteration.ValueName
        };
        if (!TryPlanRequirements(iteration.Body, depth + 1, bodyDefined, pack, pipeline, planned, out error))
        {
            return false;
        }

        defined.Add(iteration.KeyName);
        defined.Add(iteration.ValueName);
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

    /// <summary>
    /// Plans a dictionary comprehension: the key and value expressions are rendered over the bound item and the
    /// pack supplies the braces, the separators, and the comprehension clause.
    /// </summary>
    private static bool TryPlanDictionaryMap(
        DictionaryMapRequirement dictionaryMap,
        int depth,
        HashSet<string> defined,
        GenerationLanguagePack pack,
        SequenceGenerationPipeline pipeline,
        ImmutableArray<PlannedProgramStatement>.Builder planned,
        out string? error)
    {
        error = null;
        if (!TryRequireAvailable(dictionaryMap.Source, defined, out error) ||
            !TryRequireAvailable(dictionaryMap.Value, defined, out error, dictionaryMap.ItemName))
        {
            return false;
        }

        if (!TryRequireIdentifier(dictionaryMap.ResultName, pack, "dictionary result", out error) ||
            !TryRequireIdentifier(dictionaryMap.ItemName, pack, "dictionary item", out error))
        {
            return false;
        }

        if (!SemanticExpressionRenderer.TryRender(dictionaryMap.Source, pack, out var renderedSource) ||
            !SemanticExpressionRenderer.TryRender(dictionaryMap.Value, pack, out var renderedValue))
        {
            error = $"The '{pack.Language}' language pack cannot render a requested dictionary mapping.";
            return false;
        }

        string effect;
        switch (depth)
        {
            case 0:
                effect = "emit-dictionary-map-binding";
                break;
            case 1:
                effect = "emit-indented-dictionary-map-binding";
                break;
            case 2:
                effect = "emit-double-indented-dictionary-map-binding";
                break;
            default:
                error = "Program statements support at most 3 levels of nested blocks.";
                return false;
        }

        var bindings = ImmutableDictionary<string, string>.Empty
            .WithComparers(StringComparer.Ordinal)
            .Add("target", dictionaryMap.ResultName)
            .Add("key", dictionaryMap.ItemName)
            .Add("variable", dictionaryMap.ItemName)
            .Add("expression", renderedValue)
            .Add("iterable", renderedSource);
        if (!TryEmit(
                pack,
                pipeline,
                effect,
                argumentKind: "dictionary-map-binding",
                argumentValue: dictionaryMap.ResultName,
                bindings,
                depth,
                defined,
                planned,
                out error))
        {
            return false;
        }

        defined.Add(dictionaryMap.ResultName);
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

    /// <summary>
    /// Requires a name the pack can spell: one identifier, or identifiers joined by the separator the pack
    /// writes between a module and the member it exposes, such as a library error type.
    /// </summary>
    private static bool TryRequireQualifiedName(
        string name,
        GenerationLanguagePack pack,
        string description,
        out string qualified,
        out string? error)
    {
        qualified = string.Empty;
        error = null;
        if (string.IsNullOrWhiteSpace(name) || pack.InputSyntax is not { } syntax)
        {
            error = $"The {description} '{name}' is not a name of the '{pack.Language}' language pack.";
            return false;
        }

        var segments = name.Split('.', StringSplitOptions.None);
        if (segments.Length > 4 ||
            segments.Any(segment => !GenerationInputSyntaxParser.IsIdentifier(segment, syntax)))
        {
            error = $"The {description} '{name}' is not a name of the '{pack.Language}' language pack.";
            return false;
        }

        qualified = string.Join(syntax.MemberAccessFormat?.Separator ?? ".", segments);
        return true;
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
            case 3:
                effect = "write-standard-output-triple-indented-expression";
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

    /// <summary>
    /// Plans a multi-way branch: the first condition becomes the opening block header and every later
    /// one continues the same block as an alternative. When every branch leaves the enclosing body the
    /// otherwise statements follow the block at the same level; otherwise they form the final block so
    /// they run only when no condition matched.
    /// </summary>
    private static bool TryPlanChain(
        ConditionalChainRequirement chain,
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

        if (chain.Branches.IsDefaultOrEmpty)
        {
            error = "A branch chain needs at least one condition.";
            return false;
        }

        for (var index = 0; index < chain.Branches.Length; index++)
        {
            var branch = chain.Branches[index];
            if (branch is null || branch.Body.IsDefaultOrEmpty)
            {
                error = "A branch chain needs a statement under every condition.";
                return false;
            }

            if (!TryRequireAvailable(branch.Condition, defined, out error))
            {
                return false;
            }

            if (!SemanticExpressionRenderer.TryRender(branch.Condition, pack, out var renderedCondition))
            {
                error = $"The '{pack.Language}' language pack cannot render a requested condition.";
                return false;
            }

            var effect = index == 0
                ? (depth switch
                {
                    0 => "emit-conditional-header",
                    1 => "emit-indented-conditional-header",
                    _ => "emit-double-indented-conditional-header"
                })
                : (depth switch
                {
                    0 => "begin-elif",
                    1 => "begin-elif-indented",
                    _ => "begin-elif-double-indented"
                });

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

            var branchDefined = new HashSet<string>(defined, StringComparer.Ordinal);
            if (!TryPlanRequirements(
                    branch.Body,
                    depth + 1,
                    branchDefined,
                    pack,
                    pipeline,
                    planned,
                    out error))
            {
                return false;
            }
        }

        var otherwise = chain.Otherwise.IsDefault
            ? ImmutableArray<ProgramRequirement>.Empty
            : chain.Otherwise;
        if (otherwise.IsEmpty)
        {
            return true;
        }

        if (BranchesLeaveBody(chain.Branches))
        {
            return TryPlanRequirements(otherwise, depth, defined, pack, pipeline, planned, out error);
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

        return TryPlanRequirements(
            otherwise,
            depth + 1,
            new HashSet<string>(defined, StringComparer.Ordinal),
            pack,
            pipeline,
            planned,
            out error);
    }

    /// <summary>Returns true when every branch ends by leaving the enclosing body instead of falling through.</summary>
    private static bool BranchesLeaveBody(ImmutableArray<ConditionalBranch> branches) =>
        !branches.IsDefaultOrEmpty && branches.All(branch => LeavesBody(branch.Body));

    /// <summary>
    /// Returns true when a requirement sequence cannot fall through to the requirement after it: it ends
    /// with a return or a raise, or with a branch whose every path does.
    /// </summary>
    private static bool LeavesBody(ImmutableArray<ProgramRequirement> requirements) =>
        !requirements.IsDefaultOrEmpty &&
        requirements[^1] switch
        {
            ReturnRequirement => true,
            RaiseRequirement => true,
            ConditionalChainRequirement chain =>
                !chain.Otherwise.IsDefaultOrEmpty &&
                BranchesLeaveBody(chain.Branches) &&
                LeavesBody(chain.Otherwise),
            ConditionalRequirement conditional =>
                !conditional.ThenRequirements.IsDefaultOrEmpty &&
                !conditional.ElseRequirements.IsDefaultOrEmpty &&
                LeavesBody(conditional.ThenRequirements) &&
                LeavesBody(conditional.ElseRequirements),
            _ => false
        };

    /// <summary>
    /// Plans a directory listing: the pack supplies the scan, the ordering, and the entry binding, while
    /// the sort key and the body come from the requirement.
    /// </summary>
    private static bool TryPlanDirectoryListing(
        DirectoryListingRequirement listing,
        int depth,
        HashSet<string> defined,
        GenerationLanguagePack pack,
        SequenceGenerationPipeline pipeline,
        ImmutableArray<PlannedProgramStatement>.Builder planned,
        out string? error)
    {
        error = null;
        if (depth > 1)
        {
            error = "A directory listing is planned one level deep at most.";
            return false;
        }

        if (!TryRequireAvailable(listing.Directory, defined, out error) ||
            !TryRequireAvailable(
                listing.SortKey,
                defined,
                out error,
                listing.ItemName) ||
            !SemanticExpressionRenderer.TryRender(listing.Directory, pack, out var renderedDirectory) ||
            !SemanticExpressionRenderer.TryRender(listing.SortKey, pack, out var renderedKey))
        {
            return false;
        }

        var effect = depth == 0 ? "emit-directory-loop-header" : "emit-indented-directory-loop-header";
        var bindings = ImmutableDictionary<string, string>.Empty
            .WithComparers(StringComparer.Ordinal)
            .Add("directory", renderedDirectory)
            .Add("item", listing.ItemName)
            .Add("key", renderedKey);
        if (!TryEmit(
                pack,
                pipeline,
                effect,
                argumentKind: "directory-loop-header",
                argumentValue: renderedDirectory,
                bindings,
                depth,
                defined,
                planned,
                out error))
        {
            return false;
        }

        defined.Add(listing.ItemName);
        return TryPlanRequirements(listing.Body, depth + 1, defined, pack, pipeline, planned, out error);
    }

    /// <summary>
    /// Plans one import: the pack spells how a module is made available, so no language syntax is
    /// decided here.
    /// </summary>
    private static bool TryPlanImport(
        ImportRequirement import,
        int depth,
        IReadOnlySet<string> defined,
        GenerationLanguagePack pack,
        SequenceGenerationPipeline pipeline,
        ImmutableArray<PlannedProgramStatement>.Builder planned,
        out string? error)
    {
        error = null;
        if (depth != 0)
        {
            error = "An import is planned at the top level of a program.";
            return false;
        }

        var bindings = ImmutableDictionary<string, string>.Empty
            .WithComparers(StringComparer.Ordinal)
            .Add("module", import.Module);
        return TryEmit(
            pack,
            pipeline,
            effect: "emit-import",
            argumentKind: "module-name",
            argumentValue: import.Module,
            bindings,
            depth,
            defined,
            planned,
            out error);
    }

    /// <summary>
    /// Plans a file read: the pack supplies the way a file is opened and bound to a name, and the body is
    /// planned one level deeper with the stream name available.
    /// </summary>
    /// <summary>
    /// Plans a file write: the pack spells the opening line, and the body writes through the stream it names.
    /// </summary>
    private static bool TryPlanFileWrite(
        FileWriteRequirement fileWrite,
        int depth,
        HashSet<string> defined,
        GenerationLanguagePack pack,
        SequenceGenerationPipeline pipeline,
        ImmutableArray<PlannedProgramStatement>.Builder planned,
        out string? error)
    {
        error = null;
        if (depth != 0)
        {
            error = "A file write is planned at the top level of a program.";
            return false;
        }

        if (!TryRequireIdentifier(fileWrite.StreamName, pack, "file stream name", out error))
        {
            return false;
        }

        if (fileWrite.Body.IsDefaultOrEmpty)
        {
            error = "A file write needs at least one statement in its body.";
            return false;
        }

        var bindings = ImmutableDictionary<string, string>.Empty
            .WithComparers(StringComparer.Ordinal)
            .Add("path", fileWrite.Path)
            .Add("file", fileWrite.StreamName);
        if (!TryEmit(
                pack,
                pipeline,
                effect: "write-text-file",
                argumentKind: "path",
                argumentValue: fileWrite.Path,
                bindings,
                depth,
                defined,
                planned,
                out error))
        {
            return false;
        }

        defined.Add(fileWrite.StreamName);
        return TryPlanRequirements(fileWrite.Body, depth + 1, defined, pack, pipeline, planned, out error);
    }

    /// <summary>
    /// Plans one serialization step inside an open stream. The format name the request uses selects the
    /// pack operation that spells it, so no call name is decided here.
    /// </summary>
    private static bool TryPlanSerialize(
        SerializeRequirement serialize,
        int depth,
        HashSet<string> defined,
        GenerationLanguagePack pack,
        SequenceGenerationPipeline pipeline,
        ImmutableArray<PlannedProgramStatement>.Builder planned,
        out string? error)
    {
        error = null;
        if (depth != 1)
        {
            error = "A serialization step is planned inside the stream it writes through.";
            return false;
        }

        if (!TryRequireAvailable(serialize.Value, defined, out error) ||
            !TryRequireStream(serialize.StreamName, defined, out error) ||
            !SemanticExpressionRenderer.TryRender(serialize.Value, pack, out var renderedValue))
        {
            return false;
        }

        var bindings = ImmutableDictionary<string, string>.Empty
            .WithComparers(StringComparer.Ordinal)
            .Add("value", renderedValue)
            .Add("file", serialize.StreamName)
            .Add("indent", serialize.Indent);
        return TryEmit(
            pack,
            pipeline,
            effect: "serialize-value-to-stream",
            argumentKind: "serialization-format",
            argumentValue: serialize.Format,
            bindings,
            depth,
            defined,
            planned,
            out error);
    }

    /// <summary>
    /// Plans one decode step inside an open stream, binding the value it reads back to a name.
    /// </summary>
    private static bool TryPlanDeserialize(
        DeserializeRequirement deserialize,
        int depth,
        HashSet<string> defined,
        GenerationLanguagePack pack,
        SequenceGenerationPipeline pipeline,
        ImmutableArray<PlannedProgramStatement>.Builder planned,
        out string? error)
    {
        error = null;
        if (depth is not (1 or 2))
        {
            error = "A decode step is planned inside the stream it reads from.";
            return false;
        }

        if (!TryRequireIdentifier(deserialize.ResultName, pack, "decoded value name", out error) ||
            !TryRequireStream(deserialize.StreamName, defined, out error))
        {
            return false;
        }

        var bindings = ImmutableDictionary<string, string>.Empty
            .WithComparers(StringComparer.Ordinal)
            .Add("file", deserialize.StreamName)
            .Add("result", deserialize.ResultName);
        if (!TryEmit(
                pack,
                pipeline,
                effect: depth == 1
                    ? "deserialize-value-from-stream"
                    : "deserialize-value-from-stream-indented",
                argumentKind: "serialization-format",
                argumentValue: deserialize.Format,
                bindings,
                depth,
                defined,
                planned,
                out error))
        {
            return false;
        }

        defined.Add(deserialize.ResultName);
        return true;
    }

    private static bool TryRequireStream(
        string streamName,
        IReadOnlySet<string> defined,
        out string? error)
    {
        error = null;
        if (!defined.Contains(streamName))
        {
            error = $"The program reads or writes the stream '{streamName}' before opening it.";
            return false;
        }

        return true;
    }

    private static bool TryPlanFileRead(
        FileReadRequirement fileRead,
        int depth,
        HashSet<string> defined,
        GenerationLanguagePack pack,
        SequenceGenerationPipeline pipeline,
        ImmutableArray<PlannedProgramStatement>.Builder planned,
        out string? error)
    {
        error = null;
        if (depth > 1)
        {
            error = "A file read is planned at the top level of a program or inside one guarded block.";
            return false;
        }

        if (!TryRequireIdentifier(fileRead.StreamName, pack, "file stream name", out error))
        {
            return false;
        }

        if (fileRead.Body.IsDefaultOrEmpty)
        {
            error = "A file read needs at least one statement in its body.";
            return false;
        }

        var bindings = ImmutableDictionary<string, string>.Empty
            .WithComparers(StringComparer.Ordinal)
            .Add("path", fileRead.Path)
            .Add("file", fileRead.StreamName);
        if (!TryEmit(
                pack,
                pipeline,
                effect: depth == 0 ? "read-text-file" : "read-text-file-indented",
                argumentKind: "path",
                argumentValue: fileRead.Path,
                bindings,
                depth,
                defined,
                planned,
                out error))
        {
            return false;
        }

        defined.Add(fileRead.StreamName);
        return TryPlanRequirements(fileRead.Body, depth + 1, defined, pack, pipeline, planned, out error);
    }

    /// <summary>
    /// Expands a sign classification into the statements that carry it: one assignment of the value, then one
    /// independent branch per label, comparing the value against the definitional boundary of zero. A label is
    /// reported as a capitalised word, which is how these single-word labels read as output.
    /// </summary>
    private static ImmutableArray<ProgramRequirement> ExpandSignClassification(
        SignClassificationRequirement classification)
    {
        var value = new SemanticNameExpression(classification.VariableName);
        var zero = new SemanticNumberExpression("0");

        return ImmutableArray.Create<ProgramRequirement>(
            new AssignmentRequirement(classification.VariableName, classification.Value),
            Branch(SemanticExpressionOperator.GreaterThan, classification.PositiveLabel),
            Branch(SemanticExpressionOperator.LessThan, classification.NegativeLabel),
            Branch(SemanticExpressionOperator.Equal, classification.ZeroLabel));

        ConditionalRequirement Branch(SemanticExpressionOperator comparison, string label) =>
            new(
                new SemanticBinaryExpression(comparison, value, zero),
                ImmutableArray.Create<ProgramRequirement>(
                    new OutputRequirement(new SemanticTextExpression(AsReportedLabel(label)))),
                ImmutableArray<ProgramRequirement>.Empty);
    }

    private static string AsReportedLabel(string label)
    {
        var trimmed = label.Trim();
        return trimmed.Length == 0
            ? trimmed
            : char.ToUpperInvariant(trimmed[0]) + trimmed[1..];
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
        var bound = boundByRequirement;
        foreach (var name in SemanticExpressionAnalysis.GetBoundNames(expression))
        {
            if (!bound.Contains(name, StringComparer.Ordinal))
            {
                bound = [.. bound, name];
            }
        }

        foreach (var name in SemanticExpressionAnalysis.GetReferencedNames(expression))
        {
            if (!defined.Contains(name) &&
                !bound.Contains(name, StringComparer.Ordinal))
            {
                error = $"The program uses the value '{name}' before any requirement defines it.";
                return false;
            }
        }

        return true;
    }
}
