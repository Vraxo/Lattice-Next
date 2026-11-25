namespace Lattice.Core.Generation;

/// <summary>
/// The declared value kind of a function parameter, a function result, or a returned expression. The
/// requirement model uses it to reject a call or a return whose value kind provably differs from the
/// declaration; a value whose kind cannot be inferred (a bare name, a member access) is always allowed.
/// </summary>
public enum SemanticValueKind
{
    Number,
    Text,
    Boolean
}
