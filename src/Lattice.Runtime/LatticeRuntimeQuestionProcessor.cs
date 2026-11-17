using System.Collections.Immutable;
using Lattice.Client;
using Lattice.Core.Intelligence;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;

namespace Lattice.Runtime;

internal sealed class LatticeRuntimeQuestionProcessor
{
    private readonly KnowledgeBase knowledgeBase;
    private readonly ReasoningEngine reasoningEngine;
    private readonly SemanticWorldModel semanticWorldModel;
    private readonly SemanticWorldModelReasoner semanticWorldModelReasoner;

    public LatticeRuntimeQuestionProcessor(
        KnowledgeBase knowledgeBase,
        ReasoningEngine reasoningEngine,
        SemanticWorldModel semanticWorldModel,
        SemanticWorldModelReasoner semanticWorldModelReasoner)
    {
        this.knowledgeBase = knowledgeBase;
        this.reasoningEngine = reasoningEngine;
        this.semanticWorldModel = semanticWorldModel;
        this.semanticWorldModelReasoner = semanticWorldModelReasoner;
    }

    public LatticeUtteranceResult ProcessClassificationQuestion(
        SemanticUtterance utterance,
        MultiItemClassificationFormula request)
    {
        if (SemanticWorldModelResponder.TryClassify(
            request,
            semanticWorldModel,
            semanticWorldModelReasoner,
            out var semanticMessage,
            out var semanticEvidence))
        {
            return new LatticeUtteranceResult(
                LatticeUtteranceResultKind.QuestionAnswered,
                semanticMessage,
                SemanticFormatter.Format(utterance),
                SupportingEvidence: semanticEvidence);
        }

        if (!MultiItemClassificationResponder.TryRespond(
            request,
            knowledgeBase,
            reasoningEngine,
            out var message,
            out var evidence))
        {
            return new LatticeUtteranceResult(
                LatticeUtteranceResultKind.InformationUnavailable,
                "I don't have enough evidence to classify every item into exactly one requested category.",
                SemanticFormatter.Format(utterance),
                SupportingEvidence: ImmutableArray<LatticeEvidence>.Empty);
        }

        return new LatticeUtteranceResult(
            LatticeUtteranceResultKind.QuestionAnswered,
            message,
            SemanticFormatter.Format(utterance),
            SupportingEvidence: evidence);
    }

    public LatticeUtteranceResult ProcessChoiceQuestion(
        SemanticUtterance utterance,
        ChoiceQuestionFormula question)
    {
        if (SemanticWorldModelResponder.TryChoice(
            question,
            semanticWorldModel,
            semanticWorldModelReasoner,
            out var semanticMessage,
            out var semanticEvidence))
        {
            return new LatticeUtteranceResult(
                LatticeUtteranceResultKind.QuestionAnswered,
                semanticMessage,
                SemanticFormatter.Format(utterance),
                SupportingEvidence: semanticEvidence);
        }

        if (!ChoiceQuestionResponder.TryRespond(
            question,
            knowledgeBase,
            reasoningEngine,
            out var message,
            out var evidence))
        {
            return new LatticeUtteranceResult(
                LatticeUtteranceResultKind.InformationUnavailable,
                "I don't have enough evidence to distinguish the available choices.",
                SemanticFormatter.Format(utterance),
                SupportingEvidence: ImmutableArray<LatticeEvidence>.Empty);
        }

        return new LatticeUtteranceResult(
            LatticeUtteranceResultKind.QuestionAnswered,
            message,
            SemanticFormatter.Format(utterance),
            SupportingEvidence: evidence);
    }

    public LatticeUtteranceResult ProcessDefinitionQuestion(
        SemanticUtterance utterance,
        DefinitionQuestionFormula definition)
    {
        var requestedConcept = string.Join(
            " ",
            (definition.Modifiers.IsDefaultOrEmpty
                ? ImmutableArray<SemanticIdentifier>.Empty
                : definition.Modifiers)
                .Append(definition.Concept)
                .Select(identifier => identifier.Value));

        var meaning = SemanticFormatter.Format(utterance);

        if (SemanticWorldModelResponder.TryDefinition(
            requestedConcept,
            semanticWorldModel,
            semanticWorldModelReasoner,
            out var semanticMessage,
            out var semanticEvidence))
        {
            return new LatticeUtteranceResult(
                LatticeUtteranceResultKind.QuestionAnswered,
                semanticMessage,
                meaning,
                SupportingEvidence: semanticEvidence);
        }

        if (!ConceptKnowledgeFormatter.TryCreate(knowledgeBase, reasoningEngine, requestedConcept, out var explanation))
        {
            return new LatticeUtteranceResult(
                LatticeUtteranceResultKind.InformationUnavailable,
                $"I don't have structured knowledge about {requestedConcept} in the available knowledge.",
                meaning,
                SupportingEvidence: ImmutableArray<LatticeEvidence>.Empty);
        }

        return new LatticeUtteranceResult(
            LatticeUtteranceResultKind.QuestionAnswered,
            explanation!.Message,
            meaning,
            SupportingEvidence: explanation.Evidence);
    }

