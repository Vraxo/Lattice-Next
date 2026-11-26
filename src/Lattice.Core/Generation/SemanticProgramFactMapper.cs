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
    private static readonly char[] LabelTrimCharacters = [',', '.', ';', ':', ' '];

    private const string CollectionFilterFactId = "program.collection-filter";
    private const string CollectionFilterSignatureFactId = "program.collection-filter.signature";
    private const string CollectionFilterComparisonFactId = "program.collection-filter.comparison";
    private const string CollectionFilterThresholdFactId = "program.collection-filter.threshold";
    private const string CollectionFilterAverageFactId = "program.collection-filter.average";
    private const string CollectionFilterNameValueId = "name";
    private const string CollectionFilterParameterValueId = "parameter";
    private const string CollectionFilterItemValueId = "item";
    private const string CollectionFilterOperatorValueId = "operator";
    private const string CollectionFilterThresholdValueId = "expression";
    private const string GroupedAggregationFactId = "program.record-aggregation";
    private const string GroupedAggregationIntentFactId = "program.record-aggregation.intent";
    private const string GroupedAggregationRecordValueId = "record";
    private const string GroupedAggregationGroupValueId = "group";
    private const string GroupedAggregationMeasureValueId = "measure";
    private const string MultiFieldSortFactId = "program.sort.fields";
    private const string MultiFieldSortKeysValueId = "keys";
    private const string MultiFieldSortKindValue = "fields";
    private const string RecordSortFactId = "program.sort.records";
    private const string RecordSortFunctionValueId = "function";
    private const string RecordSortRecordsValueId = "records";
    private const string RecordSortFieldValueId = "field";
    private const string RecordSortOrderValueId = "order";
    private const string RecordSortSensitivityValueId = "sensitivity";
    private const string RecordSortKindValueId = "kind";
    private const string RecordSortKindValue = "records";
    private const string RecordSortDescendingValue = "descending";
    private const string RecordSortCaseInsensitiveValue = "case-insensitive";
    private const string RecordSortLiteralMap = "map";
    private const string DictionaryMappingFactId = "program.mapping.dictionary";
    private const string MappingKindValueId = "kind";
    private const string MappingItemValueId = "item";
    private const string MappingParameterValueId = "parameter";
    private const string MappingResultValueId = "result";
    private const string MappingOffsetValueId = "offset";
    private const string MappingLengthKind = "length";
    private const string MappingSquareKind = "square";
    private const string MappingOffsetKind = "offset";

    private const string DictionaryIterationFactId = "program.dictionary-iteration";
    private const string DictionaryIterationCollectionValueId = "collection";
    private const string DictionaryLiteralFactId = "program.declaration.dictionary";
    private const string DictionaryNameValueId = "name";
    private const string DictionaryKeysValueId = "keys";
    private const string DictionaryValuesValueId = "values";
    private const string DictionaryKeyNameValueId = "key";
    private const string DictionaryValueNameValueId = "value";
    private static readonly char[] LiteralEntryTrimCharacters = ['{', '}', '\'', '"', ',', '.', ':', ';', ' '];

    private const string ParityFilterFactId = "program.parity-filter";
    private const string ParityValueId = "parity";
    private const string CollectionLoopFactId = "program.collection-loop";
    private const string LoopItemValueId = "item";
    private const string LoopCollectionValueId = "collection";
    private const string ParityDivisor = "2";
    private const string SignClassificationFactId = "program.classification.sign";
    private const string SignPositiveLabelValueId = "positive";
    private const string SignNegativeLabelValueId = "negative";
    private const string SignZeroLabelValueId = "zero";
    private const string NumberDeclarationFactId = "program.declaration.numbers";
    private const string NumberDeclarationCountValueId = "count";
    private const string NumberDeclarationSumValueId = "sum";
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
        out string? error) =>
        TryCreate(facts, exampleData: null, out program, out error);

    public static bool TryCreate(
        ImmutableArray<SequenceSemanticFact> facts,
        GenerationExampleData? exampleData,
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

        // A parity filter is a whole loop with a guarded body, so it is projected on its own: the request's
        // collection, item, and parity word, or the declared example data when the request names no list.
        var parity = facts.FirstOrDefault(candidate => candidate.Id == ParityFilterFactId);
        if (parity is not null)
        {
            if (!TryCreateParityFilter(parity, facts, exampleData, out var filterProgram, out error))
            {
                return false;
            }

            program = filterProgram;
            return true;
        }

        // A collection filter is a whole function, so it is projected on its own.
        var collectionFilter = facts.FirstOrDefault(candidate => candidate.Id == CollectionFilterFactId);
        if (collectionFilter is not null)
        {
            if (!TryCreateCollectionFilter(facts, exampleData, out var filterExpressionProgram, out error))
            {
                return false;
            }

            program = filterExpressionProgram;
            return true;
        }

        // A several-key sort is a whole function, so it is projected on its own.
        var multiFieldSort = facts.FirstOrDefault(candidate =>
            candidate.Id == MultiFieldSortFactId && IsCompleteMultiFieldSort(candidate));
        if (multiFieldSort is not null)
        {
            if (!TryCreateMultiFieldSort(multiFieldSort, exampleData, out var multiFieldProgram, out error))
            {
                return false;
            }

            program = multiFieldProgram;
            return true;
        }

        // A record sort is a whole function, so it is projected on its own.
        var recordSort = facts.FirstOrDefault(candidate =>
            candidate.Id == RecordSortFactId && IsCompleteRecordSort(candidate));
        if (recordSort is not null)
        {
            if (!TryCreateRecordSort(recordSort, exampleData, out var sortProgram, out error))
            {
                return false;
            }

            program = sortProgram;
            return true;
        }

        // A dictionary mapping is a whole function, so it is projected on its own.
        var mapping = facts.FirstOrDefault(candidate =>
            candidate.Id == DictionaryMappingFactId && IsCompleteDictionaryMapping(candidate));
        if (mapping is not null)
        {
            if (!TryCreateDictionaryMapping(mapping, exampleData, out var mappingProgram, out error))
            {
                return false;
            }

            program = mappingProgram;
            return true;
        }

        // A declared dictionary is a whole data-carrying loop, so it is projected on its own.
        var dictionaryLiteral = facts.FirstOrDefault(candidate => candidate.Id == DictionaryLiteralFactId);
        if (dictionaryLiteral is not null)
        {
            if (!TryCreateDictionaryIteration(dictionaryLiteral, out var dictionaryProgram, out error))
            {
                return false;
            }

            program = dictionaryProgram;
            return true;
        }

        // A grouped total is a whole data-carrying loop, so it is projected on its own.
        var aggregation = facts.FirstOrDefault(candidate => candidate.Id == GroupedAggregationFactId);
        if (aggregation is not null)
        {
            if (!TryCreateGroupedAggregation(aggregation, facts, exampleData, out var aggregationProgram, out error))
            {
                return false;
            }

            program = aggregationProgram;
            return true;
        }

        // A request that names only the keys and values of a dictionary it does not state is planned as a
        // mapping the reader fills in, followed by the loop that reports each pair.
        var dictionaryIteration = facts.FirstOrDefault(candidate => candidate.Id == DictionaryIterationFactId);
        if (dictionaryIteration is not null)
        {
            if (!TryCreateDeclaredDictionaryIteration(
                    dictionaryIteration,
                    exampleData,
                    out var iterationProgram,
                    out error))
            {
                return false;
            }

            program = iterationProgram;
            return true;
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

        if (!TryProjectAccumulations(facts, exampleData, declarations, out error))
        {
            return false;
        }

        if (!TryProjectNumberDeclarations(facts, exampleData, declarations, out error))
        {
            return false;
        }

        if (!TryProjectSignClassifications(facts, exampleData, declarations, out error))
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
        GenerationExampleData? exampleData,
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
            string.IsNullOrWhiteSpace(names[0]))
        {
            error = "An accumulation requires one accumulator name.";
            return false;
        }

        var accumulator = names[0];
        var itemKind = SemanticCollectionItemKind.Number;
        var item = string.Empty;
        SemanticCollection? contents = null;
        var initialValue = string.Empty;

        if (TryGetCollection(fact, AccumulationItemValueId, out var items) &&
            items.Length == 1 &&
            !string.IsNullOrWhiteSpace(items[0]))
        {
            item = items[0];
        }

        if (fact.SemanticCollections.TryGetValue(AccumulationCollectionValueId, out var captured) &&
            !captured.Items.IsDefaultOrEmpty)
        {
            contents = captured;
            itemKind = captured.Items[0].Kind;
        }

        if (TryGetCollection(fact, AccumulationInitialValueId, out var initialValues) &&
            initialValues.Length == 1 &&
            !string.IsNullOrWhiteSpace(initialValues[0]))
        {
            initialValue = initialValues[0];
        }

        // A request that names no data falls back to the declared example data of its language pack: the same
        // single policy every under-specified request uses, never a task-shaped value.
        if (contents is null || item.Length == 0 || initialValue.Length == 0)
        {
            if (exampleData is null)
            {
                error = "An accumulation with unnamed data requires declared example data.";
                return false;
            }

            contents ??= new SemanticCollection(
                [.. exampleData.Numbers.Select(value =>
                    new SemanticCollectionItem(SemanticCollectionItemKind.Number, value))]);
            itemKind = contents.Items[0].Kind;
            item = item.Length == 0 ? exampleData.NumberItemName : item;
            initialValue = initialValue.Length == 0 ? exampleData.AccumulatorInitialValue : initialValue;
        }

        if (contents is null || contents.Items.IsDefaultOrEmpty || item.Length == 0 || initialValue.Length == 0)
        {
            error = "An accumulation requires one accumulator name, initial value, item name, and collection.";
            return false;
        }
        declarations.Add(new AccumulationRequirement(
            accumulator,
            itemKind,
            item,
            new SemanticListLiteralExpression(
                [.. contents.Items.Select(ToLiteralExpression)]),
            ToLiteralExpression(new SemanticCollectionItem(itemKind, initialValue)),
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

    /// <summary>
    /// Projects "prints each number in [...] only if it is even" and its unnamed form onto one iteration whose
    /// body prints the item behind a parity guard. The guard is a remainder comparison against zero, which is
    /// what parity means; the collection, the item name, and the parity word stay request data, and a request
    /// that names no list takes the declared example data.
    /// </summary>
    private static bool TryCreateParityFilter(
        SequenceSemanticFact parity,
        ImmutableArray<SequenceSemanticFact> facts,
        GenerationExampleData? exampleData,
        out SemanticProgram? program,
        out string? error)
    {
        program = null;
        error = null;

        if (!parity.Values.TryGetValue(ParityValueId, out var parityWord) ||
            !TryReadParityOperator(parityWord, out var comparison))
        {
            error = "A parity filter requires the parity it selects for.";
            return false;
        }

        var itemName = string.Empty;
        SemanticCollection? contents = null;
        if (parity.Values.TryGetValue(LoopItemValueId, out var capturedItem) &&
            !string.IsNullOrWhiteSpace(capturedItem))
        {
            itemName = capturedItem.Trim();
        }

        if (parity.SemanticCollections.TryGetValue(LoopCollectionValueId, out var capturedContents) &&
            !capturedContents.Items.IsDefaultOrEmpty)
        {
            contents = capturedContents;
        }

        var loop = facts.FirstOrDefault(candidate => candidate.Id == CollectionLoopFactId);
        if (loop is not null)
        {
            if (itemName.Length == 0 &&
                loop.Values.TryGetValue(LoopItemValueId, out var item) &&
                !string.IsNullOrWhiteSpace(item))
            {
                itemName = item.Trim();
            }

            if (contents is null &&
                loop.SemanticCollections.TryGetValue(LoopCollectionValueId, out var fromLoop) &&
                !fromLoop.Items.IsDefaultOrEmpty)
            {
                contents = fromLoop;
            }
        }

        if (exampleData is not null)
        {
            contents ??= new SemanticCollection(
                [.. exampleData.Numbers.Select(value =>
                    new SemanticCollectionItem(SemanticCollectionItemKind.Number, value))]);
            itemName = itemName.Length == 0 ? exampleData.NumberItemName : itemName;
        }

        if (contents is null || contents.Items.IsDefaultOrEmpty || itemName.Length == 0)
        {
            error = "A parity filter requires an item name and a collection.";
            return false;
        }

        var name = new SemanticNameExpression(itemName);
        var predicate = new SemanticBinaryExpression(
            comparison,
            new SemanticBinaryExpression(
                SemanticExpressionOperator.Remainder,
                name,
                new SemanticNumberExpression(ParityDivisor)),
            new SemanticNumberExpression("0"));

        var filter = new SemanticProgram(
        [
            new IterationRequirement(
                itemName,
                new SemanticListLiteralExpression([.. contents.Items.Select(ToLiteralExpression)]),
                [
                    new ConditionalRequirement(
                        predicate,
                        [new OutputRequirement(name)],
                        ImmutableArray<ProgramRequirement>.Empty)
                ])
        ]);

        if (!SemanticProgramValidator.IsValid(filter))
        {
            error = "The projected parity filter requirements are not structurally valid.";
            return false;
        }

        program = filter;
        return true;
    }

    /// <summary>
    /// Projects "given a dictionary, prints each key with its value" onto a mapping the program fills in later,
    /// followed by the loop that reports each pair. The three names are request data; the note that marks the
    /// mapping as still empty comes from the declared policy.
    /// </summary>
    private static bool TryCreateDeclaredDictionaryIteration(
        SequenceSemanticFact iteration,
        GenerationExampleData? exampleData,
        out SemanticProgram? program,
        out string? error)
    {
        program = null;
        error = null;

        var keyName = ReadName(iteration, DictionaryKeyNameValueId);
        var valueName = ReadName(iteration, DictionaryValueNameValueId);
        var collectionName = ReadName(iteration, DictionaryIterationCollectionValueId);
        if (keyName is null || valueName is null || collectionName is null)
        {
            error = "A dictionary iteration requires the key, the value, and the collection names.";
            return false;
        }

        if (string.Equals(keyName, valueName, StringComparison.Ordinal))
        {
            error = "A dictionary iteration requires two different names.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(exampleData?.EmptyMappingNote))
        {
            error = "A dictionary iteration requires the declared note that marks the mapping as empty.";
            return false;
        }

        var iterationProgram = new SemanticProgram(
        [
            new AssignmentRequirement(
                collectionName,
                new SemanticPlaceholderMappingExpression(exampleData.EmptyMappingNote)),
            new DictionaryIterationRequirement(
                keyName,
                valueName,
                new SemanticNameExpression(collectionName),
                [
                    new OutputRequirement(new SemanticArgumentsExpression(
                    [
                        new SemanticNameExpression(keyName),
                        new SemanticNameExpression(valueName)
                    ]))
                ])
        ]);

        if (!SemanticProgramValidator.IsValid(iterationProgram))
        {
            error = "The projected dictionary iteration requirements are not structurally valid.";
            return false;
        }

        program = iterationProgram;
        return true;
    }

    /// <summary>
    /// Projects "returns a new list containing only the values that satisfy …" onto a function that returns the
    /// filtered collection, followed by the demonstration call the sample policy supplies. The function name,
    /// the parameter, the item, and the comparison are request data; only the numbers reported when the request
    /// states no collection come from the policy.
    /// </summary>
    private static bool TryCreateCollectionFilter(
        ImmutableArray<SequenceSemanticFact> facts,
        GenerationExampleData? exampleData,
        out SemanticProgram? program,
        out string? error)
    {
        program = null;
        error = null;

        var signature = facts.FirstOrDefault(candidate => candidate.Id == CollectionFilterSignatureFactId);
        var comparison = facts.FirstOrDefault(candidate => candidate.Id == CollectionFilterComparisonFactId);
        if (signature is null || comparison is null)
        {
            error = "A collection filter requires the names and the comparison its function uses.";
            return false;
        }

        var functionName = ReadName(signature, CollectionFilterNameValueId);
        var parameterName = ReadName(signature, CollectionFilterParameterValueId);
        var itemName = ReadName(signature, CollectionFilterItemValueId);
        if (functionName is null || parameterName is null || itemName is null)
        {
            error = "A collection filter requires the function, the parameter, and the item names.";
            return false;
        }

        if (!comparison.ComparisonOperators.TryGetValue(CollectionFilterOperatorValueId, out var comparisonOperator))
        {
            error = "A collection filter requires the comparison its predicate makes.";
            return false;
        }

        var isAverage = facts.Any(fact => fact.Id == CollectionFilterAverageFactId);
        SemanticExpression right;
        if (isAverage)
        {
            right = new SemanticBinaryExpression(
                SemanticExpressionOperator.Divide,
                new SemanticTotalExpression(new SemanticNameExpression(parameterName)),
                new SemanticLengthExpression(new SemanticNameExpression(parameterName)));
        }
        else
        {
            var threshold = facts.FirstOrDefault(candidate => candidate.Id == CollectionFilterThresholdFactId);
            if (threshold is null ||
                !threshold.Expressions.TryGetValue(CollectionFilterThresholdValueId, out var thresholdValue))
            {
                error = "A collection filter requires the value it compares against.";
                return false;
            }

            right = thresholdValue;
        }

        if (exampleData is null ||
            exampleData.SampleCall is null ||
            exampleData.Numbers.IsDefaultOrEmpty)
        {
            error = "A collection filter requires the declared sample data.";
            return false;
        }

        var sampleNumbers = exampleData.Numbers;

        var sample = ImmutableArray.CreateBuilder<SemanticExpression>(sampleNumbers.Length);
        foreach (var number in sampleNumbers)
        {
            var value = new SemanticNumberExpression(number.Trim());
            if (!SemanticExpressionAnalysis.IsValid(value))
            {
                error = "A collection filter requires numeric sample values.";
                return false;
            }

            sample.Add(value);
        }

        var filteredProgram = new SemanticProgram(
        [
            new FunctionRequirement(
                functionName,
                [new FunctionParameter(parameterName, null)],
                null,
                [
                    new ReturnRequirement(new SemanticFilterExpression(
                        itemName,
                        new SemanticNameExpression(parameterName),
                        new SemanticBinaryExpression(
                            comparisonOperator,
                            new SemanticNameExpression(itemName),
                            right)))
                ]),
            new OutputRequirement(new SemanticFunctionCallExpression(
                functionName,
                [new SemanticListLiteralExpression(sample.ToImmutable())]))
        ]);

        if (!SemanticProgramValidator.IsValid(filteredProgram))
        {
            error = "The projected collection filter requirements are not structurally valid.";
            return false;
        }

        program = filteredProgram;
        return true;
    }

    /// <summary>
    /// Projects "groups the records by one field and totals another" onto the declared sample data: the records
    /// the request names are the loop's item, the two field names are its keys, and the totals dictionary, the
    /// current key, and the reported pair come from the policy. Nothing here names a group-and-sum algorithm;
    /// the plan is a data assignment, an empty mapping, one loop that raises each element, and one loop that
    /// reports the pairs.
    /// </summary>
    private static bool TryCreateGroupedAggregation(
        SequenceSemanticFact aggregation,
        ImmutableArray<SequenceSemanticFact> facts,
        GenerationExampleData? exampleData,
        out SemanticProgram? program,
        out string? error)
    {
        program = null;
        error = null;

        if (exampleData?.SampleCall?.GroupedAggregation is not { } grouped)
        {
            error = "A grouped record total requires the declared sample policy.";
            return false;
        }

        if (!facts.Any(fact => fact.Id == GroupedAggregationIntentFactId))
        {
            error = "A grouped record total requires the request's intent.";
            return false;
        }

        if (!grouped.Groups.IsDefaultOrEmpty &&
            grouped.Groups.Length != grouped.Measures.Length)
        {
            error = "Every sample record of a grouped total needs one amount.";
            return false;
        }

        var itemName = ReadName(aggregation, GroupedAggregationRecordValueId);
        var groupName = ReadName(aggregation, GroupedAggregationGroupValueId);
        var measureName = ReadName(aggregation, GroupedAggregationMeasureValueId);
        if (itemName is null || groupName is null || measureName is null)
        {
            error = "A grouped record total requires the item, the group, and the measure names.";
            return false;
        }

        var recordItemName = itemName;
        var groupFieldName = groupName;
        var measureFieldName = measureName;
        var initialValue = exampleData.AccumulatorInitialValue;
        if (!SemanticExpressionAnalysis.IsValid(new SemanticNumberExpression(initialValue)))
        {
            error = "A grouped record total requires a numeric starting value.";
            return false;
        }

        var records = ImmutableArray.CreateBuilder<SemanticExpression>(grouped.Groups.Length);
        for (var index = 0; index < grouped.Groups.Length; index++)
        {
            records.Add(new SemanticRecordLiteralExpression(
            [
                new SemanticRecordEntry(groupName, new SemanticTextExpression(grouped.Groups[index])),
                new SemanticRecordEntry(measureName, new SemanticNumberExpression(grouped.Measures[index].Trim()))
            ]));
        }

        SemanticExpression GroupElement() =>
            new SemanticSubscriptExpression(
                new SemanticNameExpression(recordItemName),
                new SemanticTextExpression(groupFieldName));

        SemanticExpression MeasureElement() =>
            new SemanticSubscriptExpression(
                new SemanticNameExpression(recordItemName),
                new SemanticTextExpression(measureFieldName));

        var groupedProgram = new SemanticProgram(
        [
            new AssignmentRequirement(
                grouped.RecordsName,
                new SemanticListLiteralExpression(records.ToImmutable())),
            new AssignmentRequirement(
                grouped.TotalsName,
                new SemanticMapLiteralExpression(ImmutableArray<SemanticRecordEntry>.Empty)),
            new IterationRequirement(
                recordItemName,
                new SemanticNameExpression(grouped.RecordsName),
                [
                    new AssignmentRequirement(grouped.KeyName, GroupElement()),
                    new ElementAssignmentRequirement(
                        new SemanticSubscriptExpression(
                            new SemanticNameExpression(grouped.TotalsName),
                            new SemanticNameExpression(grouped.KeyName)),
                        new SemanticBinaryExpression(
                            SemanticExpressionOperator.Add,
                            new SemanticMethodCallExpression(
                                new SemanticNameExpression(grouped.TotalsName),
                                "get",
                                [
                                    new SemanticNameExpression(grouped.KeyName),
                                    new SemanticNumberExpression(initialValue)
                                ]),
                            MeasureElement()))
                ]),
            new DictionaryIterationRequirement(
                grouped.KeyName,
                grouped.TotalName,
                new SemanticNameExpression(grouped.TotalsName),
                [
                    new OutputRequirement(new SemanticArgumentsExpression(
                    [
                        new SemanticNameExpression(grouped.KeyName),
                        new SemanticNameExpression(grouped.TotalName)
                    ]))
                ])
        ]);

        if (!SemanticProgramValidator.IsValid(groupedProgram))
        {
            error = "The projected grouped record total requirements are not structurally valid.";
            return false;
        }

        program = groupedProgram;
        return true;
    }

    /// <summary>
    /// Whether a fact describes a several-key sort rather than another family that mentions records.
    /// </summary>
    private static bool IsCompleteMultiFieldSort(SequenceSemanticFact fact) =>
        fact.Values.TryGetValue(RecordSortKindValueId, out var kind) &&
        string.Equals(kind?.Trim(), MultiFieldSortKindValue, StringComparison.Ordinal);

    /// <summary>
    /// Projects "returns the records sorted by those fields" onto a function that orders its own parameter by one
    /// value per requested key, followed by the demonstration call the policy supplies. The names and the ordering
    /// are request data; the generator's item name and the sample records are policy.
    /// </summary>
    private static bool TryCreateMultiFieldSort(
        SequenceSemanticFact sortFact,
        GenerationExampleData? exampleData,
        out SemanticProgram? program,
        out string? error)
    {
        program = null;
        error = null;

        if (exampleData?.SampleCall?.Sort?.MultiField is not { } sort)
        {
            error = "A several-key record sort requires the declared sample policy.";
            return false;
        }

        var functionName = ReadName(sortFact, RecordSortFunctionValueId) ?? sort.FunctionName;
        var recordsName = ReadName(sortFact, RecordSortRecordsValueId) ?? sort.RecordsName;
        var keysName = ReadName(sortFact, MultiFieldSortKeysValueId) ?? sort.KeysName;
        if (functionName is null || recordsName is null || keysName is null)
        {
            error = "A several-key record sort requires the names its function and parameters take.";
            return false;
        }

        var caseInsensitive = sortFact.Values.TryGetValue(RecordSortSensitivityValueId, out var sensitivity) &&
            string.Equals(sensitivity?.Trim(), RecordSortCaseInsensitiveValue, StringComparison.Ordinal);

        SemanticExpression key = new SemanticSubscriptExpression(
            new SemanticNameExpression(sort.ItemName),
            new SemanticNameExpression(sort.KeyItemName));
        if (caseInsensitive)
        {
            key = new SemanticCaseFoldExpression(key);
        }

        var generator = new SemanticGeneratorExpression(
            sort.KeyItemName,
            new SemanticNameExpression(keysName),
            key);

        var records = ImmutableArray.CreateBuilder<SemanticExpression>(sort.Records.Length);
        foreach (var record in sort.Records)
        {
            if (record.Length != sort.Fields.Length)
            {
                error = "A sample record must hold one value per declared field.";
                return false;
            }

            var entries = ImmutableArray.CreateBuilder<SemanticRecordEntry>(record.Length);
            for (var index = 0; index < record.Length; index++)
            {
                var field = sort.Fields[index];
                SemanticExpression value = field.IsText
                    ? new SemanticTextExpression(record[index])
                    : new SemanticNumberExpression(record[index].Trim());
                entries.Add(new SemanticRecordEntry(field.Name, value));
            }

            records.Add(string.Equals(sort.RecordsLiteral, RecordSortLiteralMap, StringComparison.Ordinal)
                ? new SemanticMapLiteralExpression(entries.ToImmutable())
                : new SemanticRecordLiteralExpression(entries.ToImmutable()));
        }

        var keys = ImmutableArray.CreateBuilder<SemanticExpression>(sort.SortKeys.Length);
        foreach (var sortKey in sort.SortKeys)
        {
            keys.Add(new SemanticTextExpression(sortKey));
        }

        var multiFieldProgram = new SemanticProgram(
        [
            new FunctionRequirement(
                functionName,
                [
                    new FunctionParameter(recordsName, null),
                    new FunctionParameter(keysName, null)
                ],
                null,
                [
                    new ReturnRequirement(new SemanticSortExpression(
                        new SemanticNameExpression(recordsName),
                        sort.ItemName,
                        new SemanticTupleExpression(generator),
                        Descending: false))
                ]),
            new OutputRequirement(new SemanticFunctionCallExpression(
                functionName,
                [
                    new SemanticListLiteralExpression(records.ToImmutable()),
                    new SemanticListLiteralExpression(keys.ToImmutable())
                ]))
        ]);

        if (!SemanticProgramValidator.IsValid(multiFieldProgram))
        {
            error = "The projected several-key sort requirements are not structurally valid.";
            return false;
        }

        program = multiFieldProgram;
        return true;
    }

    /// <summary>
    /// Whether a fact describes a record sort rather than another family that mentions records.
    /// </summary>
    private static bool IsCompleteRecordSort(SequenceSemanticFact fact) =>
        fact.Values.TryGetValue(RecordSortKindValueId, out var kind) &&
        string.Equals(kind?.Trim(), RecordSortKindValue, StringComparison.Ordinal);

    /// <summary>
    /// Projects "returns the records sorted by that field" onto a function that orders its own parameter by one
    /// field, followed by the demonstration call the policy supplies because the request states no data. The
    /// function, the two parameter names, and the ordering are request data.
    /// </summary>
    private static bool TryCreateRecordSort(
        SequenceSemanticFact sortFact,
        GenerationExampleData? exampleData,
        out SemanticProgram? program,
        out string? error)
    {
        program = null;
        error = null;

        if (exampleData?.SampleCall?.Sort is not { } sort)
        {
            error = "A record sort requires the declared sample sort policy.";
            return false;
        }

        var functionName = ReadName(sortFact, RecordSortFunctionValueId) ?? sort.FunctionName;
        var recordsName = ReadName(sortFact, RecordSortRecordsValueId) ?? sort.RecordsName;
        var fieldName = ReadName(sortFact, RecordSortFieldValueId) ?? sort.FieldName;
        if (functionName is null || recordsName is null || fieldName is null)
        {
            error = "A record sort requires the names its function and parameters take.";
            return false;
        }

        var descending = sortFact.Values.TryGetValue(RecordSortOrderValueId, out var order) &&
            string.Equals(order?.Trim(), RecordSortDescendingValue, StringComparison.Ordinal);
        var caseInsensitive = sortFact.Values.TryGetValue(RecordSortSensitivityValueId, out var sensitivity) &&
            string.Equals(sensitivity?.Trim(), RecordSortCaseInsensitiveValue, StringComparison.Ordinal);

        var sample = sort.Samples.FirstOrDefault(candidate => candidate.CaseInsensitive == caseInsensitive);
        if (sample is null)
        {
            error = "The declared sample sort policy has no demonstration for this ordering.";
            return false;
        }

        SemanticExpression key = new SemanticSubscriptExpression(
            new SemanticNameExpression(sort.ItemName),
            new SemanticNameExpression(fieldName));
        if (caseInsensitive)
        {
            key = new SemanticCaseFoldExpression(key);
        }

        var records = ImmutableArray.CreateBuilder<SemanticExpression>(sample.Records.Length);
        foreach (var record in sample.Records)
        {
            if (record.Length != sort.Fields.Length)
            {
                error = "A sample record must hold one value per declared field.";
                return false;
            }

            var entries = ImmutableArray.CreateBuilder<SemanticRecordEntry>(record.Length);
            for (var index = 0; index < record.Length; index++)
            {
                var field = sort.Fields[index];
                SemanticExpression value = field.IsText
                    ? new SemanticTextExpression(record[index])
                    : new SemanticNumberExpression(record[index].Trim());
                entries.Add(new SemanticRecordEntry(field.Name, value));
            }

            records.Add(string.Equals(sample.RecordsLiteral, RecordSortLiteralMap, StringComparison.Ordinal)
                ? new SemanticMapLiteralExpression(entries.ToImmutable())
                : new SemanticRecordLiteralExpression(entries.ToImmutable()));
        }

        var sortProgram = new SemanticProgram(
        [
            new FunctionRequirement(
                functionName,
                [
                    new FunctionParameter(recordsName, null),
                    new FunctionParameter(fieldName, null)
                ],
                null,
                [
                    new ReturnRequirement(new SemanticSortExpression(
                        new SemanticNameExpression(recordsName),
                        sort.ItemName,
                        key,
                        descending))
                ]),
            new OutputRequirement(new SemanticFunctionCallExpression(
                functionName,
                [
                    new SemanticListLiteralExpression(records.ToImmutable()),
                    new SemanticTextExpression(sample.Field)
                ]))
        ]);

        if (!SemanticProgramValidator.IsValid(sortProgram))
        {
            error = "The projected record sort requirements are not structurally valid.";
            return false;
        }

        program = sortProgram;
        return true;
    }

    /// <summary>Reads one captured name from a fact, trimmed of the punctuation a sentence leaves behind.</summary>
    private static string? ReadName(SequenceSemanticFact fact, string valueId)
    {
        if (!fact.Values.TryGetValue(valueId, out var value) || string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var name = value.Trim().Trim(LiteralEntryTrimCharacters);
        return name.Length == 0 ? null : name;
    }

    /// <summary>
    /// Whether a mapping fact carries every value its projection needs. A request that names only some of them
    /// is not a mapping request, so the other projections still get their turn instead of a failed plan.
    /// </summary>
    private static bool IsCompleteDictionaryMapping(SequenceSemanticFact mapping) =>
        mapping.Values.TryGetValue(MappingKindValueId, out var kind) && !string.IsNullOrWhiteSpace(kind) &&
        mapping.Values.TryGetValue(MappingItemValueId, out var item) && !string.IsNullOrWhiteSpace(item) &&
        mapping.Values.TryGetValue(MappingParameterValueId, out var parameter) &&
        !string.IsNullOrWhiteSpace(parameter);

    /// <summary>
    /// Projects "builds a dictionary that maps each number to the square of it" onto a function that returns a
    /// dictionary comprehension, followed by the demonstration call the policy supplies because the request
    /// states none. The item, the parameter, and any declared result name are request data.
    /// </summary>
    private static bool TryCreateDictionaryMapping(
        SequenceSemanticFact mapping,
        GenerationExampleData? exampleData,
        out SemanticProgram? program,
        out string? error)
    {
        program = null;
        error = null;

        if (exampleData is null || exampleData.SampleCall is null)
        {
            error = "A dictionary mapping requires the declared sample-call policy.";
            return false;
        }

        if (!mapping.Values.TryGetValue(MappingKindValueId, out var kind) ||
            !mapping.Values.TryGetValue(MappingItemValueId, out var declaredItem) ||
            !mapping.Values.TryGetValue(MappingParameterValueId, out var declaredParameter) ||
            string.IsNullOrWhiteSpace(declaredItem) ||
            string.IsNullOrWhiteSpace(declaredParameter))
        {
            error = "A dictionary mapping requires the item and the parameter it maps.";
            return false;
        }

        var item = declaredItem.Trim().Trim(LiteralEntryTrimCharacters);
        var parameter = declaredParameter.Trim().Trim(LiteralEntryTrimCharacters);
        if (item.Length == 0 || parameter.Length == 0)
        {
            error = "A dictionary mapping requires the item and the parameter it maps.";
            return false;
        }

        var result = mapping.Values.TryGetValue(MappingResultValueId, out var declaredResult) &&
            !string.IsNullOrWhiteSpace(declaredResult)
            ? declaredResult.Trim().Trim(LiteralEntryTrimCharacters)
            : exampleData.SampleCall.ResultName;
        if (result.Length == 0)
        {
            error = "A dictionary mapping requires the name of the dictionary it builds.";
            return false;
        }

        var itemName = new SemanticNameExpression(item);
        SemanticExpression value;
        ImmutableArray<string> samples;
        switch (kind?.Trim())
        {
            case MappingLengthKind:
                value = new SemanticLengthExpression(itemName);
                samples = exampleData.SampleCall.Texts;
                break;
            case MappingSquareKind:
                value = new SemanticBinaryExpression(
                    SemanticExpressionOperator.Multiply,
                    itemName,
                    new SemanticNameExpression(item));
                samples = exampleData.SampleCall.Numbers;
                break;
            case MappingOffsetKind:
                if (!mapping.Values.TryGetValue(MappingOffsetValueId, out var declaredOffset) ||
                    !decimal.TryParse(declaredOffset?.Trim(), System.Globalization.NumberStyles.Number,
                        System.Globalization.CultureInfo.InvariantCulture, out _))
                {
                    error = "A dictionary mapping by offset requires the offset it adds.";
                    return false;
                }

                value = new SemanticBinaryExpression(
                    SemanticExpressionOperator.Add,
                    itemName,
                    new SemanticNumberExpression(declaredOffset!.Trim()));
                samples = exampleData.SampleCall.Numbers;
                break;
            default:
                error = "A dictionary mapping requires the value it maps each item to.";
                return false;
        }

        var sampleItems = ImmutableArray.CreateBuilder<SemanticExpression>(samples.Length);
        foreach (var sample in samples)
        {
            sampleItems.Add(kind?.Trim() == MappingLengthKind
                ? new SemanticTextExpression(sample)
                : new SemanticNumberExpression(sample));
        }

        var mappingProgram = new SemanticProgram(
        [
            new FunctionRequirement(
                exampleData.SampleCall.FunctionName,
                [new FunctionParameter(parameter, null)],
                null,
                [
                    new DictionaryMapRequirement(
                        result,
                        item,
                        new SemanticNameExpression(parameter),
                        value),
                    new ReturnRequirement(new SemanticNameExpression(result))
                ]),
            new OutputRequirement(new SemanticFunctionCallExpression(
                exampleData.SampleCall.FunctionName,
                [new SemanticListLiteralExpression(sampleItems.ToImmutable())]))
        ]);

        if (!SemanticProgramValidator.IsValid(mappingProgram))
        {
            error = "The projected dictionary mapping requirements are not structurally valid.";
            return false;
        }

        program = mappingProgram;
        return true;
    }

    /// <summary>
    /// Projects "stores {'apple': 1.5} in a dictionary named prices, then prints each product and its price"
    /// onto an assignment of a mapping literal and a key/value loop printing both names. The declared name,
    /// the keys, the values, and the two loop names are all request data.
    /// </summary>
    private static bool TryCreateDictionaryIteration(
        SequenceSemanticFact literal,
        out SemanticProgram? program,
        out string? error)
    {
        program = null;
        error = null;

        if (!literal.Values.TryGetValue(DictionaryNameValueId, out var declaredName) ||
            string.IsNullOrWhiteSpace(declaredName))
        {
            error = "A declared dictionary requires a name.";
            return false;
        }

        var name = declaredName.Trim().Trim(LiteralEntryTrimCharacters);
        if (name.Length == 0)
        {
            error = "A declared dictionary requires a name.";
            return false;
        }

        if (!TryGetCollection(literal, DictionaryKeysValueId, out var keys) || keys.IsDefaultOrEmpty)
        {
            error = "A declared dictionary requires the entries the request states.";
            return false;
        }

        if (!TryGetCollection(literal, DictionaryValuesValueId, out var values) ||
            values.Length != keys.Length)
        {
            error = "A declared dictionary needs one value per key.";
            return false;
        }

        if (!literal.Values.TryGetValue(DictionaryKeyNameValueId, out var declaredKey) ||
            !literal.Values.TryGetValue(DictionaryValueNameValueId, out var declaredValue) ||
            string.IsNullOrWhiteSpace(declaredKey) ||
            string.IsNullOrWhiteSpace(declaredValue))
        {
            error = "A declared dictionary iteration requires the key and value names it reports.";
            return false;
        }

        var keyName = declaredKey.Trim().Trim(LiteralEntryTrimCharacters);
        var valueName = declaredValue.Trim().Trim(LiteralEntryTrimCharacters);
        if (keyName.Length == 0 || valueName.Length == 0 ||
            string.Equals(keyName, valueName, StringComparison.Ordinal))
        {
            error = "A declared dictionary iteration requires two different names.";
            return false;
        }

        var entries = ImmutableArray.CreateBuilder<SemanticRecordEntry>(keys.Length);
        for (var index = 0; index < keys.Length; index++)
        {
            var key = keys[index].Trim().Trim(LiteralEntryTrimCharacters);
            if (key.Length == 0)
            {
                error = "A declared dictionary requires a key for every entry.";
                return false;
            }

            entries.Add(new SemanticRecordEntry(key, DataExpression(values[index])));
        }

        var dictionary = new SemanticProgram(
        [
            new AssignmentRequirement(name, new SemanticMapLiteralExpression(entries.ToImmutable())),
            new DictionaryIterationRequirement(
                keyName,
                valueName,
                new SemanticNameExpression(name),
                [
                    new OutputRequirement(
                        new SemanticArgumentsExpression(
                        [
                            new SemanticNameExpression(keyName),
                            new SemanticNameExpression(valueName)
                        ]))
                ])
        ]);

        if (!SemanticProgramValidator.IsValid(dictionary))
        {
            error = "The projected dictionary iteration requirements are not structurally valid.";
            return false;
        }

        program = dictionary;
        return true;
    }

    /// <summary>Reads one captured cell as the value it spells: a number when it parses as one, else text.</summary>
    private static SemanticExpression DataExpression(string cell)
    {
        var value = cell.Trim().Trim(LiteralEntryTrimCharacters);
        if (value.Length > 1 && value.StartsWith('-') &&
            decimal.TryParse(value[1..], System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out _))
        {
            return new SemanticUnaryExpression(
                SemanticExpressionOperator.Negative,
                new SemanticNumberExpression(value[1..]));
        }

        return decimal.TryParse(value, System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out _)
            ? new SemanticNumberExpression(value)
            : new SemanticTextExpression(value);
    }

    private static bool TryReadParityOperator(string parityWord, out SemanticExpressionOperator comparison)
    {
        comparison = SemanticExpressionOperator.Equal;
        switch (parityWord.Trim().ToLowerInvariant())
        {
            case "even":
                return true;
            case "odd":
                comparison = SemanticExpressionOperator.NotEqual;
                return true;
            default:
                return false;
        }
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

    private static SemanticExpression ToLiteralExpression(SemanticCollectionItem item)
    {
        if (item.Kind == SemanticCollectionItemKind.Text)
        {
            return new SemanticTextExpression(item.Value);
        }

        var value = item.Value.Trim();
        return value.StartsWith('-') && value.Length > 1
            ? new SemanticUnaryExpression(
                SemanticExpressionOperator.Negative,
                new SemanticNumberExpression(value[1..]))
            : new SemanticNumberExpression(value);
    }

    /// <summary>
    /// Projects a request that asks for numbered variables and then combines them: "stores two numbers in
    /// variables, adds them, and prints the result". The request supplies the count and the name of the sum;
    /// the values and the ordinal names come from the pack's declared example data, because the request names
    /// neither.
    /// </summary>
    private static bool TryProjectNumberDeclarations(
        ImmutableArray<SequenceSemanticFact> facts,
        GenerationExampleData? exampleData,
        ImmutableArray<ProgramRequirement>.Builder declarations,
        out string? error)
    {
        error = null;
        var fact = facts.FirstOrDefault(candidate => candidate.Id == NumberDeclarationFactId);
        if (fact is null)
        {
            return true;
        }

        if (!fact.Values.TryGetValue(NumberDeclarationCountValueId, out var countText) ||
            !TryParseQuantity(countText, out var count) ||
            count < 1)
        {
            error = "A number declaration requires a quantity.";
            return false;
        }

        if (exampleData is null ||
            exampleData.NumberVariableValues.Length < count ||
            exampleData.Ordinals.Length < count)
        {
            error = "A number declaration needs declared example values and ordinal names.";
            return false;
        }

        var names = ImmutableArray.CreateBuilder<string>(count);
        for (var index = 0; index < count; index++)
        {
            names.Add($"{exampleData.Ordinals[index]}_{exampleData.NumberItemName}");
            declarations.Add(new AssignmentRequirement(
                names[index],
                new SemanticNumberExpression(exampleData.NumberVariableValues[index])));
        }

        if (!fact.Values.TryGetValue(NumberDeclarationSumValueId, out var sumName) ||
            string.IsNullOrWhiteSpace(sumName))
        {
            return true;
        }

        SemanticExpression sum = new SemanticNameExpression(names[0]);
        for (var index = 1; index < names.Count; index++)
        {
            sum = new SemanticBinaryExpression(
                SemanticExpressionOperator.Add,
                sum,
                new SemanticNameExpression(names[index]));
        }

        declarations.Add(new AssignmentRequirement(sumName, sum));
        return true;
    }

    /// <summary>
    /// Projects a request that classifies a value by its sign: "reports whether an integer is positive,
    /// negative, or zero". The three labels come from the request and the value and the variable name come
    /// from the pack's declared example data, because the request names neither.
    /// </summary>
    private static bool TryProjectSignClassifications(
        ImmutableArray<SequenceSemanticFact> facts,
        GenerationExampleData? exampleData,
        ImmutableArray<ProgramRequirement>.Builder declarations,
        out string? error)
    {
        error = null;
        var fact = facts.FirstOrDefault(candidate => candidate.Id == SignClassificationFactId);
        if (fact is null)
        {
            return true;
        }

        if (!TryGetLabel(fact, SignPositiveLabelValueId, out var positive) ||
            !TryGetLabel(fact, SignNegativeLabelValueId, out var negative) ||
            !TryGetLabel(fact, SignZeroLabelValueId, out var zero))
        {
            error = "A sign classification requires one label per branch.";
            return false;
        }

        if (exampleData is null || string.IsNullOrWhiteSpace(exampleData.IntegerValue))
        {
            error = "A sign classification needs a declared example integer.";
            return false;
        }

        var value = exampleData.IntegerValue.Trim();
        SemanticExpression literal = value.StartsWith('-') && value.Length > 1
            ? new SemanticUnaryExpression(
                SemanticExpressionOperator.Negative,
                new SemanticNumberExpression(value[1..]))
            : new SemanticNumberExpression(value);

        declarations.Add(new SignClassificationRequirement(
            exampleData.NumberItemName,
            literal,
            positive,
            negative,
            zero));
        return true;
    }

    private static bool TryGetLabel(SequenceSemanticFact fact, string valueId, out string label)
    {
        label = string.Empty;
        if (!fact.Values.TryGetValue(valueId, out var text) || string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        // The request separates its labels with commas and the word "or", so a captured label is the
        // word between those separators.
        label = text.Trim().Trim(LabelTrimCharacters);
        return label.Length > 0;
    }

    private static bool TryParseQuantity(string? text, out int quantity)
    {
        quantity = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (int.TryParse(text, out var numeric) && numeric > 0)
        {
            quantity = numeric;
            return true;
        }

        quantity = text.Trim().ToLowerInvariant() switch
        {
            "one" => 1,
            "two" => 2,
            "three" => 3,
            "four" => 4,
            "five" => 5,
            "six" => 6,
            "seven" => 7,
            "eight" => 8,
            "nine" => 9,
            "ten" => 10,
            _ => 0
        };
        return quantity > 0;
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
