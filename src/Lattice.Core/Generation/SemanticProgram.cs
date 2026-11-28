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

/// <summary>
/// Updates a value in place by applying an arithmetic operator to its current value. The target is a
/// name the surrounding body binds or a member access on the instance that body belongs to; nothing
/// here names a target-language compound operator.
/// </summary>
public sealed record AugmentedAssignmentRequirement(
    SemanticExpression Target,
    SemanticExpressionOperator Operator,
    SemanticExpression Value) : ProgramRequirement;

/// <summary>
/// Binds one element of a collection by its key, such as raising the running total held under a group's
/// key. The target is an ordinary expression over an already-bound collection, so the same shape serves a
/// mapping and a list; the selected language pack decides how an element is written.
/// </summary>
public sealed record ElementAssignmentRequirement(
    SemanticExpression Target,
    SemanticExpression Value) : ProgramRequirement;

/// <summary>
/// Repeats an ordered body while its condition holds. The condition is evaluated again before
/// every iteration, so assignments in the body can determine when repetition ends.
/// </summary>
public sealed record WhileRequirement(
    SemanticExpression Condition,
    ImmutableArray<ProgramRequirement> Body) : ProgramRequirement;

public sealed record ConditionalRequirement(
    SemanticExpression Condition,
    ImmutableArray<ProgramRequirement> ThenRequirements,
    ImmutableArray<ProgramRequirement> ElseRequirements) : ProgramRequirement;

public sealed record OutputRequirement(
    SemanticExpression Value) : ProgramRequirement;

/// <summary>
/// Classifies one value by its sign and reports one label per branch. A sign has three branches by
/// definition — greater than zero, less than zero, and equal to zero — so the boundaries are part of the
/// kind; the variable, the value, and the three labels stay request or example data.
/// </summary>
public sealed record SignClassificationRequirement(
    string VariableName,
    SemanticExpression Value,
    string PositiveLabel,
    string NegativeLabel,
    string ZeroLabel) : ProgramRequirement;

/// <summary>
/// Makes one library module available to the program. The module name stays request data and the
/// selected language pack spells how a module is made available.
/// </summary>
public sealed record ImportRequirement(string Module) : ProgramRequirement;

/// <summary>
/// Iterates the entries of one directory in a stable order, binding each entry to an item name. The sort
/// key is an expression over that item, and the body holds the statements that consume the entries. The
/// selected language pack spells how a directory is scanned and ordered, so nothing here names an API.
/// </summary>
public sealed record DirectoryListingRequirement(
    SemanticExpression Directory,
    string ItemName,
    SemanticExpression SortKey,
    ImmutableArray<ProgramRequirement> Body) : ProgramRequirement;

/// <summary>
/// Writes into one file. The pack spells how the file is opened for writing and how the stream it yields is
/// named; the body holds the statements that write through that stream.
/// </summary>
public sealed record FileWriteRequirement(
    string Path,
    string StreamName,
    ImmutableArray<ProgramRequirement> Body) : ProgramRequirement;

/// <summary>
/// Writes one value into an already open stream in a serialized form. The format is data the request
/// names, and the pack decides which call spells that format.
/// </summary>
public sealed record SerializeRequirement(
    string Format,
    SemanticExpression Value,
    string StreamName,
    string Indent) : ProgramRequirement;

/// <summary>
/// Reads one already open stream back as a value, binding the result to a name. A decode failure is an
/// ordinary named error type and is handled by a surrounding attempt requirement.
/// </summary>
public sealed record DeserializeRequirement(
    string Format,
    string StreamName,
    string ResultName) : ProgramRequirement;

/// <summary>
/// Reads the text lines of one file. The read stream binds to a name the body iterates, and the body
/// holds the statements that consume it, so a file read composes with the same model as any other
/// block. The selected language pack spells how a file is opened for reading; the path stays request
/// data and no target-language mode or encoding is named here.
/// </summary>
public sealed record FileReadRequirement(
    string Path,
    string StreamName,
    ImmutableArray<ProgramRequirement> Body) : ProgramRequirement;

/// <summary>
/// Stops the current element's work in the enclosing iteration: the rest of that body is skipped and the next
/// element begins. The loop is the requirement's context, so this carries no name and no condition of its own.
/// </summary>
public sealed record ContinueRequirement : ProgramRequirement;

/// <summary>Leaves the innermost enclosing repetition, resuming after its body.</summary>
public sealed record BreakRequirement : ProgramRequirement;

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
/// <summary>
/// Binds a dictionary built by evaluating a key expression and a value expression for every item of a source,
/// such as `{number: number * number for number in numbers}`. The two expressions are ordinary semantic
/// expressions over the item name, and the selected language pack spells the comprehension.
/// </summary>
public sealed record DictionaryMapRequirement(
    string ResultName,
    string ItemName,
    SemanticExpression Source,
    SemanticExpression Value) : ProgramRequirement;

public sealed record FunctionRequirement(
    string Name,
    ImmutableArray<FunctionParameter> Parameters,
    SemanticValueKind? ReturnKind,
    ImmutableArray<ProgramRequirement> Body) : ProgramRequirement;

/// <summary>
/// A declared parameter: the name the body can use and, when the request or the program specifies one,
/// the value kind a caller must supply. A kind that no source declares stays null and never rejects a
/// caller.
/// </summary>
public sealed record FunctionParameter(
    string Name,
    SemanticValueKind? Kind);

/// <summary>Returns an expression as the result of the enclosing function declaration.</summary>
public sealed record ReturnRequirement(
    SemanticExpression Value) : ProgramRequirement;

/// <summary>
/// Calls a declared function with ordered argument expressions. When the program also declares the
/// callee, the arguments must match its parameters in count and kind.
/// </summary>
public sealed record CallRequirement(
    string FunctionName,
    ImmutableArray<SemanticExpression> Arguments) : ProgramRequirement
{
    /// <summary>When set, the call is a method call on this instance rather than a free function.</summary>
    public string? Receiver { get; init; }

    /// <summary>
    /// When set, the call reaches this knowledge-declared API contract. The selected language pack must declare
    /// an operation for that contract, so a call the knowledge does not model stays unrealizable instead of
    /// being emitted with whatever operation happens to accept a call.
    /// </summary>
    public string? ApiContract { get; init; }
}

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
    ImmutableArray<MethodRequirement> Methods) : ProgramRequirement
{
    /// <summary>The name a method body uses for the instance its method was called on.</summary>
    public string InstanceName { get; init; } = "self";
}

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

/// <summary>One condition of a multi-way branch together with the requirements it guards.</summary>
public sealed record ConditionalBranch(
    SemanticExpression Condition,
    ImmutableArray<ProgramRequirement> Body);

/// <summary>
/// A multi-way branch: the conditions are tried in order and the first satisfied one runs its body.
/// The otherwise requirements describe what happens when none matched, and may be empty.
/// </summary>
public sealed record ConditionalChainRequirement(
    ImmutableArray<ConditionalBranch> Branches,
    ImmutableArray<ProgramRequirement> Otherwise) : ProgramRequirement;
