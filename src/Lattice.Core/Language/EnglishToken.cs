namespace Lattice.Core.Language;

internal readonly record struct EnglishToken(
    string Value,
    bool IsQuoted,
    bool IsPunctuation,
    int Start,
    int Length);
