using System.Collections.Immutable;
using Lattice.Core.Semantics;

namespace Lattice.Core.Language;

internal static class EnglishDescriptionParser
{
    internal static bool TryParseDescription(
        string[] tokens,
        int start,
        int end,
        bool expectPluralHead,
        out EnglishDescription? description)
    {
        description = null;

        if (start >= end)
        {
            return false;
        }

        var lexicalWords = new List<string>();
        var relativeModifiers = new List<string>();
        var index = start;

        while (index < end && !EnglishGrammarTokens.Is(tokens[index], "that"))
        {
            if (!EnglishGrammarTokens.IsLowerWord(tokens[index]) ||
                EnglishGrammarTokens.IsReservedIndividualTerm(tokens[index]))
            {
                return false;
            }

            lexicalWords.Add(tokens[index].ToLowerInvariant());
            index++;

            if (index < end && EnglishGrammarTokens.Is(tokens[index], "and"))
            {
                index++;

                if (index >= end ||
                    EnglishGrammarTokens.Is(tokens[index], "that") ||
                    EnglishGrammarTokens.Is(tokens[index], "and") ||
                    EnglishMorphology.IsPluralHead(tokens[index]))
                {
                    return false;
                }
            }
        }

        if (lexicalWords.Count == 0)
        {
            return false;
        }

        while (index < end)
        {
            var relativeCopula = expectPluralHead ? "are" : "is";

            if (index + 2 >= end ||
                !EnglishGrammarTokens.Is(tokens[index], "that") ||
                !EnglishGrammarTokens.Is(tokens[index + 1], relativeCopula))
            {
                return false;
            }

            index += 2;

            if (!TryParseRelativeAdjectives(tokens, ref index, end, relativeModifiers))
            {
                return false;
            }
        }

        var head = lexicalWords[^1];

        if (expectPluralHead)
        {
            if (!EnglishMorphology.IsPluralHead(head))
            {
                return false;
            }

            head = EnglishMorphology.Singularize(head);
        }
        else if (EnglishMorphology.IsPlural(head))
        {
            return false;
        }

        var modifiers = lexicalWords
            .Take(lexicalWords.Count - 1)
            .ToImmutableArray();

        description = new EnglishDescription(
            head,
            modifiers,
            relativeModifiers.ToImmutableArray());
        return true;
    }

    private static bool TryParseRelativeAdjectives(
        string[] tokens,
        ref int index,
        int end,
        List<string> modifiers)
    {
        if (index >= end ||
            !EnglishGrammarTokens.IsLowerWord(tokens[index]) ||
            EnglishGrammarTokens.IsReservedIndividualTerm(tokens[index]))
        {
            return false;
        }

        modifiers.Add(tokens[index].ToLowerInvariant());
        index++;

        while (index < end && EnglishGrammarTokens.Is(tokens[index], "and"))
        {
            index++;

            if (index >= end ||
                EnglishGrammarTokens.Is(tokens[index], "that") ||
                !EnglishGrammarTokens.IsLowerWord(tokens[index]) ||
                EnglishMorphology.IsPluralHead(tokens[index]) ||
                EnglishGrammarTokens.IsReservedIndividualTerm(tokens[index]))
            {
                return false;
            }

            modifiers.Add(tokens[index].ToLowerInvariant());
            index++;
        }

        return index == end || EnglishGrammarTokens.Is(tokens[index], "that");
    }

    internal static bool TryCreateSubjectTerm(
        string[] tokens,
        int start,
        int end,
        bool allowPronoun,
        out SemanticTerm? term)
    {
        term = null;
        var count = end - start;

        if (count == 1)
        {
            return TryCreateIndividual(tokens[start], allowPronoun, out term);
        }

        if (count <= 1 ||
            !TryCreateDefiniteDescription(tokens, start, count, out var description))
        {
            return false;
        }

        term = description;
        return true;
    }

    internal static bool TryCreateObjectTerm(
        string[] tokens,
        int start,
        int end,
        out SemanticTerm? term)
    {
        term = null;
        var count = end - start;

        if (count == 1)
        {
            return TryCreateIndividual(tokens[start], allowPronoun: true, out term);
        }

        if (count <= 1 ||
            !TryCreateDefiniteDescription(tokens, start, count, out var description))
        {
            return false;
        }

        term = description;
        return true;
    }

    internal static bool TryCreateDefiniteDescription(
        string[] tokens,
        int start,
        int count,
        out DescriptiveIndividualTerm? description)
    {
        description = null;

        if (count < 2 || !EnglishGrammarTokens.Is(tokens[start], "the") ||
            !TryParseDescription(
                tokens,
                start + 1,
                start + count,
                expectPluralHead: false,
                out var parsedDescription))
        {
            return false;
        }

        description = parsedDescription!.ToDefiniteTerm();
        return true;
    }

    internal static bool TryCreateIndividual(
        string value,
        bool allowPronoun,
        out SemanticTerm? term)
    {
        term = null;

        if (allowPronoun && EnglishGrammarTokens.Is(value, "it"))
        {
            term = new UnresolvedReferenceTerm(EnglishGrammarTokens.CreateIdentifier("it"));
            return true;
        }

        if (!EnglishGrammarTokens.IsWord(value) || EnglishGrammarTokens.IsReservedIndividualTerm(value))
        {
            return false;
        }

        term = new IndividualTerm(EnglishGrammarTokens.CreateIdentifier(value));
        return true;
    }
}
