using System.Collections.Immutable;

namespace Lattice.Core.Generation;

/// <summary>Projects generic request facts into the language-neutral program requirement model.</summary>
public static class SemanticProgramFactMapper
{
    private sealed record RequirementEvent(int Position, int Order, ProgramRequirement Requirement);

    private const string AssignmentTargetFactId = "program.assignment.target";
    private const string AssignmentValueFactId = "program.assignment.value";
    private const string ConditionFactId = "program.requirement.condition";
    private const string OutputFactId = "program.requirement.output";
    private const string OutputValueId = "value";
    private const string BranchSeparatorValueId = "branch";
    private const string ConditionLayoutValueId = "layout";
    private const string TrailingConditionLayout = "trailing";
    private const string FunctionFactId = "program.requirement.function";
    private const string FunctionParametersValueId = "parameters";
    private const string FunctionBodyFactId = "program.requirement.function.body";
    private const string FunctionResultFactId = "program.requirement.function.result";
    private const string FunctionBranchesFactId = "program.requirement.function.branches";
    private const string FunctionOtherwiseFactId = "program.requirement.function.otherwise";
    private const string FunctionCallFactId = "program.requirement.function.call";
    private const string ConditionValueId = "condition";
    private const string ReturnValueId = "value";
    private const string CallValueId = "call";
    private const string FunctionKeywordFactId = "program.function";

    public static bool TryCreate(
        ImmutableArray<SequenceSemanticFact> facts,
        out SemanticProgram? program,
        out string? error)
    {
        program = null;
        error = null;
        if (facts.IsDefault)
        {
            error = "Semantic program facts must be initialized.";
            return false;
        }

        var events = ImmutableArray.CreateBuilder<RequirementEvent>();
        var eventOrder = 0;
        var declarations = ImmutableArray.CreateBuilder<ProgramRequirement>();
        if (!TryProjectFunctions(facts, declarations, events, ref eventOrder, out error))
        {
            return false;
        }

        if (!TryProjectAssignments(facts, events, ref eventOrder, out error))
        {
            return false;
        }

        if (!TryProjectOutputs(facts, events, ref eventOrder, out var branchSeparatorPosition, out error))
        {
            return false;
        }

        SemanticExpression? condition = null;
        var conditionPosition = 0;
        var conditionFact = facts.FirstOrDefault(fact => fact.Id == ConditionFactId);
        if (conditionFact is not null)
        {
            if (!TryGetExpressions(conditionFact, "condition", out var conditions) ||
                conditions.Length != 1)
            {
                error = "A generic scalar conditional requires exactly one typed condition expression.";
                return false;
            }

            condition = conditions[0];
            conditionPosition = GetCollectionPositions(conditionFact, "condition", conditions.Length)[0];

            // A trailing conditional states its requirement before the condition ("print 'adult' if ...",
            // or "if ... print 'adult'"). Every requirement in such a request belongs to the true branch.
            if (conditionFact.Values.TryGetValue(ConditionLayoutValueId, out var layout) &&
                string.Equals(layout, TrailingConditionLayout, StringComparison.Ordinal))
            {
                conditionPosition = -1;
            }
        }

        var requirements = ImmutableArray.CreateBuilder<ProgramRequirement>();
        foreach (var declaration in declarations)
        {
            requirements.Add(declaration);
        }

        if (condition is null)
        {
            requirements.AddRange(Ordered(events).Select(item => item.Requirement));
        }
        else
        {
            requirements.AddRange(Ordered(events.Where(item => item.Position < conditionPosition))
                .Select(item => item.Requirement));
            var branchEvents = Ordered(events.Where(item => item.Position >= conditionPosition)).ToArray();
            var separatorPosition = branchSeparatorPosition.GetValueOrDefault(-1);
            var hasSeparator = separatorPosition > conditionPosition;
            var thenRequirements = hasSeparator
                ? branchEvents.TakeWhile(item => item.Position < separatorPosition)
                    .Select(item => item.Requirement)
                    .ToImmutableArray()
                : branchEvents.Select(item => item.Requirement).ToImmutableArray();
            var elseRequirements = hasSeparator
                ? branchEvents.SkipWhile(item => item.Position < separatorPosition)
                    .Select(item => item.Requirement)
                    .ToImmutableArray()
                : ImmutableArray<ProgramRequirement>.Empty;

            if (thenRequirements.IsDefaultOrEmpty && elseRequirements.IsDefaultOrEmpty)
            {
                error = "A generic scalar conditional requires at least one branch requirement.";
                return false;
            }

            if (thenRequirements.IsDefaultOrEmpty)
            {
                error = "A generic scalar conditional requires a true-branch requirement before its false branch.";
                return false;
            }

            requirements.Add(new ConditionalRequirement(condition, thenRequirements, elseRequirements));
        }

        if (requirements.Count == 0)
        {
            error = "No supported generic scalar program requirements were found.";
            return false;
        }

        var projection = new SemanticProgram(requirements.ToImmutable());
        if (!SemanticProgramValidator.IsValid(projection))
        {
            error = "The projected generic program requirements are not structurally valid.";
            return false;
        }

        program = projection;
        return true;
    }

