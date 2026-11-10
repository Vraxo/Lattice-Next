using System.Collections.Immutable;
using Lattice.Core.Semantics;

namespace Lattice.Core.Language;

internal static partial class NativeEnglishGrammar
{
    private static readonly HashSet<string> ReservedIndividualTerms = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "there", "is", "are", "be", "exist",
        "something", "that", "by", "not", "no", "every", "all", "only", "some",
        "to", "of", "class", "count", "as", "and"
    };

    private static bool TryParseDescription(
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

        while (index < end && !Is(tokens[index], "that"))
        {
            if (!IsLowerWord(tokens[index]) ||
                ReservedIndividualTerms.Contains(tokens[index]))
            {
                return false;
            }

            lexicalWords.Add(tokens[index].ToLowerInvariant());
            index++;

            if (index < end && Is(tokens[index], "and"))
            {
                index++;

                if (index >= end ||
                    Is(tokens[index], "that") ||
                    Is(tokens[index], "and") ||
                    IsPluralHead(tokens[index]))
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
                !Is(tokens[index], "that") ||
                !Is(tokens[index + 1], relativeCopula))
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
            if (!IsPluralHead(head))
            {
                return false;
            }

            head = Singularize(head);
        }
        else if (IsPlural(head))
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
            !IsLowerWord(tokens[index]) ||
            ReservedIndividualTerms.Contains(tokens[index]))
        {
            return false;
        }

        modifiers.Add(tokens[index].ToLowerInvariant());
        index++;

        while (index < end && Is(tokens[index], "and"))
        {
            index++;

            if (index >= end ||
                Is(tokens[index], "that") ||
                !IsLowerWord(tokens[index]) ||
                IsPluralHead(tokens[index]) ||
                ReservedIndividualTerms.Contains(tokens[index]))
            {
                return false;
            }

            modifiers.Add(tokens[index].ToLowerInvariant());
            index++;
        }

        return index == end || Is(tokens[index], "that");
    }

    private static bool TryCreateSubjectTerm(
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

        return count > 1 &&
            TryCreateDefiniteDescription(tokens, start, count, out var description) &&
            AssignDescription(description!, out term);
    }

    private static bool TryCreateObjectTerm(
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

        return count > 1 &&
            TryCreateDefiniteDescription(tokens, start, count, out var description) &&
            AssignDescription(description!, out term);
    }

    private static bool AssignDescription(
        DescriptiveIndividualTerm description,
        out SemanticTerm? term)
    {
        term = description;
        return true;
    }

    private static bool TryCreateDefiniteDescription(
        string[] tokens,
        int start,
        int count,
        out DescriptiveIndividualTerm? description)
    {
        description = null;

        if (count < 2 || !Is(tokens[start], "the") ||
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

    private static bool TryCreateIndividual(
        string value,
        bool allowPronoun,
        out SemanticTerm? term)
    {
        term = null;

        if (allowPronoun && Is(value, "it"))
        {
            term = new UnresolvedReferenceTerm(Id("it"));
            return true;
        }

        if (!IsWord(value) || ReservedIndividualTerms.Contains(value))
        {
            return false;
        }

        term = new IndividualTerm(Id(value));
        return true;
    }
}
