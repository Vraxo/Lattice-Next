using System.Collections.Immutable;

namespace Lattice.Core.Generation;

/// <summary>
/// Structural validation for the language-neutral requirement model. It checks that a program is
/// well formed — identifiers, expressions, branch bodies, nesting, function declarations, calls,
/// returns, and collection bindings — without picking a target language; the selected language pack
/// still decides whether a name is writable.
/// </summary>
public static class SemanticProgramValidator
{
    public const int MaximumRequirements = 64;
    public const int MaximumDepth = 3;
    public const int MaximumNameLength = 64;
    public const int MaximumParameters = 8;

    /// <summary>The parameters and result kind of one declared function.</summary>
    private sealed record FunctionSignature(
        ImmutableArray<SemanticValueKind> ParameterKinds,
        SemanticValueKind ReturnKind);

    /// <summary>
    /// What the surrounding requirements make available to the requirement being validated: the
    /// program's function declarations, the enclosing function's declared names, and the enclosing
    /// function's result kind when the requirement is inside a function body.
    /// </summary>
    private sealed record RequirementContext(
        ImmutableDictionary<string, FunctionSignature> Functions,
        ImmutableDictionary<string, SemanticValueKind> FunctionResults,
        ImmutableDictionary<string, SemanticValueKind> Names,
        SemanticValueKind? ReturnKind)
    {
        public static RequirementContext Empty { get; } = new(
            ImmutableDictionary.Create<string, FunctionSignature>(StringComparer.Ordinal),
            ImmutableDictionary.Create<string, SemanticValueKind>(StringComparer.Ordinal),
            ImmutableDictionary.Create<string, SemanticValueKind>(StringComparer.Ordinal),
            null);
    }

    /// <summary>Returns true when the program is structurally valid.</summary>
    public static bool IsValid(SemanticProgram? program)
    {
        if (program is null ||
            program.Requirements.IsDefaultOrEmpty ||
            program.Requirements.Length > MaximumRequirements ||
            program.Collections.IsDefault)
        {
            return false;
        }

        if (program.Collections.Length > MaximumRequirements ||
            !program.Collections.All(IsValidBinding))
        {
            return false;
        }

        if (!TryCollectFunctions(program.Requirements, out var context))
        {
            return false;
        }

        return AreValid(program.Requirements, depth: 0, context);
    }

    private static bool AreValid(
        ImmutableArray<ProgramRequirement> requirements,
        int depth,
        RequirementContext context)
    {
        if (requirements.IsDefault ||
            requirements.Length > MaximumRequirements ||
            depth > MaximumDepth)
        {
            return false;
        }

        return requirements.All(requirement => IsValid(requirement, depth, context));
    }

    private static bool IsValid(ProgramRequirement requirement, int depth, RequirementContext context) =>
        requirement switch
        {
            AssignmentRequirement assignment =>
                IsValidName(assignment.VariableName) &&
                SemanticExpressionAnalysis.IsValid(assignment.Value),
            OutputRequirement output => SemanticExpressionAnalysis.IsValid(output.Value),
            ConditionalRequirement conditional =>
                SemanticExpressionAnalysis.IsValid(conditional.Condition) &&
                (!conditional.ThenRequirements.IsDefaultOrEmpty ||
                 !conditional.ElseRequirements.IsDefaultOrEmpty) &&
                AreValid(conditional.ThenRequirements, depth + 1, context) &&
                AreValid(conditional.ElseRequirements, depth + 1, context),
            IterationRequirement iteration =>
                IsValidName(iteration.ItemName) &&
                SemanticExpressionAnalysis.IsValid(iteration.Source) &&
                !iteration.Body.IsDefaultOrEmpty &&
                AreValid(iteration.Body, depth + 1, context),
            FilterRequirement filter =>
                IsValidName(filter.ResultName) &&
                IsValidName(filter.ItemName) &&
                !string.Equals(filter.ResultName, filter.ItemName, StringComparison.Ordinal) &&
                SemanticExpressionAnalysis.IsValid(filter.Source) &&
                ReferencesName(filter.Predicate, filter.ItemName),
            MapRequirement map =>
                IsValidName(map.ResultName) &&
                IsValidName(map.ItemName) &&
                !string.Equals(map.ResultName, map.ItemName, StringComparison.Ordinal) &&
                SemanticExpressionAnalysis.IsValid(map.Source) &&
                ReferencesName(map.Value, map.ItemName),
            AccumulationRequirement accumulation =>
                IsValidName(accumulation.AccumulatorName) &&
                IsValidName(accumulation.ItemName) &&
                !string.Equals(accumulation.AccumulatorName, accumulation.ItemName, StringComparison.Ordinal) &&
                accumulation.AccumulatorKind is SemanticCollectionItemKind.Number or SemanticCollectionItemKind.Text &&
                SemanticExpressionAnalysis.IsValid(accumulation.Source) &&
                SemanticExpressionAnalysis.IsValid(accumulation.InitialValue) &&
                ReferencesName(accumulation.Update, accumulation.AccumulatorName) &&
                ReferencesName(accumulation.Update, accumulation.ItemName),
            SortRequirement sort =>
                IsValidName(sort.ResultName) &&
                IsValidName(sort.ItemName) &&
                !string.Equals(sort.ResultName, sort.ItemName, StringComparison.Ordinal) &&
                SemanticExpressionAnalysis.IsValid(sort.Source) &&
                ReferencesName(sort.Key, sort.ItemName),
            DictionaryIterationRequirement iteration =>
                IsValidName(iteration.KeyName) &&
                IsValidName(iteration.ValueName) &&
                !string.Equals(iteration.KeyName, iteration.ValueName, StringComparison.Ordinal) &&
                SemanticExpressionAnalysis.IsValid(iteration.Source) &&
                !iteration.Body.IsDefaultOrEmpty &&
                AreValid(iteration.Body, depth + 1, context),
            FunctionRequirement function => IsValidFunction(function, depth, context),
            ReturnRequirement ret => IsValidReturn(ret, context),
            CallRequirement call => IsValidCall(call, context),
            ClassRequirement declaredClass => IsValidClass(declaredClass, depth, context),
            RaiseRequirement raise => IsValidRaise(raise, context),
            TryRequirement attempt => IsValidTry(attempt, depth, context),
            _ => false
        };

