using System.Collections.Immutable;
using Lattice.Client;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;

namespace Lattice.Runtime;

using static Lattice.Runtime.TaskRequestResponderHelpers;

/// <summary>
/// Explains how to operate a modelled piece of equipment from its procedure knowledge. This path shares no
/// state with program generation; it answers an explanation request, not a code request.
/// </summary>
internal static class TaskOperationExplanationResponder
{
    internal static bool TryExplainOperation(
        TaskRequestFormula request,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;

        if (request.OutputFormat != "procedure")
        {
            return false;
        }

        var subject = model.FindConcept(request.Subject, SemanticConceptKind.Type);
        if (subject is null)
        {
            return false;
        }

        var procedureMatch = model.Concepts
            .Where(concept => concept.Kind == SemanticConceptKind.Individual &&
                reasoner.IsInstanceOf(concept.Id, "operating-procedure"))
            .Select(procedure => new
            {
                Procedure = procedure,
                SubjectAssertion = reasoner.GetRelations(procedure.Id, "procedure-for")
                    .FirstOrDefault(assertion => assertion.Object == subject.Id)
            })
            .FirstOrDefault(candidate => candidate.SubjectAssertion is not null);

        if (procedureMatch is null)
        {
            return false;
        }

        var steps = reasoner.GetRelations(procedureMatch.Procedure.Id, "procedure-has-step")
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
                Number = model.GetDataAssertions(item.Step!.Id, "step-number").FirstOrDefault()?.Value
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

        var controls = reasoner.GetRelations(subject.Id, "manual-transmission-uses-control")
            .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
            .Where(control => control is not null)
            .Select(control => control!)
            .ToArray();
        var clutchPedal = model.FindConcept("clutch-pedal", SemanticConceptKind.Individual);
        var brakePedal = model.FindConcept("brake-pedal", SemanticConceptKind.Individual);
        var acceleratorPedal = model.FindConcept("accelerator-pedal", SemanticConceptKind.Individual);
        var clutch = model.FindConcept("clutch", SemanticConceptKind.Individual);
        var engine = model.FindConcept("engine", SemanticConceptKind.Individual);
        var gearbox = model.FindConcept("gearbox", SemanticConceptKind.Individual);

        if (controls.Length == 0 || clutchPedal is null || brakePedal is null ||
            acceleratorPedal is null || clutch is null || engine is null || gearbox is null ||
            !reasoner.GetRelations(clutchPedal.Id, "control-operates-component")
                .Any(assertion => assertion.Object == clutch.Id) ||
            !reasoner.GetRelations(clutch.Id, "component-connects-to")
                .Any(assertion => assertion.Object == engine.Id) ||
            !reasoner.GetRelations(clutch.Id, "component-connects-to")
                .Any(assertion => assertion.Object == gearbox.Id))
        {
            return false;
        }

        var clutchFoot = reasoner.GetRelations(clutchPedal.Id, "control-operated-by-foot")
            .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
            .FirstOrDefault(concept => concept is not null);
        var brakeFoot = reasoner.GetRelations(brakePedal.Id, "control-operated-by-foot")
            .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
            .FirstOrDefault(concept => concept is not null);
        var acceleratorFoot = reasoner.GetRelations(acceleratorPedal.Id, "control-operated-by-foot")
            .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
            .FirstOrDefault(concept => concept is not null);

        if (clutchFoot is null || brakeFoot is null || acceleratorFoot is null)
        {
            return false;
        }

        var paragraphs = new List<string>
        {
            $"A manual transmission uses the {JoinNames(controls.Select(control => control.Label).ToArray())}. The clutch pedal is operated with your {clutchFoot.Label}, while the brake and accelerator are operated with your {brakeFoot.Label}. Pressing the clutch disengages the engine from the gearbox so you can change gears."
        };
        var sources = new List<SemanticWorldModelSource>
        {
            procedureMatch.SubjectAssertion!.Source
        };
        sources.AddRange(reasoner.GetMembershipEvidence(procedureMatch.Procedure.Id, "operating-procedure"));
        sources.AddRange(reasoner.GetRelations(subject.Id, "manual-transmission-uses-control")
            .Select(assertion => assertion.Source));
        sources.AddRange(reasoner.GetMembershipEvidence(clutchPedal.Id, "vehicle-control"));
        sources.AddRange(reasoner.GetMembershipEvidence(brakePedal.Id, "vehicle-control"));
        sources.AddRange(reasoner.GetMembershipEvidence(acceleratorPedal.Id, "vehicle-control"));
        sources.AddRange(reasoner.GetRelations(clutchPedal.Id, "control-operated-by-foot")
            .Select(assertion => assertion.Source));
        sources.AddRange(reasoner.GetRelations(brakePedal.Id, "control-operated-by-foot")
            .Select(assertion => assertion.Source));
        sources.AddRange(reasoner.GetRelations(acceleratorPedal.Id, "control-operated-by-foot")
            .Select(assertion => assertion.Source));
        sources.AddRange(reasoner.GetRelations(clutchPedal.Id, "control-operates-component")
            .Select(assertion => assertion.Source));
        sources.AddRange(reasoner.GetRelations(clutch.Id, "component-connects-to")
            .Select(assertion => assertion.Source));

        foreach (var step in steps)
        {
            var actionAssertion = reasoner.GetRelations(step!.Step.Id, "step-action").FirstOrDefault();
            var action = actionAssertion is null
                ? null
                : model.FindConcept(actionAssertion.Object, SemanticConceptKind.Individual);
            if (action is null)
            {
                return false;
            }

            paragraphs.Add($"**{step.Number}. {step.Step.Label}:** {EnsureSentenceEnding(action.Label)}");
            sources.Add(step.Assertion.Source);
            sources.AddRange(model.GetDataAssertions(step.Step.Id, "step-number")
                .Select(assertion => assertion.Source));
            sources.Add(actionAssertion!.Source);
            sources.AddRange(reasoner.GetMembershipEvidence(step.Step.Id, "procedure-step"));
            sources.AddRange(reasoner.GetMembershipEvidence(action.Id, "operation-action"));
        }

        var safetyNotes = reasoner.GetRelations(procedureMatch.Procedure.Id, "procedure-has-safety-note");
        if (safetyNotes.IsDefaultOrEmpty)
        {
            return false;
        }

        paragraphs.Add("**Safety reminders:**");
        foreach (var assertion in safetyNotes)
        {
            var note = model.FindConcept(assertion.Object, SemanticConceptKind.Individual);
            if (note is null)
            {
                return false;
            }

            paragraphs.Add($"- {EnsureSentenceEnding(note.Label)}");
            sources.Add(assertion.Source);
            sources.AddRange(reasoner.GetMembershipEvidence(note.Id, "safety-note"));
        }

        message = string.Join(Environment.NewLine + Environment.NewLine, paragraphs);
        evidence = ImmutableArray.Create(new LatticeEvidence(
            LatticeEvidenceKind.Derived,
            LatticeEvidencePolarity.Positive,
            sources
                .DistinctBy(source => source.SourceId)
                .Select(ToEvidenceSource)
                .ToImmutableArray(),
            ImmutableArray<LatticeEvidence>.Empty));
        return true;
    }
}
