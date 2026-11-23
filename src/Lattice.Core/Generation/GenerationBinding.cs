namespace Lattice.Core.Generation;

public sealed record GenerationBinding(
    string Name,
    string Source,
    string Encoding)
{
    public string? Key { get; init; }
}