    /// <summary>
    /// Projects declared functions into function requirements. A declaration is projected once, before
    /// every other requirement, and each captured call becomes an output of that call.
    /// </summary>
    private static bool TryProjectFunctions(
        ImmutableArray<SequenceSemanticFact> facts,
        ImmutableArray<ProgramRequirement>.Builder declarations,
        ImmutableArray<RequirementEvent>.Builder events,
        ref int eventOrder,
        out string? error)
    {
        error = null;
        var declarationFacts = facts.Where(fact => fact.Id == FunctionFactId).ToArray();
        if (declarationFacts.Length == 0)
        {
            if (facts.Any(fact => string.Equals(fact.Id, FunctionKeywordFactId, StringComparison.Ordinal)))
            {
                error = "A request that declares a function is planned only when its declaration is captured.";
                return false;
            }

            return true;
        }

        if (declarationFacts.Length > 1)
        {
            error = "A generic program supports one captured function declaration per request.";
            return false;
        }

        var declaration = declarationFacts[0];
        if (!TryGetNames(declaration, out var functionNames) ||
            functionNames.IsDefaultOrEmpty ||
            functionNames.Any(string.IsNullOrWhiteSpace))
        {
            error = "A generic function requirement requires declared function names.";
            return false;
        }

        var declaredParameters = GetParameterLists(declaration, functionNames.Length, out error);
        if (declaredParameters is not { } parameters)
        {
            return false;
        }

        var results = ImmutableArray<SemanticExpression>.Empty;
        var resultFact = facts.FirstOrDefault(fact => fact.Id == FunctionResultFactId);
        if (resultFact is not null &&
            !TryGetExpressions(resultFact, ReturnValueId, out results))
        {
            error = "A generic function result requires typed value expressions.";
            return false;
        }

        var bodyFact = facts.FirstOrDefault(fact => fact.Id == FunctionBodyFactId);
        var branchFact = facts.FirstOrDefault(fact => fact.Id == FunctionBranchesFactId);
        var otherwiseFact = facts.FirstOrDefault(fact => fact.Id == FunctionOtherwiseFactId);
        if (functionNames.Length > 1 &&
            (bodyFact is not null || branchFact is not null || otherwiseFact is not null))
        {
            error = "A generic program describes a captured function body for one declared function at a time.";
            return false;
        }

        var assignments = ImmutableArray<AssignmentRequirement>.Empty;
        if (bodyFact is not null)
        {
            if (!TryGetNames(bodyFact, out var localNames) ||
                !TryGetExpressions(bodyFact, ReturnValueId, out var localValues) ||
                localNames.IsDefaultOrEmpty ||
                localNames.Length != localValues.Length ||
                localNames.Any(string.IsNullOrWhiteSpace))
            {
                error = "A generic function body requires aligned local names and typed value expressions.";
                return false;
            }

            assignments = Enumerable.Range(0, localNames.Length)
                .Select(index => new AssignmentRequirement(localNames[index], localValues[index]))
                .ToImmutableArray();
        }

        ConditionalChainRequirement? chain = null;
        if (branchFact is not null)
        {
            if (!TryGetExpressions(branchFact, ConditionValueId, out var conditions) ||
                !TryGetExpressions(branchFact, ReturnValueId, out var branchValues) ||
                conditions.IsDefaultOrEmpty ||
                conditions.Length != branchValues.Length)
            {
                error = "A generic branch chain requires aligned conditions and typed results.";
                return false;
            }

            var otherwise = ImmutableArray<ProgramRequirement>.Empty;
            if (otherwiseFact is not null)
            {
                if (!TryGetExpressions(otherwiseFact, ReturnValueId, out var otherwiseValues) ||
                    otherwiseValues.Length != 1)
                {
                    error = "A generic otherwise clause requires exactly one typed value expression.";
                    return false;
                }

                otherwise = [new ReturnRequirement(otherwiseValues[0])];
            }

            chain = new ConditionalChainRequirement(
                Enumerable.Range(0, conditions.Length)
                    .Select(index => new ConditionalBranch(
                        conditions[index],
                        [new ReturnRequirement(branchValues[index])]))
                    .ToImmutableArray(),
                otherwise);
        }
        else if (otherwiseFact is not null)
        {
            error = "A generic otherwise clause requires the branch chain it completes.";
            return false;
        }

        var bodies = ImmutableArray.CreateBuilder<ImmutableArray<ProgramRequirement>>();
        var returned = ImmutableArray.CreateBuilder<SemanticExpression>();
        for (var index = 0; index < functionNames.Length; index++)
        {
            var body = ImmutableArray.CreateBuilder<ProgramRequirement>();
            foreach (var assignment in assignments)
            {
                body.Add(assignment);
            }

            if (functionNames.Length > 1)
            {
                if (results.Length != functionNames.Length)
                {
                    error = "A generic function declaration group requires one result per declared function.";
                    return false;
                }

                body.Add(new ReturnRequirement(results[index]));
                returned.Add(results[index]);
            }
            else if (chain is not null)
            {
                body.Add(chain);
                foreach (var branch in chain.Branches)
                {
                    returned.Add(((ReturnRequirement)branch.Body[0]).Value);
                }

                if (!chain.Otherwise.IsDefaultOrEmpty)
                {
                    returned.Add(((ReturnRequirement)chain.Otherwise[0]).Value);
                }
            }
            else if (results.Length == 1)
            {
                body.Add(new ReturnRequirement(results[0]));
                returned.Add(results[0]);
            }

            if (body.Count == 0)
            {
                error = "A generic function requirement requires at least one body requirement.";
                return false;
            }

            bodies.Add(body.ToImmutable());
        }

        var resultKind = InferResultKind(returned);
        for (var index = 0; index < functionNames.Length; index++)
        {
            declarations.Add(new FunctionRequirement(
                functionNames[index],
                parameters[index],
                resultKind,
                bodies[index]));
        }

        var callFact = facts.FirstOrDefault(fact => fact.Id == FunctionCallFactId);
        if (callFact is not null)
        {
            if (!TryGetExpressions(callFact, CallValueId, out var calls) || calls.IsDefaultOrEmpty)
            {
                error = "A generic function call requires typed call expressions.";
                return false;
            }

            var callPositions = GetCollectionPositions(callFact, CallValueId, calls.Length);
            for (var index = 0; index < calls.Length; index++)
            {
                events.Add(new RequirementEvent(
                    callPositions[index],
                    eventOrder++,
                    new OutputRequirement(calls[index])));
            }
        }

        return true;
    }

