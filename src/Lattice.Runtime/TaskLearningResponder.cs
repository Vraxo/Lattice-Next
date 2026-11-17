using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Lattice.Client;
using Lattice.Core.Generation;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;

namespace Lattice.Runtime;

using static Lattice.Runtime.TaskRequestResponderHelpers;

internal static class TaskLearningResponder
{
    internal static bool TryExplainLearningMethod(
        TaskRequestFormula request,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;

        var method = model.FindConcept(request.Subject, SemanticConceptKind.Individual);
        if (method is null || !reasoner.IsInstanceOf(method.Id, "machine-learning-method"))
        {
            return false;
        }

        var principleAssertions = reasoner.GetRelations(method.Id, "method-has-principle");
        var representationAssertions = reasoner.GetRelations(method.Id, "method-learns-representation");
        var objectiveAssertions = reasoner.GetRelations(method.Id, "method-uses-objective");
        var exampleAssertions = reasoner.GetRelations(method.Id, "method-has-example");
        var applicationAssertions = reasoner.GetRelations(method.Id, "method-used-for");

        if (principleAssertions.Length == 0 || representationAssertions.Length == 0 ||
            objectiveAssertions.Length == 0 || exampleAssertions.Length == 0)
        {
            return false;
        }

        var principle = model.FindConcept(principleAssertions[0].Object, SemanticConceptKind.Individual);
        var representation = model.FindConcept(representationAssertions[0].Object, SemanticConceptKind.Individual);
        var objective = model.FindConcept(objectiveAssertions[0].Object, SemanticConceptKind.Individual);
        var example = model.FindConcept(exampleAssertions[0].Object, SemanticConceptKind.Individual);
        if (principle is null || representation is null || objective is null || example is null)
        {
            return false;
        }

        var pairAssertions = reasoner.GetRelations(principle.Id, "principle-uses-pair");
        var positivePair = pairAssertions
            .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
            .FirstOrDefault(pair => pair is not null &&
                reasoner.GetRelations(pair.Id, "pair-has-role").Any(role =>
                    model.FindConcept(role.Object, SemanticConceptKind.Individual)?.Id == "same-underlying-item"));
        var negativePair = pairAssertions
            .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
            .FirstOrDefault(pair => pair is not null &&
                reasoner.GetRelations(pair.Id, "pair-has-role").Any(role =>
                    model.FindConcept(role.Object, SemanticConceptKind.Individual)?.Id == "different-underlying-items"));

        var positiveEffect = reasoner.GetRelations(objective.Id, "objective-pulls-positive-pairs-together");
        var negativeEffect = reasoner.GetRelations(objective.Id, "objective-pushes-negative-pairs-apart");
        var examplePositive = reasoner.GetRelations(example.Id, "example-demonstrates-positive-pair");
        var exampleNegative = reasoner.GetRelations(example.Id, "example-demonstrates-negative-pair");
        if (positivePair is null || negativePair is null || positiveEffect.IsDefaultOrEmpty ||
            negativeEffect.IsDefaultOrEmpty || examplePositive.IsDefaultOrEmpty ||
            exampleNegative.IsDefaultOrEmpty)
        {
            return false;
        }

        var positiveExamplePair = model.FindConcept(examplePositive[0].Object, SemanticConceptKind.Individual);
        var negativeExamplePair = model.FindConcept(exampleNegative[0].Object, SemanticConceptKind.Individual);
        if (positiveExamplePair is null || negativeExamplePair is null)
        {
            return false;
        }

        var positivePairRole = reasoner.GetRelations(positiveExamplePair.Id, "pair-has-role").FirstOrDefault();
        var negativePairRole = reasoner.GetRelations(negativeExamplePair.Id, "pair-has-role").FirstOrDefault();
        var positiveRole = positivePairRole is null ? null : model.FindConcept(positivePairRole.Object, SemanticConceptKind.Individual);
        var negativeRole = negativePairRole is null ? null : model.FindConcept(negativePairRole.Object, SemanticConceptKind.Individual);
        if (positiveRole is null || negativeRole is null)
        {
            return false;
        }

        var paragraphs = new List<string>
        {
            $"{method.Label} is a machine-learning method built around {principle.Label}. It trains a model to turn each example into an {representation.Label}, a numerical representation the model can compare.",
            $"During training, it compares pairs of examples. A **positive pair** brings together {positiveRole.Label}; a **negative pair** brings together {negativeRole.Label}.",
            $"The training objective encourages embeddings from positive pairs to become more similar and embeddings from negative pairs to become less similar. In other words, the model learns what should count as alike or different in the data.",
            $"For an image example, {example.Label}: {positiveExamplePair.Label} can form a positive pair, while {negativeExamplePair.Label} can form a negative pair."
        };

        var applications = applicationAssertions
            .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
            .Where(concept => concept is not null)
            .Select(concept => concept!.Label)
            .ToArray();
        if (applications.Length > 0)
        {
            paragraphs.Add($"Once trained, the learned representations can be useful for tasks such as {JoinNames(applications)}.");
        }

        paragraphs.Add("The key intuition: examples that belong together should end up close in the embedding space, while examples that differ should end up farther apart.");

        var sources = new List<SemanticWorldModelSource>();
        sources.AddRange(reasoner.GetMembershipEvidence(method.Id, "machine-learning-method"));
        sources.AddRange(principleAssertions.Select(assertion => assertion.Source));
        sources.AddRange(representationAssertions.Select(assertion => assertion.Source));
        sources.AddRange(objectiveAssertions.Select(assertion => assertion.Source));
        sources.AddRange(exampleAssertions.Select(assertion => assertion.Source));
        sources.AddRange(applicationAssertions.Select(assertion => assertion.Source));
        sources.AddRange(pairAssertions.Select(assertion => assertion.Source));
        sources.AddRange(reasoner.GetRelations(positiveExamplePair.Id, "pair-has-role").Select(assertion => assertion.Source));
        sources.AddRange(reasoner.GetRelations(negativeExamplePair.Id, "pair-has-role").Select(assertion => assertion.Source));
        sources.AddRange(positiveEffect.Select(assertion => assertion.Source));
        sources.AddRange(negativeEffect.Select(assertion => assertion.Source));
        sources.AddRange(examplePositive.Select(assertion => assertion.Source));
        sources.AddRange(exampleNegative.Select(assertion => assertion.Source));

        message = string.Join(Environment.NewLine + Environment.NewLine, paragraphs);
        evidence = ImmutableArray.Create(new LatticeEvidence(
            LatticeEvidenceKind.Derived,
            LatticeEvidencePolarity.Positive,
            sources.DistinctBy(source => source.SourceId).Select(ToEvidenceSource).ToImmutableArray(),
            ImmutableArray<LatticeEvidence>.Empty));
        return true;
    }