    private static bool IsValidFunction(
        FunctionRequirement function,
        int depth,
        RequirementContext context)
    {
        if (!IsValidName(function.Name) ||
            function.Parameters.IsDefaultOrEmpty ||
            function.Parameters.Length > MaximumParameters ||
            !IsValidValueKind(function.ReturnKind) ||
            function.Body.IsDefaultOrEmpty)
        {
            return false;
        }

        if (!function.Parameters.All(parameter =>
                parameter is not null && IsValidName(parameter.Name) && IsValidValueKind(parameter.Kind)))
        {
            return false;
        }

        if (function.Parameters
            .Select(parameter => parameter.Name)
            .Distinct(StringComparer.Ordinal)
            .Count() != function.Parameters.Length)
        {
            return false;
        }

        var bodyContext = context with
        {
            Names = function.Parameters
                .ToImmutableDictionary(parameter => parameter.Name, parameter => parameter.Kind, StringComparer.Ordinal),
            ReturnKind = function.ReturnKind
        };

        return AreValid(function.Body, depth + 1, bodyContext);
    }

    private static bool IsValidReturn(ReturnRequirement requirement, RequirementContext context) =>
        context.ReturnKind is { } returnKind &&
        SemanticExpressionAnalysis.IsValid(requirement.Value) &&
        SemanticValueKindAnalysis.IsCompatible(
            SemanticValueKindAnalysis.Infer(requirement.Value, context.Names, context.FunctionResults),
            returnKind);

