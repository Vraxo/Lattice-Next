using System.Collections.Immutable;
using Lattice.Core.Semantics;

namespace Lattice.Core.Language;

internal static class EnglishDescriptionParser
{
    private static readonly string[] ExistentialWitnessNames =
        ["y", "z", "w", "v", "u", "t", "s", "r", "q", "p"];
    internal static bool TryParseDescription(
        string[] tokens,
        int start,
        int end,
        bool expectPluralHead,
        out EnglishDescription? description,
        bool allowNegatedRelativePredicates = false,
        bool allowRelationalRelativePredicates = false,
        VariableTerm? relationSubject = null,
        bool allowExistentialRelationalObjects = true,
        bool allowNegativeRelationalRelativePredicates = true)
    {
        description = null;

        if (start >= end)
        {
            return false;
        }

        var lexicalWords = new List<string>();
        var relativeModifiers = new List<string>();
        var negativeRelativePredicates = new List<string>();
        var relativeRelations = new List<(string Predicate, SemanticTerm Object, bool IsNegative)>();
        var existentialRelativeConditions = new List<SemanticFormula>();
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

            if (index + 1 >= end) return false;
            var startsRelationalClause = EnglishGrammarTokens.Is(tokens[index], "that") ||
                (EnglishGrammarTokens.Is(tokens[index], "and") && allowRelationalRelativePredicates);
            if (!startsRelationalClause) return false;
            if (!EnglishGrammarTokens.Is(tokens[index + 1], relativeCopula))
            {
                if (!allowRelationalRelativePredicates ||
                    !TryParseRelationalRelativePredicate(
                        tokens,
                        ref index,
                        end,
                        expectPluralHead,
                        relationSubject ?? new VariableTerm(EnglishGrammarTokens.CreateIdentifier("x")),
                        relativeRelations,
                        existentialRelativeConditions,
                        allowExistentialRelationalObjects,
                        allowNegativeRelationalRelativePredicates)) return false;
                continue;
            }
            if (!EnglishGrammarTokens.Is(tokens[index], "that")) return false;

            index += 2;

            if (index < end && EnglishGrammarTokens.Is(tokens[index], "not"))
            {
                if (!allowNegatedRelativePredicates ||
                    negativeRelativePredicates.Count > 0)
                {
                    return false;
                }

                index++;

                if (!TryParseRelativeNegatedPredicate(
                    tokens,
                    ref index,
                    end,
                    expectPluralHead,
                    out var negativePredicate))
                {
                    return false;
                }

                negativeRelativePredicates.Add(negativePredicate!);
            }
            else if (!TryParseRelativeAdjectives(tokens, ref index, end, relativeModifiers))
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
            relativeModifiers.ToImmutableArray(),
            negativeRelativePredicates.ToImmutableArray(),
            relativeRelations.ToImmutableArray(),
            existentialRelativeConditions.ToImmutableArray());
        return true;
    }

    private static bool TryParseRelationalRelativePredicate(
        string[] tokens,
        ref int index,
        int end,
        bool pluralSubject,
        VariableTerm relationSubject,
        List<(string Predicate, SemanticTerm Object, bool IsNegative)> relations,
        List<SemanticFormula> existentialConditions,
        bool allowExistentialRelationalObjects,
        bool allowNegativeRelationalRelativePredicates)
    {
        if (index + 1 >= end)
        {
            return false;
        }

        var auxiliary = tokens[index + 1];
        var isNegative = EnglishGrammarTokens.Is(auxiliary, pluralSubject ? "do" : "does");
        if (isNegative && !allowNegativeRelationalRelativePredicates)
        {
            return false;
        }

        int verbIndex;
        int objectStart;

        if (EnglishGrammarTokens.Is(auxiliary, "do") ||
            EnglishGrammarTokens.Is(auxiliary, "does"))
        {
            if (!isNegative ||
                index + 3 >= end ||
                !EnglishGrammarTokens.Is(tokens[index + 2], "not") ||
                !EnglishGrammarTokens.IsLowerWord(tokens[index + 3]) ||
                !EnglishRelationLexicon.IsBaseRelationVerb(tokens[index + 3]))
            {
                return false;
            }

            verbIndex = index + 3;
            objectStart = index + 4;
        }
        else
        {
            var verb = auxiliary;

            if (pluralSubject)
            {
                if (!EnglishRelationLexicon.IsBaseRelationVerb(verb))
                {
                    return false;
                }
            }
            else if (!EnglishMorphology.IsThirdPersonVerb(verb))
            {
                return false;
            }

            verbIndex = index + 1;
            objectStart = index + 2;
        }

        var predicate = isNegative || pluralSubject
            ? EnglishMorphology.InflectThirdPersonSingular(tokens[verbIndex])
            : tokens[verbIndex].ToLowerInvariant();

        var objectEnd = FindNextRelationalConjunction(tokens, objectStart, end, pluralSubject);
        if (objectStart < objectEnd && EnglishGrammarTokens.IsArticle(tokens[objectStart]))
        {
            if (!allowExistentialRelationalObjects || isNegative)
            {
                return false;
            }

            var witness = CreateExistentialWitness(
                relationSubject,
                existentialConditions.Count);
            if (!TryParseDescription(
                tokens,
                objectStart + 1,
                objectEnd,
                expectPluralHead: false,
                out var objectDescription,
                allowRelationalRelativePredicates: true,
                relationSubject: witness,
                allowExistentialRelationalObjects: true,
                allowNegativeRelationalRelativePredicates: false))
            {
                return false;
            }

            var objectConditions = EnglishSemanticFactory.GetConjuncts(
                objectDescription!.ToExistentialConditions(witness));
            var relation = new PredicateApplication(
                new SemanticIdentifier(predicate),
                ImmutableArray.Create<SemanticTerm>(
                    relationSubject,
                    witness));
            existentialConditions.Add(new ExistentialQuantification(
                witness,
                EnglishSemanticFactory.Combine(objectConditions.Add(relation))));
        }
        else
        {
            if (!TryCreateObjectTerm(tokens, objectStart, objectEnd, out var relationObject) ||
                relationObject is not IndividualTerm individual ||
                string.IsNullOrWhiteSpace(individual.Identifier.Value))
            {
                return false;
            }

            relations.Add((predicate, individual, isNegative));
        }

        index = objectEnd < end ? objectEnd : end;
        return true;
    }

    private static VariableTerm CreateExistentialWitness(
        VariableTerm subject,
        int index)
    {
        var variableNames = new[] { "x", "y", "z", "w", "v", "u", "t", "s", "r", "q", "p" };
        var subjectName = subject.Identifier.Value;
        var subjectIndex = Array.IndexOf(variableNames, subjectName);
        string name;

        if (subjectIndex >= 0)
        {
            var witnessIndex = subjectIndex + index + 1;
            name = witnessIndex < variableNames.Length
                ? variableNames[witnessIndex]
                : $"witness{witnessIndex + 1}";
        }
        else if (subjectName.StartsWith("witness", StringComparison.Ordinal) &&
            int.TryParse(subjectName["witness".Length..], out var parentWitnessIndex))
        {
            name = $"witness{parentWitnessIndex + index + 1}";
        }
        else
        {
            name = $"witness{variableNames.Length + index + 1}";
        }

        return new VariableTerm(EnglishGrammarTokens.CreateIdentifier(name));
    }

    private static int FindNextRelationalConjunction(
        string[] tokens,
        int objectStart,
        int end,
        bool pluralSubject)
    {
        for (var index = objectStart + 1; index + 1 < end; index++)
        {
            if (!EnglishGrammarTokens.Is(tokens[index], "and"))
            {
                continue;
            }

            var next = tokens[index + 1];
            if (EnglishRelationLexicon.IsBaseRelationVerb(next) ||
                EnglishMorphology.IsThirdPersonVerb(next))
            {
                return index;
            }

            if (index + 3 < end &&
                EnglishGrammarTokens.Is(next, pluralSubject ? "do" : "does") &&
                EnglishGrammarTokens.Is(tokens[index + 2], "not") &&
                EnglishRelationLexicon.IsBaseRelationVerb(tokens[index + 3]))
            {
                return index;
            }
        }

        return end;
    }

    private static bool TryParseRelativeNegatedPredicate(
        string[] tokens,
        ref int index,
        int end,
        bool expectPluralHead,
        out string? predicate)
    {
        predicate = null;

        if (index >= end)
        {
            return false;
        }

        if (EnglishGrammarTokens.IsArticle(tokens[index]))
        {
            if (index + 1 >= end ||
                !EnglishGrammarTokens.IsLowerWord(tokens[index + 1]) ||
                EnglishGrammarTokens.IsReservedIndividualTerm(tokens[index + 1]) ||
                EnglishMorphology.IsPlural(tokens[index + 1]))
            {
                return false;
            }

            predicate = tokens[index + 1].ToLowerInvariant();
            index += 2;
            return index == end || EnglishGrammarTokens.Is(tokens[index], "that");
        }

        var word = tokens[index];

        if (!EnglishGrammarTokens.IsLowerWord(word) ||
            EnglishGrammarTokens.IsReservedIndividualTerm(word))
        {
            return false;
        }

        if (EnglishMorphology.IsPlural(word))
        {
            if (!expectPluralHead)
            {
                return false;
            }

            predicate = EnglishMorphology.Singularize(word);
        }
        else
        {
            predicate = word.ToLowerInvariant();
        }

        index++;
        return index == end || EnglishGrammarTokens.Is(tokens[index], "that");
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