    internal static bool TryPlanLearningPath(
        TaskRequestFormula request,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;

        if (request.Subject != "search-engine-optimization" ||
            request.OutputFormat != "learning-path" ||
            request.Context is null ||
            !request.Context.Contains("webpage", StringComparison.Ordinal))
        {
            return false;
        }

        var skill = model.FindConcept(request.Subject, SemanticConceptKind.Individual);
        if (skill is null)
        {
            return false;
        }

        var steps = reasoner.GetRelations(skill.Id, "seo-has-learning-step")
            .Select(assertion => new
            {
                Assertion = assertion,
                Step = model.FindConcept(assertion.Object, SemanticConceptKind.Individual)
            })
            .Where(item => item.Step is not null)
            .Select(item => new
            {
                item.Assertion,
                Step = item.Step!,
                Number = model.GetDataAssertions(item.Step!.Id, "learning-step-number").FirstOrDefault()?.Value
            })
            .Select(item => int.TryParse(
                item.Number,
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out var number)
                    ? new { item.Assertion, item.Step, Number = number }
                    : null)
            .Where(item => item is not null)
            .OrderBy(item => item!.Number)
            .ToArray();

        if (steps.Length == 0)
        {
            return false;
        }

        var paragraphs = new List<string>
        {
            "A practical way to learn search-engine optimization (SEO) is to work through your own webpage in stages. Start by understanding how search engines discover and index pages; then improve relevance, usability, and measurement."
        };
        var sources = new List<SemanticWorldModelSource>(
            reasoner.GetMembershipEvidence(skill.Id, "digital-marketing-practice"));

        foreach (var item in steps)
        {
            var focusAssertion = reasoner.GetRelations(item!.Step.Id, "step-focuses-on").FirstOrDefault();
            var practiceAssertion = reasoner.GetRelations(item.Step.Id, "step-has-practice").FirstOrDefault();
            var focus = focusAssertion is null
                ? null
                : model.FindConcept(focusAssertion.Object, SemanticConceptKind.Individual);
            var practice = practiceAssertion is null
                ? null
                : model.FindConcept(practiceAssertion.Object, SemanticConceptKind.Individual);

            if (focus is null || practice is null)
            {
                return false;
            }

            paragraphs.Add($"**{item.Number}. {item.Step.Label}:** Learn about {focus.Label}. Practice: {EnsureSentenceEnding(practice.Label)}");
            sources.Add(item.Assertion.Source);
            sources.Add(focusAssertion!.Source);
            sources.Add(practiceAssertion!.Source);
            sources.AddRange(model.GetDataAssertions(item.Step.Id, "learning-step-number")
                .Select(assertion => assertion.Source));
        }

        paragraphs.Add("Use a free search-engine webmaster tool to inspect indexing and search performance. Change one thing at a time, then compare impressions, clicks, and relevant queries over several weeks. Avoid shortcuts such as keyword stuffing or buying links; aim to make the page genuinely useful to the people searching for it.");
        message = string.Join(Environment.NewLine + Environment.NewLine, paragraphs);
        evidence = ImmutableArray.Create(new LatticeEvidence(
            LatticeEvidenceKind.Derived,
            LatticeEvidencePolarity.Positive,
            sources.DistinctBy(source => source.SourceId).Select(ToEvidenceSource).ToImmutableArray(),
            ImmutableArray<LatticeEvidence>.Empty));
        return true;
    }
}