    public LatticeUtteranceResult ProcessEnumerationRemainderQuestion(
        SemanticUtterance utterance,
        EnumerationRemainderQuestionFormula question)
    {
        var ordinal = question.MissingPosition switch
        {
            1 => "first",
            2 => "second",
            3 => "third",
            4 => "fourth",
            5 => "fifth",
            6 => "sixth",
            7 => "seventh",
            8 => "eighth",
            9 => "ninth",
            10 => "tenth",
            _ => $"{question.MissingPosition}{GetOrdinalSuffix(question.MissingPosition)}"
        };
        var group = SingularizeEnumerationGroup(question.Group);
        var member = char.ToUpperInvariant(question.Member[0]) + question.Member[1..];
        var requiredType = group switch
        {
            "daughter" => "female-person",
            "son" => "male-person",
            "child" => "person",
            _ => null
        };

        if (requiredType is not null)
        {
            var anchor = semanticWorldModel.FindConcept(question.Member, SemanticConceptKind.Individual);
            if (anchor is null ||
                question.ExpectedCount < 2 ||
                question.MissingPosition != question.ExpectedCount ||
                question.NamedMembers.IsDefault ||
                question.NamedMembers.Length != question.ExpectedCount - 1 ||
                question.NamedMembers.Distinct(StringComparer.Ordinal).Count() != question.NamedMembers.Length ||
                question.NamedMembers.Contains(question.Member, StringComparer.Ordinal) ||
                !semanticWorldModelReasoner.IsInstanceOf(anchor.Id, requiredType))
            {
                return new LatticeUtteranceResult(
                    LatticeUtteranceResultKind.InformationUnavailable,
                    "I cannot identify the missing family member from the stated relationships and available type knowledge.",
                    SemanticFormatter.Format(utterance),
                    SupportingEvidence: ImmutableArray<LatticeEvidence>.Empty);
            }

            var membershipPremise = new LatticeEvidence(
                LatticeEvidenceKind.Asserted,
                LatticeEvidencePolarity.Positive,
                semanticWorldModelReasoner.GetMembershipEvidence(anchor.Id, requiredType)
                    .Select(source => new LatticeEvidenceSource(
                        LatticeKnowledgeSourceKind.FoundationalKnowledge,
                        $"{anchor.Label} is modeled as a {group}.",
                        source.SourceId,
                        null))
                    .ToImmutableArray(),
                ImmutableArray<LatticeEvidence>.Empty);
            var userPremise = new LatticeEvidence(
                LatticeEvidenceKind.Asserted,
                LatticeEvidencePolarity.Positive,
                ImmutableArray.Create(new LatticeEvidenceSource(
                    LatticeKnowledgeSourceKind.UserAssertion,
                    utterance.SourceText,
                    "family-set-description",
                    null)),
                ImmutableArray<LatticeEvidence>.Empty);
            var derivedEvidence = new LatticeEvidence(
                LatticeEvidenceKind.Derived,
                LatticeEvidencePolarity.Positive,
                ImmutableArray<LatticeEvidenceSource>.Empty,
                ImmutableArray.Create(userPremise, membershipPremise));

            return new LatticeUtteranceResult(
                LatticeUtteranceResultKind.QuestionAnswered,
                $"The {ordinal} {group} is {member}.",
                SemanticFormatter.Format(utterance),
                SupportingEvidence: ImmutableArray.Create(derivedEvidence));
        }
        var source = new LatticeEvidenceSource(
            LatticeKnowledgeSourceKind.UserAssertion,
            utterance.SourceText,
            SourceId: null,
            ResolvedReferent: null);
        var evidence = new LatticeEvidence(
            LatticeEvidenceKind.Derived,
            LatticeEvidencePolarity.Positive,
            ImmutableArray.Create(source),
            ImmutableArray<LatticeEvidence>.Empty);

        return new LatticeUtteranceResult(
            LatticeUtteranceResultKind.QuestionAnswered,
            $"The {ordinal} {group} is {member}.",
            SemanticFormatter.Format(utterance),
            SupportingEvidence: ImmutableArray.Create(evidence));
    }

