using System.Collections.Immutable;
using System.Globalization;
using Lattice.Client;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;

namespace Lattice.Runtime;
using static Lattice.Runtime.SemanticWorldModelResponseFormatting;

internal static class SemanticContextResponder
{

internal static bool TryExplainProductionLocationReasons(
        string activityId,
        string locationId,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;

        var activity = model.FindConcept(activityId, SemanticConceptKind.Type);
        var location = model.FindConcept(locationId, SemanticConceptKind.Individual);
        if (activity is null || location is null)
        {
            return false;
        }

        var locationMembership = reasoner.GetMembershipEvidence(location.Id, "geographic-region");
        var advantages = reasoner.GetRelations(location.Id, "has-advantage")
            .Select(assertion => new
            {
                Assertion = assertion,
                Advantage = model.FindConcept(assertion.Object, SemanticConceptKind.Individual)
            })
            .Where(candidate =>
                candidate.Advantage is not null &&
                reasoner.IsInstanceOf(candidate.Advantage.Id, "production-advantage") &&
                reasoner.GetRelations(candidate.Advantage.Id, "advantage-for-activity")
                    .Any(assertion => assertion.Object == activity.Id))
            .ToArray();

        if (advantages.Length == 0 || locationMembership.IsDefaultOrEmpty)
        {
            return false;
        }

        var paragraphs = new List<string>
        {
            $"{Capitalize(activity.Label)} productions are often made in {location.Label} because it offers several financial and practical advantages:"
        };
        var sources = new List<SemanticWorldModelSource>(locationMembership);

        foreach (var candidate in advantages)
        {
            var advantage = candidate.Advantage!;
            var activityAssertions = reasoner.GetRelations(advantage.Id, "advantage-for-activity")
                .Where(assertion => assertion.Object == activity.Id)
                .ToArray();
            var details = reasoner.GetRelations(advantage.Id, "advantage-has-detail")
                .Select(assertion => new
                {
                    Assertion = assertion,
                    Detail = model.FindConcept(assertion.Object, SemanticConceptKind.Individual)
                })
                .Where(detail =>
                    detail.Detail is not null &&
                    reasoner.IsInstanceOf(detail.Detail.Id, "production-detail"))
                .ToArray();

            if (activityAssertions.Length == 0 || details.Length == 0)
            {
                return false;
            }

            paragraphs.Add(
                $"**{Capitalize(advantage.Label)}:** {string.Join("; ", details.Select(detail => detail.Detail!.Label))}.");
            sources.Add(candidate.Assertion.Source);
            sources.AddRange(reasoner.GetMembershipEvidence(advantage.Id, "production-advantage"));
            sources.AddRange(activityAssertions.Select(assertion => assertion.Source));
            sources.AddRange(details.Select(detail => detail.Assertion.Source));
            foreach (var detail in details)
            {
                sources.AddRange(reasoner.GetMembershipEvidence(detail.Detail!.Id, "production-detail"));
            }
        }

        message = string.Join(Environment.NewLine + Environment.NewLine, paragraphs);
        evidence = ImmutableArray.Create(ToEvidence(
            sources.Distinct().ToImmutableArray(),
            LatticeEvidenceKind.Derived));
        return true;
    }

internal static bool TryContextualRelationRoleQuery(
        string subjectId,
        string rolePhrase,
        string contextId,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;

        var subject = model.FindConcept(subjectId, SemanticConceptKind.Individual);
        var context = model.FindConcept(contextId, SemanticConceptKind.Individual);
        if (subject is null || context is null)
        {
            return false;
        }

        var normalizedRole = rolePhrase.Replace(' ', '-');
        var roleRelations = model.Relations
            .Where(relation =>
                relation.SubjectKind == SemanticRelationArgumentKind.Individual &&
                relation.ObjectKind == SemanticRelationArgumentKind.Individual &&
                relation.Verb.StartsWith("is ", StringComparison.OrdinalIgnoreCase) &&
                relation.Verb.EndsWith(" of", StringComparison.OrdinalIgnoreCase))
            .Select(relation => new
            {
                Relation = relation,
                Domain = relation.DomainType is null
                    ? null
                    : model.FindConcept(relation.DomainType, SemanticConceptKind.Type),
                Range = relation.RangeType is null
                    ? null
                    : model.FindConcept(relation.RangeType, SemanticConceptKind.Type),
                Role = relation.Verb[3..^3].Replace(' ', '-')
            })
            .Where(candidate =>
                candidate.Domain is not null &&
                candidate.Range is not null &&
                normalizedRole.EndsWith(candidate.Role, StringComparison.OrdinalIgnoreCase) &&
                reasoner.IsInstanceOf(subject.Id, candidate.Range.Id))
            .ToArray();

        if (roleRelations.Length != 1)
        {
            return false;
        }

        var roleRelation = roleRelations[0];
        var candidates = model.RelationAssertions
            .Where(assertion =>
                assertion.Relation == roleRelation.Relation.Id &&
                assertion.Object == subject.Id)
            .Select(assertion => new
            {
                Assertion = assertion,
                Candidate = model.FindConcept(assertion.Subject, SemanticConceptKind.Individual)
            })
            .Where(candidate =>
                candidate.Candidate is not null &&
                reasoner.IsInstanceOf(candidate.Candidate.Id, roleRelation.Domain!.Id))
            .ToArray();

        var contextRelations = model.Relations
            .Where(relation =>
                relation.SubjectKind == SemanticRelationArgumentKind.Individual &&
                relation.ObjectKind == SemanticRelationArgumentKind.Individual)
            .Select(relation => new
            {
                Relation = relation,
                Domain = relation.DomainType is null
                    ? null
                    : model.FindConcept(relation.DomainType, SemanticConceptKind.Type),
                Range = relation.RangeType is null
                    ? null
                    : model.FindConcept(relation.RangeType, SemanticConceptKind.Type)
            })
            .Where(candidate =>
                candidate.Domain is not null &&
                candidate.Range is not null &&
                reasoner.IsInstanceOf(context.Id, candidate.Range.Id))
            .ToArray();

        var matches = candidates
            .Select(candidate => new
            {
                candidate.Assertion,
                Candidate = candidate.Candidate!,
                ContextMatches = contextRelations
                    .Where(contextRelation =>
                        reasoner.IsInstanceOf(candidate.Candidate!.Id, contextRelation.Domain!.Id))
                    .SelectMany(contextRelation => model.RelationAssertions
                        .Where(assertion =>
                            assertion.Subject == candidate.Candidate!.Id &&
                            assertion.Relation == contextRelation.Relation.Id &&
                            assertion.Object == context.Id)
                        .Select(assertion => new
                        {
                            Assertion = assertion,
                            Relation = contextRelation
                        }))
                    .ToArray()
            })
            .Where(candidate => candidate.ContextMatches.Length == 1)
            .ToArray();

        if (matches.Length != 1)
        {
            return false;
        }

        var match = matches[0];
        var contextMatch = match.ContextMatches[0];
        var roleText = $"is the {roleRelation.Relation.Verb[3..]}";
        message = $"{match.Candidate.Label} {roleText} the {subject.Label} in {context.Label}.";

        var sources = new List<SemanticWorldModelSource>
        {
            match.Assertion.Source,
            contextMatch.Assertion.Source
        };
        sources.AddRange(reasoner.GetMembershipEvidence(match.Candidate.Id, roleRelation.Domain!.Id));
        sources.AddRange(reasoner.GetMembershipEvidence(subject.Id, roleRelation.Range!.Id));
        sources.AddRange(reasoner.GetMembershipEvidence(match.Candidate.Id, contextMatch.Relation.Domain!.Id));
        sources.AddRange(reasoner.GetMembershipEvidence(context.Id, contextMatch.Relation.Range!.Id));
        evidence = ImmutableArray.Create(ToEvidence(
            sources.Distinct().ToImmutableArray(),
            LatticeEvidenceKind.Derived));
        return true;
    }

internal static bool TryRegionalName(
        SemanticWorldModelQuestionFormula request,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;

        var subject = model.FindConcept(request.Subject, SemanticConceptKind.Individual);
        var variant = model.FindConcept(request.TargetType ?? string.Empty, SemanticConceptKind.Individual);
        if (subject is null || variant is null)
        {
            return false;
        }

        var alternateNames = reasoner.GetRelations(subject.Id, "has-alternate-name");
        var matches = alternateNames
            .Select(assertion => (
                Assertion: assertion,
                Term: model.FindConcept(assertion.Object, SemanticConceptKind.Individual)))
            .Where(item =>
                item.Term is not null &&
                reasoner.IsInstanceOf(item.Term.Id, "golf-term"))
            .Select(item => (
                item.Assertion,
                Term: item.Term!,
                VariantAssertions: reasoner.GetRelations(item.Term!.Id, "term-used-in-variant")
                    .Where(assertion => assertion.Object == variant.Id)
                    .ToArray()))
            .Where(item => item.VariantAssertions.Length > 0)
            .ToArray();
        if (matches.Length != 1)
        {
            return false;
        }

        var match = matches[0];
        var sources = new List<SemanticWorldModelSource>
        {
            match.Assertion.Source,
            match.VariantAssertions[0].Source
        };
        sources.AddRange(reasoner.GetMembershipEvidence(subject.Id, "golf-score"));
        sources.AddRange(reasoner.GetMembershipEvidence(match.Term.Id, "golf-term"));
        sources.AddRange(reasoner.GetMembershipEvidence(variant.Id, "language-variety"));

        var scoreProperty = model.FindDataProperty("strokes-relative-to-par");
        var scoreAssertions = scoreProperty is null
            ? []
            : model.GetDataAssertions(subject.Id, scoreProperty.Id);
        var scoreContext = string.Empty;
        if (scoreAssertions.Length == 1 &&
            decimal.TryParse(scoreAssertions[0].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var score) &&
            score == -3)
        {
            scoreContext = " It refers to a score of three under par on a single hole.";
            sources.Add(scoreAssertions[0].Source);
        }

        message = $"In {variant.Label}, {subject.Label} is called {match.Term.Label}.{scoreContext}";
        evidence = ImmutableArray.Create(ToEvidence(
            sources.Distinct().ToImmutableArray(),
            LatticeEvidenceKind.Derived));
        return true;
    }

internal static bool TryIngredients(
        SemanticWorldModelQuestionFormula request,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;

        var dish = model.FindConcept(request.Subject, SemanticConceptKind.Individual);
        if (dish is null)
        {
            return false;
        }

        var commonAssertions = reasoner.GetRelations(dish.Id, "has-common-ingredient");
        var optionalAssertions = reasoner.GetRelations(dish.Id, "may-include-ingredient");
        var common = ResolveIngredients(commonAssertions, model, reasoner);
        var optional = ResolveIngredients(optionalAssertions, model, reasoner);
        if (common.Length == 0)
        {
            return false;
        }

        var commonNames = JoinNames(common.Select(item => item.Ingredient.Label).ToArray());
        var optionalSentence = optional.Length == 0
            ? string.Empty
            : $" It may also include {JoinNames(optional.Select(item => item.Ingredient.Label).ToArray())}.";
        message = $"{Capitalize(dish.Label)} commonly includes {commonNames}.{optionalSentence} Exact combinations vary by region and personal preference.";

        var sources = common.SelectMany(item => new[]
            {
                item.Assertion.Source
            }.Concat(reasoner.GetMembershipEvidence(item.Ingredient.Id, "ingredient")))
            .Concat(optional.SelectMany(item => new[]
            {
                item.Assertion.Source
            }.Concat(reasoner.GetMembershipEvidence(item.Ingredient.Id, "ingredient"))))
            .Concat(reasoner.GetMembershipEvidence(dish.Id, "breakfast-dish"))
            .Distinct()
            .ToImmutableArray();
        if (sources.IsDefaultOrEmpty)
        {
            return false;
        }

        evidence = ImmutableArray.Create(ToEvidence(sources, LatticeEvidenceKind.Derived));
        return true;
    }

    private static ImmutableArray<(SemanticConcept Ingredient, SemanticRelationAssertion Assertion)> ResolveIngredients(
        ImmutableArray<SemanticRelationAssertion> assertions,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner) =>
        assertions
            .Select(assertion => (
                Ingredient: model.FindConcept(assertion.Object, SemanticConceptKind.Individual),
                Assertion: assertion))
            .Where(item =>
                item.Ingredient is not null &&
                reasoner.IsInstanceOf(item.Ingredient.Id, "ingredient"))
            .Select(item => (item.Ingredient!, item.Assertion))
            .ToImmutableArray();
}
