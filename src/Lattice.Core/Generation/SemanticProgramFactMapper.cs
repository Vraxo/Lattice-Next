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
    private const string FunctionConversionFactId = "program.requirement.function.conversion";
    private const string FunctionTemplateFactId = "program.requirement.function.template";
    private const string FunctionSamplesFactId = "program.requirement.function.samples";
    private const string ConversionTargetValueId = "target";
    private const string ConversionErrorValueId = "error";
    private const string ConversionFallbackValueId = "fallback";
    private const string TemplatePrefixValueId = "prefix";
    private const string TemplateSuffixValueId = "suffix";
    private const string SampleValueId = "sample";
    private const string FunctionKeywordFactId = "program.function";

    /// <summary>The separators a request's multi-word class identity is read through.</summary>
    private static readonly char[] ClassIdentitySeparators =
        [' ', '\t', '\r', '\n', '-', '_', ',', '.', ':', ';', '/', '\\'];
    private const string DirectoryListingFactId = "program.requirement.directory-listing";
    private const string SerializationRoundTripFactId = "program.requirement.serialization-round-trip";
    private const string FileLinesFactId = "program.requirement.file-lines";
    private const string FileLinesPathValueId = "path";
    private const string FileLinesStreamValueId = "stream";
    private const string FileLinesItemValueId = "item";
    private const string FileLinesCounterValueId = "counter";
    private const string ClassFactId = "program.requirement.class";
    private const string ClassIdentityValueId = "name";
    private const string ClassInstanceValueId = "instance";
    private const string ClassFieldFactId = "program.requirement.class.field";
    private const string ClassFieldNameValueId = "field";
    private const string ClassFieldInitialValueId = "initial";
    private const string AccumulationFactId = "program.accumulation";
    private const string AccumulationNameValueId = "accumulator";
    private const string AccumulationInitialValueId = "initial";
    private const string AccumulationItemValueId = "item";
    private const string AccumulationCollectionValueId = "collection";
    private const string ClassMethodFactId = "program.requirement.class.method";
    private const string ClassMethodNameValueId = "method";
    private const string ClassMethodParameterValueId = "parameter";
    private const string ClassMethodResultValueId = "result";
    private const string ClassMethodGuardFactId = "program.requirement.class.method.guard";
    private const string ClassMethodUpdateFactId = "program.requirement.class.method.update";
    private const string ClassMethodResultFactId = "program.requirement.class.method.result";
    private const string GuardLeftValueId = "left";
    private const string GuardRightValueId = "right";
    private const string GuardOperatorValueId = "operator";
    private const string GuardValueId = "value";
    private const string ScenarioMethodValueId = "method";
    private const string DefaultInstanceName = "self";
    private const string NoResultMarker = "none";

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

        if (!TryProjectClasses(facts, declarations, out error))
        {
            return false;
        }

        if (!TryProjectSerializationRoundTrips(facts, declarations, out error))
        {
            return false;
        }

        if (!TryProjectDirectoryListings(facts, declarations, out error))
        {
            return false;
        }

        if (!TryProjectFileLines(facts, declarations, out error))
        {
            return false;
        }

        if (!TryProjectAccumulations(facts, declarations, out error))
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
            var details = string.Join(" | ", projection.Requirements.Select((requirement, index) =>
                $"{index}:{requirement.GetType().Name}" +
                $":single={SemanticProgramValidator.IsValid(new SemanticProgram([requirement]))}" +
                (requirement is AccumulationRequirement accumulation
                    ? $":source={SemanticExpressionAnalysis.IsValid(accumulation.Source)}" +
                      $":update={SemanticExpressionAnalysis.IsValid(accumulation.Update)}" +
                      $":initial={SemanticExpressionAnalysis.IsValid(accumulation.InitialValue)}" +
                      $":kind={accumulation.AccumulatorKind}" +
                      $":name={accumulation.AccumulatorName}:item={accumulation.ItemName}"
                    : string.Empty)));
            error = $"The projected generic program requirements are not structurally valid. [{details}]";
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

        var conversionFact = facts.FirstOrDefault(fact => fact.Id == FunctionConversionFactId);
        var templateFact = facts.FirstOrDefault(fact => fact.Id == FunctionTemplateFactId);
        var samplesFact = facts.FirstOrDefault(fact => fact.Id == FunctionSamplesFactId);
        if (functionNames.Length > 1 &&
            (conversionFact is not null || templateFact is not null || samplesFact is not null))
        {
            error = "A generic program captures a converted, templated, or sampled result for one function at a time.";
            return false;
        }

        if (parameters[0].IsDefaultOrEmpty)
        {
            error = "A generic function requirement requires named parameters.";
            return false;
        }

        ProgramRequirement? conversion = null;
        if (conversionFact is not null &&
            !TryBuildConversion(conversionFact, parameters[0][0].Name, out conversion, out error))
        {
            return false;
        }

        ReturnRequirement? templateResult = null;
        if (templateFact is not null &&
            !TryBuildTemplateResult(templateFact, parameters[0][0].Name, out templateResult, out error))
        {
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
            else if (conversion is not null)
            {
                body.Add(conversion);
            }
            else if (templateResult is not null)
            {
                body.Add(templateResult);
                returned.Add(templateResult.Value);
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

        if (samplesFact is not null)
        {
            if (!samplesFact.Collections.TryGetValue(SampleValueId, out var samples) ||
                samples.IsDefaultOrEmpty)
            {
                error = "A generic sample call requires the captured sample arguments.";
                return false;
            }

            foreach (var sample in samples)
            {
                events.Add(new RequirementEvent(
                    0,
                    eventOrder++,
                    new OutputRequirement(new SemanticFunctionCallExpression(
                        functionNames[0],
                        [new SemanticTextExpression(sample)]))));
            }
        }

        var callFact = facts.FirstOrDefault(fact => fact.Id == FunctionCallFactId);
        if (callFact is not null)
        {
            if (!TryGetExpressions(callFact, CallValueId, out var calls) || calls.IsDefaultOrEmpty)
            {
                error = "A generic function call requires typed call expressions.";
                return false;
            }

            if (calls.Any(call =>
                    call is not SemanticFunctionCallExpression declaredCall ||
                    !functionNames.Contains(declaredCall.Name, StringComparer.Ordinal)))
            {
                error = "A generic function call must call one of the declared functions.";
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
    /// Builds the attempted conversion a declaration performs and the recovery it describes: the body
    /// converts the declared parameter and the handler returns the captured fallback message.
    /// </summary>
    private static bool TryBuildConversion(
        SequenceSemanticFact fact,
        string parameterName,
        out ProgramRequirement? requirement,
        out string? error)
    {
        requirement = null;
        error = null;
        if (!fact.Values.TryGetValue(ConversionTargetValueId, out var targetText) ||
            !Enum.TryParse<SemanticValueKind>(targetText, ignoreCase: false, out var target) ||
            target is not (SemanticValueKind.Number or SemanticValueKind.Text) ||
            !fact.Values.TryGetValue(ConversionErrorValueId, out var errorType) ||
            string.IsNullOrWhiteSpace(errorType) ||
            !fact.Values.TryGetValue(ConversionFallbackValueId, out var fallback))
        {
            error = "A generic conversion requires a target kind, a handled error type, and a fallback message.";
            return false;
        }

        requirement = new TryRequirement(
            [
                new ReturnRequirement(new SemanticConversionExpression(
                    target,
                    new SemanticNameExpression(parameterName)))
            ],
            [
                new HandlerRequirement(
                    errorType,
                    [new ReturnRequirement(new SemanticTextExpression(fallback))])
            ],
            ResultName: null);
        return true;
    }

    /// <summary>
    /// Builds the text a declaration returns when it embeds one parameter between captured fragments.
    /// </summary>
    private static bool TryBuildTemplateResult(
        SequenceSemanticFact fact,
        string parameterName,
        out ReturnRequirement? requirement,
        out string? error)
    {
        requirement = null;
        error = null;
        if (!fact.Values.TryGetValue(TemplatePrefixValueId, out var prefix) ||
            !fact.Values.TryGetValue(TemplateSuffixValueId, out var suffix))
        {
            error = "A generic text template requires the fragments that surround the embedded value.";
            return false;
        }

        requirement = new ReturnRequirement(new SemanticTextTemplateExpression(
            [
                new SemanticTextExpression(prefix),
                new SemanticNameExpression(parameterName),
                new SemanticTextExpression(suffix)
            ]));
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

        if (declared.IsDefault || declared.Length == 0)
        {
            error = "A generic function requirement requires declared parameters.";
            return null;
        }

        if (count == 1)
        {
            // A single declaration captures one entry per spoken parameter, so every entry names a parameter.
            var names = declared
                .SelectMany(entry => entry.Split(
                    ',',
                    StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                .ToImmutableArray();
            if (names.IsDefaultOrEmpty || names.Any(string.IsNullOrWhiteSpace))
            {
                error = "A generic function requirement requires named parameters.";
                return null;
            }

            return [names.Select(name => new FunctionParameter(name, null)).ToImmutableArray()];
        }

        if (declared.Length != count)
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

    /// <summary>
    /// Projects a request that writes held data to a file in a serialized form and reads it back. The request
    /// supplies the data, the file, the format, and every name; the model supplies the encode and decode
    /// steps, and the pack decides which calls spell the format.
    /// </summary>
    private static bool TryProjectSerializationRoundTrips(
        ImmutableArray<SequenceSemanticFact> facts,
        ImmutableArray<ProgramRequirement>.Builder declarations,
        out string? error)
    {
        error = null;
        var fact = facts.FirstOrDefault(candidate => candidate.Id == SerializationRoundTripFactId);
        if (fact is null)
        {
            return true;
        }

        string[] scalars =
        [
            "records",
            "path",
            "result-prefix",
            "output-stream",
            "input-stream",
            "format",
            "indent",
            "module"
        ];
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var valueId in scalars)
        {
            if (!TryGetCollection(fact, valueId, out var captured) || captured.Length != 1)
            {
                error = $"A serialization round trip requires exactly one '{valueId}' value.";
                return false;
            }

            values[valueId] = captured[0];
        }

        if (!TryGetCollection(fact, "fields", out var fields) || fields.IsDefaultOrEmpty)
        {
            error = "A serialization round trip requires the field names of the data it holds.";
            return false;
        }

        if (!TryGetCollection(fact, "values", out var cells) || cells.IsDefaultOrEmpty)
        {
            error = "A serialization round trip requires the values of the data it holds.";
            return false;
        }

        if (fields.Length == 0 || cells.Length == 0 || cells.Length % fields.Length != 0)
        {
            error = "A serialization round trip needs one value per field for every record it holds.";
            return false;
        }

        var records = ImmutableArray.CreateBuilder<SemanticExpression>();
        for (var offset = 0; offset < cells.Length; offset += fields.Length)
        {
            var entries = ImmutableArray.CreateBuilder<SemanticRecordEntry>();
            for (var index = 0; index < fields.Length; index++)
            {
                entries.Add(new SemanticRecordEntry(
                    fields[index],
                    DataValue(cells[offset + index])));
            }

            records.Add(new SemanticRecordLiteralExpression(entries.ToImmutable()));
        }

        var recordsName = values["records"];
        var resultName = values["result-prefix"] + recordsName;
        var inputStream = values["input-stream"];
        declarations.Add(new ImportRequirement(values["module"]));
        declarations.Add(new AssignmentRequirement(
            recordsName,
            new SemanticListLiteralExpression(records.ToImmutable())));
        declarations.Add(new FileWriteRequirement(
            values["path"],
            values["output-stream"],
            [
                new SerializeRequirement(
                    values["format"],
                    new SemanticNameExpression(recordsName),
                    values["output-stream"],
                    values["indent"])
            ]));
        declarations.Add(new FileReadRequirement(
            values["path"],
            inputStream,
            [
                new DeserializeRequirement(values["format"], inputStream, resultName)
            ]));
        declarations.Add(new OutputRequirement(new SemanticNameExpression(resultName)));
        return true;
    }

    /// <summary>
    /// Reads one declared data cell as the value kind it names: a canonical number stays a number and every
    /// other cell is text.
    /// </summary>
    private static SemanticExpression DataValue(string cell) =>
        cell.Length > 0 && cell.All(char.IsAsciiDigit)
            ? new SemanticNumberExpression(cell)
            : new SemanticTextExpression(cell);

    /// <summary>
    /// Projects a request that lists the files of a directory and reports each one's name and size, with a
    /// named failure handled. The request supplies every name and text fragment; the requirement model
    /// supplies the shape, and the pack spells the scan, the ordering, and the handler.
    /// </summary>
    private static bool TryProjectDirectoryListings(
        ImmutableArray<SequenceSemanticFact> facts,
        ImmutableArray<ProgramRequirement>.Builder declarations,
        out string? error)
    {
        error = null;
        var fact = facts.FirstOrDefault(candidate => candidate.Id == DirectoryListingFactId);
        if (fact is null)
        {
            return true;
        }

        string[] required =
        [
            "directory",
            "item",
            "member",
            "size-call",
            "size-member",
            "filter",
            "error",
            "position",
            "message",
            "recovery",
            "imports"
        ];
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var modules = ImmutableArray<string>.Empty;
        foreach (var valueId in required)
        {
            if (!TryGetCollection(fact, valueId, out var captured) || captured.IsDefaultOrEmpty)
            {
                error = $"A directory listing requires a '{valueId}' value.";
                return false;
            }

            if (!string.Equals(valueId, "imports", StringComparison.Ordinal) && captured.Length != 1)
            {
                error = $"A directory listing requires exactly one '{valueId}' value.";
                return false;
            }

            if (string.Equals(valueId, "imports", StringComparison.Ordinal))
            {
                modules = captured;
                continue;
            }

            values[valueId] = captured[0];
        }

        if (!int.TryParse(
                values["position"],
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out var position))
        {
            error = "A directory listing requires an argument position the program reads.";
            return false;
        }

        foreach (var module in modules)
        {
            declarations.Add(new ImportRequirement(module));
        }

        var directory = values["directory"];
        var item = values["item"];
        declarations.Add(new AssignmentRequirement(
            directory,
            new SemanticProgramArgumentExpression(position)));
        declarations.Add(new TryRequirement(
            [
                new DirectoryListingRequirement(
                    new SemanticNameExpression(directory),
                    item,
                    new SemanticMemberAccessExpression(new SemanticNameExpression(item), values["member"]),
                    [
                        new ConditionalRequirement(
                            new SemanticMethodCallExpression(
                                new SemanticNameExpression(item),
                                values["filter"],
                                []),
                            [
                                new OutputRequirement(BuildMessage(
                                    values["message"],
                                    [
                                        new SemanticMemberAccessExpression(
                                            new SemanticNameExpression(item),
                                            values["member"]),
                                        new SemanticMemberAccessExpression(
                                            new SemanticMethodCallExpression(
                                                new SemanticNameExpression(item),
                                                values["size-call"],
                                                []),
                                            values["size-member"])
                                    ]))
                            ],
                            [])
                    ])
            ],
            [
                new HandlerRequirement(
                    values["error"],
                    [
                        new OutputRequirement(BuildMessage(
                            values["recovery"],
                            [new SemanticNameExpression(directory)]))
                    ])
            ],
            ResultName: null));
        return true;
    }

    /// <summary>
    /// Builds one text template from a message the request declares and the values it reports. The text
    /// between placeholders stays request data, in placeholder order, so no shared code spells it.
    /// </summary>
    private static SemanticTextTemplateExpression BuildMessage(
        string template,
        ImmutableArray<SemanticExpression> values)
    {
        var parts = ImmutableArray.CreateBuilder<SemanticExpression>();
        var remaining = template;
        for (var index = 0; index < values.Length; index++)
        {
            var marker = "{" + index.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}";
            var position = remaining.IndexOf(marker, StringComparison.Ordinal);
            if (position < 0)
            {
                parts.Add(values[index]);
                continue;
            }

            if (position > 0)
            {
                parts.Add(new SemanticTextExpression(remaining[..position]));
            }

            parts.Add(values[index]);
            remaining = remaining[(position + marker.Length)..];
        }

        if (remaining.Length > 0)
        {
            parts.Add(new SemanticTextExpression(remaining));
        }

        return new SemanticTextTemplateExpression([.. parts]);
    }

    /// <summary>
    /// Projects a request that counts the non-empty lines of a text file. The request supplies the path
    /// and every name; the shape — start at zero, read the file, test each line, update, then report — is
    /// the requirement model's, and the pack spells each statement.
    /// </summary>
    private static bool TryProjectFileLines(
        ImmutableArray<SequenceSemanticFact> facts,
        ImmutableArray<ProgramRequirement>.Builder declarations,
        out string? error)
    {
        error = null;
        var fact = facts.FirstOrDefault(candidate => candidate.Id == FileLinesFactId);
        if (fact is null)
        {
            return true;
        }

        if (!TryGetCollection(fact, FileLinesPathValueId, out var paths) ||
            paths.Length != 1 ||
            string.IsNullOrWhiteSpace(paths[0]) ||
            !TryGetCollection(fact, FileLinesStreamValueId, out var streams) ||
            streams.Length != 1 ||
            !TryGetCollection(fact, FileLinesItemValueId, out var items) ||
            items.Length != 1 ||
            !TryGetCollection(fact, FileLinesCounterValueId, out var counters) ||
            counters.Length != 1)
        {
            error = "A file line count requires one path, stream name, line name, and counter name.";
            return false;
        }

        var counter = counters[0];
        declarations.Add(new AssignmentRequirement(counter, new SemanticNumberExpression("0")));
        declarations.Add(new FileReadRequirement(
            paths[0],
            streams[0],
            [
                new IterationRequirement(
                    items[0],
                    new SemanticNameExpression(streams[0]),
                    [
                        new ConditionalRequirement(
                            new SemanticMethodCallExpression(
                                new SemanticNameExpression(items[0]),
                                "strip",
                                []),
                            [
                                new AugmentedAssignmentRequirement(
                                    new SemanticNameExpression(counter),
                                    SemanticExpressionOperator.Add,
                                    new SemanticNumberExpression("1"))
                            ],
                            [])
                    ])
            ]));
        declarations.Add(new OutputRequirement(new SemanticNameExpression(counter)));
        return true;
    }

    /// <summary>
    /// Projects a request that adds every item of a collection into a named accumulator: "starts total at 0
    /// and adds each number in [1, 2, 3] to total". The request supplies the accumulator, its initial value,
    /// the item name, and the collection contents; the shape — initialize, iterate, update, report — is the
    /// requirement model's and the pack spells each statement.
    /// </summary>
    private static bool TryProjectAccumulations(
        ImmutableArray<SequenceSemanticFact> facts,
        ImmutableArray<ProgramRequirement>.Builder declarations,
        out string? error)
    {
        error = null;
        var fact = facts.FirstOrDefault(candidate => candidate.Id == AccumulationFactId);
        if (fact is null)
        {
            return true;
        }

        if (!TryGetCollection(fact, AccumulationNameValueId, out var names) ||
            names.Length != 1 ||
            string.IsNullOrWhiteSpace(names[0]) ||
            !TryGetCollection(fact, AccumulationInitialValueId, out var initialValues) ||
            initialValues.Length != 1 ||
            string.IsNullOrWhiteSpace(initialValues[0]) ||
            !TryGetCollection(fact, AccumulationItemValueId, out var items) ||
            items.Length != 1 ||
            string.IsNullOrWhiteSpace(items[0]) ||
            !fact.SemanticCollections.TryGetValue(AccumulationCollectionValueId, out var contents) ||
            contents.Items.IsDefaultOrEmpty)
        {
            error = "An accumulation requires one accumulator name, initial value, item name, and collection.";
            return false;
        }

        var accumulator = names[0];
        var item = items[0];
        var itemKind = contents.Items[0].Kind;
        declarations.Add(new AccumulationRequirement(
            accumulator,
            itemKind,
            item,
            new SemanticListLiteralExpression(
                [.. contents.Items.Select(ToLiteralExpression)]),
            ToLiteralExpression(new SemanticCollectionItem(itemKind, initialValues[0])),
            new SemanticBinaryExpression(
                SemanticExpressionOperator.Add,
                new SemanticNameExpression(accumulator),
                new SemanticNameExpression(item))));

        // A request that asks to display the total reports the accumulator itself, unless it already stated
        // the output as a typed value expression.
        if (RequestsOutput(facts) && !CarriesTypedOutput(facts))
        {
            declarations.Add(new OutputRequirement(new SemanticNameExpression(accumulator)));
        }

        return true;
    }

    private static bool RequestsOutput(ImmutableArray<SequenceSemanticFact> facts) =>
        facts.Any(fact =>
            string.Equals(fact.Id, "program.output", StringComparison.Ordinal) ||
            fact.Id.StartsWith("value.output", StringComparison.Ordinal));

    private static bool CarriesTypedOutput(ImmutableArray<SequenceSemanticFact> facts) =>
        facts.Any(fact =>
            string.Equals(fact.Id, OutputFactId, StringComparison.Ordinal) &&
            (fact.Expressions.ContainsKey(OutputValueId) ||
             fact.ExpressionCollections.ContainsKey(OutputValueId)));

    private static SemanticExpression ToLiteralExpression(SemanticCollectionItem item) =>
        item.Kind == SemanticCollectionItemKind.Text
            ? new SemanticTextExpression(item.Value)
            : new SemanticNumberExpression(item.Value);

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

    /// <summary>
    /// Projects a declared class from request facts: the identity the request names, the fields it
    /// declares with what they start from, and the methods with their parameters, in-place updates,
    /// guards, and returned values. Method bodies keep request order: guards first, then updates, then
    /// the value the method returns.
    /// </summary>
    private static bool TryProjectClasses(
        ImmutableArray<SequenceSemanticFact> facts,
        ImmutableArray<ProgramRequirement>.Builder declarations,
        out string? error)
    {
        error = null;
        var classFacts = facts.Where(fact => fact.Id == ClassFactId).ToArray();
        if (classFacts.Length == 0)
        {
            return true;
        }

        if (classFacts.Length > 1)
        {
            error = "A generic program declares one class per request.";
            return false;
        }

        var classFact = classFacts[0];
        if (!TryGetCollection(classFact, ClassIdentityValueId, out var identities) ||
            identities.Length != 1 ||
            !TryDeriveClassName(identities[0], out var className))
        {
            error = "A generic class declaration requires an identity the request names.";
            return false;
        }

        // The instance a body refers to is declared by the request when it says so, and otherwise takes
        // the neutral default the model documents.
        var instanceName = DefaultInstanceName;
        if (TryGetCollection(classFact, ClassInstanceValueId, out var declaredInstances) &&
            declaredInstances.Length == 1 &&
            !string.IsNullOrWhiteSpace(declaredInstances[0]))
        {
            instanceName = declaredInstances[0];
        }

        var fields = ImmutableArray<ClassField>.Empty;
        var fieldFact = facts.FirstOrDefault(fact => fact.Id == ClassFieldFactId);
        if (fieldFact is not null)
        {
            if (!TryGetCollection(fieldFact, ClassFieldNameValueId, out var fieldNames) ||
                fieldNames.IsDefaultOrEmpty ||
                !TryGetOptionalCollection(fieldFact, ClassFieldInitialValueId, out var initialTexts) ||
                (!initialTexts.IsDefaultOrEmpty && initialTexts.Length != fieldNames.Length))
            {
                error = "A generic class field requires one initial value per declared field.";
                return false;
            }

            var fieldBuilder = ImmutableArray.CreateBuilder<ClassField>(fieldNames.Length);
            for (var index = 0; index < fieldNames.Length; index++)
            {
                // A field the request gives no starting value for starts from the value the constructor
                // receives for it, so the two names stay one value.
                if (initialTexts.IsDefaultOrEmpty)
                {
                    fieldBuilder.Add(new ClassField(fieldNames[index], new SemanticNameExpression(fieldNames[index])));
                    continue;
                }

                if (!TryReadInitialValue(initialTexts[index], out var initialValue))
                {
                    error = $"A generic class field '{fieldNames[index]}' starts from a value the request does not describe.";
                    return false;
                }

                fieldBuilder.Add(new ClassField(fieldNames[index], initialValue));
            }

            fields = fieldBuilder.ToImmutable();
        }

        var methods = ImmutableArray<MethodRequirement>.Empty;
        var methodFact = facts.FirstOrDefault(fact => fact.Id == ClassMethodFactId);
        if (methodFact is not null)
        {
            if (!TryGetCollection(methodFact, ClassMethodNameValueId, out var methodNames) ||
                methodNames.IsDefaultOrEmpty ||
                !TryGetOptionalCollection(methodFact, ClassMethodParameterValueId, out var parameterLists) ||
                (!parameterLists.IsDefaultOrEmpty && parameterLists.Length != methodNames.Length) ||
                !TryGetOptionalCollection(methodFact, ClassMethodResultValueId, out var resultKinds) ||
                (!resultKinds.IsDefaultOrEmpty && resultKinds.Length != methodNames.Length))
            {
                error = "A generic class method requires aligned names, parameters, and declared result kinds.";
                return false;
            }

            var guards = facts.FirstOrDefault(fact => fact.Id == ClassMethodGuardFactId);
            var updates = facts.FirstOrDefault(fact => fact.Id == ClassMethodUpdateFactId);
            var results = facts.FirstOrDefault(fact => fact.Id == ClassMethodResultFactId);
            var methodBuilder = ImmutableArray.CreateBuilder<MethodRequirement>(methodNames.Length);
            for (var index = 0; index < methodNames.Length; index++)
            {
                var methodName = methodNames[index];
                var parameters = parameterLists.IsDefaultOrEmpty
                    ? ImmutableArray<FunctionParameter>.Empty
                    : SplitParameters(parameterLists[index]);
                SemanticValueKind? returnKind = null;
                if (!resultKinds.IsDefaultOrEmpty &&
                    !string.Equals(resultKinds[index], NoResultMarker, StringComparison.OrdinalIgnoreCase))
                {
                    if (!Enum.TryParse<SemanticValueKind>(resultKinds[index], ignoreCase: false, out var declaredKind) ||
                        !Enum.IsDefined(declaredKind))
                    {
                        error = $"A generic class method '{methodName}' declares a result kind the model does not know.";
                        return false;
                    }

                    returnKind = declaredKind;
                }

                var body = ImmutableArray.CreateBuilder<ProgramRequirement>();
                if (!TryAddMethodGuards(
                        guards,
                        methodName,
                        parameters,
                        fields,
                        instanceName,
                        body,
                        out error) ||
                    !TryAddMethodUpdates(
                        updates,
                        methodName,
                        parameters,
                        fields,
                        instanceName,
                        body,
                        out error) ||
                    !TryAddMethodResults(results, methodName, parameters, fields, instanceName, body, out error))
                {
                    return false;
                }

                if (body.Count == 0)
                {
                    error = $"A generic class method '{methodName}' requires at least one body requirement.";
                    return false;
                }

                methodBuilder.Add(new MethodRequirement(methodName, parameters, returnKind, body.ToImmutable()));
            }

            methods = methodBuilder.ToImmutable();
        }

        if (fields.IsDefaultOrEmpty && methods.IsDefaultOrEmpty)
        {
            error = "A generic class declaration requires at least one field or method.";
            return false;
        }

        declarations.Add(new ClassRequirement(className, fields, methods)
        {
            InstanceName = instanceName
        });
        return true;
    }

    private static bool TryAddMethodGuards(
        SequenceSemanticFact? guardFact,
        string methodName,
        ImmutableArray<FunctionParameter> parameters,
        ImmutableArray<ClassField> fields,
        string instanceName,
        ImmutableArray<ProgramRequirement>.Builder body,
        out string? error)
    {
        error = null;
        if (!TryReadScopedEntries(
                guardFact,
                methodName,
                [GuardLeftValueId, GuardRightValueId, GuardOperatorValueId, GuardValueId],
                out var entries,
                out error))
        {
            return false;
        }

        foreach (var entry in entries)
        {
            if (!TryReadOperand(entry[0], parameters, fields, instanceName, out var left) ||
                !TryReadOperand(entry[1], parameters, fields, instanceName, out var right) ||
                !TryReadOperator(entry[2], out var comparison) ||
                !TryReadOperand(entry[3], parameters, fields, instanceName, out var returnedValue))
            {
                error = $"A generic class method '{methodName}' guards an update with a comparison the model cannot express.";
                return false;
            }

            body.Add(new ConditionalChainRequirement(
                [new ConditionalBranch(new SemanticBinaryExpression(comparison, left, right), [new ReturnRequirement(returnedValue)])],
                ImmutableArray<ProgramRequirement>.Empty));
        }

        return true;
    }

    private static bool TryAddMethodUpdates(
        SequenceSemanticFact? updateFact,
        string methodName,
        ImmutableArray<FunctionParameter> parameters,
        ImmutableArray<ClassField> fields,
        string instanceName,
        ImmutableArray<ProgramRequirement>.Builder body,
        out string? error)
    {
        error = null;
        if (!TryReadScopedEntries(
                updateFact,
                methodName,
                [ClassFieldNameValueId, GuardOperatorValueId, GuardValueId],
                out var entries,
                out error))
        {
            return false;
        }

        foreach (var entry in entries)
        {
            if (!fields.Any(field => string.Equals(field.Name, entry[0], StringComparison.Ordinal)) ||
                !TryReadUpdateOperator(entry[1], out var updateOperator) ||
                !TryReadOperand(entry[2], parameters, fields, instanceName, out var updatedBy))
            {
                error = $"A generic class method '{methodName}' updates a value the model cannot express.";
                return false;
            }

            body.Add(new AugmentedAssignmentRequirement(
                new SemanticMemberAccessExpression(new SemanticNameExpression(instanceName), entry[0]),
                updateOperator,
                updatedBy));
        }

        return true;
    }

    private static bool TryAddMethodResults(
        SequenceSemanticFact? resultFact,
        string methodName,
        ImmutableArray<FunctionParameter> parameters,
        ImmutableArray<ClassField> fields,
        string instanceName,
        ImmutableArray<ProgramRequirement>.Builder body,
        out string? error)
    {
        error = null;
        if (!TryReadScopedEntries(resultFact, methodName, [GuardValueId], out var entries, out error))
        {
            return false;
        }

        foreach (var entry in entries)
        {
            if (!TryReadOperand(entry[0], parameters, fields, instanceName, out var returnedValue))
            {
                error = $"A generic class method '{methodName}' returns a value the model cannot express.";
                return false;
            }

            body.Add(new ReturnRequirement(returnedValue));
        }

        return true;
    }

    /// <summary>
    /// Reads the entries of a fact that belongs to one method: the captured method names select the
    /// entries, and every value id names one aligned column of that entry.
    /// </summary>
    private static bool TryReadScopedEntries(
        SequenceSemanticFact? fact,
        string methodName,
        ImmutableArray<string> valueIds,
        out ImmutableArray<ImmutableArray<string>> entries,
        out string? error)
    {
        error = null;
        entries = ImmutableArray<ImmutableArray<string>>.Empty;
        if (fact is null)
        {
            return true;
        }

        if (!TryGetCollection(fact, ScenarioMethodValueId, out var owners))
        {
            error = "A generic class method behaviour requires the method it belongs to.";
            return false;
        }

        var columns = ImmutableArray.CreateBuilder<ImmutableArray<string>>(valueIds.Length);
        foreach (var valueId in valueIds)
        {
            if (!TryGetCollection(fact, valueId, out var column) || column.Length != owners.Length)
            {
                error = "A generic class method behaviour requires one aligned value per captured entry.";
                return false;
            }

            columns.Add(column);
        }

        var builder = ImmutableArray.CreateBuilder<ImmutableArray<string>>();
        for (var index = 0; index < owners.Length; index++)
        {
            if (!string.Equals(owners[index], methodName, StringComparison.Ordinal))
            {
                continue;
            }

            builder.Add(columns.Select(column => column[index]).ToImmutableArray());
        }

        entries = builder.ToImmutable();
        return true;
    }

    /// <summary>
    /// Reads one request value: a name the method receives, a field of the declaring class, a boolean,
    /// a number, or a quoted text.
    /// </summary>
    private static bool TryReadOperand(
        string text,
        ImmutableArray<FunctionParameter> parameters,
        ImmutableArray<ClassField> fields,
        string instanceName,
        out SemanticExpression expression)
    {
        expression = null!;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (parameters.Any(parameter => string.Equals(parameter.Name, text, StringComparison.Ordinal)))
        {
            expression = new SemanticNameExpression(text);
            return true;
        }

        if (fields.Any(field => string.Equals(field.Name, text, StringComparison.Ordinal)))
        {
            expression = new SemanticMemberAccessExpression(new SemanticNameExpression(instanceName), text);
            return true;
        }

        if (bool.TryParse(text, out var boolean))
        {
            expression = new SemanticBooleanExpression(boolean);
            return true;
        }

        var number = new SemanticNumberExpression(text);
        if (SemanticExpressionAnalysis.IsValid(number))
        {
            expression = number;
            return true;
        }

        if (text.Length > 1 && text[0] == '"' && text[^1] == '"')
        {
            var quoted = new SemanticTextExpression(text[1..^1]);
            if (SemanticExpressionAnalysis.IsValid(quoted))
            {
                expression = quoted;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Reads what a field starts from: a boolean, a number, a quoted text, or the name of the value the
    /// constructor receives for it.
    /// </summary>
    private static bool TryReadInitialValue(string text, out SemanticExpression expression)
    {
        expression = null!;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (bool.TryParse(text, out var boolean))
        {
            expression = new SemanticBooleanExpression(boolean);
            return true;
        }

        var number = new SemanticNumberExpression(text);
        if (SemanticExpressionAnalysis.IsValid(number))
        {
            expression = number;
            return true;
        }

        if (text.Length > 1 && text[0] == '"' && text[^1] == '"')
        {
            var quoted = new SemanticTextExpression(text[1..^1]);
            if (SemanticExpressionAnalysis.IsValid(quoted))
            {
                expression = quoted;
                return true;
            }
        }

        if (IsIdentifierCharacters(text))
        {
            expression = new SemanticNameExpression(text);
            return true;
        }

        return false;
    }

    private static bool TryReadOperator(string text, out SemanticExpressionOperator comparison)
    {
        comparison = default;
        return Enum.TryParse(text, ignoreCase: false, out comparison) &&
            Enum.IsDefined(comparison) &&
            comparison is SemanticExpressionOperator.Equal or
                SemanticExpressionOperator.NotEqual or
                SemanticExpressionOperator.LessThan or
                SemanticExpressionOperator.LessThanOrEqual or
                SemanticExpressionOperator.GreaterThan or
                SemanticExpressionOperator.GreaterThanOrEqual;
    }

    private static bool TryReadUpdateOperator(string text, out SemanticExpressionOperator updateOperator)
    {
        updateOperator = default;
        return Enum.TryParse(text, ignoreCase: false, out updateOperator) &&
            Enum.IsDefined(updateOperator) &&
            updateOperator is SemanticExpressionOperator.Add or SemanticExpressionOperator.Subtract;
    }

    private static ImmutableArray<FunctionParameter> SplitParameters(string parameterList) =>
        string.IsNullOrWhiteSpace(parameterList)
            ? ImmutableArray<FunctionParameter>.Empty
            : parameterList
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(name => new FunctionParameter(name, null))
                .ToImmutableArray();

    /// <summary>
    /// Turns the words a request uses for a class into an identifier: a name the request already spells
    /// as one is kept, and a multi-word identity is joined with capitalized words.
    /// </summary>
    private static bool TryDeriveClassName(string words, out string className)
    {
        className = string.Empty;
        if (string.IsNullOrWhiteSpace(words))
        {
            return false;
        }

        var trimmed = words.Trim();
        var derived = IsIdentifierCharacters(trimmed)
            ? trimmed
            : string.Concat(trimmed
                .Split(
                    ClassIdentitySeparators,
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(word => char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant()));
        if (derived.Length == 0 || derived.Length > 128 || !IsIdentifierCharacters(derived))
        {
            return false;
        }

        className = derived;
        return true;
    }

    private static bool IsIdentifierCharacters(string value) =>
        (char.IsLetter(value[0]) || value[0] == '_') &&
        value.All(character => char.IsLetterOrDigit(character) || character == '_');

    private static bool TryGetCollection(
        SequenceSemanticFact fact,
        string valueId,
        out ImmutableArray<string> values)
    {
        if (fact.Collections.TryGetValue(valueId, out values))
        {
            return true;
        }

        if (fact.Values.TryGetValue(valueId, out var single))
        {
            values = [single];
            return true;
        }

        values = ImmutableArray<string>.Empty;
        return false;
    }

    private static bool TryGetOptionalCollection(
        SequenceSemanticFact fact,
        string valueId,
        out ImmutableArray<string> values)
    {
        values = ImmutableArray<string>.Empty;
        return !fact.Collections.ContainsKey(valueId) && !fact.Values.ContainsKey(valueId) ||
            TryGetCollection(fact, valueId, out values);
    }

}
