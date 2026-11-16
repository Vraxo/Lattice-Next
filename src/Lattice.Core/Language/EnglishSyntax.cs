using System.Collections.Immutable;

namespace Lattice.Core.Language;

internal enum EnglishClauseKind
{
    Main,
    Relative,
    Infinitive,
    Coordinated,
    Subordinate
}

internal sealed record EnglishNounPhrase(
    string Text,
    string Head,
    string? Determiner,
    ImmutableArray<string> Modifiers,
    int StartToken,
    int EndToken);

internal sealed record EnglishPrepositionalPhrase(
    string Preposition,
    EnglishNounPhrase Object,
    int StartToken,
    int EndToken);

internal enum EnglishSubjectBinding
{
    Explicit,
    Implicit,
    Antecedent,
    PreviousClause
}

internal sealed record EnglishClause(
    EnglishClauseKind Kind,
    string Verb,
    EnglishNounPhrase? Subject,
    EnglishNounPhrase? Object,
    ImmutableArray<EnglishPrepositionalPhrase> PrepositionalPhrases,
    EnglishSubjectBinding SubjectBinding,
    int VerbTokenIndex);

internal sealed record EnglishSentenceSyntax(
    ImmutableArray<EnglishToken> Tokens,
    ImmutableArray<EnglishClause> Clauses);