    /// <summary>
    /// Reads the parameter list of every declared function. A declaration group carries one captured
    /// parameter list per function, and each captured list names its parameters in call order.
    /// </summary>
    private static ImmutableArray<ImmutableArray<FunctionParameter>>? GetParameterLists(
        SequenceSemanticFact declaration,
        int count,
        out string? error)
    {
        error = null;
        ImmutableArray<string> declared;
        if (declaration.Collections.TryGetValue(FunctionParametersValueId, out var collection))
        {
            declared = collection;
        }
        else if (declaration.Values.TryGetValue(FunctionParametersValueId, out var single))
        {
            declared = [single];
        }
        else
        {
            error = "A generic function requirement requires declared parameters.";
            return null;
        }

        if (declared.IsDefault || declared.Length != count)
        {
            error = "A generic function requirement requires one parameter list per declared function.";
            return null;
        }

        var lists = ImmutableArray.CreateBuilder<ImmutableArray<FunctionParameter>>();
        foreach (var entry in declared)
        {
            var parameterNames = entry.Split(
                ',',
                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parameterNames.Length == 0)
            {
                error = "A generic function requirement requires named parameters.";
                return null;
            }

            lists.Add(parameterNames
                .Select(name => new FunctionParameter(name, null))
                .ToImmutableArray());
        }

        return lists.ToImmutable();
    }

    /// <summary>
    /// Infers the result kind a group of declarations states: every returned value must agree on one
    /// known kind, and an undeclared result leaves the kind unknown.
    /// </summary>
    private static SemanticValueKind? InferResultKind(
        ImmutableArray<SemanticExpression>.Builder returned)
    {
        if (returned.Count == 0)
        {
            return null;
        }

        SemanticValueKind? result = null;
        foreach (var expression in returned)
        {
            var kind = SemanticValueKindAnalysis.Infer(
                expression,
                ImmutableDictionary.Create<string, SemanticValueKind>(StringComparer.Ordinal),
                ImmutableDictionary.Create<string, SemanticValueKind>(StringComparer.Ordinal));
            if (kind is not { } known)
            {
                return null;
            }

            if (result is { } declared && declared != known)
            {
                return null;
            }

            result = known;
        }

        return result;
    }