    private static bool IsValidCall(CallRequirement call, RequirementContext context)
    {
        if (!IsValidName(call.FunctionName) ||
            call.Arguments.IsDefault ||
            call.Arguments.Length > MaximumParameters ||
            !call.Arguments.All(argument => SemanticExpressionAnalysis.IsValid(argument)))
        {
            return false;
        }

        if (!context.Functions.TryGetValue(call.FunctionName, out var signature))
        {
            return true;
        }

        if (signature.ParameterKinds.Length != call.Arguments.Length)
        {
            return false;
        }

        for (var index = 0; index < call.Arguments.Length; index++)
        {
            var inferred = SemanticValueKindAnalysis.Infer(
                call.Arguments[index],
                context.Names,
                context.FunctionResults);
            if (!SemanticValueKindAnalysis.IsCompatible(inferred, signature.ParameterKinds[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidClass(
        ClassRequirement declaredClass,
        int depth,
        RequirementContext context)
    {
        if (!IsValidName(declaredClass.Name) ||
            declaredClass.Fields.IsDefault ||
            declaredClass.Fields.Length > MaximumParameters ||
            declaredClass.Methods.IsDefault ||
            declaredClass.Methods.Length > MaximumParameters ||
            (declaredClass.Fields.IsDefaultOrEmpty && declaredClass.Methods.IsDefaultOrEmpty))
        {
            return false;
        }

        if (!declaredClass.Fields.All(field =>
                field is not null &&
                IsValidName(field.Name) &&
                SemanticExpressionAnalysis.IsValid(field.InitialValue)) ||
            declaredClass.Fields
                .Select(field => field.Name)
                .Distinct(StringComparer.Ordinal)
                .Count() != declaredClass.Fields.Length)
        {
            return false;
        }

        if (!declaredClass.Methods.All(method => IsValidMethod(method, depth, context)))
        {
            return false;
        }

        return declaredClass.Methods
            .Select(method => method.Name)
            .Distinct(StringComparer.Ordinal)
            .Count() == declaredClass.Methods.Length;
    }

    private static bool IsValidMethod(
        MethodRequirement method,
        int depth,
        RequirementContext context)
    {
        if (method is null ||
            !IsValidName(method.Name) ||
            method.Parameters.IsDefault ||
            method.Parameters.Length > MaximumParameters ||
            (method.ReturnKind is { } returnKind && !IsValidValueKind(returnKind)) ||
            method.Body.IsDefaultOrEmpty)
        {
            return false;
        }

        if (!method.Parameters.All(parameter =>
                parameter is not null && IsValidName(parameter.Name) && IsValidValueKind(parameter.Kind)))
        {
            return false;
        }

        if (method.Parameters
            .Select(parameter => parameter.Name)
            .Distinct(StringComparer.Ordinal)
            .Count() != method.Parameters.Length)
        {
            return false;
        }

        var bodyContext = context with
        {
            Names = method.Parameters
                .ToImmutableDictionary(parameter => parameter.Name, parameter => parameter.Kind, StringComparer.Ordinal),
            ReturnKind = method.ReturnKind
        };

        return AreValid(method.Body, depth + 1, bodyContext);
    }

    private static bool IsValidRaise(RaiseRequirement requirement, RequirementContext context)
    {
        if (!IsValidName(requirement.ErrorType))
        {
            return false;
        }

        if (requirement.Condition is null)
        {
            return true;
        }

        return SemanticExpressionAnalysis.IsValid(requirement.Condition) &&
            SemanticValueKindAnalysis.IsCompatible(
                SemanticValueKindAnalysis.Infer(
                    requirement.Condition,
                    context.Names,
                    context.FunctionResults),
                SemanticValueKind.Boolean);
    }

    private static bool IsValidTry(
        TryRequirement attempt,
        int depth,
        RequirementContext context)
    {
        if (attempt.Body.IsDefaultOrEmpty ||
            attempt.Handlers.IsDefaultOrEmpty ||
            attempt.Handlers.Length > MaximumParameters ||
            (attempt.ResultName is not null && !IsValidName(attempt.ResultName)))
        {
            return false;
        }

        if (!attempt.Handlers.All(handler =>
                handler is not null &&
                IsValidName(handler.ErrorType) &&
                !handler.Body.IsDefaultOrEmpty &&
                AreValid(handler.Body, depth + 1, context)))
        {
            return false;
        }

        if (attempt.Handlers
            .Select(handler => handler.ErrorType)
            .Distinct(StringComparer.Ordinal)
            .Count() != attempt.Handlers.Length)
        {
            return false;
        }

        return AreValid(attempt.Body, depth + 1, context);
    }

    /// <summary>
    /// Collects the program's top-level function declarations so a call can be checked against a
    /// declaration that appears later, and rejects two declarations that share one name.
    /// </summary>
    private static bool TryCollectFunctions(
        ImmutableArray<ProgramRequirement> requirements,
        out RequirementContext context)
    {
        context = RequirementContext.Empty;
        var functions = ImmutableDictionary.CreateBuilder<string, FunctionSignature>(StringComparer.Ordinal);
        var results = ImmutableDictionary.CreateBuilder<string, SemanticValueKind>(StringComparer.Ordinal);

        foreach (var function in requirements.OfType<FunctionRequirement>())
        {
            if (function is null ||
                !IsValidName(function.Name) ||
                function.Parameters.IsDefaultOrEmpty ||
                function.Parameters.Length > MaximumParameters ||
                !function.Parameters.All(parameter => parameter is not null && IsValidValueKind(parameter.Kind)) ||
                !IsValidValueKind(function.ReturnKind))
            {
                return false;
            }

            var parameterKinds = function.Parameters
                .Select(parameter => parameter.Kind)
                .ToImmutableArray();
            if (!functions.TryAdd(function.Name, new FunctionSignature(parameterKinds, function.ReturnKind)))
            {
                return false;
            }

            results[function.Name] = function.ReturnKind;
        }

        context = context with
        {
            Functions = functions.ToImmutable(),
            FunctionResults = results.ToImmutable()
        };
        return true;
    }

    private static bool ReferencesName(SemanticExpression expression, string name) =>
        SemanticExpressionAnalysis.IsValid(expression) &&
        SemanticExpressionAnalysis.GetReferencedNames(expression).Contains(name, StringComparer.Ordinal);

    private static bool IsValidBinding(CollectionBinding binding) =>
        binding is not null &&
        IsValidName(binding.Name) &&
        binding.ElementKind is SemanticCollectionItemKind.Number or SemanticCollectionItemKind.Text;

    private static bool IsValidValueKind(SemanticValueKind kind) => Enum.IsDefined(kind);

    private static bool IsValidName(string? name)
    {
        if (string.IsNullOrEmpty(name) ||
            name.Length > MaximumNameLength ||
            char.IsDigit(name[0]) ||
            !(char.IsAsciiLetter(name[0]) || name[0] == '_'))
        {
            return false;
        }

        return name.All(character =>
            char.IsAsciiLetterOrDigit(character) || character == '_');
    }
}
