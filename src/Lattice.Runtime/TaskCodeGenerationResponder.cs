using System.Collections.Immutable;
using Lattice.Client;
using Lattice.Core.Reasoning;
using Lattice.Core.Generation;
using Lattice.Core.Semantics;

namespace Lattice.Runtime;

using static Lattice.Runtime.TaskRequestResponderHelpers;

internal static class TaskCodeGenerationResponder
{
    internal static bool TryGenerateCode(
        TaskRequestFormula request,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;
        Console.WriteLine($"CODEGEN start: {request.Subject}|{request.Context}|{request.Scope}");

        if (request.Scope != "program-generation")
        {
            return false;
        }

        var task = model.FindConcept(request.Subject, SemanticConceptKind.Individual);
        var application = model.FindConcept(request.Context, SemanticConceptKind.Individual);
        if (task is null ||
            application is null ||
            !reasoner.IsInstanceOf(task.Id, "blender-codegen-code-generation-task") ||
            !reasoner.IsInstanceOf(application.Id, "blender-codegen-software-application"))
        {
            Console.WriteLine($"CODEGEN task/app fail: task={task?.Id}; app={application?.Id}");
            return false;
        }
        Console.WriteLine("CODEGEN task/app ok");

        var languageAssertions = reasoner.GetRelations(application.Id, "blender-codegen-application-uses-language");
        var languageAssertion = languageAssertions.Length == 1 ? languageAssertions[0] : null;
        var language = languageAssertion is null
            ? null
            : model.FindConcept(languageAssertion.Object, SemanticConceptKind.Individual);
        if (language is null || !reasoner.IsInstanceOf(language.Id, "programming-language"))
        {
            Console.WriteLine($"CODEGEN language fail: assertions={languageAssertions.Length}; language={language?.Id}");
            return false;
        }
        Console.WriteLine($"CODEGEN language ok: {language.Id}");

        var stepAssertions = reasoner.GetRelations(task.Id, "blender-codegen-has-generation-step");
        var steps = stepAssertions
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
                Order = model.GetDataAssertions(item.Step!.Id, "blender-codegen-generation-step-order")
                    .FirstOrDefault()?.Value
            })
            .Select(item => int.TryParse(
                item.Order,
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out var order)
                    ? new { item.Assertion, item.Step, Order = order }
                    : null)
            .Where(item => item is not null)
            .OrderBy(item => item!.Order)
            .ToArray();

        if (steps.Length == 0 ||
            steps.Length != stepAssertions.Length ||
            steps.Select(step => step!.Order).Distinct().Count() != steps.Length)
        {
            Console.WriteLine($"CODEGEN steps fail: {steps.Length}/{stepAssertions.Length}");
            return false;
        }
        Console.WriteLine($"CODEGEN steps ok: {steps.Length}");

        var goals = ImmutableArray.CreateBuilder<GenerationGoal>(steps.Length);
        var sources = new List<SemanticWorldModelSource>();
        sources.AddRange(reasoner.GetMembershipEvidence(task.Id, "blender-codegen-code-generation-task"));
        sources.AddRange(reasoner.GetMembershipEvidence(application.Id, "blender-codegen-software-application"));
        sources.AddRange(reasoner.GetMembershipEvidence(language.Id, "programming-language"));
        sources.Add(languageAssertion!.Source);

        foreach (var step in steps)
        {
            Console.WriteLine($"CODEGEN step: {step!.Step.Id}");
            var effectAssertions = reasoner.GetRelations(step!.Step.Id, "blender-codegen-step-has-generation-effect");
            var argumentKindAssertions = reasoner.GetRelations(step.Step.Id, "blender-codegen-step-has-argument-kind");
            var effectAssertion = effectAssertions.Length == 1 ? effectAssertions[0] : null;
            var argumentKindAssertion = argumentKindAssertions.Length == 1 ? argumentKindAssertions[0] : null;
            var effect = effectAssertion is null
                ? null
                : model.FindConcept(effectAssertion.Object, SemanticConceptKind.Individual);
            var argumentKind = argumentKindAssertion is null
                ? null
                : model.FindConcept(argumentKindAssertion.Object, SemanticConceptKind.Individual);
            if (effect is null || argumentKind is null ||
                !reasoner.IsInstanceOf(effect.Id, "blender-codegen-generation-effect") ||
                !reasoner.IsInstanceOf(argumentKind.Id, "blender-codegen-generation-argument-kind"))
            {
                Console.WriteLine($"CODEGEN effect/kind fail: {step.Step.Id}; effects={effectAssertions.Length}; kinds={argumentKindAssertions.Length}; effect={effect?.Id}; kind={argumentKind?.Id}");
                return false;
            }

            var bindings = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
            foreach (var bindingAssertion in reasoner.GetRelations(step.Step.Id, "blender-codegen-step-has-binding"))
            {
                var binding = model.FindConcept(bindingAssertion.Object, SemanticConceptKind.Individual);
                var valueAssertions = binding is null
                    ? ImmutableArray<SemanticRelationAssertion>.Empty
                    : reasoner.GetRelations(binding.Id, "blender-codegen-binding-has-value");
                var valueAssertion = valueAssertions.Length == 1 ? valueAssertions[0] : null;
                var value = valueAssertion is null
                    ? null
                    : model.FindConcept(valueAssertion.Object, SemanticConceptKind.Individual);
                if (binding is null || value is null ||
                    !reasoner.IsInstanceOf(binding.Id, "blender-codegen-generation-binding") ||
                    !reasoner.IsInstanceOf(value.Id, "blender-codegen-generation-binding-value") ||
                    bindings.ContainsKey(binding.Label))
                {
                    Console.WriteLine($"CODEGEN binding fail: {step.Step.Id}; binding={binding?.Id}; value={value?.Id}");
                    return false;
                }

                bindings.Add(binding.Label, value.Label);
                sources.Add(bindingAssertion.Source);
                sources.Add(valueAssertion!.Source);
                sources.AddRange(reasoner.GetMembershipEvidence(binding.Id, "blender-codegen-generation-binding"));
                sources.AddRange(reasoner.GetMembershipEvidence(value.Id, "blender-codegen-generation-binding-value"));
            }

            goals.Add(new GenerationGoal(
                SemanticWorldModel.Normalize(effect.Label),
                SemanticWorldModel.Normalize(argumentKind.Label),
                step.Step.Label,
                ImmutableArray<string>.Empty)
            {
                Bindings = bindings.ToImmutable()
            });

            sources.Add(step.Assertion.Source);
            sources.AddRange(model.GetDataAssertions(step.Step.Id, "blender-codegen-generation-step-order")
                .Select(assertion => assertion.Source));
            sources.Add(effectAssertion!.Source);
            sources.Add(argumentKindAssertion!.Source);
            sources.AddRange(reasoner.GetMembershipEvidence(step.Step.Id, "blender-codegen-generation-step"));
            sources.AddRange(reasoner.GetMembershipEvidence(effect.Id, "blender-codegen-generation-effect"));
            sources.AddRange(reasoner.GetMembershipEvidence(argumentKind.Id, "blender-codegen-generation-argument-kind"));

            foreach (var apiAssertion in reasoner.GetRelations(step.Step.Id, "blender-codegen-step-references-api"))
            {
                var api = model.FindConcept(apiAssertion.Object, SemanticConceptKind.Individual);
                if (api is null || !reasoner.IsInstanceOf(api.Id, "application-programming-interface"))
                {
                    Console.WriteLine($"CODEGEN API fail: {step.Step.Id}; api={api?.Id}");
                    return false;
                }

                sources.Add(apiAssertion.Source);
                sources.AddRange(reasoner.GetMembershipEvidence(api.Id, "application-programming-interface"));
            }
        }

        Console.WriteLine($"CODEGEN goals ok: {goals.Count}");
        var loader = new GenerationLanguagePackLoader();
        if (!loader.TryLoadEmbedded(out var packs, out var loadError))
        {
            Console.WriteLine($"CODEGEN pack load fail: {loadError}");
            return false;
        }
        if (!packs.TryGetValue(language.Id, out var pack))
        {
            Console.WriteLine($"CODEGEN pack missing: {language.Id}");
            return false;
        }
        if (!new SequenceGenerationPipeline().TryGenerateProgram(
                pack,
                goals.ToImmutable(),
                out var program,
                out var generationError))
        {
            Console.WriteLine($"CODEGEN generation fail: {generationError}");
            return false;
        }
        Console.WriteLine("CODEGEN generation ok");

        var safetyNotes = reasoner.GetRelations(task.Id, "blender-codegen-code-generation-task-has-safety-note")
            .Select(assertion => new
            {
                Assertion = assertion,
                Note = model.FindConcept(assertion.Object, SemanticConceptKind.Individual)
            })
            .Where(item => item.Note is not null)
            .ToArray();
        if (safetyNotes.Any(item => !reasoner.IsInstanceOf(item.Note!.Id, "blender-codegen-code-generation-safety-note")))
        {
            Console.WriteLine("CODEGEN safety note fail");
            return false;
        }
        Console.WriteLine($"CODEGEN safety ok: {safetyNotes.Length}");

        var fence = new string((char)96, 3);
        var messageLines = new List<string>
        {
            $"## {task.Label}",
            "### Script",
            fence + language.Id,
            program!.Text,
            fence,
            "### Steps"
        };
        messageLines.AddRange(steps.Select(step => $"- {step!.Step.Label}"));
        if (safetyNotes.Length > 0)
        {
            messageLines.Add("### Safety");
            messageLines.AddRange(safetyNotes.Select(item => $"- {item.Note!.Label}"));
            sources.AddRange(safetyNotes.Select(item => item.Assertion.Source));
            sources.AddRange(safetyNotes.SelectMany(item =>
                reasoner.GetMembershipEvidence(item.Note!.Id, "blender-codegen-code-generation-safety-note")));
        }

        message = string.Join(Environment.NewLine, messageLines);
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