    private static bool TryProjectAssignments(
        ImmutableArray<SequenceSemanticFact> facts,
        ImmutableArray<RequirementEvent>.Builder events,
        ref int eventOrder,
        out string? error)
    {
        error = null;
        var assignmentTarget = facts.FirstOrDefault(fact => fact.Id == AssignmentTargetFactId);
        var assignmentValue = facts.FirstOrDefault(fact => fact.Id == AssignmentValueFactId);
        if (assignmentTarget is null && assignmentValue is null)
        {
            return true;
        }

        if (assignmentTarget is null || assignmentValue is null ||
            !TryGetNames(assignmentTarget, out var variableNames) ||
            !TryGetExpressions(assignmentValue, OutputValueId, out var values) ||
            variableNames.IsDefaultOrEmpty ||
            variableNames.Length != values.Length ||
            variableNames.Any(string.IsNullOrWhiteSpace))
        {
            error = "A generic assignment requires aligned variable names and typed value expressions.";
            return false;
        }

        var targetPositions = GetCollectionPositions(assignmentTarget, "name", variableNames.Length);
        var valuePositions = GetCollectionPositions(assignmentValue, OutputValueId, values.Length);
        for (var index = 0; index < variableNames.Length; index++)
        {
            events.Add(new RequirementEvent(
                Math.Min(targetPositions[index], valuePositions[index]),
                eventOrder++,
                new AssignmentRequirement(variableNames[index], values[index])));
        }

        return true;
    }

    private static bool TryProjectOutputs(
        ImmutableArray<SequenceSemanticFact> facts,
        ImmutableArray<RequirementEvent>.Builder events,
        ref int eventOrder,
        out int? branchSeparatorPosition,
        out string? error)
    {
        error = null;
        branchSeparatorPosition = null;
        var outputFact = facts.FirstOrDefault(fact => fact.Id == OutputFactId);
        if (outputFact is null)
        {
            return true;
        }

        if (!TryGetExpressions(outputFact, OutputValueId, out var outputValues))
        {
            if (outputFact.Values.ContainsKey(OutputValueId) ||
                outputFact.Collections.ContainsKey(OutputValueId))
            {
                error = "A generic output requirement requires typed value expressions.";
                return false;
            }

            outputValues = ImmutableArray<SemanticExpression>.Empty;
        }

        if (!outputValues.IsDefaultOrEmpty)
        {
            var outputPositions = GetCollectionPositions(outputFact, OutputValueId, outputValues.Length);
            for (var index = 0; index < outputValues.Length; index++)
            {
                events.Add(new RequirementEvent(
                    outputPositions[index],
                    eventOrder++,
                    new OutputRequirement(outputValues[index])));
            }
        }

        if (outputFact.Values.ContainsKey(BranchSeparatorValueId) &&
            outputFact.ValuePositions is not null &&
            outputFact.ValuePositions.TryGetValue(BranchSeparatorValueId, out var separatorPosition))
        {
            branchSeparatorPosition = separatorPosition;
        }

        return true;
    }

    private static IEnumerable<RequirementEvent> Ordered(IEnumerable<RequirementEvent> events) =>
        events
            .OrderBy(item => item.Position)
            .ThenBy(item => item.Order);

    private static ImmutableArray<int> GetCollectionPositions(
        SequenceSemanticFact fact,
        string valueId,
        int count)
    {
        if (fact.CollectionPositions is not null &&
            fact.CollectionPositions.TryGetValue(valueId, out var positions) &&
            positions.Length == count)
        {
            return positions;
        }

        return Enumerable.Range(0, count).ToImmutableArray();
    }

    private static bool TryGetNames(
        SequenceSemanticFact fact,
        out ImmutableArray<string> values)
    {
        if (fact.Values.TryGetValue("name", out var value))
        {
            values = [value];
            return true;
        }

        return fact.Collections.TryGetValue("name", out values);
    }

    private static bool TryGetExpressions(
        SequenceSemanticFact fact,
        string valueId,
        out ImmutableArray<SemanticExpression> expressions)
    {
        if (fact.Expressions.TryGetValue(valueId, out var expression))
        {
            expressions = [expression];
            return true;
        }

        return fact.ExpressionCollections.TryGetValue(valueId, out expressions);
    }
}
