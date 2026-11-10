using System.Collections.Frozen;

namespace Lattice.Core.Language;

internal sealed class EnglishLexiconData(
    FrozenSet<string> reservedTerms,
    FrozenDictionary<string, string> explicitPluralNounForms,
    FrozenSet<string> invariantNouns,
    FrozenSet<string> singularNounsEndingInS,
    FrozenDictionary<string, string> irregularThirdPersonVerbs,
    FrozenSet<string> baseRelationVerbs,
    FrozenDictionary<string, string> pastParticiples)
{
    internal FrozenSet<string> ReservedTerms { get; } = reservedTerms;
    internal FrozenDictionary<string, string> ExplicitPluralNounForms { get; } = explicitPluralNounForms;
    internal FrozenSet<string> InvariantNouns { get; } = invariantNouns;
    internal FrozenSet<string> SingularNounsEndingInS { get; } = singularNounsEndingInS;
    internal FrozenDictionary<string, string> IrregularThirdPersonVerbs { get; } = irregularThirdPersonVerbs;
    internal FrozenSet<string> BaseRelationVerbs { get; } = baseRelationVerbs;
    internal FrozenDictionary<string, string> PastParticiples { get; } = pastParticiples;
}
