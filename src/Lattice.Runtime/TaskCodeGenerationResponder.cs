using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Lattice.Client;
using Lattice.Core.Generation;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;
using Lattice.Runtime.Generation;

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

        if (request.Program is not null &&
            request.TargetLanguage is { Length: > 0 } targetLanguage)
        {
            if (!ProgramBackendCatalog.TryGenerate(
                targetLanguage,
                request.Program,
                out var source,
                out _))
            {
                return false;
            }

            var fence = new string((char)96, 3);
            message = string.Join(
                Environment.NewLine,
                fence + targetLanguage,
                source,
                fence);
            return true;
        }

        if (request.Subject == "python-program" &&
            request.Context == "python" &&
            request.OutputFormat == "python-source" &&
            !request.GenerationSteps.IsDefaultOrEmpty)
        {
            var loader = new GenerationLanguagePackLoader();
            if (!loader.TryLoadEmbedded(out var packs, out _) ||
                !packs.TryGetValue(request.Context, out var pack))
            {
                return false;
            }

            var goals = request.GenerationSteps
                .Select(step => new GenerationGoal(
                    step.Effect,
                    step.ArgumentKind,
                    step.ArgumentValue,
                    ImmutableArray<string>.Empty))
                .ToImmutableArray();

            if (!new SequenceGenerationPipeline().TryGenerateProgram(
                pack,
                goals,
                out var program,
                out _))
            {
                return false;
            }

            var fence = new string((char)96, 3);
            message = string.Join(
                Environment.NewLine,
                fence + request.Context,
                program!.Text,
                fence);
            return true;
        }

        if (request.Subject == "python-program" &&
            request.Context == "python" &&
            request.OutputFormat == "python-source" &&
            request.Scope == "write-standard-output" &&
            request.RequestedValue is { Length: > 0 } requestedValue)
        {
            var loader = new GenerationLanguagePackLoader();
            if (!loader.TryLoadEmbedded(out var packs, out _) ||
                !packs.TryGetValue(request.Context, out var pack) ||
                !new SequenceGenerationPipeline().TryGenerate(
                    pack,
                    new GenerationGoal(
                        request.Scope,
                        "text",
                        requestedValue,
                        ImmutableArray<string>.Empty),
                    out var generated,
                    out _))
            {
                return false;
            }

            var fence = new string((char)96, 3);
            message = string.Join(
                Environment.NewLine,
                fence + request.Context,
                generated!.Text,
                fence);
            return true;
        }

        if (request.OutputFormat != "python-script" ||
            request.Context != "blender" ||
            request.Subject != "blender-object-replacement" ||
            request.Scope != "object-to-point-light")
        {
            return false;
        }

        var task = model.FindConcept("blender-object-replacement", SemanticConceptKind.Individual);
        if (task is null || !reasoner.IsInstanceOf(task.Id, "blender-code-generation-task"))
        {
            return false;
        }

        var stepAssertions = reasoner.GetRelations(task.Id, "script-has-step");
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
                Number = model.GetDataAssertions(item.Step!.Id, "script-step-number").FirstOrDefault()?.Value,
                Operation = reasoner.GetRelations(item.Step!.Id, "step-uses-operation").FirstOrDefault(),
                Api = reasoner.GetRelations(item.Step!.Id, "step-uses-api").FirstOrDefault()
            })
            .Select(item => int.TryParse(
                item.Number,
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out var number) && item.Operation is not null && item.Api is not null
                    ? new { item.Assertion, item.Step, item.Operation, item.Api, Number = number }
                    : null)
            .Where(item => item is not null)
            .OrderBy(item => item!.Number)
            .ToArray();

        var codeBlocks = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["op-snapshot-objects"] = "objects = list(bpy.data.objects)",
            ["op-iterate-objects"] = "for obj in objects:",
            ["op-match-name"] = "    if name_pattern.fullmatch(obj.name) is None:\n        continue",
            ["op-create-light-data"] = "    light_data = bpy.data.lights.new(name=f\"{obj.name}_Point\", type=\"POINT\")",
            ["op-create-light-object"] = "    light_obj = bpy.data.objects.new(name=f\"{obj.name}_Point\", object_data=light_data)",
            ["op-copy-world-position"] = "    light_obj.location = obj.matrix_world.translation.copy()",
            ["op-link-collections"] = "    collections = list(obj.users_collection)\n    if collections:\n        for collection in collections:\n            collection.objects.link(light_obj)\n    else:\n        bpy.context.scene.collection.objects.link(light_obj)",
            ["op-remove-original"] = "    bpy.data.objects.remove(obj, do_unlink=True)"
        };

        if (steps.Length != codeBlocks.Count ||
            steps.Select(item => item!.Operation!.Object).Distinct(StringComparer.Ordinal).Count() != codeBlocks.Count)
        {
            return false;
        }

        var codeLines = new List<string>();
        var sources = new List<SemanticWorldModelSource>();
        sources.AddRange(reasoner.GetMembershipEvidence(task.Id, "blender-code-generation-task"));

        foreach (var step in steps)
        {
            var operation = model.FindConcept(step!.Operation!.Object, SemanticConceptKind.Individual);
            var api = model.FindConcept(step.Api!.Object, SemanticConceptKind.Individual);
            if (operation is null || api is null ||
                !reasoner.IsInstanceOf(operation.Id, "python-code-operation") ||
                !reasoner.IsInstanceOf(api.Id, "blender-api") ||
                !codeBlocks.TryGetValue(operation.Id, out var block))
            {
                return false;
            }

            codeLines.Add(block);
            sources.Add(step.Assertion.Source);
            sources.AddRange(model.GetDataAssertions(step.Step.Id, "script-step-number")
                .Select(assertion => assertion.Source));
            sources.Add(step.Operation.Source);
            sources.Add(step.Api.Source);
            sources.AddRange(reasoner.GetMembershipEvidence(step.Step.Id, "blender-script-step"));
            sources.AddRange(reasoner.GetMembershipEvidence(operation.Id, "python-code-operation"));
            sources.AddRange(reasoner.GetMembershipEvidence(api.Id, "blender-api"));
        }

        var code = string.Join(Environment.NewLine, new[]
        {
            "import bpy",
            "import re",
            "",
            "target_name = \"YourObjectName\"",
            "name_pattern = re.compile(rf\"{re.escape(target_name)}(?:\\.\\d+)?$\")",
            "",
            string.Join(Environment.NewLine, codeLines)
        });

        message = string.Join(Environment.NewLine + Environment.NewLine, new[]
        {
            "## Blender object replacement",
            "Set `target_name` to the exact object name you want to replace. The script also matches Blender's numbered duplicates, such as `.001` and `.002`.",
            "### Script",
            "```python",
            code,
            "```",
            "### What it does",
            "- Finds every object with the selected name, including numbered duplicates.",
            "- Creates a point light at each object's world-space position.",
            "- Links each light into the same collections as its original object.",
            "- Removes the original object after creating and linking its replacement.",
            "**Before running:** Save a copy of your Blender file. The original objects are removed."
        });

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
