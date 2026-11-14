using System.Collections.Immutable;

namespace Lattice.Core.Semantics;

public sealed record FamilySetCompletionQuestionFormula(
    string Anchor,
    string OwnerRelation,
    string MemberType,
    int ExpectedCount,
    int RequestedOrdinal,
    ImmutableArray<string> KnownMembers) : SemanticFormula;
