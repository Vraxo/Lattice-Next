using Lattice.Core.Semantics;

namespace Lattice.Core.Language;

public sealed record ParseResult
{
    private ParseResult(SemanticUtterance? utterance, string? error)
    {
        Utterance = utterance;
        Error = error;
    }

    public SemanticUtterance? Utterance { get; }

    public string? Error { get; }

    public bool IsSuccess => Utterance is not null;

    public static ParseResult Success(SemanticUtterance utterance) =>
        new(utterance, null);

    public static ParseResult Failure(string error) =>
        new(null, error);
}
