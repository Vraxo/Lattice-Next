using System.Collections.Immutable;
using Lattice.Core.Reasoning;

namespace Lattice.Core.Generation;

/// <summary>
/// Resolves the surface bindings for a semantic relation assertion.
/// </summary>
public static class SemanticSequenceBindings
{
    public static bool TryFromAssertion(
        SemanticWorldModel model,
        SemanticRelationAssertion assertion,
        SequenceTokenizer tokenizer,
        out ImmutableDictionary<string, ImmutableArray<string>> bindings,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(assertion);
        ArgumentNullException.ThrowIfNull(tokenizer);

        bindings = ImmutableDictionary<string, ImmutableArray<string>>.Empty;
        error = null;

        if (!model.RelationAssertions.Contains(assertion))
        {
            error = "The relation assertion is not part of the supplied semantic model.";
            return false;
        }

        var subject = model.Concepts.FirstOrDefault(concept => concept.Id == assertion.Subject);
        var relation = model.FindRelation(assertion.Relation);
        var target = model.Concepts.FirstOrDefault(concept => concept.Id == assertion.Object);

        if (subject is null || relation is null || target is null)
        {
            error = "The assertion must reference a declared subject, relation, and object.";
            return false;
        }

        if (!MatchesKind(subject.Kind, relation.SubjectKind) ||
            !MatchesKind(target.Kind, relation.ObjectKind))
        {
            error = "The assertion arguments do not match the declared relation kinds.";
            return false;
        }

        if (!tokenizer.TryTokenize(subject.Label, out var subjectTokens, out var subjectError) ||
            !tokenizer.TryTokenize(relation.Verb, out var relationTokens, out var relationError) ||
            !tokenizer.TryTokenize(target.Label, out var objectTokens, out var objectError))
        {
            error = subjectError ?? relationError ?? objectError ??
                "The assertion concepts and relation must have valid surface labels.";
            return false;
        }

        if (subjectTokens.IsDefaultOrEmpty ||
            relationTokens.IsDefaultOrEmpty ||
            objectTokens.IsDefaultOrEmpty)
        {
            error = "The assertion concepts and relation must have non-empty surface labels.";
            return false;
        }

        bindings = bindings
            .Add("subject", subjectTokens)
            .Add("relation", relationTokens)
            .Add("object", objectTokens);
        return true;
    }

    private static bool MatchesKind(
        SemanticConceptKind conceptKind,
        SemanticRelationArgumentKind argumentKind) =>
        (conceptKind, argumentKind) switch
        {
            (SemanticConceptKind.Individual, SemanticRelationArgumentKind.Individual) => true,
            (SemanticConceptKind.Type, SemanticRelationArgumentKind.Type) => true,
            _ => false
        };

}
