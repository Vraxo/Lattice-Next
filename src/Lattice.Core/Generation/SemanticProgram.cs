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

/// <summary>
/// Binds a collection of the items of a source that satisfy a predicate. The predicate is an ordinary
/// semantic expression over the item name, so any expressible comparison or boolean tree works.
/// </summary>
public sealed record FilterRequirement(
    string ResultName,
    string ItemName,
    SemanticExpression Source,
    SemanticExpression Predicate) : ProgramRequirement;

/// <summary>
/// Binds a collection built by evaluating one expression for every item of a source. The value
/// expression is an ordinary semantic expression over the item name.
/// </summary>
public sealed record MapRequirement(
    string ResultName,
    string ItemName,
    SemanticExpression Source,
    SemanticExpression Value) : ProgramRequirement;

/// <summary>
/// Binds an accumulator that starts at an initial value and folds a source through an update
/// expression. The update expression must depend on both the accumulator and the current item, and the
/// accumulated name stays available to later requirements.
/// </summary>
public sealed record AccumulationRequirement(
    string AccumulatorName,
    SemanticCollectionItemKind AccumulatorKind,
    string ItemName,
    SemanticExpression Source,
    SemanticExpression InitialValue,
    SemanticExpression Update) : ProgramRequirement;

/// <summary>
/// Binds an ordered collection of the items of a source, ordered by a key expression over the item
/// name, in ascending or descending order.
/// </summary>
public sealed record SortRequirement(
    string ResultName,
    string ItemName,
    SemanticExpression Source,
    SemanticExpression Key,
    bool Descending) : ProgramRequirement;

/// <summary>
/// Declares a function: its request-derived name, ordered parameters with their declared value kinds,
/// the declared result kind, and an ordered body of requirements. Nothing here names a target-language
/// keyword; the language pack decides how a declaration is written.
/// </summary>
public sealed record FunctionRequirement(
    string Name,
    ImmutableArray<FunctionParameter> Parameters,
    SemanticValueKind ReturnKind,
    ImmutableArray<ProgramRequirement> Body) : ProgramRequirement;

/// <summary>A declared parameter: the name the body can use and the value kind a caller must supply.</summary>
public sealed record FunctionParameter(
    string Name,
    SemanticValueKind Kind);

/// <summary>Returns an expression as the result of the enclosing function declaration.</summary>
public sealed record ReturnRequirement(
    SemanticExpression Value) : ProgramRequirement;

/// <summary>
/// Calls a declared function with ordered argument expressions. When the program also declares the
/// callee, the arguments must match its parameters in count and kind.
/// </summary>
public sealed record CallRequirement(
    string FunctionName,
    ImmutableArray<SemanticExpression> Arguments) : ProgramRequirement;

/// <summary>One declared class field: the member name and the value the constructor starts it with.</summary>
public sealed record ClassField(
    string Name,
    SemanticExpression InitialValue);

/// <summary>
/// One declared method: its name, ordered parameters, an optional declared result kind (nothing when
/// the method returns no value), and an ordered body.
/// </summary>
public sealed record MethodRequirement(
    string Name,
    ImmutableArray<FunctionParameter> Parameters,
    SemanticValueKind? ReturnKind,
    ImmutableArray<ProgramRequirement> Body);

/// <summary>
/// Declares a class: its identity, the fields its constructor initializes, and its methods. Nothing
/// here names a target-language construct; the language pack decides how a class is written.
/// </summary>
public sealed record ClassRequirement(
    string Name,
    ImmutableArray<ClassField> Fields,
    ImmutableArray<MethodRequirement> Methods) : ProgramRequirement;

/// <summary>
/// Raises an error of one type, either unconditionally or when a condition holds. The condition is an
/// ordinary boolean expression over values the surrounding requirements bind.
/// </summary>
public sealed record RaiseRequirement(
    string ErrorType,
    SemanticExpression? Condition) : ProgramRequirement;

/// <summary>Handles one error type by running an ordered recovery body.</summary>
public sealed record HandlerRequirement(
    string ErrorType,
    ImmutableArray<ProgramRequirement> Body);

/// <summary>
/// Runs a body and recovers from a failure of one of the handled error types. The optional result name
/// binds the value the body produced on the successful path, so later requirements can consume a
/// result while the handlers describe what happens when the attempt fails.
/// </summary>
public sealed record TryRequirement(
    ImmutableArray<ProgramRequirement> Body,
    ImmutableArray<HandlerRequirement> Handlers,
    string? ResultName) : ProgramRequirement;
