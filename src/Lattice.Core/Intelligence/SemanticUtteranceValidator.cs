using System.Collections.Immutable;
using Lattice.Core.Semantics;

namespace Lattice.Core.Intelligence;

public sealed class SemanticUtteranceValidator
{
    public bool TryValidate(
        SemanticTranslationCandidate? candidate,
        out string? error)
    {
        error = null;

        if (candidate is null || candidate.Meaning is null)
        {
            error = "The translator returned an incomplete semantic candidate.";
            return false;
        }

        return candidate.Kind switch
        {
            UtteranceKind.Assertion => TryValidateAssertion(candidate.Meaning, out error),
            UtteranceKind.Question => TryValidateQuestion(candidate.Meaning, out error),
            _ => SetError("The translator returned an unsupported utterance kind.", out error)
        };
    }

    private static bool TryValidateAssertion(
        SemanticFormula meaning,
        out string? error)
    {
        switch (meaning)
        {
            case PredicateApplication predicate:
                return TryValidateGroundPredicate(predicate, allowUnresolvedReference: true, out error);

            case ExistentialQuantification existential:
                return TryValidateExistential(existential, out error);

            case UniversalQuantification universal:
                return TryValidateUniversalRule(universal, out error);

            default:
                error = "Assertions must be ground facts, supported existential claims, or supported universal rules.";
                return false;
        }
    }

    private static bool TryValidateQuestion(
        SemanticFormula meaning,
        out string? error)
    {
        switch (meaning)
        {
            case PredicateApplication predicate:
                return TryValidateGroundPredicate(predicate, allowUnresolvedReference: true, out error);

            case ExistentialQuantification existential:
                return TryValidateExistential(existential, out error);

            default:
                error = "Questions must be ground predicate queries or supported existence queries.";
                return false;
        }
    }

    private static bool TryValidateExistential(
        ExistentialQuantification existential,
        out string? error)
    {
        if (existential.Variable is null ||
            !IsValidIdentifier(existential.Variable.Identifier.Value))
        {
            error = "An existential claim must declare a valid variable.";
            return false;
        }

        return TryValidateUnaryConditions(
            existential.Body,
            existential.Variable,
            out error);
    }

    private static bool TryValidateUniversalRule(
        UniversalQuantification universal,
        out string? error)
    {
        if (universal.Variable is null ||
            !IsValidIdentifier(universal.Variable.Identifier.Value) ||
            universal.Body is not Implication implication)
        {
            error = "A universal assertion must contain one implication with a valid bound variable.";
            return false;
        }

        if (!TryValidateUniversalConditions(implication.Premise, universal.Variable, out error))
        {
            return false;
        }

        var conclusion = implication.Conclusion switch
        {
            PredicateApplication predicate => predicate,
            Negation { Formula: PredicateApplication predicate } => predicate,
            _ => null
        };

        if (conclusion is null ||
            !IsValidIdentifier(conclusion.Predicate.Value) ||
            conclusion.Arguments.IsDefault)
        {
            error = "A universal rule must conclude with a supported predicate or its explicit negation.";
            return false;
        }

        if (conclusion.Arguments.Length == 1 &&
            conclusion.Arguments[0] is VariableTerm unaryVariable &&
            unaryVariable.Identifier == universal.Variable.Identifier)
        {
            return true;
        }

        if (conclusion.Arguments.Length == 2 &&
            conclusion.Arguments[0] is VariableTerm subject &&
            subject.Identifier == universal.Variable.Identifier &&
            IsConcreteIndividual(conclusion.Arguments[1]))
        {
            return true;
        }

        error = "A universal rule must conclude with its bound individual or relate it to one fixed concrete object.";
        return false;
    }

    private static bool TryValidateUniversalConditions(SemanticFormula formula, VariableTerm variable, out string? error)
    {
        if (!TryGetFlatConjuncts(formula, out var conjuncts))
        {
            error = "Universal conditions must be a flat, non-empty conjunction of supported premises.";
            return false;
        }
        var hasUnaryPremise = false;
        foreach (var conjunct in conjuncts)
        {
            var isNegative = conjunct is Negation;
            var predicate = conjunct switch
            {
                PredicateApplication positive => positive,
                Negation { Formula: PredicateApplication negative } => negative,
                _ => null
            };
            if (predicate is null || !IsValidIdentifier(predicate.Predicate.Value) || predicate.Arguments.IsDefault)
            {
                error = "Every universal condition must be a supported predicate.";
                return false;
            }
            if (predicate.Arguments.Length == 1 &&
                predicate.Arguments[0] is VariableTerm unary && unary.Identifier == variable.Identifier)
            {
                hasUnaryPremise = true;
                continue;
            }
            if (!isNegative && predicate.Arguments.Length == 2 &&
                predicate.Arguments[0] is VariableTerm subject && subject.Identifier == variable.Identifier &&
                IsConcreteIndividual(predicate.Arguments[1])) continue;
            error = "Universal conditions may contain unary premises and positive relations to one fixed concrete object.";
            return false;
        }
        if (!hasUnaryPremise)
        {
            error = "A universal rule must include at least one unary premise to identify its subject.";
            return false;
        }
        error = null;
        return true;
    }