    public LatticeUtteranceResult ProcessChargedParticleAccelerationQuestion(
        SemanticUtterance utterance,
        ChargedParticleAccelerationFormula question)
    {
        var meaning = SemanticFormatter.Format(utterance);
        if (!SemanticWorldModelResponder.TryRespond(
            question,
            semanticWorldModel,
            semanticWorldModelReasoner,
            out var message,
            out var evidence))
        {
            return new LatticeUtteranceResult(
                LatticeUtteranceResultKind.InformationUnavailable,
                "I don't have enough structured physical data to calculate that final speed.",
                meaning,
                SupportingEvidence: ImmutableArray<LatticeEvidence>.Empty);
        }

        return new LatticeUtteranceResult(
            LatticeUtteranceResultKind.QuestionAnswered,
            message,
            meaning,
            SupportingEvidence: evidence);
    }

    public LatticeUtteranceResult ProcessSemanticWorldModelQuestion(
        SemanticUtterance utterance,
        SemanticWorldModelQuestionFormula question)
    {
        var meaning = SemanticFormatter.Format(utterance);
        if (question.Kind == SemanticWorldModelQuestionKind.Clarification)
        {
            var clarificationMessage = $"Which {question.Subject} are you referring to? Please list them, and I'll check each one against {question.OtherSubject}.";
            return new LatticeUtteranceResult(
                LatticeUtteranceResultKind.ClarificationRequired,
                clarificationMessage,
                meaning);
        }

        if (!SemanticWorldModelResponder.TryRespond(
            question,
            semanticWorldModel,
            semanticWorldModelReasoner,
            out var message,
            out var evidence))
        {
            return new LatticeUtteranceResult(
                LatticeUtteranceResultKind.InformationUnavailable,
                "I don't have enough structured semantic knowledge to answer that question.",
                meaning,
                SupportingEvidence: ImmutableArray<LatticeEvidence>.Empty);
        }

        return new LatticeUtteranceResult(
            LatticeUtteranceResultKind.QuestionAnswered,
            message,
            meaning,
            SupportingEvidence: evidence);
    }

    public LatticeUtteranceResult ProcessRelationQueryQuestion(
        SemanticUtterance utterance,
        SemanticRelationQueryFormula question)
    {
        var meaning = SemanticFormatter.Format(utterance);
        if (!SemanticWorldModelResponder.TryRespond(
            question,
            semanticWorldModel,
            semanticWorldModelReasoner,
            out var message,
            out var evidence))
        {
            return new LatticeUtteranceResult(
                LatticeUtteranceResultKind.InformationUnavailable,
                "I don't have enough structured semantic knowledge to answer that question.",
                meaning,
                SupportingEvidence: ImmutableArray<LatticeEvidence>.Empty);
        }

        return new LatticeUtteranceResult(
            LatticeUtteranceResultKind.QuestionAnswered,
            message,
            meaning,
            SupportingEvidence: evidence);
    }

    public LatticeUtteranceResult ProcessRelationSourceQueryQuestion(
        SemanticUtterance utterance,
        SemanticRelationSourceQueryFormula question)
    {
        var meaning = SemanticFormatter.Format(utterance);
        if (!SemanticWorldModelResponder.TryRespond(
            question,
            semanticWorldModel,
            semanticWorldModelReasoner,
            out var message,
            out var evidence))
        {
            return new LatticeUtteranceResult(
                LatticeUtteranceResultKind.InformationUnavailable,
                "I don't have enough structured semantic knowledge to answer that question.",
                meaning,
                SupportingEvidence: ImmutableArray<LatticeEvidence>.Empty);
        }

        return new LatticeUtteranceResult(
            LatticeUtteranceResultKind.QuestionAnswered,
            message,
            meaning,
            SupportingEvidence: evidence);
    }

    private static string SingularizeEnumerationGroup(string group) =>
        group switch
        {
            "children" => "child",
            "people" => "person",
            "men" => "man",
            "women" => "woman",
            "mice" => "mouse",
            "geese" => "goose",
            "teeth" => "tooth",
            "feet" => "foot",
            _ when group.EndsWith("ies", StringComparison.Ordinal) => $"{group[..^3]}y",
            _ when group.EndsWith("sses", StringComparison.Ordinal) ||
                   group.EndsWith("shes", StringComparison.Ordinal) ||
                   group.EndsWith("ches", StringComparison.Ordinal) ||
                   group.EndsWith("xes", StringComparison.Ordinal) ||
                   group.EndsWith("zes", StringComparison.Ordinal) => group[..^2],
            _ when group.EndsWith("s", StringComparison.Ordinal) &&
                   !group.EndsWith("ss", StringComparison.Ordinal) => group[..^1],
            _ => group
        };

    private static string GetOrdinalSuffix(int value)
    {
        var lastTwoDigits = value % 100;
        if (lastTwoDigits is 11 or 12 or 13)
        {
            return "th";
        }

        return (value % 10) switch
        {
            1 => "st",
            2 => "nd",
            3 => "rd",
            _ => "th"
        };
    }
}
