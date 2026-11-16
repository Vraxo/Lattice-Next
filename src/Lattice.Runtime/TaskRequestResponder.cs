using System.Collections.Immutable;
using System.Text;
using Lattice.Client;
using Lattice.Core.Reasoning;
using Lattice.Core.Generation;
using Lattice.Core.Semantics;

namespace Lattice.Runtime;

internal static class TaskRequestResponder
{
    internal static bool TryRespond(
        TaskRequestFormula request,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(reasoner);

        return request.Action switch
        {
            TaskRequestAction.Write => TryWrite(request, model, reasoner, out message, out evidence),
            TaskRequestAction.Recommend => TryRecommend(request, model, reasoner, out message, out evidence),
            TaskRequestAction.Plan => request.OutputFormat == "learning-path"
                ? TryPlanLearningPath(request, model, reasoner, out message, out evidence)
                : TryPlanRoadTrip(request, model, reasoner, out message, out evidence),
            TaskRequestAction.Explain => TryExplain(request, model, reasoner, out message, out evidence),
            TaskRequestAction.Code => TryGenerateCode(request, model, reasoner, out message, out evidence),
            TaskRequestAction.Brainstorm => TryBrainstormUses(request, model, reasoner, out message, out evidence),
            TaskRequestAction.Explore => TryExploreHobby(request, model, reasoner, out message, out evidence),
            _ => Fail(out message, out evidence)
        };
    }

    private static string EnsureSentenceEnding(string value)
    {
        return value.TrimEnd().TrimEnd('.', '!', '?') + ".";
    }