    private static bool TryValidateUnaryConditions(
        SemanticFormula formula,
        VariableTerm variable,
        out string? error,
        bool allowNegation = false)
    {
        if (!TryGetFlatConjuncts(formula, out var conjuncts))
        {
            error = "Quantified conditions must be one unary predicate or a flat, non-empty conjunction of unary predicates.";
            return false;
        }

        foreach (var conjunct in conjuncts)
        {
            var predicate = conjunct switch
            {
                PredicateApplication positive => positive,
                Negation { Formula: PredicateApplication negative } when allowNegation => negative,
                _ => null
            };

            if (predicate is null ||
                !IsValidIdentifier(predicate.Predicate.Value) ||
                predicate.Arguments.IsDefault ||
                predicate.Arguments.Length != 1 ||
                predicate.Arguments[0] is not VariableTerm argument ||
                argument.Identifier != variable.Identifier)
            {
                error = allowNegation
                    ? "Every quantified condition must be a unary predicate or its explicit negation over the declared variable."
                    : "Every quantified condition must be a unary predicate over the declared variable.";
                return false;
            }
        }

        error = null;
        return true;
    }

    private static bool TryGetFlatConjuncts(
        SemanticFormula formula,
        out ImmutableArray<SemanticFormula> conjuncts)
    {
        switch (formula)
        {
            case PredicateApplication predicate:
                conjuncts = ImmutableArray.Create<SemanticFormula>(predicate);
                return true;

            case Negation { Formula: PredicateApplication }:
                conjuncts = ImmutableArray.Create(formula);
                return true;

            case Conjunction conjunction when !conjunction.Conjuncts.IsDefaultOrEmpty:
                conjuncts = conjunction.Conjuncts;
                return true;

            default:
                conjuncts = ImmutableArray<SemanticFormula>.Empty;
                return false;
        }
    }

    private static bool TryValidateGroundPredicate(
        PredicateApplication predicate,
        bool allowUnresolvedReference,
        out string? error)
    {
        if (!IsValidIdentifier(predicate.Predicate.Value) ||
            predicate.Arguments.IsDefault ||
            predicate.Arguments.Length is < 1 or > 2)
        {
            error = "A supported predicate must have a valid identifier and one or two arguments.";
            return false;
        }

        var unresolvedCount = 0;

        foreach (var argument in predicate.Arguments)
        {
            switch (argument)
            {
                case IndividualTerm individual when
                    IsValidIdentifier(individual.Identifier.Value):
                    break;

                case DescriptiveIndividualTerm description when
                    IsValidDescription(description):
                    break;

                case UnresolvedReferenceTerm reference when
                    allowUnresolvedReference &&
                    string.Equals(reference.Reference.Value, "it", StringComparison.Ordinal):
                    unresolvedCount++;

                    if (unresolvedCount > 1)
                    {
                        error = "A relation may contain at most one unresolved reference.";
                        return false;
                    }

                    break;

                default:
                    error = "A ground predicate may contain only named individuals, definite descriptions, and one supported unresolved reference.";
                    return false;
            }
        }

        if (unresolvedCount == 1 &&
            predicate.Arguments.Length == 2 &&
            !IsConcreteIndividual(predicate.Arguments[0] is UnresolvedReferenceTerm
                ? predicate.Arguments[1]
                : predicate.Arguments[0]))
        {
            error = "A relational reference must be paired with one concrete individual.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool IsConcreteIndividual(SemanticTerm? term) =>
        term switch
        {
            IndividualTerm individual => IsValidIdentifier(individual.Identifier.Value),
            DescriptiveIndividualTerm description => IsValidDescription(description),
            _ => false
        };

    private static bool IsValidDescription(DescriptiveIndividualTerm description) =>
        IsValidIdentifier(description.Head.Value) &&
        !description.Modifiers.IsDefault &&
        description.Modifiers.All(modifier => IsValidIdentifier(modifier.Value));

    private static bool IsValidIdentifier(string? value)
    {
        if (string.IsNullOrEmpty(value) ||
            value[0] is < 'a' or > 'z')
        {
            return false;
        }

        return value.All(character =>
            character is >= 'a' and <= 'z' or
                >= '0' and <= '9' or '-');
    }

    private static bool SetError(string message, out string? error)
    {
        error = message;
        return false;
    }
}
