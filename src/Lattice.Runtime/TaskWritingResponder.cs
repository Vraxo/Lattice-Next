using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Lattice.Client;
using Lattice.Core.Generation;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;

namespace Lattice.Runtime;

using static Lattice.Runtime.TaskRequestResponderHelpers;

internal static class TaskWritingResponder
{
    internal static bool TryWrite(
        TaskRequestFormula request,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;

        if (SemanticWorldModel.Normalize(request.Subject) != "monopsony" ||
            !SemanticWorldModelResponder.TryDefinition(
                "monopsony",
                model,
                reasoner,
                out var definition,
                out var definitionEvidence))
        {
            return false;
        }

        if (!SemanticWorldModelResponder.TryRespond(
            new SemanticWorldModelQuestionFormula(
                SemanticWorldModelQuestionKind.Causes,
                "monopsony-power"),
            model,
            reasoner,
            out var causes,
            out var causesEvidence))
        {
            return false;
        }

        var buyerMarketStructure = model.FindConcept("buyer-market-structure", SemanticConceptKind.Type);
        var wageSettingPower = model.FindConcept("wage-setting-power", SemanticConceptKind.Type);
        var wageMarkdown = model.FindConcept("wage-markdown", SemanticConceptKind.Type);
        var powerDefinition = model.FindDefinition("monopsony-power");
        if (buyerMarketStructure is null ||
            wageSettingPower is null ||
            wageMarkdown is null ||
            powerDefinition is null ||
            !model.RelationAssertions.Any(assertion =>
                assertion.Subject == powerDefinition.Concept &&
                assertion.Relation == "is-a" &&
                assertion.Object == wageSettingPower.Id) ||
            !powerDefinition.SufficientConditions.Any(condition =>
                condition.Relation == "has-market-power-effect" &&
                condition.TargetType == wageMarkdown.Id))
        {
            return false;
        }

        var paragraphs = new List<string>
        {
            definition,
            $"The model distinguishes monopsony as a {buyerMarketStructure.Label} from monopsony power, a {wageSettingPower.Label} whose modeled effect is a {wageMarkdown.Label}."
        };
        var sources = definitionEvidence
            .Concat(causesEvidence)
            .SelectMany(item => item.Sources)
            .ToList();

        if (request.Requirements.Contains("examples"))
        {
            if (!TryDescribeRuralMarket(model, reasoner, out var ruralExample, out var ruralSources) ||
                !TryDescribeRegionalMarket(model, reasoner, out var regionalExample, out var regionalSources))
            {
                return false;
            }

            paragraphs.Add(ruralExample);
            paragraphs.Add(regionalExample);
            sources.AddRange(ruralSources.Select(ToEvidenceSource));
            sources.AddRange(regionalSources.Select(ToEvidenceSource));
        }

        paragraphs.Add(causes);

        if (request.Requirements.Contains("research-citations") &&
            !causes.Contains("Research references include", StringComparison.Ordinal))
        {
            return false;
        }

        message = string.Join(" ", paragraphs);
        evidence = ImmutableArray.Create(new LatticeEvidence(
            LatticeEvidenceKind.Derived,
            LatticeEvidencePolarity.Positive,
            sources
                .DistinctBy(source => $"{source.Kind}:{source.SourceId}")
                .ToImmutableArray(),
            ImmutableArray<LatticeEvidence>.Empty));
        return true;
    }
}
