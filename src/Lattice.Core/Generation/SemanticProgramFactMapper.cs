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
        }

        var requirements = ImmutableArray.CreateBuilder<ProgramRequirement>();
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

            requirements.Add(new ConditionalRequirement(condition, thenRequirements, elseRequirements));
        }

        if (requirements.Count == 0)
        {
            error = "No supported generic scalar program requirements were found.";
            return false;
        }

        program = new SemanticProgram(requirements.ToImmutable());
        return true;
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