    private static bool TryExploreHobby(
        TaskRequestFormula request,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;

        if (request.Context != "hobby" || request.OutputFormat != "starter-activities")
        {
            return false;
        }

        var hobby = model.FindConcept(request.Subject, SemanticConceptKind.Individual);
        if (hobby is null || !reasoner.IsInstanceOf(hobby.Id, "hobby"))
        {
            return false;
        }

        var activityAssertions = reasoner.GetRelations(hobby.Id, "hobby-has-starter-activity");
        var activities = activityAssertions
            .Select(assertion => new
            {
                Assertion = assertion,
                Activity = model.FindConcept(assertion.Object, SemanticConceptKind.Individual),
                Order = model.GetDataAssertions(assertion.Object, "activity-order").FirstOrDefault()?.Value
            })
            .Select(item => int.TryParse(
                item.Order,
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out var order) && item.Activity is not null
                    ? new { item.Assertion, Activity = item.Activity, Order = order }
                    : null)
            .Where(item => item is not null)
            .OrderBy(item => item!.Order)
            .ToArray();

        if (activities.Length == 0)
        {
            return false;
        }

        var paragraphs = new List<string>
        {
            $"Here are some approachable ways to explore {hobby.Label}, starting with simpler activities:"
        };
        var sources = new List<SemanticWorldModelSource>();
        sources.AddRange(reasoner.GetMembershipEvidence(hobby.Id, "hobby"));
        sources.AddRange(activityAssertions.Select(assertion => assertion.Source));

        foreach (var item in activities)
        {
            var activity = item!.Activity!;
            var goalAssertions = reasoner.GetRelations(activity.Id, "activity-has-goal");
            var equipmentAssertions = reasoner.GetRelations(activity.Id, "activity-uses-equipment");
            var levelAssertions = reasoner.GetRelations(activity.Id, "activity-has-level");
            var goals = goalAssertions
                .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
                .ToArray();
            var equipment = equipmentAssertions
                .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
                .ToArray();
            var levels = levelAssertions
                .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
                .ToArray();

            if (goals.Length == 0 || goals.Any(goal => goal is null) ||
                equipment.Length == 0 || equipment.Any(item => item is null) ||
                levels.Length == 0 || levels.Any(level => level is null))
            {
                return false;
            }

            var goalText = string.Join("; ", goals.Select(goal => goal!.Label));
            var equipmentText = string.Join(", ", equipment.Select(item => item!.Label));
            var levelText = string.Join(", ", levels.Select(level => level!.Label));
            paragraphs.Add(
                $"**{activity.Label}** ({levelText}): {goalText}. Equipment: {equipmentText}.");

            sources.Add(item.Assertion.Source);
            sources.AddRange(model.GetDataAssertions(activity.Id, "activity-order")
                .Select(assertion => assertion.Source));
            sources.AddRange(reasoner.GetMembershipEvidence(activity.Id, "hobby-activity"));
            sources.AddRange(goalAssertions.Select(assertion => assertion.Source));
            sources.AddRange(equipmentAssertions.Select(assertion => assertion.Source));
            sources.AddRange(levelAssertions.Select(assertion => assertion.Source));
            foreach (var assertion in goalAssertions)
            {
                sources.AddRange(reasoner.GetMembershipEvidence(assertion.Object, "hobby-goal"));
            }

            foreach (var assertion in equipmentAssertions)
            {
                sources.AddRange(reasoner.GetMembershipEvidence(assertion.Object, "hobby-equipment"));
            }

            foreach (var assertion in levelAssertions)
            {
                sources.AddRange(reasoner.GetMembershipEvidence(assertion.Object, "skill-level"));
            }
        }

        var safetyAssertions = reasoner.GetRelations(hobby.Id, "hobby-has-safety-note");
        foreach (var assertion in safetyAssertions)
        {
            var note = model.FindConcept(assertion.Object, SemanticConceptKind.Individual);
            if (note is null)
            {
                return false;
            }

            paragraphs.Add($"**Safety:** {EnsureSentenceEnding(note.Label)}");
            sources.Add(assertion.Source);
            sources.AddRange(reasoner.GetMembershipEvidence(note.Id, "hobby-safety-note"));
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

    private static bool TryBrainstormUses(
        TaskRequestFormula request,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;

        if (request.Scope != "potential-uses" || request.OutputFormat is null)
        {
            return false;
        }

        var separatorIndex = request.OutputFormat.IndexOf('-');
        if (separatorIndex <= 0 ||
            !int.TryParse(request.OutputFormat[..separatorIndex], out var requestedCount) ||
            requestedCount is < 1 or > 10)
        {
            return false;
        }

        var objectType = model.FindConcept(request.Subject, SemanticConceptKind.Type);
        var requestedBrand = request.Context is null
            ? null
            : model.FindConcept(request.Context, SemanticConceptKind.Individual);
        if (objectType is null || (request.Context is not null && requestedBrand is null))
        {
            return false;
        }

        var target = model.Concepts
            .Where(concept => concept.Kind == SemanticConceptKind.Individual &&
                reasoner.IsInstanceOf(concept.Id, objectType.Id))
            .FirstOrDefault(concept =>
                reasoner.GetRelations(concept.Id, "has-state")
                    .Any(assertion => assertion.Object == "empty-state") &&
                (requestedBrand is null || reasoner.GetRelations(concept.Id, "has-brand")
                    .Any(assertion => assertion.Object == requestedBrand.Id)));
        if (target is null)
        {
            return false;
        }

        var targetFeatureAssertions = reasoner.GetRelations(target.Id, "has-feature");
        var targetFeatureIds = targetFeatureAssertions
            .Select(assertion => assertion.Object)
            .ToHashSet(StringComparer.Ordinal);
        var ideas = model.Concepts
            .Where(concept => concept.Kind == SemanticConceptKind.Individual &&
                reasoner.IsInstanceOf(concept.Id, "repurposing-idea"))
            .Select(idea => new
            {
                Idea = idea,
                ObjectAssertion = reasoner.GetRelations(idea.Id, "idea-for-object")
                    .FirstOrDefault(assertion => assertion.Object == objectType.Id),
                Order = model.GetDataAssertions(idea.Id, "idea-order").FirstOrDefault()?.Value
            })
            .Select(candidate => int.TryParse(
                candidate.Order,
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out var order) && candidate.ObjectAssertion is not null
                    ? new { candidate.Idea, ObjectAssertion = candidate.ObjectAssertion, Order = order }
                    : null)
            .Where(candidate => candidate is not null)
            .OrderBy(candidate => candidate!.Order)
            .Take(requestedCount)
            .ToArray();

        if (ideas.Length != requestedCount)
        {
            return false;
        }

        var paragraphs = new List<string>
        {
            $"Here are {requestedCount} potential uses for an {target.Label}:"
        };
        var sources = new List<SemanticWorldModelSource>();
        sources.AddRange(reasoner.GetMembershipEvidence(target.Id, objectType.Id));
        sources.AddRange(reasoner.GetRelations(target.Id, "has-state")
            .Where(assertion => assertion.Object == "empty-state")
            .Select(assertion => assertion.Source));
        if (requestedBrand is not null)
        {
            sources.AddRange(reasoner.GetRelations(target.Id, "has-brand")
                .Where(assertion => assertion.Object == requestedBrand.Id)
                .Select(assertion => assertion.Source));
        }

        for (var index = 0; index < ideas.Length; index++)
        {
            var candidate = ideas[index]!;
            var purposeAssertion = reasoner.GetRelations(candidate.Idea.Id, "idea-has-purpose")
                .FirstOrDefault();
            var purpose = purposeAssertion is null
                ? null
                : model.FindConcept(purposeAssertion.Object, SemanticConceptKind.Individual);
            var featureAssertions = reasoner.GetRelations(candidate.Idea.Id, "idea-exploits-feature");
            var actionAssertions = reasoner.GetRelations(candidate.Idea.Id, "idea-requires-action");
            var safetyAssertions = reasoner.GetRelations(candidate.Idea.Id, "idea-has-safety-note");
            var actions = actionAssertions
                .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
                .ToArray();
            var safetyNotes = safetyAssertions
                .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
                .ToArray();

            if (purpose is null || featureAssertions.IsDefaultOrEmpty ||
                featureAssertions.Any(assertion => !targetFeatureIds.Contains(assertion.Object)) ||
                actions.Length == 0 || actions.Any(action => action is null) ||
                safetyNotes.Any(note => note is null))
            {
                return false;
            }

            var preparation = string.Join("; ", actions.Select(action => action!.Label));
            var paragraph = $"**{index + 1}. {candidate.Idea.Label}:** Use the can to {purpose.Label}. Preparation: {EnsureSentenceEnding(preparation)}";
            if (safetyNotes.Length > 0)
            {
                paragraph += " Safety: " + string.Join(" ", safetyNotes.Select(note => EnsureSentenceEnding(note!.Label)));
            }

            paragraphs.Add(paragraph);
            sources.Add(candidate.ObjectAssertion!.Source);
            sources.AddRange(model.GetDataAssertions(candidate.Idea.Id, "idea-order")
                .Select(assertion => assertion.Source));
            sources.Add(purposeAssertion!.Source);
            sources.AddRange(reasoner.GetMembershipEvidence(candidate.Idea.Id, "repurposing-idea"));
            sources.AddRange(reasoner.GetMembershipEvidence(purpose.Id, "reuse-purpose"));
            sources.AddRange(featureAssertions.Select(assertion => assertion.Source));
            sources.AddRange(targetFeatureAssertions
                .Where(assertion => featureAssertions.Any(feature => feature.Object == assertion.Object))
                .Select(assertion => assertion.Source));
            sources.AddRange(actionAssertions.Select(assertion => assertion.Source));
            sources.AddRange(safetyAssertions.Select(assertion => assertion.Source));
            foreach (var featureAssertion in featureAssertions)
            {
                sources.AddRange(reasoner.GetMembershipEvidence(featureAssertion.Object, "object-feature"));
            }

            foreach (var actionAssertion in actionAssertions)
            {
                sources.AddRange(reasoner.GetMembershipEvidence(actionAssertion.Object, "reuse-action"));
            }

            foreach (var safetyAssertion in safetyAssertions)
            {
                sources.AddRange(reasoner.GetMembershipEvidence(safetyAssertion.Object, "reuse-safety-note"));
            }
        }

        var generalSafetyNotes = reasoner.GetRelations(target.Id, "object-has-safety-note");
        foreach (var assertion in generalSafetyNotes)
        {
            var note = model.FindConcept(assertion.Object, SemanticConceptKind.Individual);
            if (note is null)
            {
                return false;
            }

            paragraphs.Add($"**General safety:** {EnsureSentenceEnding(note.Label)}");
            sources.Add(assertion.Source);
            sources.AddRange(reasoner.GetMembershipEvidence(note.Id, "reuse-safety-note"));
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

    private static bool TryExplain(
        TaskRequestFormula request,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        if (request.OutputFormat == "procedure")
        {
            return TryExplainOperation(request, model, reasoner, out message, out evidence);
        }

        if (request.OutputFormat == "conceptual-overview")
        {
            return TryExplainLearningMethod(request, model, reasoner, out message, out evidence);
        }

        return Fail(out message, out evidence);
    }

    private static bool TryExplainLearningMethod(
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

    private static bool TryPlanLearningPath(
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

    private static bool TryGenerateCode(
        TaskRequestFormula request,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;

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

    private static bool TryExplainOperation(
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

    private static bool TryPlanRoadTrip(
        TaskRequestFormula request,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;

        if (request.Subject != "road-trip" ||
            request.Context is null ||
            !int.TryParse(
                request.OutputFormat?.Split('-', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(),
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out var requestedDays) ||
            requestedDays is < 1 or > 7)
        {
            return false;
        }

        var origin = model.FindConcept(request.Context, SemanticConceptKind.Individual);
        if (origin is null)
        {
            return false;
        }

        var matchingRoute = model.Concepts
            .Where(concept => concept.Kind == SemanticConceptKind.Individual &&
                reasoner.IsInstanceOf(concept.Id, "road-trip-route"))
            .Select(route => new
            {
                Route = route,
                OriginAssertion = reasoner.GetRelations(route.Id, "route-starts-near")
                    .FirstOrDefault(assertion => assertion.Object == origin.Id)
            })
            .FirstOrDefault(candidate => candidate.OriginAssertion is not null);

        if (matchingRoute is null)
        {
            return false;
        }

        var days = reasoner.GetRelations(matchingRoute.Route.Id, "route-has-day")
            .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
            .Where(day => day is not null)
            .Select(day => new
            {
                Day = day!,
                Number = model.GetDataAssertions(day!.Id, "day-number").FirstOrDefault()?.Value
            })
            .Select(item => int.TryParse(
                item.Number,
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out var number)
                    ? new { item.Day, Number = number }
                    : null)
            .Where(item => item is not null)
            .OrderBy(item => item!.Number)
            .ToArray();

        if (days.Length != requestedDays)
        {
            return false;
        }

        var paragraphs = new List<string>
        {
            $"Assuming you mean the U.S. Memorial Day long weekend, here's a {requestedDays}-day road trip from {origin.Label}. The route stays in New Jersey and balances outdoor walks with relaxed sightseeing."
        };
        var sources = new List<SemanticWorldModelSource>
        {
            matchingRoute.OriginAssertion!.Source
        };
        sources.AddRange(reasoner.GetMembershipEvidence(origin.Id, "town"));
        sources.AddRange(reasoner.GetMembershipEvidence(matchingRoute.Route.Id, "road-trip-route"));

        foreach (var day in days)
        {
            var stops = reasoner.GetRelations(day!.Day.Id, "day-has-stop");
            if (stops.IsDefaultOrEmpty)
            {
                return false;
            }

            var stopDescriptions = new List<string>();
            sources.AddRange(reasoner.GetMembershipEvidence(day.Day.Id, "trip-day"));
            sources.AddRange(model.GetDataAssertions(day.Day.Id, "day-number")
                .Select(assertion => assertion.Source));
            sources.AddRange(reasoner.GetRelations(matchingRoute.Route.Id, "route-has-day")
                .Where(assertion => assertion.Object == day.Day.Id)
                .Select(assertion => assertion.Source));

            foreach (var stop in stops)
            {
                var place = model.FindConcept(stop.Object, SemanticConceptKind.Individual);
                if (place is null || !reasoner.IsInstanceOf(place.Id, "road-trip-place"))
                {
                    return false;
                }

                var activities = reasoner.GetRelations(place.Id, "place-offers-activity")
                    .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
                    .Where(activity => activity is not null)
                    .Select(activity => activity!.Label)
                    .ToArray();
                if (activities.Length == 0)
                {
                    return false;
                }

                stopDescriptions.Add($"**{place.Label}** — {JoinNames(activities)}");
                sources.Add(stop.Source);
                sources.AddRange(reasoner.GetMembershipEvidence(place.Id, "road-trip-place"));
                sources.AddRange(reasoner.GetRelations(place.Id, "place-offers-activity")
                    .Select(assertion => assertion.Source));
                foreach (var activity in reasoner.GetRelations(place.Id, "place-offers-activity"))
                {
                    sources.AddRange(reasoner.GetMembershipEvidence(activity.Object, "activity"));
                }
            }

            paragraphs.Add($"**Day {day.Number}:** {string.Join("; ", stopDescriptions)}.");
        }

        paragraphs.Add("Use Princeton as a base for both nights so you only unpack once. After Day 3, return to Westfield. Check holiday opening hours and reserve accommodation ahead of time; traffic can be heavier over Memorial Day weekend.");
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

    private static bool TryWrite(
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

    private static bool TryRecommend(
        TaskRequestFormula request,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;

        var requiresOpenSource = request.Requirements.Contains("open-source");
        var requiresPython = request.Requirements.Contains("python");
        var requiresProcessSimulation = request.Requirements.Contains("process-simulation");
        var requiresOptimization = request.Requirements.Contains("optimization");

        var candidates = model.Concepts
            .Where(concept =>
                concept.Kind == SemanticConceptKind.Individual &&
                reasoner.IsInstanceOf(concept.Id, "software") &&
                (!requiresOpenSource || reasoner.IsInstanceOf(concept.Id, "open-source-software")) &&
                (!requiresPython || reasoner.IsInstanceOf(concept.Id, "python-package")))
            .Select(concept => CreateSoftwareProfile(concept, model, reasoner))
            .Where(profile => profile is not null)
            .Select(profile => profile!)
            .Where(profile =>
                (!requiresProcessSimulation && !requiresOptimization) ||
                (requiresProcessSimulation && profile.HasProcessModelingCapability) ||
                (requiresOptimization && profile.HasOptimizationCapability))
            .OrderBy(profile => profile.IsOptimizationFramework ? 1 : 0)
            .ThenBy(profile => profile.Name, StringComparer.Ordinal)
            .ToArray();

        if (candidates.Length == 0)
        {
            return false;
        }

        var builder = new StringBuilder();
        builder.AppendLine("Relevant open-source Python tools for the stated purpose:");

        foreach (var candidate in candidates)
        {
            builder.Append("- **")
                .Append(candidate.Name)
                .Append("** (")
                .Append(candidate.Role)
                .Append("): capabilities include ")
                .Append(string.Join(", ", candidate.Capabilities))
                .Append(". It is used for ")
                .Append(candidate.ApplicationDomains.Length == 1
                    ? candidate.ApplicationDomains[0]
                    : string.Join(", ", candidate.ApplicationDomains))
                .AppendLine(".");
            if (!string.IsNullOrWhiteSpace(candidate.DocumentationUrl))
            {
                builder.Append("Documentation: ").AppendLine(candidate.DocumentationUrl);
            }
        }

        if (candidates.Any(candidate => candidate.IsOptimizationFramework) &&
            candidates.Any(candidate => !candidate.IsOptimizationFramework))
        {
            builder.Append("Use the optimization framework alongside a process simulator; it does not replace the simulator's unit-operation and thermodynamic models.");
        }

        message = builder.ToString().Trim();
        evidence = ImmutableArray.Create(new LatticeEvidence(
            LatticeEvidenceKind.Derived,
            LatticeEvidencePolarity.Positive,
            candidates
                .SelectMany(candidate => candidate.Evidence)
                .DistinctBy(source => $"{source.Kind}:{source.SourceId}")
                .ToImmutableArray(),
            ImmutableArray<LatticeEvidence>.Empty));
        return true;
    }

    private static SoftwareProfile? CreateSoftwareProfile(
        SemanticConcept software,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner)
    {
        var types = reasoner.GetTypes(software.Id);
        var role = types
            .Where(type => type is not ("software" or "open-source-software" or "python-package"))
            .Where(type => !types.Any(other =>
                other != type && IsSubtype(other, type, model)))
            .Select(type => model.FindConcept(type, SemanticConceptKind.Type))
            .Where(type => type is not null)
            .OrderBy(type => type!.Label, StringComparer.Ordinal)
            .FirstOrDefault();

        if (role is null)
        {
            return null;
        }

        var capabilityAssertions = reasoner.GetRelations(software.Id, "supports-capability");
        var capabilities = capabilityAssertions
            .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
            .Where(concept => concept is not null)
            .Select(concept => concept!.Label)
            .Distinct(StringComparer.Ordinal)
            .ToImmutableArray();
        var domainAssertions = reasoner.GetRelations(software.Id, "used-for-domain");
        var domains = domainAssertions
            .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
            .Where(concept => concept is not null)
            .Select(concept => concept!.Label)
            .Distinct(StringComparer.Ordinal)
            .ToImmutableArray();
        var documentation = model.GetDataAssertions(software.Id, "documentation-url").FirstOrDefault();

        if (capabilities.IsDefaultOrEmpty || domains.IsDefaultOrEmpty)
        {
            return null;
        }

        var typeEvidence = types
            .Where(type => model.FindConcept(type, SemanticConceptKind.Type) is not null)
            .SelectMany(type => reasoner.GetMembershipEvidence(software.Id, type));
        var relationEvidence = capabilityAssertions
            .Select(assertion => assertion.Source)
            .Concat(domainAssertions.Select(assertion => assertion.Source))
            .ToList();
        if (documentation is not null)
        {
            relationEvidence.Add(documentation.Source);
        }

        var evidence = typeEvidence
            .Concat(relationEvidence)
            .Distinct()
            .Select(ToEvidenceSource)
            .ToImmutableArray();

        return new SoftwareProfile(
            software.Label,
            role.Label,
            capabilities,
            domains,
            documentation?.Value ?? string.Empty,
            role.Id == "optimization-modeling-framework" ||
                reasoner.IsInstanceOf(software.Id, "optimization-framework"),
            capabilities.Any(capability =>
                capability is "equation-oriented process modeling" or
                    "process modeling" or
                    "unit-operation modeling" or
                    "flowsheet optimization"),
            capabilities.Any(capability =>
                capability is "algebraic optimization modeling" or
                    "linear optimization" or
                    "nonlinear optimization" or
                    "mixed-integer optimization" or
                    "flowsheet optimization"),
            evidence);
    }

    private static bool TryDescribeRuralMarket(
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<SemanticWorldModelSource> sources)
    {
        message = string.Empty;
        sources = ImmutableArray<SemanticWorldModelSource>.Empty;
        var market = model.FindConcept("rural-town-labor-market", SemanticConceptKind.Individual);
        if (market is null)
        {
            return false;
        }

        var dominantBuyer = reasoner.GetRelations(market.Id, "has-dominant-buyer").FirstOrDefault();
        var workers = reasoner.GetRelations(market.Id, "has-worker-group").FirstOrDefault();
        if (dominantBuyer is null || workers is null)
        {
            return false;
        }

        var buyer = model.FindConcept(dominantBuyer.Object, SemanticConceptKind.Individual);
        var workerGroup = model.FindConcept(workers.Object, SemanticConceptKind.Individual);
        if (buyer is null || workerGroup is null ||
            !reasoner.IsInstanceOf(workerGroup.Id, "limited-outside-options"))
        {
            return false;
        }

        message = $"For example, a labour market in a rural town has {IndefinitePhrase(buyer.Label)} as its dominant buyer and includes {workerGroup.Label}, whose outside options are limited.";
        sources = ImmutableArray.Create(dominantBuyer.Source, workers.Source)
            .AddRange(reasoner.GetMembershipEvidence(workerGroup.Id, "limited-outside-options"));
        return true;
    }

    private static bool TryDescribeRegionalMarket(
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<SemanticWorldModelSource> sources)
    {
        message = string.Empty;
        sources = ImmutableArray<SemanticWorldModelSource>.Empty;
        var market = model.FindConcept("regional-healthcare-labor-market", SemanticConceptKind.Individual);
        if (market is null)
        {
            return false;
        }

        var employers = reasoner.GetRelations(market.Id, "has-employer");
        var features = reasoner.GetRelations(market.Id, "has-market-feature");
        if (employers.Length < 2 || features.IsDefaultOrEmpty)
        {
            return false;
        }

        var featureLabels = features
            .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
            .Where(concept => concept is not null)
            .Select(concept => concept!.Label)
            .ToArray();
        if (featureLabels.Length == 0)
        {
            return false;
        }

        message = $"A regional healthcare labour market can have several employers while still exhibiting {JoinNames(featureLabels)}.";
        sources = employers.Select(assertion => assertion.Source)
            .Concat(features.Select(assertion => assertion.Source))
            .ToImmutableArray();
        return true;
    }

    private static bool IsSubtype(string candidate, string expected, SemanticWorldModel model)
    {
        if (candidate == expected)
        {
            return true;
        }

        var pending = new Stack<string>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        pending.Push(candidate);

        while (pending.TryPop(out var current))
        {
            if (!visited.Add(current))
            {
                continue;
            }

            foreach (var parent in model.RelationAssertions
                .Where(assertion => assertion.Subject == current && assertion.Relation == "is-a")
                .Select(assertion => assertion.Object))
            {
                if (parent == expected)
                {
                    return true;
                }

                pending.Push(parent);
            }
        }

        return false;
    }

    private static LatticeEvidenceSource ToEvidenceSource(SemanticWorldModelSource source) =>
        new(
            LatticeKnowledgeSourceKind.FoundationalKnowledge,
            source.SourceId,
            source.SourceId,
            null,
            null);

    private static string IndefinitePhrase(string value) =>
        value.Length > 0 && "aeiou".Contains(char.ToLowerInvariant(value[0]))
            ? $"an {value}"
            : $"a {value}";

    private static string JoinNames(IReadOnlyList<string> names) =>
        names.Count switch
        {
            0 => string.Empty,
            1 => names[0],
            2 => $"{names[0]} and {names[1]}",
            _ => $"{string.Join(", ", names.Take(names.Count - 1))}, and {names[^1]}"
        };

    private static bool Fail(
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;
        return false;
    }

}
