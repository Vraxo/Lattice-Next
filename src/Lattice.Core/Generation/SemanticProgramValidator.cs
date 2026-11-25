using System.Collections.Immutable;

namespace Lattice.Core.Generation;

/// <summary>
/// Structural validation for the language-neutral requirement model. It checks that a program is
/// well formed — identifiers, expressions, branch bodies, nesting, and collection bindings — without
/// picking a target language; the selected language pack still decides whether a name is writable.
/// </summary>
public static class SemanticProgramValidator
{
    public const int MaximumRequirements = 64;
    public const int MaximumDepth = 3;
    public const int MaximumNameLength = 64;

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

        return AreValid(program.Requirements, depth: 0);
    }

    private static bool AreValid(ImmutableArray<ProgramRequirement> requirements, int depth)
    {
        if (requirements.IsDefault ||
            requirements.Length > MaximumRequirements ||
            depth > MaximumDepth)
        {
            return false;
        }

        return requirements.All(requirement => IsValid(requirement, depth));
    }

    private static bool IsValid(ProgramRequirement requirement, int depth) => requirement switch
    {
        AssignmentRequirement assignment =>
            IsValidName(assignment.VariableName) &&
            SemanticExpressionAnalysis.IsValid(assignment.Value),
        OutputRequirement output => SemanticExpressionAnalysis.IsValid(output.Value),
        ConditionalRequirement conditional =>
            SemanticExpressionAnalysis.IsValid(conditional.Condition) &&
            (!conditional.ThenRequirements.IsDefaultOrEmpty ||
             !conditional.ElseRequirements.IsDefaultOrEmpty) &&
            AreValid(conditional.ThenRequirements, depth + 1) &&
            AreValid(conditional.ElseRequirements, depth + 1),
        IterationRequirement iteration =>
            IsValidName(iteration.ItemName) &&
            SemanticExpressionAnalysis.IsValid(iteration.Source) &&
            !iteration.Body.IsDefaultOrEmpty &&
            AreValid(iteration.Body, depth + 1),
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
            AreValid(iteration.Body, depth + 1),
        _ => false
    };

    private static bool ReferencesName(SemanticExpression expression, string name) =>
        SemanticExpressionAnalysis.IsValid(expression) &&
        SemanticExpressionAnalysis.GetReferencedNames(expression).Contains(name, StringComparer.Ordinal);

    private static bool IsValidBinding(CollectionBinding binding) =>
        binding is not null &&
        IsValidName(binding.Name) &&
        binding.ElementKind is SemanticCollectionItemKind.Number or SemanticCollectionItemKind.Text;

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
