namespace Lattice.Core.Semantics;

public readonly record struct SemanticIdentifier
{
    public SemanticIdentifier(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}
