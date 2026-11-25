using System.Collections.Immutable;

namespace Lattice.Core.Generation;

/// <summary>A language-neutral, ordered set of requirements for one requested program.</summary>
public sealed record SemanticProgram(ImmutableArray<ProgramRequirement> Requirements)
{
    /// <summary>
    /// Collections this request binds: a name and its element kind. Contents stay unknown until the
    /// request supplies them, so no sample data is stored here.
    /// </summary>
    public ImmutableArray<CollectionBinding> Collections { get; init; } =
        ImmutableArray<CollectionBinding>.Empty;
}

/// <summary>A named collection the request refers to, with its element kind but no fixed contents.</summary>
public sealed record CollectionBinding(
    string Name,
    SemanticCollectionItemKind ElementKind);

/// <summary>A request-derived program requirement, independent of target-language syntax.</summary>
public abstract record ProgramRequirement;

public sealed record AssignmentRequirement(
    string VariableName,
    SemanticExpression Value) : ProgramRequirement;

public sealed record ConditionalRequirement(
    SemanticExpression Condition,
    ImmutableArray<ProgramRequirement> ThenRequirements,
    ImmutableArray<ProgramRequirement> ElseRequirements) : ProgramRequirement;

public sealed record OutputRequirement(
    SemanticExpression Value) : ProgramRequirement;

/// <summary>
/// An iteration over a collection value, binding each element to an item name. The body is an ordered
/// requirement list, so loops compose with the same model as top-level statements.
/// </summary>
public sealed record IterationRequirement(
    string ItemName,
    SemanticExpression Source,
    ImmutableArray<ProgramRequirement> Body) : ProgramRequirement;

/// <summary>
/// An iteration over a key/value collection, binding the key and value names separately. The two names
/// must differ, because both are visible inside the body.
/// </summary>
public sealed record DictionaryIterationRequirement(
    string KeyName,
    string ValueName,
    SemanticExpression Source,
    ImmutableArray<ProgramRequirement> Body) : ProgramRequirement;
