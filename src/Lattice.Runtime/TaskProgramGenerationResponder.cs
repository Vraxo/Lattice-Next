using System.Collections.Immutable;
using Lattice.Client;
using Lattice.Core.Reasoning;
using Lattice.Core.Generation;
using Lattice.Core.Semantics;

namespace Lattice.Runtime;

using static Lattice.Runtime.TaskRequestResponderHelpers;

/// <summary>
/// The one entry point for every request that asks for a program or a script, whichever application it names.
/// Nothing here knows an application, a task, or an API: the request supplies the task and the application,
/// the domain knowledge supplies the steps, their effects, their argument kinds, their bindings, the APIs they
/// reach, and the safety notes, and the selected language pack supplies the syntax. A request whose task or
/// application the knowledge does not model is refused so that the caller reports it as unavailable instead of
/// inventing a program.
/// </summary>
internal static class TaskProgramGenerationResponder
{
    internal static bool TryGenerateProgram(
        TaskRequestFormula request,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;

        if (request.Scope != "program-generation")
        {
            return false;
        }

        var task = model.FindConcept(request.Subject, SemanticConceptKind.Individual);
        var slotValues = ImmutableDictionary.Create<string, string>(StringComparer.Ordinal);
        if (task is null &&
            model.TryMatchLexeme(request.Phrase, out var matchedTask, out var matchedLexeme, out var captures))
        {
            if (!TryResolveSlots(model, reasoner, matchedLexeme!, captures, out slotValues))
            {
                return false;
            }

            task = matchedTask;
        }

        var application = model.FindConcept(request.Context, SemanticConceptKind.Individual);
        if (task is null ||
            application is null ||
            !reasoner.IsInstanceOf(task.Id, "code-generation-task") ||
            !reasoner.IsInstanceOf(application.Id, "software-application"))
        {
            return false;
        }

        var languageAssertions = reasoner.GetRelations(application.Id, "application-uses-language");
        var languageAssertion = languageAssertions.Length == 1 ? languageAssertions[0] : null;
        var language = languageAssertion is null
            ? null
            : model.FindConcept(languageAssertion.Object, SemanticConceptKind.Individual);
        if (language is null || !reasoner.IsInstanceOf(language.Id, "programming-language"))
        {
            return false;
        }

        // The knowledge decides the shape and the order: a step's body is rendered one level deeper, and an
        // alternative branch follows the body it belongs to, so the depth is derived from where a step sits
        // rather than spelled into the effect it names.
        if (!TryReadSteps(model, reasoner, task.Id, "has-generation-step", out var steps) || steps.Length == 0)
        {
            return false;
        }

        var goals = ImmutableArray.CreateBuilder<GenerationGoal>(steps.Length);
        var sources = new List<SemanticWorldModelSource>();
        sources.AddRange(reasoner.GetMembershipEvidence(task.Id, "code-generation-task"));
        sources.AddRange(reasoner.GetMembershipEvidence(application.Id, "software-application"));
        sources.AddRange(reasoner.GetMembershipEvidence(language.Id, "programming-language"));
        sources.Add(languageAssertion!.Source);

        var seenSteps = new HashSet<string>(StringComparer.Ordinal);
        if (!TryAddSteps(model, reasoner, steps, 0, slotValues, goals, sources, seenSteps))
        {
            return false;
        }

        var loader = new GenerationLanguagePackLoader();
        if (!loader.TryLoadEmbedded(out var packs, out var loadError))
        {
            return false;
        }
        if (!packs.TryGetValue(language.Id, out var pack))
        {
            return false;
        }
        if (!new SequenceGenerationPipeline().TryGenerateProgram(
                pack,
                goals.ToImmutable(),
                out var program,
                out var generationError))
        {
            return false;
        }

        var safetyNotes = reasoner.GetRelations(task.Id, "code-generation-task-has-safety-note")
            .Select(assertion => new
            {
                Assertion = assertion,
                Note = model.FindConcept(assertion.Object, SemanticConceptKind.Individual)
            })
            .Where(item => item.Note is not null)
            .ToArray();
        if (safetyNotes.Any(item => !reasoner.IsInstanceOf(item.Note!.Id, "code-generation-safety-note")))
        {
            return false;
        }

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
        // Every realized goal names the step it came from, so the list follows the knowledge's order and depth.
        messageLines.AddRange(goals.Select(goal => $"- {goal.ArgumentValue}"));
        if (safetyNotes.Length > 0)
        {
            messageLines.Add("### Safety");
            messageLines.AddRange(safetyNotes.Select(item => $"- {item.Note!.Label}"));
            sources.AddRange(safetyNotes.Select(item => item.Assertion.Source));
            sources.AddRange(safetyNotes.SelectMany(item =>
                reasoner.GetMembershipEvidence(item.Note!.Id, "code-generation-safety-note")));
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

    /// <summary>The deepest block the language packs spell is the fourth level, so the root may nest four deep.</summary>
    private const int MaximumStepDepth = 4;

    /// <summary>
    /// Reads one ordered list of steps from a relation. The order is the order the assertions declare them in,
    /// so no stored sequence number has to agree with the declarations.
    /// </summary>
    private static bool TryReadSteps(
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        string subject,
        string relation,
        out ImmutableArray<(SemanticRelationAssertion Assertion, SemanticConcept Step)> steps)
    {
        var assertions = reasoner.GetRelations(subject, relation);
        var builder = ImmutableArray.CreateBuilder<(SemanticRelationAssertion Assertion, SemanticConcept Step)>(
            assertions.Length);
        foreach (var assertion in assertions)
        {
            var step = model.FindConcept(assertion.Object, SemanticConceptKind.Individual);
            if (step is null)
            {
                steps = ImmutableArray<(SemanticRelationAssertion Assertion, SemanticConcept Step)>.Empty;
                return false;
            }

            builder.Add((assertion, step));
        }

        steps = builder.ToImmutable();
        return true;
    }

    /// <summary>
    /// Adds one listed level of steps, then each step's body and its alternative branch one level deeper. A step
    /// names the family of the effect it wants; the block it sits in is what makes that effect indented, so the
    /// knowledge never spells a depth and the same family serves every depth that has one.
    /// </summary>
    private static bool TryAddSteps(
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        ImmutableArray<(SemanticRelationAssertion Assertion, SemanticConcept Step)> steps,
        int depth,
        ImmutableDictionary<string, string> slotValues,
        ImmutableArray<GenerationGoal>.Builder goals,
        List<SemanticWorldModelSource> sources,
        HashSet<string> seen)
    {
        // A body that renders nothing cannot run out of depth: only a step that is actually emitted needs one.
        if (steps.IsDefaultOrEmpty)
        {
            return true;
        }

        const string familyPrefix = "emit-";
        var depthWord = depth switch
        {
            0 => string.Empty,
            1 => "indented-",
            2 => "double-indented-",
            3 => "triple-indented-",
            _ => null
        };

        if (depthWord is null || depth >= MaximumStepDepth)
        {
            return false;
        }

        foreach (var (assertion, step) in steps)
        {
            if (!seen.Add(step.Id))
            {
                return false;
            }

            var effectAssertions = reasoner.GetRelations(step.Id, "step-has-generation-effect");
            var argumentKindAssertions = reasoner.GetRelations(step.Id, "step-has-argument-kind");
            var effectAssertion = effectAssertions.Length == 1 ? effectAssertions[0] : null;
            var argumentKindAssertion = argumentKindAssertions.Length == 1 ? argumentKindAssertions[0] : null;
            var effect = effectAssertion is null
                ? null
                : model.FindConcept(effectAssertion.Object, SemanticConceptKind.Individual);
            var argumentKind = argumentKindAssertion is null
                ? null
                : model.FindConcept(argumentKindAssertion.Object, SemanticConceptKind.Individual);
            if (effect is null || argumentKind is null ||
                !reasoner.IsInstanceOf(effect.Id, "code-generation-effect") ||
                !reasoner.IsInstanceOf(argumentKind.Id, "code-generation-argument-kind"))
            {
                return false;
            }

            var family = SemanticWorldModel.Normalize(effect.Label);
            if (!family.StartsWith(familyPrefix, StringComparison.Ordinal) || family.Length == familyPrefix.Length)
            {
                return false;
            }

            var bindings = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
            foreach (var bindingAssertion in reasoner.GetRelations(step.Id, "step-has-binding"))
            {
                var binding = model.FindConcept(bindingAssertion.Object, SemanticConceptKind.Individual);
                var valueAssertions = binding is null
                    ? ImmutableArray<SemanticRelationAssertion>.Empty
                    : reasoner.GetRelations(binding.Id, "binding-has-value");
                var valueAssertion = valueAssertions.Length == 1 ? valueAssertions[0] : null;
                var value = valueAssertion is null
                    ? null
                    : model.FindConcept(valueAssertion.Object, SemanticConceptKind.Individual);
                if (binding is null || value is null ||
                    !reasoner.IsInstanceOf(binding.Id, "code-generation-binding") ||
                    !reasoner.IsInstanceOf(value.Id, "code-generation-binding-value") ||
                    bindings.ContainsKey(binding.Label))
                {
                    return false;
                }

                // A binding value the knowledge declares as carrying a slot takes the text the request phrase
                // captured for it, and the declared placeholder stands in when the request fills nothing.
                var label = value.Label;
                foreach (var slotAssertion in reasoner.GetRelations(value.Id, "binding-value-has-slot"))
                {
                    var slot = model.FindConcept(slotAssertion.Object, SemanticConceptKind.Individual);
                    if (slot is null)
                    {
                        return false;
                    }

                    if (slotValues.TryGetValue(slot.Id, out var filled))
                    {
                        label = label.Replace(slot.Label, filled, StringComparison.Ordinal);
                    }
                }

                bindings.Add(binding.Label, label);
                sources.Add(bindingAssertion.Source);
                sources.Add(valueAssertion!.Source);
                sources.AddRange(reasoner.GetMembershipEvidence(binding.Id, "code-generation-binding"));
                sources.AddRange(reasoner.GetMembershipEvidence(value.Id, "code-generation-binding-value"));
            }

            // The API contracts this step reaches become the goal's capabilities, so only a pack operation
            // that declares the same contract can realize the step.
            var capabilities = ImmutableArray.CreateBuilder<string>();
            foreach (var apiAssertion in reasoner.GetRelations(step.Id, "step-references-api"))
            {
                var api = model.FindConcept(apiAssertion.Object, SemanticConceptKind.Individual);
                if (api is null || !reasoner.IsInstanceOf(api.Id, "application-programming-interface"))
                {
                    return false;
                }

                capabilities.Add($"api:{api.Id}");
                sources.Add(apiAssertion.Source);
                sources.AddRange(reasoner.GetMembershipEvidence(api.Id, "application-programming-interface"));
            }

            goals.Add(new GenerationGoal(
                familyPrefix + depthWord + family[familyPrefix.Length..],
                SemanticWorldModel.Normalize(argumentKind.Label),
                step.Label,
                capabilities.ToImmutable())
            {
                Bindings = bindings.ToImmutable()
            });

            sources.Add(assertion.Source);
            sources.Add(effectAssertion!.Source);
            sources.Add(argumentKindAssertion!.Source);
            sources.AddRange(reasoner.GetMembershipEvidence(step.Id, "code-generation-step"));
            sources.AddRange(reasoner.GetMembershipEvidence(effect.Id, "code-generation-effect"));
            sources.AddRange(reasoner.GetMembershipEvidence(argumentKind.Id, "code-generation-argument-kind"));

            if (!TryReadSteps(model, reasoner, step.Id, "step-has-body", out var body) ||
                !TryAddSteps(model, reasoner, body, depth + 1, slotValues, goals, sources, seen))
            {
                return false;
            }

            // An alternative branch aligns with the step that owns it, the way an else keyword aligns with its
            // if, and the branch's own body is one level deeper than the branch keyword.
            if (!TryReadSteps(model, reasoner, step.Id, "step-has-alternative-branch", out var alternative) ||
                !TryAddSteps(model, reasoner, alternative, depth, slotValues, goals, sources, seen))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Turns what a slot-bearing form captured into the text each slot stands for. A `text` slot keeps the
    /// phrase as written; any other kind must resolve to a declared individual of that kind, so an unknown
    /// light kind is a miss rather than invented data.
    /// </summary>
    private static bool TryResolveSlots(
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        SemanticLexeme lexeme,
        ImmutableDictionary<string, string> captures,
        out ImmutableDictionary<string, string> values)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        foreach (var slot in lexeme.Slots)
        {
            if (!captures.TryGetValue(slot.Name, out var text) || string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            if (slot.Kind == "text")
            {
                builder[slot.Concept] = text;
                continue;
            }

            var concept = model.FindConcept(text, SemanticConceptKind.Individual);
            if (concept is null || !reasoner.IsInstanceOf(concept.Id, slot.Kind))
            {
                values = builder.ToImmutable();
                return false;
            }

            builder[slot.Concept] = concept.Label;
        }

        values = builder.ToImmutable();
        return true;
    }
}
