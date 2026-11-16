using System.Collections.Immutable;
using System.Globalization;
using Lattice.Client;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;

namespace Lattice.Runtime;

internal static class SemanticWorldModelResponder
{
    internal static bool TryRespond(
        ChargedParticleAccelerationFormula request,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(reasoner);

        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;
        var particle = model.FindConcept(request.Particle, SemanticConceptKind.Individual);
        if (particle is null)
        {
            return false;
        }

        var massAssertion = model.GetDataAssertions(particle.Id, "mass-atomic-mass-units").FirstOrDefault();
        var chargeAssertion = model.GetDataAssertions(particle.Id, "charge-elementary-charge-units").FirstOrDefault();
        if (massAssertion is null ||
            chargeAssertion is null ||
            !double.TryParse(massAssertion.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var mass) ||
            !double.TryParse(chargeAssertion.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var charge) ||
            !ChargedParticleAccelerationCalculator.TryCalculateFinalSpeed(
                (double)request.InitialSpeedMetersPerSecond,
                (double)request.PotentialDifferenceVolts,
                charge,
                mass,
                out var result,
                out _))
        {
            return false;
        }

        var energyDirection = result!.KineticEnergyChangeElectronVolts >= 0
            ? "increases"
            : "decreases";
        message =
            $"The {particle.Label}'s final speed is approximately {FormatScientific(result.FinalSpeedMetersPerSecond)} m/s. " +
            $"Its initial kinetic energy is about {result.InitialKineticEnergyElectronVolts.ToString("0.00", CultureInfo.InvariantCulture)} eV. " +
            $"Because ΔK = −qΔV, this potential difference {energyDirection} its kinetic energy by " +
            $"{Math.Abs(result.KineticEnergyChangeElectronVolts).ToString("0.00", CultureInfo.InvariantCulture)} eV, " +
            $"giving a final kinetic energy of about {result.FinalKineticEnergyElectronVolts.ToString("0.00", CultureInfo.InvariantCulture)} eV.";

        var sources = reasoner.GetMembershipEvidence(particle.Id, "charged-particle")
            .Add(massAssertion.Source)
            .Add(chargeAssertion.Source)
            .Distinct()
            .ToImmutableArray();
        if (sources.IsDefaultOrEmpty)
        {
            return false;
        }

        evidence = ImmutableArray.Create(ToEvidence(sources, LatticeEvidenceKind.Derived));
        return true;
    }

    internal static bool TryRespond(
        SemanticWorldModelQuestionFormula request,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(reasoner);

        return request.Kind switch
        {
            SemanticWorldModelQuestionKind.Definition =>
                TryDefinition(request.Concept ?? request.Subject, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.WhyClassification =>
                TryWhy(request.Subject, request.Concept ?? string.Empty, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.CapabilityExplanation =>
                TryExplainCapability(request.Subject, request.Concept ?? string.Empty, request.OtherSubject, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.Ability =>
                TryExplainAbility(request.Subject, request.Concept ?? string.Empty, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.Mechanism =>
                TryExplainMechanism(request.Subject, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.Causes =>
                TryExplainCauses(request.Subject, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.Compare =>
                TryCompare(request.Subject, request.OtherSubject ?? string.Empty, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.SamplingParameters =>
                TryExplainSamplingParameters(request.Subject, request.OtherSubject ?? string.Empty, request.TargetType ?? string.Empty, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.WhyUse =>
                TryWhyUse(request.Subject, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.Applications =>
                TryExplainApplications(request.Subject, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.RequiredKnowledge =>
                TryExplainRequiredKnowledge(request.Subject, request.Concept ?? string.Empty, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.ContributionQuery =>
                TryContributionQuery(request, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.WhyHarmful =>
                TryExplainHarmfulness(request.Subject, request.Concept ?? string.Empty, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.BeginnerGuidance =>
                TryBeginnerGuidance(request.Subject, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.CreativeIdeas =>
                TryCreativeIdeas(request.Subject, request.OtherSubject, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.ListExamples =>
                TryListExamples(request.Subject, request.Limit, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.TypeInstanceListQuery =>
                TryListTypeInstances(request.Subject, request.TargetType ?? string.Empty, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.MaximumDataValue =>
                TryMaximumDataValue(request, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.HighestDataValue =>
                TryHighestDataValue(request, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.NotableExamples =>
                TryNotableExamples(request, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.RegionalName =>
                TryRegionalName(request, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.Ingredients =>
                TryIngredients(request, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.EventOutcomes =>
                TryExplainEventOutcomes(request.Subject, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.ContextualRelationRoleQuery =>
                TryContextualRelationRoleQuery(request.Subject, request.Concept ?? string.Empty, request.OtherSubject ?? string.Empty, model, reasoner, out message, out evidence),
            SemanticWorldModelQuestionKind.ProductionLocationReasons =>
                TryExplainProductionLocationReasons(request.Subject, request.OtherSubject ?? string.Empty, model, reasoner, out message, out evidence),
            _ => Fail(out message, out evidence)
        };
    }

    internal static bool TryRespond(
        SemanticRelationQueryFormula request,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(reasoner);

        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;
        var subject = model.FindConcept(request.Subject, SemanticConceptKind.Individual);
        if (subject is null)
        {
            return false;
        }

        var matchingRelations = model.Relations
            .Where(relation => MatchesVerb(relation.Verb, request.Verb))
            .ToArray();
        if (matchingRelations.Length == 0)
        {
            return false;
        }

        var targetType = request.TargetType is null
            ? null
            : model.FindConcept(request.TargetType, SemanticConceptKind.Type);
        if (request.TargetType is not null && targetType is null)
        {
            return false;
        }

        var matches = matchingRelations
            .SelectMany(relation => reasoner.GetRelations(subject.Id, relation.Id)
                .Select(assertion => (Relation: relation, Assertion: assertion)))
            .Select(match => (
                match.Relation,
                Target: model.FindConcept(match.Assertion.Object, SemanticConceptKind.Individual),
                match.Assertion.Source))
            .Where(match =>
                match.Target is not null &&
                (request.TargetType is null ||
                 reasoner.IsInstanceOf(match.Target.Id, targetType!.Id)))
            .ToArray();
        var scopeEvidence = ImmutableArray<LatticeEvidence>.Empty;
        if (request.Scope is not null)
        {
            var scope = model.FindConcept(request.Scope, SemanticConceptKind.Individual);
            if (scope is null)
            {
                return false;
            }

            var scopeAssertions = model.Relations
                .SelectMany(relation => reasoner.GetRelations(scope.Id, relation.Id))
                .Where(assertion =>
                {
                    if (request.TargetType is null)
                    {
                        return true;
                    }

                    var relation = model.FindRelation(assertion.Relation);
                    return relation is not null &&
                        relation.SubjectKind == SemanticRelationArgumentKind.Individual &&
                        relation.ObjectKind == SemanticRelationArgumentKind.Individual &&
                        relation.DomainType is not null &&
                        relation.RangeType is not null &&
                        reasoner.IsInstanceOf(scope.Id, relation.DomainType) &&
                        reasoner.IsInstanceOf(assertion.Object, relation.RangeType) &&
                        reasoner.IsInstanceOf(assertion.Object, targetType!.Id);
                })
                .ToArray();
            var scopedTargetIds = scopeAssertions
                .Select(assertion => assertion.Object)
                .ToHashSet(StringComparer.Ordinal);
            matches = matches
                .Where(match => scopedTargetIds.Contains(match.Target!.Id))
                .ToArray();
            if (matches.Length == 0)
            {
                return false;
            }

            scopeEvidence = ImmutableArray.Create(ToEvidence(
                scopeAssertions
                    .Where(assertion => matches.Any(match => match.Target!.Id == assertion.Object))
                    .Select(assertion => assertion.Source)
                    .Distinct()
                    .ToImmutableArray(),
                LatticeEvidenceKind.Derived));
        }

        if (matches.Length == 0)
        {
            return false;
        }

        var relation = matches[0].Relation;
        var targets = matches
            .Select(match => match.Target!)
            .DistinctBy(target => target.Id)
            .ToArray();
        if (request.TargetType is not null)
        {
            var typePhrase = targets.Length == 1 ? IndefinitePhrase(targetType!.Label) : PluralizeType(targetType!.Label);
            var names = JoinNames(targets.Select(target => target.Label).ToArray());
            message = targets.Length == 1
                ? $"{names} is {typePhrase} {relation.Verb} by {subject.Label}."
                : $"{names} are {typePhrase} {relation.Verb} by {subject.Label}.";
        }
        else
        {
            var targetPhrase = request.Scope is null
                ? JoinNames(targets.Select(target => $"the {target.Label}").ToArray())
                : JoinNames(targets.Select(target => target.Label).ToArray());
            message = request.Scope is null
                ? $"The {subject.Label} {relation.Verb} {targetPhrase}."
                : $"{Capitalize(subject.Label)} {relation.Verb} {targetPhrase}.";
        }
        var relationSources = matches.Select(match => match.Source);
        if (request.TargetType is not null)
        {
            relationSources = relationSources.Concat(targets.SelectMany(target =>
                reasoner.GetMembershipEvidence(target.Id, targetType!.Id)));
        }

        var relationEvidence = ToEvidence(
            relationSources.Distinct().ToImmutableArray(),
            LatticeEvidenceKind.Derived);
        evidence = scopeEvidence.IsDefaultOrEmpty
            ? ImmutableArray.Create(relationEvidence)
            : scopeEvidence.Add(relationEvidence);
        return true;
    }

    internal static bool TryRespond(
        SemanticRelationSourceQueryFormula request,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(reasoner);

        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;
        var target = model.FindConcept(request.Object, SemanticConceptKind.Individual);
        if (target is null)
        {
            return false;
        }

        var matchingRelationIds = model.Relations
            .Where(relation => MatchesVerb(relation.Verb, request.Verb))
            .Select(relation => relation.Id)
            .ToHashSet(StringComparer.Ordinal);
        if (matchingRelationIds.Count == 0)
        {
            return false;
        }

        var matches = model.RelationAssertions
            .Where(assertion =>
                assertion.Object == target.Id &&
                matchingRelationIds.Contains(assertion.Relation))
            .Select(assertion => (
                Subject: model.FindConcept(assertion.Subject, SemanticConceptKind.Individual),
                Relation: model.FindRelation(assertion.Relation),
                assertion.Source))
            .Where(match =>
                match.Subject is not null &&
                match.Relation is not null &&
                (request.SubjectType is null ||
                 reasoner.IsInstanceOf(match.Subject.Id, request.SubjectType)))
            .ToArray();
        if (matches.Length == 0)
        {
            return false;
        }

        var subjects = matches
            .Select(match => match.Subject!)
            .DistinctBy(subject => subject.Id)
            .ToArray();
        var relation = matches[0].Relation!;
        var subjectPhrase = JoinNames(subjects.Select(subject =>
            request.SubjectType is null ? $"the {subject.Label}" : subject.Label).ToArray());
        var pluralSubject = subjects.Length > 1 ||
            subjects[0].Label.EndsWith("s", StringComparison.OrdinalIgnoreCase);
        var verb = pluralSubject
            ? NormalizeVerb(relation.Verb.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0])
            : relation.Verb;
        var objectPhrase = request.SubjectType is null ? $"the {target.Label}" : target.Label;
        message = $"{Capitalize(subjectPhrase)} {verb} {objectPhrase}.";
        var sources = matches
            .Select(match => match.Source)
            .Concat(request.SubjectType is null
                ? []
                : subjects.SelectMany(subject =>
                    reasoner.GetMembershipEvidence(subject.Id, request.SubjectType)))
            .Distinct()
            .ToImmutableArray();
        evidence = ImmutableArray.Create(ToEvidence(sources, LatticeEvidenceKind.Derived));
        return true;
    }

    private static string PluralizeType(string value)
    {
        if (value.EndsWith('y') && value.Length > 1 &&
            !"aeiou".Contains(char.ToLowerInvariant(value[^2])))
        {
            return $"{value[..^1]}ies";
        }

        return value.EndsWith("s", StringComparison.OrdinalIgnoreCase)
            ? $"{value}es"
            : $"{value}s";
    }

    private static bool MatchesVerb(string relationVerb, string requestedVerb)
    {
        var relationHead = relationVerb.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        return string.Equals(
            NormalizeVerb(relationHead),
            NormalizeVerb(requestedVerb),
            StringComparison.Ordinal);
    }

    private static string NormalizeVerb(string value)
    {
        var verb = SemanticWorldModel.Normalize(value);
        return verb switch
        {
            "has" => "have",
            "does" => "do",
            _ when verb.EndsWith("ies", StringComparison.Ordinal) => $"{verb[..^3]}y",
            _ when verb.EndsWith("ed", StringComparison.Ordinal) => verb[..^2],
            _ when verb.EndsWith("s", StringComparison.Ordinal) => verb[..^1],
            _ => verb
        };
    }

    internal static bool TryDefinition(
        string concept,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;

        var individual = model.FindConcept(concept, SemanticConceptKind.Individual);
        if (individual is not null)
        {
            return TryDescribeIndividual(individual, model, reasoner, out message, out evidence);
        }

        var type = model.FindConcept(concept, SemanticConceptKind.Type);
        var definition = type is null ? null : model.FindDefinition(type.Id);
        if (type is null || definition is null)
        {
            return false;
        }

        var parent = reasoner.GetDirectSupertypes(type.Id)
            .Select(parentId => model.FindConcept(parentId, SemanticConceptKind.Type))
            .FirstOrDefault(candidate => candidate is not null);

        if (definition.DefinitionKind == SemanticDefinitionKind.Primitive)
        {
            var necessaryPhrases = definition.NecessaryConditions
                .Select(condition => FormatCondition(condition, model))
                .Where(phrase => phrase is not null)
                .Select(phrase => phrase!)
                .ToArray();
            var necessaryClause = necessaryPhrases.Length == 0
                ? string.Empty
                : $" Its instances must {JoinConditions(necessaryPhrases)}.";
            message = parent is null
                ? $"{Capitalize(type.Label)} is a primitive root type in the current model; it has no further structural definition.{necessaryClause}"
                : $"{Capitalize(type.Label)} is currently modeled as a primitive subtype of {IndefinitePhrase(parent.Label)}; the model does not claim a sufficient structural definition for it.{necessaryClause}";
            evidence = ImmutableArray.Create(ToEvidence([definition.Source], LatticeEvidenceKind.Asserted));
            return true;
        }

        var expressionPhrase = FormatExpression(definition.SufficientExpression, model);
        if (expressionPhrase is null)
        {
            return false;
        }

        message = parent is null
            ? $"{Capitalize(IndefinitePhrase(type.Label))} is defined by instances that {expressionPhrase}."
            : $"{Capitalize(IndefinitePhrase(type.Label))} is {IndefinitePhrase(parent.Label)} that {expressionPhrase}.";
        evidence = ImmutableArray.Create(ToEvidence([definition.Source], LatticeEvidenceKind.Asserted));
        return true;
    }

    private static bool TryDescribeIndividual(
        SemanticConcept individual,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;
        var types = reasoner.GetTypes(individual.Id);
        var specificTypes = types
            .Where(candidate => !types.Any(other =>
                other != candidate && IsSubtype(other, candidate, model)))
            .Select(type => model.FindConcept(type, SemanticConceptKind.Type))
            .Where(type => type is not null && type.Id is not (
                "entity" or "software" or "capability" or "research-publication"))
            .ToArray();

        if (specificTypes.Length == 0)
        {
            return false;
        }

        var clauses = new List<string>
        {
            $"{individual.Label} is {JoinConditions(specificTypes.Select(DescribeTypeMembership).ToArray())}."
        };
        var sources = specificTypes
            .SelectMany(type => reasoner.GetMembershipEvidence(individual.Id, type!.Id))
            .ToList();

        var capabilityAssertions = reasoner.GetRelations(individual.Id, "supports-capability");
        var capabilities = capabilityAssertions
            .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
            .Where(target => target is not null)
            .Select(target => target!)
            .DistinctBy(target => target.Id)
            .ToArray();

        if (capabilities.Length > 0)
        {
            clauses.Add($"It supports {JoinConditions(capabilities.Select(target => target.Label).ToArray())}.");
            sources.AddRange(capabilityAssertions.Select(assertion => assertion.Source));
            foreach (var capability in capabilities)
            {
                sources.AddRange(reasoner.GetMembershipEvidence(capability.Id, "capability"));
            }
        }

        var domainAssertions = reasoner.GetRelations(individual.Id, "used-for-domain");
        var domains = domainAssertions
            .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
            .Where(target => target is not null)
            .Select(target => target!)
            .DistinctBy(target => target.Id)
            .ToArray();
        if (domains.Length > 0)
        {
            clauses.Add($"It is used for {JoinConditions(domains.Select(target => target.Label).ToArray())}.");
            sources.AddRange(domainAssertions.Select(assertion => assertion.Source));
        }

        var documentation = model.GetDataAssertions(individual.Id, "documentation-url");
        if (documentation.Length > 0)
        {
            clauses.Add($"Its documentation is available at {documentation[0].Value}.");
            sources.AddRange(documentation.Select(assertion => assertion.Source));
        }

        message = string.Join(" ", clauses);
        evidence = ImmutableArray.Create(ToEvidence(
            sources.Distinct().ToImmutableArray(),
            LatticeEvidenceKind.Derived));
        return true;
    }

    private static string DescribeTypeMembership(SemanticConcept type)
    {
        if (type.Id == "open-source-software")
        {
            return type.Label;
        }

        return IndefinitePhrase(type.Label);
    }

    private static bool TryContributionQuery(
        SemanticWorldModelQuestionFormula request,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;

        var recipient = model.FindConcept(request.Subject, SemanticConceptKind.Individual);
        var resourceType = model.FindConcept(request.Concept ?? string.Empty, SemanticConceptKind.Type);
        var location = model.FindConcept(request.OtherSubject ?? string.Empty, SemanticConceptKind.Individual);
        var facilityType = model.FindConcept(request.TargetType ?? string.Empty, SemanticConceptKind.Type);
        if (recipient is null || resourceType is null || location is null || facilityType is null)
        {
            return false;
        }

        var headquartersAssertions = model.RelationAssertions
            .Where(assertion =>
                assertion.Subject == recipient.Id &&
                assertion.Relation == "has-headquarters-in" &&
                assertion.Object == location.Id)
            .ToArray();
        if (headquartersAssertions.Length == 0)
        {
            return false;
        }

        var siteAssertions = model.RelationAssertions
            .Where(assertion =>
                assertion.Relation == "situated-in" &&
                assertion.Object == location.Id)
            .Select(assertion => (
                Site: model.FindConcept(assertion.Subject, SemanticConceptKind.Individual),
                Assertion: assertion))
            .Where(candidate =>
                candidate.Site is not null &&
                reasoner.IsInstanceOf(candidate.Site.Id, "site") &&
                reasoner.IsInstanceOf(candidate.Site.Id, resourceType.Id) &&
                reasoner.IsInstanceOf(candidate.Site.Id, facilityType.Id))
            .ToArray();
        if (siteAssertions.Length != 1)
        {
            return false;
        }

        var site = siteAssertions[0].Site!;
        var contributions = model.RelationAssertions
            .Where(assertion =>
                assertion.Object == site.Id &&
                assertion.Relation is "funded-acquisition-of" or "contributed-property-to")
            .Select(assertion => (
                Contributor: model.FindConcept(assertion.Subject, SemanticConceptKind.Individual),
                Relation: model.FindRelation(assertion.Relation),
                Assertion: assertion))
            .Where(item => item.Contributor is not null && item.Relation is not null)
            .OrderBy(item => item.Assertion.Relation == "funded-acquisition-of" ? 0 : 1)
            .ToArray();
        if (contributions.Length == 0)
        {
            return false;
        }

        var clauses = contributions
            .Select(item => $"{item.Contributor!.Label} {item.Relation!.Verb} the {site.Label}.")
            .ToArray();
        message = string.Join(" ", clauses);

        var sources = headquartersAssertions
            .Select(assertion => assertion.Source)
            .Concat(siteAssertions.Select(candidate => candidate.Assertion.Source))
            .Concat(contributions.Select(item => item.Assertion.Source))
            .Concat(reasoner.GetMembershipEvidence(site.Id, "site"))
            .Concat(reasoner.GetMembershipEvidence(site.Id, resourceType.Id))
            .Concat(reasoner.GetMembershipEvidence(site.Id, facilityType.Id))
            .Distinct()
            .ToImmutableArray();
        evidence = ImmutableArray.Create(ToEvidence(sources, LatticeEvidenceKind.Derived));
        return true;
    }

    private static bool TryExplainEventOutcomes(
        string eventId,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;
        var eventConcept = model.FindConcept(eventId, SemanticConceptKind.Individual);
        if (eventConcept is null)
        {
            return false;
        }

        var effects = reasoner.GetRelations(eventConcept.Id)
            .Where(assertion => assertion.Relation is "ends" or "begins" or "results-in" or "leads-to")
            .OrderBy(assertion => model.FindRelation(assertion.Relation)?.Order ?? int.MaxValue)
            .Select(assertion => (
                Relation: model.FindRelation(assertion.Relation),
                Target: model.FindConcept(assertion.Object, SemanticConceptKind.Individual),
                assertion.Source))
            .Where(effect => effect.Relation is not null && effect.Target is not null)
            .ToArray();
        if (effects.Length == 0)
        {
            return false;
        }

        var clauses = effects.Select(effect =>
        {
            var verb = effect.Relation!.Verb;
            var target = effect.Target!.Label;
            return effect.Relation.Id switch
            {
                "ends" => $"daylight ends",
                "begins" => $"{target} begins",
                "results-in" => $"it results in {target}",
                "leads-to" => $"it leads to {target}",
                _ => $"{verb} {target}"
            };
        }).ToArray();

        var causes = model.RelationAssertions
            .Where(assertion => assertion.Object == eventConcept.Id && assertion.Relation == "causes")
            .Select(assertion => (
                Cause: model.FindConcept(assertion.Subject, SemanticConceptKind.Individual),
                Assertion: assertion))
            .Where(item => item.Cause is not null)
            .ToArray();

        var causeDetails = new List<string>();
        var sources = effects.Select(effect => effect.Source).ToList();
        foreach (var cause in causes)
        {
            sources.Add(cause.Assertion.Source);
            var turning = reasoner.GetRelations(cause.Cause!.Id, "turns-location-away-from");
            foreach (var assertion in turning)
            {
                var target = model.FindConcept(assertion.Object, SemanticConceptKind.Individual);
                if (target is null)
                {
                    continue;
                }

                var relation = model.FindRelation(assertion.Relation);
                if (relation is null)
                {
                    continue;
                }

                causeDetails.Add($"{cause.Cause.Label} {relation.Verb} {target.Label}");
                sources.Add(assertion.Source);
            }
        }

        var appearance = model.FindConcept("sun", SemanticConceptKind.Individual);
        var horizon = model.FindConcept("local-horizon", SemanticConceptKind.Individual);
        SemanticRelationAssertion[] appearanceAssertions = appearance is null || horizon is null
            ? []
            : reasoner.GetRelations(appearance.Id, "appears-below")
                .Where(assertion => assertion.Object == horizon.Id)
                .ToArray();
        if (appearanceAssertions.Length > 0)
        {
            sources.AddRange(appearanceAssertions.Select(assertion => assertion.Source));
        }

        var outcomeSentence = $"When {eventConcept.Label} occurs, {string.Join(" and ", clauses)}.";
        var explanation = causeDetails.Count == 0
            ? string.Empty
            : $" This happens because {string.Join(" and ", causeDetails)}.";
        var apparentMotion = appearanceAssertions.Length > 0
            ? " The Sun appears to move below the local horizon; it has not gone out or disappeared."
            : string.Empty;

        message = outcomeSentence + explanation + apparentMotion;
        evidence = ImmutableArray.Create(ToEvidence(
            sources.Distinct().ToImmutableArray(),
            LatticeEvidenceKind.Derived));
        return true;
    }

    private static bool TryHighestDataValue(
        SemanticWorldModelQuestionFormula request,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;

        var featureType = model.FindConcept(request.Subject, SemanticConceptKind.Type);
        var property = model.FindDataProperty(request.Concept ?? string.Empty);
        var scope = model.FindConcept(request.TargetType ?? string.Empty, SemanticConceptKind.Individual);
        if (featureType is null ||
            property is null ||
            property.ValueKind != SemanticDataValueKind.Number ||
            property.DomainType != featureType.Id ||
            scope is null)
        {
            return false;
        }

        var interpretations = reasoner.GetRelations(scope.Id, "has-scope-interpretation");
        if (interpretations.IsDefaultOrEmpty)
        {
            return false;
        }

        var answers = new List<(SemanticConcept Scope, SemanticConcept Feature, decimal Value)>();
        var sources = new List<SemanticWorldModelSource>();
        sources.AddRange(interpretations.Select(assertion => assertion.Source));

        foreach (var interpretation in interpretations)
        {
            var interpretedScope = model.FindConcept(interpretation.Object, SemanticConceptKind.Individual);
            if (interpretedScope is null)
            {
                return false;
            }

            var highestAssertions = reasoner.GetRelations(interpretedScope.Id, "has-highest-feature");
            if (highestAssertions.Length != 1)
            {
                return false;
            }

            var feature = model.FindConcept(highestAssertions[0].Object, SemanticConceptKind.Individual);
            if (feature is null || !reasoner.IsInstanceOf(feature.Id, featureType.Id))
            {
                return false;
            }

            var featureValues = model.GetDataAssertions(feature.Id, property.Id);
            if (featureValues.Length != 1 ||
                !decimal.TryParse(featureValues[0].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
            {
                return false;
            }

            var scopedCandidates = model.Concepts
                .Where(concept =>
                    concept.Kind == SemanticConceptKind.Individual &&
                    reasoner.IsInstanceOf(concept.Id, featureType.Id))
                .SelectMany(concept =>
                {
                    var locations = reasoner.GetRelations(concept.Id, "located-in-geographic-region")
                        .Where(assertion => assertion.Object == interpretedScope.Id)
                        .ToArray();
                    var values = model.GetDataAssertions(concept.Id, property.Id);
                    return locations.SelectMany(location => values.Select(candidateValue => (
                        Concept: concept,
                        Location: location,
                        Assertion: candidateValue)));
                })
                .Where(candidate => decimal.TryParse(
                    candidate.Assertion.Value,
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out _))
                .Select(candidate => (
                    candidate.Concept,
                    candidate.Location,
                    candidate.Assertion,
                    Value: decimal.Parse(candidate.Assertion.Value, NumberStyles.Number, CultureInfo.InvariantCulture)))
                .ToArray();
            if (scopedCandidates.Length == 0 ||
                scopedCandidates.Max(candidate => candidate.Value) != value ||
                !scopedCandidates.Any(candidate => candidate.Concept.Id == feature.Id && candidate.Value == value))
            {
                return false;
            }

            answers.Add((interpretedScope, feature, value));
            sources.Add(highestAssertions[0].Source);
            sources.AddRange(reasoner.GetMembershipEvidence(feature.Id, featureType.Id));
            sources.AddRange(scopedCandidates.SelectMany(candidate => new[]
            {
                candidate.Location.Source,
                candidate.Assertion.Source
            }));
        }

        var answerText = answers.Select(answer =>
            $"For {answer.Scope.Label}, {answer.Feature.Label} is the tallest {featureType.Label}, at {answer.Value.ToString("N0", CultureInfo.InvariantCulture)} {request.OtherSubject}.");
        message = $"The phrase \"{scope.Label}\" can refer to different geographic scopes. {string.Join(" ", answerText)}";
        evidence = ImmutableArray.Create(ToEvidence(sources.Distinct().ToImmutableArray(), LatticeEvidenceKind.Derived));
        return true;
    }

    private static bool TryNotableExamples(
        SemanticWorldModelQuestionFormula request,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;

        if (request.Limit is < 1 or > 12)
        {
            return false;
        }

        var filmType = model.FindConcept(request.Concept ?? string.Empty, SemanticConceptKind.Type);
        var yearProperty = model.FindDataProperty("film-release-year");
        if (filmType is null ||
            yearProperty is null ||
            yearProperty.ValueKind != SemanticDataValueKind.Number ||
            !reasoner.IsSubtypeOf(filmType.Id, "comedy-film"))
        {
            return false;
        }

        var films = new List<(SemanticConcept Film, int Year, string Genres, ImmutableArray<SemanticWorldModelSource> Sources)>();
        foreach (var film in model.Concepts.Where(concept =>
            concept.Kind == SemanticConceptKind.Individual &&
            reasoner.IsInstanceOf(concept.Id, filmType.Id)))
        {
            var yearAssertions = model.GetDataAssertions(film.Id, yearProperty.Id);
            var genreAssertions = reasoner.GetRelations(film.Id, "has-film-genre");
            if (yearAssertions.Length != 1 ||
                !int.TryParse(yearAssertions[0].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var year) ||
                genreAssertions.IsDefaultOrEmpty)
            {
                continue;
            }

            var genres = genreAssertions
                .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
                .Where(concept => concept is not null && reasoner.IsInstanceOf(concept.Id, "film-genre"))
                .Select(concept => concept!.Label)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (genres.Length == 0)
            {
                continue;
            }

            var sources = reasoner.GetMembershipEvidence(film.Id, filmType.Id)
                .Concat(yearAssertions.Select(assertion => assertion.Source))
                .Concat(genreAssertions.Select(assertion => assertion.Source))
                .ToImmutableArray();
            films.Add((film, year, JoinNames(genres), sources));
        }

        if (films.Count < request.Limit)
        {
            return false;
        }

        var selected = films
            .OrderBy(item => item.Year)
            .ThenBy(item => item.Film.Label, StringComparer.OrdinalIgnoreCase)
            .Take(request.Limit)
            .ToArray();
        var entries = selected.Select((item, index) =>
            $"{index + 1}. {item.Film.Label} ({item.Year}) — {item.Genres}");
        message = $"There is no single objective ranking, but here are {request.Limit} widely regarded comedy films from the 1980s, spanning several styles:\n{string.Join("\n", entries)}";
        evidence = ImmutableArray.Create(ToEvidence(
            selected.SelectMany(item => item.Sources).Distinct().ToImmutableArray(),
            LatticeEvidenceKind.Derived));
        return true;
    }

    private static bool TryExplainProductionLocationReasons(
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

    private static bool TryContextualRelationRoleQuery(
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

    private static bool TryRegionalName(
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

    private static bool TryIngredients(
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

    private static bool TryMaximumDataValue(
        SemanticWorldModelQuestionFormula request,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;

        var property = model.FindDataProperty(request.Concept ?? string.Empty);
        if (property is null || property.ValueKind != SemanticDataValueKind.Number)
        {
            return false;
        }

        var candidates = model.Concepts
            .Where(concept =>
                concept.Kind == SemanticConceptKind.Individual &&
                reasoner.IsInstanceOf(concept.Id, property.DomainType))
            .SelectMany(concept => model.GetDataAssertions(concept.Id, property.Id)
                .Where(assertion => decimal.TryParse(
                    assertion.Value,
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out _))
                .Select(assertion => (
                    Concept: concept,
                    Assertion: assertion,
                    Value: decimal.Parse(
                        assertion.Value,
                        NumberStyles.Number,
                        CultureInfo.InvariantCulture))))
            .ToArray();
        if (candidates.Length == 0)
        {
            return false;
        }

        var maximum = candidates.Max(candidate => candidate.Value);
        var winners = candidates
            .Where(candidate => candidate.Value == maximum)
            .ToArray();
        var names = JoinNames(winners.Select(winner => winner.Concept.Label).ToArray());
        var verb = winners.Length == 1 ? "has" : "have";
        var metric = string.IsNullOrWhiteSpace(request.OtherSubject)
            ? property.Id.Replace('-', ' ')
            : request.OtherSubject;
        message = $"{names} {verb} won the most {metric} ({maximum.ToString(CultureInfo.InvariantCulture)}).";
        evidence = ImmutableArray.Create(ToEvidence(
            winners.Select(winner => winner.Assertion.Source).Distinct().ToImmutableArray(),
            LatticeEvidenceKind.Derived));
        return true;
    }

    private static bool TryListTypeInstances(
        string requestedType,
        string countText,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;
        if (!int.TryParse(countText, NumberStyles.None, CultureInfo.InvariantCulture, out var requestedCount) ||
            requestedCount is < 1 or > 20)
        {
            return false;
        }

        var type = model.FindConcept(requestedType, SemanticConceptKind.Type);
        if (type is null)
        {
            return false;
        }

        var instances = model.Concepts
            .Where(concept =>
                concept.Kind == SemanticConceptKind.Individual &&
                reasoner.IsInstanceOf(concept.Id, type.Id))
            .OrderBy(concept => concept.Label, StringComparer.OrdinalIgnoreCase)
            .Take(requestedCount)
            .ToArray();
        if (instances.Length == 0)
        {
            return false;
        }

        var sources = instances
            .SelectMany(instance => reasoner.GetMembershipEvidence(instance.Id, type.Id))
            .Distinct()
            .ToImmutableArray();
        if (sources.IsDefaultOrEmpty)
        {
            return false;
        }

        var pluralType = type.Label.EndsWith("y", StringComparison.OrdinalIgnoreCase)
            ? $"{type.Label[..^1]}ies"
            : type.Label.EndsWith("s", StringComparison.OrdinalIgnoreCase)
                ? type.Label
                : $"{type.Label}s";
        message = $"The model identifies {instances.Length} {pluralType}: {JoinNames(instances.Select(instance => instance.Label).ToArray())}. The model does not rank them.";
        evidence = ImmutableArray.Create(ToEvidence(sources, LatticeEvidenceKind.Derived));
        return true;
    }

    private static bool TryListExamples(
        string requestedType,
        int limit,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;

        if (limit is < 1 or > 10)
        {
            return false;
        }

        var type = model.FindConcept(requestedType, SemanticConceptKind.Type);
        if (type is null)
        {
            return false;
        }

        var examples = model.Concepts
            .Where(concept =>
                concept.Kind == SemanticConceptKind.Individual &&
                reasoner.IsInstanceOf(concept.Id, type.Id))
            .OrderBy(concept => concept.Label, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToArray();
        if (examples.Length < limit)
        {
            return false;
        }

        var sources = examples
            .SelectMany(example => reasoner.GetMembershipEvidence(example.Id, type.Id))
            .Distinct()
            .ToImmutableArray();
        if (sources.IsDefaultOrEmpty)
        {
            return false;
        }

        var pluralLabel = type.Label.EndsWith("y", StringComparison.OrdinalIgnoreCase)
            ? $"{type.Label[..^1]}ies"
            : $"{type.Label}s";
        message = $"Here are {limit} prominent {pluralLabel}:\n{string.Join("\n", examples.Select((example, index) => $"{index + 1}. {example.Label}"))}";
        evidence = ImmutableArray.Create(ToEvidence(sources, LatticeEvidenceKind.Derived));
        return true;
    }

    private static bool TryCreativeIdeas(
        string requestedCategory,
        string? companyName,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;

        if (requestedCategory == "business-ideas")
        {
            return TryBusinessIdeas(companyName, model, reasoner, out message, out evidence);
        }

        var category = model.FindConcept(requestedCategory, SemanticConceptKind.Individual);
        if (category is null)
        {
            return false;
        }

        var ideaAssertions = reasoner.GetRelations(category.Id, "has-diy-idea");
        if (ideaAssertions.IsDefaultOrEmpty)
        {
            return false;
        }

        var ideas = new List<(string Label, string Materials, string Method, string Attachment)>();
        var sources = new List<SemanticWorldModelSource>();
        foreach (var assertion in ideaAssertions)
        {
            var idea = model.FindConcept(assertion.Object, SemanticConceptKind.Individual);
            if (idea is null)
            {
                continue;
            }

            var materials = reasoner.GetRelations(idea.Id, "uses-material")
                .Select(item => model.FindConcept(item.Object, SemanticConceptKind.Individual))
                .Where(item => item is not null)
                .Select(item => item!.Label)
                .ToArray();
            var methods = reasoner.GetRelations(idea.Id, "made-by-method")
                .Select(item => model.FindConcept(item.Object, SemanticConceptKind.Individual))
                .Where(item => item is not null)
                .Select(item => item!.Label)
                .ToArray();
            var attachments = reasoner.GetRelations(idea.Id, "attaches-with")
                .Select(item => model.FindConcept(item.Object, SemanticConceptKind.Individual))
                .Where(item => item is not null)
                .Select(item => item!.Label)
                .ToArray();
            if (materials.Length == 0 || methods.Length == 0 || attachments.Length == 0)
            {
                continue;
            }

            ideas.Add((
                idea.Label,
                JoinConditions(materials),
                JoinConditions(methods),
                JoinConditions(attachments)));
            sources.Add(assertion.Source);
            sources.AddRange(reasoner.GetRelations(idea.Id, "uses-material").Select(item => item.Source));
            sources.AddRange(reasoner.GetRelations(idea.Id, "made-by-method").Select(item => item.Source));
            sources.AddRange(reasoner.GetRelations(idea.Id, "attaches-with").Select(item => item.Source));
        }

        if (ideas.Count == 0)
        {
            return false;
        }

        var suggestions = ideas.Select((idea, index) =>
            $"{index + 1}. **{Capitalize(idea.Label)}** — Materials: {idea.Materials}. Make it by {idea.Method}; attach it with {IndefinitePhrase(idea.Attachment)}.");
        message = $"Here are some DIY ideas for {category.Label}:\n{string.Join("\n", suggestions)}";
        evidence = ImmutableArray.Create(ToEvidence(
            sources.Distinct().ToImmutableArray(),
            LatticeEvidenceKind.Derived));
        return true;
    }

    private static bool TryBusinessIdeas(
        string? companyName,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;

        var category = model.FindConcept("business-ideas", SemanticConceptKind.Individual);
        if (category is null)
        {
            return false;
        }

        var ideaAssertions = reasoner.GetRelations(category.Id, "has-business-idea");
        if (ideaAssertions.IsDefaultOrEmpty)
        {
            return false;
        }

        var ideas = new List<(string Name, string Customer, string Problem, string Offering, string Revenue)>();
        var sources = new List<SemanticWorldModelSource>();
        foreach (var assertion in ideaAssertions)
        {
            var idea = model.FindConcept(assertion.Object, SemanticConceptKind.Individual);
            if (idea is null ||
                !TryGetBusinessIdeaDetail(idea.Id, "serves-customer", out var customer, out var customerSource) ||
                !TryGetBusinessIdeaDetail(idea.Id, "addresses-problem", out var problem, out var problemSource) ||
                !TryGetBusinessIdeaDetail(idea.Id, "offers-service", out var offering, out var offeringSource) ||
                !TryGetBusinessIdeaDetail(idea.Id, "earns-revenue-through", out var revenue, out var revenueSource))
            {
                continue;
            }

            ideas.Add((idea.Label, customer, problem, offering, revenue));
            sources.Add(assertion.Source);
            sources.Add(customerSource);
            sources.Add(problemSource);
            sources.Add(offeringSource);
            sources.Add(revenueSource);
        }

        if (ideas.Count == 0)
        {
            return false;
        }

        var company = string.IsNullOrWhiteSpace(companyName)
            ? "the company"
            : companyName.Trim();
        var suggestions = ideas.Select((idea, index) =>
            $"{index + 1}. **{Capitalize(idea.Name)}** — Customers: {idea.Customer}. Problem: {idea.Problem}. Offer: {idea.Offering}. Revenue: {idea.Revenue}.");
        message = $"The prompt does not specify what {company} does yet. Here are some possible directions it could explore:\n{string.Join("\n", suggestions)}\n\nThese are proposals, not facts about an existing company. The best choice depends on your team's expertise, location, available capital, and evidence of customer demand.";
        evidence = ImmutableArray.Create(ToEvidence(
            sources.Distinct().ToImmutableArray(),
            LatticeEvidenceKind.Derived));
        return true;

        bool TryGetBusinessIdeaDetail(
            string subject,
            string relation,
            out string label,
            out SemanticWorldModelSource source)
        {
            label = string.Empty;
            source = new SemanticWorldModelSource(string.Empty);
            var assertions = reasoner.GetRelations(subject, relation);
            if (assertions.Length != 1)
            {
                return false;
            }

            var concept = model.FindConcept(assertions[0].Object, SemanticConceptKind.Individual);
            if (concept is null)
            {
                return false;
            }

            label = concept.Label;
            source = assertions[0].Source;
            return true;
        }
    }

    private static bool TryBeginnerGuidance(
        string requestedActivity,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;

        var activity = model.FindConcept(requestedActivity, SemanticConceptKind.Individual);
        if (activity is null || !reasoner.IsInstanceOf(activity.Id, "physical-activity"))
        {
            return false;
        }

        var firstSteps = reasoner.GetRelations(activity.Id, "has-first-beginner-step");
        if (firstSteps.Length != 1)
        {
            return false;
        }

        var steps = new List<(SemanticConcept Step, SemanticConcept Purpose)>();
        var sources = new List<SemanticWorldModelSource>
        {
            firstSteps[0].Source
        };
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var currentId = firstSteps[0].Object;

        while (true)
        {
            if (!visited.Add(currentId))
            {
                return false;
            }

            var step = model.FindConcept(currentId, SemanticConceptKind.Individual);
            if (step is null || !reasoner.IsInstanceOf(step.Id, "beginner-practice"))
            {
                return false;
            }

            var purposes = reasoner.GetRelations(step.Id, "has-practice-purpose");
            if (purposes.Length != 1)
            {
                return false;
            }

            var purpose = model.FindConcept(purposes[0].Object, SemanticConceptKind.Individual);
            if (purpose is null || !reasoner.IsInstanceOf(purpose.Id, "training-purpose"))
            {
                return false;
            }

            steps.Add((step, purpose));
            sources.AddRange(reasoner.GetMembershipEvidence(step.Id, "beginner-practice"));
            sources.AddRange(reasoner.GetMembershipEvidence(purpose.Id, "training-purpose"));
            sources.Add(purposes[0].Source);

            var nextSteps = reasoner.GetRelations(step.Id, "followed-by");
            if (nextSteps.Length == 0)
            {
                break;
            }

            if (nextSteps.Length != 1)
            {
                return false;
            }

            sources.Add(nextSteps[0].Source);
            currentId = nextSteps[0].Object;
        }

        if (steps.Count == 0)
        {
            return false;
        }

        var safetyAssertions = reasoner.GetRelations(activity.Id, "has-safety-guidance");
        var safetyGuidance = safetyAssertions
            .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
            .Where(concept => concept is not null)
            .Select(concept => concept!)
            .ToArray();
        var warningAssertions = reasoner.GetRelations(activity.Id, "stop-and-seek-help-for");
        var warningGuidance = warningAssertions
            .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
            .Where(concept => concept is not null)
            .Select(concept => concept!)
            .ToArray();
        if (safetyGuidance.Length == 0 || warningGuidance.Length == 0)
        {
            return false;
        }

        sources.AddRange(safetyAssertions.Select(assertion => assertion.Source));
        sources.AddRange(warningAssertions.Select(assertion => assertion.Source));
        sources.AddRange(safetyGuidance.Concat(warningGuidance).SelectMany(concept =>
            reasoner.GetMembershipEvidence(concept.Id, "safety-guidance")));
        sources.AddRange(reasoner.GetMembershipEvidence(activity.Id, "physical-activity"));

        var stepText = string.Join("\n", steps.Select((item, index) =>
            $"{index + 1}. {Capitalize(item.Step.Label)}; purpose: {item.Purpose.Label}."));
        var warningIds = warningGuidance.Select(concept => concept.Id)
            .ToHashSet(StringComparer.Ordinal);
        var safetyText = JoinConditions(safetyGuidance
            .Where(concept => !warningIds.Contains(concept.Id))
            .Select(concept => concept.Label)
            .ToArray());
        var warningText = JoinConditions(warningGuidance.Select(concept => concept.Label).ToArray());
        message = $"To start {activity.Label}, use a gradual beginner routine:\n{stepText}\nSafety considerations include {safetyText}. Stop and seek medical help if you experience {warningText}.";
        evidence = ImmutableArray.Create(ToEvidence(
            sources.Distinct().ToImmutableArray(),
            LatticeEvidenceKind.Derived));
        return true;
    }

    private static bool TryExplainHarmfulness(
        string requestedSubject,
        string requestedTarget,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;

        var subject = model.FindConcept(requestedSubject, SemanticConceptKind.Individual);
        var targetType = model.FindConcept(requestedTarget, SemanticConceptKind.Type);
        var effectType = model.FindConcept("adverse-effect", SemanticConceptKind.Type);
        if (subject is null ||
            targetType is null ||
            effectType is null ||
            !reasoner.IsInstanceOf(subject.Id, "technology-use"))
        {
            return false;
        }

        var effectAssertions = reasoner.GetRelations(subject.Id, "can-contribute-to")
            .Select(assertion => (
                Effect: model.FindConcept(assertion.Object, SemanticConceptKind.Individual),
                Assertion: assertion))
            .Where(candidate =>
                candidate.Effect is not null &&
                reasoner.IsInstanceOf(candidate.Effect.Id, effectType.Id) &&
                reasoner.GetRelations(candidate.Effect.Id, "affects-person")
                    .Any(assertion => assertion.Object == targetType.Id))
            .ToArray();
        if (effectAssertions.Length == 0)
        {
            return false;
        }

        var descriptions = new List<string>();
        var sources = new List<SemanticWorldModelSource>();
        foreach (var candidate in effectAssertions)
        {
            var mechanismAssertions = reasoner.GetRelations(candidate.Effect!.Id, "can-arise-through");
            var mechanisms = mechanismAssertions
                .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
                .Where(concept => concept is not null)
                .Select(concept => concept!)
                .DistinctBy(concept => concept.Id)
                .ToArray();
            if (mechanisms.Length == 0)
            {
                continue;
            }

            descriptions.Add(
                $"{candidate.Effect.Label} (which can arise through {JoinConditions(mechanisms.Select(item => item.Label).ToArray())})");
            sources.Add(candidate.Assertion.Source);
            sources.AddRange(reasoner.GetMembershipEvidence(candidate.Effect.Id, effectType.Id));
            sources.AddRange(reasoner.GetRelations(candidate.Effect.Id, "affects-person")
                .Where(assertion => assertion.Object == targetType.Id)
                .Select(assertion => assertion.Source));
            sources.AddRange(mechanismAssertions.Select(assertion => assertion.Source));
            sources.AddRange(mechanisms.SelectMany(mechanism =>
                reasoner.GetMembershipEvidence(mechanism.Id, "adverse-effect-mechanism")));
        }

        if (descriptions.Count == 0)
        {
            return false;
        }

        message =
            $"{Capitalize(subject.Label)} is not inherently bad for people, but excessive or poorly timed use can contribute to {JoinConditions(descriptions.ToArray())}. These risks depend on usage patterns and can often be reduced through breaks, notification limits, comfortable viewing habits, and keeping phones away from bedtime.";
        evidence = ImmutableArray.Create(ToEvidence(
            sources.Distinct().ToImmutableArray(),
            LatticeEvidenceKind.Derived));
        return true;
    }

    private static bool TryExplainAbility(
        string requestedSubject,
        string requestedAbility,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;

        var subject = model.FindConcept(requestedSubject, SemanticConceptKind.Type);
        var ability = model.FindConcept(requestedAbility, SemanticConceptKind.Individual);
        if (subject is null || ability is null)
        {
            return false;
        }

        var adaptations = reasoner.GetRelations(subject.Id, "has-adaptation")
            .Select(assertion => (
                Concept: model.FindConcept(assertion.Object, SemanticConceptKind.Individual),
                Assertion: assertion))
            .Where(candidate => candidate.Concept is not null)
            .Select(candidate => (
                Concept: candidate.Concept!,
                Assertion: candidate.Assertion,
                AbilityAssertion: reasoner.GetRelations(candidate.Concept!.Id, "enables-ability")
                    .FirstOrDefault(assertion => assertion.Object == ability.Id)))
            .Where(candidate => candidate.AbilityAssertion is not null)
            .ToArray();

        if (adaptations.Length == 0)
        {
            return false;
        }

        var labels = adaptations
            .Select(candidate => candidate.Concept.Label)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var sources = adaptations
            .SelectMany(candidate => new[]
            {
                candidate.Assertion.Source,
                candidate.AbilityAssertion!.Source
            })
            .Concat(adaptations.SelectMany(candidate =>
                reasoner.GetMembershipEvidence(candidate.Concept.Id, "survival-adaptation")))
            .Concat(reasoner.GetMembershipEvidence(ability.Id, "biological-ability"))
            .Distinct()
            .ToImmutableArray();

        message = $"{Capitalize(requestedSubject)} can {ability.Label} because {string.Join("; ", labels)}.";
        evidence = ImmutableArray.Create(ToEvidence(sources, LatticeEvidenceKind.Derived));
        return true;
    }

    private static bool TryExplainCapability(
        string requestedSubject,
        string requestedCapability,
        string? requestedContext,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;
        if (!string.Equals(requestedCapability, "survive", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(requestedContext) ||
            !requestedContext.Contains("water", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var subjectType = model.FindConcept(requestedSubject, SemanticConceptKind.Type);
        var ability = model.FindConcept("survive-for-long-without-water", SemanticConceptKind.Individual);
        var condition = model.FindConcept("water-deprivation-condition", SemanticConceptKind.Individual);
        if (subjectType is null || ability is null || condition is null)
        {
            return false;
        }

        var adaptations = reasoner.GetRelations(subjectType.Id, "has-adaptation")
            .Select(assertion => (
                Concept: model.FindConcept(assertion.Object, SemanticConceptKind.Individual),
                Assertion: assertion))
            .Where(candidate => candidate.Concept is not null)
            .Select(candidate => (
                Concept: candidate.Concept!,
                candidate.Assertion,
                AbilityAssertion: reasoner.GetRelations(candidate.Concept!.Id, "enables-ability")
                    .FirstOrDefault(assertion => assertion.Object == ability.Id),
                ConditionAssertion: reasoner.GetRelations(candidate.Concept!.Id, "supports-survival-under")
                    .FirstOrDefault(assertion => assertion.Object == condition.Id)))
            .Where(candidate =>
                candidate.AbilityAssertion is not null &&
                candidate.ConditionAssertion is not null)
            .ToArray();

        var waterConservation = adaptations
            .Where(candidate => reasoner.IsInstanceOf(candidate.Concept.Id, "water-conservation"))
            .ToArray();
        var dehydrationTolerance = adaptations
            .Where(candidate => reasoner.IsInstanceOf(candidate.Concept.Id, "dehydration-tolerance-adaptation"))
            .ToArray();
        var processes = waterConservation
            .SelectMany(candidate => reasoner.GetRelations(candidate.Concept.Id, "reduces-water-loss"))
            .Select(assertion => (
                Concept: model.FindConcept(assertion.Object, SemanticConceptKind.Individual),
                assertion.Source))
            .Where(candidate => candidate.Concept is not null)
            .ToArray();

        if (waterConservation.Length == 0 ||
            dehydrationTolerance.Length == 0 ||
            processes.Length == 0)
        {
            return false;
        }

        var processNames = processes
            .Select(process => process.Concept!.Label)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var adaptationDetails = waterConservation
            .Select(candidate => candidate.Concept.Label)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var dehydrationDetails = dehydrationTolerance
            .Select(candidate => candidate.Concept.Label)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var subjectLabel = subjectType.Label.EndsWith("s", StringComparison.OrdinalIgnoreCase)
            ? subjectType.Label
            : $"{subjectType.Label}s";
        message = $"{subjectLabel} can survive periods without drinking water because their water-conservation adaptations limit water loss through {JoinConditions(processNames)}. In practice, {JoinConditions(adaptationDetails)}. Their dehydration-tolerance adaptation also supports survival during water deprivation: {JoinConditions(dehydrationDetails)}.";

        var sources = adaptations
            .SelectMany(candidate => new[]
            {
                candidate.Assertion.Source,
                candidate.AbilityAssertion!.Source,
                candidate.ConditionAssertion!.Source
            })
            .Concat(processes.Select(process => process.Source))
            .Concat(waterConservation.SelectMany(candidate =>
                reasoner.GetMembershipEvidence(candidate.Concept.Id, "water-conservation")))
            .Concat(dehydrationTolerance.SelectMany(candidate =>
                reasoner.GetMembershipEvidence(candidate.Concept.Id, "dehydration-tolerance-adaptation")))
            .Concat(reasoner.GetMembershipEvidence(ability.Id, "biological-ability"))
            .Distinct()
            .ToImmutableArray();
        evidence = ImmutableArray.Create(ToEvidence(sources, LatticeEvidenceKind.Derived));
        return true;
    }

    private static bool TryExplainCauses(
        string requestedConcept,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;
        var type = model.FindConcept(requestedConcept, SemanticConceptKind.Type);
        if (type is null)
        {
            return false;
        }

        var instances = model.Concepts
            .Where(concept =>
                concept.Kind == SemanticConceptKind.Individual &&
                reasoner.IsInstanceOf(concept.Id, type.Id))
            .ToArray();
        var mechanisms = instances
            .SelectMany(instance => reasoner.GetRelations(instance.Id, "has-mechanism"))
            .ToArray();
        if (mechanisms.Length == 0)
        {
            return false;
        }

        var labels = mechanisms
            .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
            .Where(concept => concept is not null)
            .Select(concept => concept!.Label)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (labels.Length == 0)
        {
            return false;
        }

        var clauses = new List<string>
        {
            $"{Capitalize(type.Label)} can arise through {JoinConditions(labels)}."
        };
        var sources = mechanisms.Select(assertion => assertion.Source)
            .Concat(instances.SelectMany(instance =>
                reasoner.GetMembershipEvidence(instance.Id, type.Id)))
            .ToList();

        var references = instances
            .SelectMany(instance => reasoner.GetRelations(instance.Id, "has-research-reference"))
            .ToArray();
        var referenceDetails = references
            .Select(assertion => FormatResearchReference(assertion.Object, model, reasoner))
            .OfType<string>()
            .ToArray();
        if (referenceDetails.Length > 0)
        {
            clauses.Add($"Research references include {JoinConditions(referenceDetails)}.");
            sources.AddRange(references.Select(assertion => assertion.Source));
            foreach (var reference in references)
            {
                sources.AddRange(reasoner.GetRelations(reference.Object)
                    .Select(assertion => assertion.Source));
                sources.AddRange(model.GetDataAssertions(reference.Object, "doi-url")
                    .Select(assertion => assertion.Source));
            }
        }

        message = string.Join(" ", clauses);
        evidence = ImmutableArray.Create(ToEvidence(sources.Distinct().ToImmutableArray(), LatticeEvidenceKind.Derived));
        return true;
    }

    internal static bool TryClassify(
        MultiItemClassificationFormula request,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;
        var itemType = request.ItemType.Value;
        var results = new List<(string Item, ImmutableArray<string> Categories, ImmutableArray<SemanticWorldModelSource> Sources)>();

        foreach (var requestedItem in request.Items)
        {
            var item = model.FindConcept(requestedItem.Value);
            if (item is null)
            {
                return false;
            }

            var matching = new List<(string Label, ImmutableArray<SemanticWorldModelSource> Sources)>();
            foreach (var category in request.Categories)
            {
                var categoryType = model.FindConcept(
                    $"{category.Value}-{itemType}",
                    SemanticConceptKind.Type);
                if (categoryType is null)
                {
                    continue;
                }

                var sources = item.Kind switch
                {
                    SemanticConceptKind.Individual =>
                        reasoner.GetMembershipEvidence(item.Id, categoryType.Id),
                    SemanticConceptKind.Type =>
                        GetSubtypeEvidence(item.Id, categoryType.Id, model),
                    _ => ImmutableArray<SemanticWorldModelSource>.Empty
                };
                if (!sources.IsDefaultOrEmpty)
                {
                    matching.Add((category.Value, sources));
                }
            }

            if (matching.Count == 0)
            {
                return false;
            }

            results.Add((
                item.Label,
                matching.Select(item => item.Label).ToImmutableArray(),
                matching.SelectMany(item => item.Sources).Distinct().ToImmutableArray()));
        }

        var groups = request.Categories
            .Select(category => (
                Category: category.Value,
                Items: results
                    .Where(result => result.Categories.Contains(category.Value))
                    .Select(result => result.Item)
                    .ToArray()))
            .Where(group => group.Items.Length > 0)
            .ToArray();

        message = string.Join(
            " ",
            groups.Select(group => FormatClassificationGroup(
                group.Category,
                group.Items,
                itemType)));

        evidence = results
            .Select(result => ToEvidence(result.Sources, LatticeEvidenceKind.Derived))
            .ToImmutableArray();
        return true;
    }

    private static string FormatClassificationGroup(
        string category,
        IReadOnlyList<string> items,
        string itemType)
    {
        var categoryPhrase = category.Replace('-', ' ');
        var itemNames = Capitalize(JoinNames(items
            .Select(item => item.Length == 0
                ? item
                : $"{char.ToLowerInvariant(item[0])}{item[1..]}")
            .ToArray()));

        if (itemType.StartsWith("thing-for-", StringComparison.Ordinal))
        {
            var purpose = itemType["thing-for-".Length..].Replace('-', ' ');
            return $"{Capitalize(categoryPhrase)} for {purpose}: {itemNames}.";
        }

        var typePhrase = itemType.Replace('-', ' ');
        var pluralType = typePhrase.EndsWith("s", StringComparison.Ordinal)
            ? typePhrase
            : $"{typePhrase}s";

        return $"{Capitalize(categoryPhrase)} {pluralType}: {itemNames}.";
    }

    private static ImmutableArray<SemanticWorldModelSource> GetSubtypeEvidence(
        string candidate,
        string expected,
        SemanticWorldModel model)
    {
        if (candidate == expected)
        {
            return [];
        }

        var pending = new Queue<string>();
        var visited = new HashSet<string>(StringComparer.Ordinal) { candidate };
        var previous = new Dictionary<string, SemanticRelationAssertion>(StringComparer.Ordinal);
        pending.Enqueue(candidate);

        while (pending.TryDequeue(out var current))
        {
            foreach (var assertion in model.RelationAssertions.Where(assertion =>
                assertion.Subject == current &&
                assertion.Relation == "is-a"))
            {
                if (!visited.Add(assertion.Object))
                {
                    continue;
                }

                previous[assertion.Object] = assertion;
                if (assertion.Object == expected)
                {
                    var sources = new List<SemanticWorldModelSource>();
                    var type = expected;
                    while (type != candidate)
                    {
                        var step = previous[type];
                        sources.Add(step.Source);
                        type = step.Subject;
                    }

                    sources.Reverse();
                    return sources.ToImmutableArray();
                }

                pending.Enqueue(assertion.Object);
            }
        }

        return [];
    }

    internal static bool TryChoice(
        ChoiceQuestionFormula question,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        ArgumentNullException.ThrowIfNull(question);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(reasoner);

        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;
        var category = model.FindConcept(question.Category.Value, SemanticConceptKind.Type);
        if (category is null)
        {
            return false;
        }

        var supported = question.Choices
            .Select(choice => (
                Choice: model.FindConcept(choice.Value, SemanticConceptKind.Individual),
                Evidence: model.FindConcept(choice.Value, SemanticConceptKind.Individual) is { } individual
                    ? reasoner.GetMembershipEvidence(individual.Id, category.Id)
                    : ImmutableArray<SemanticWorldModelSource>.Empty))
            .Where(candidate => candidate.Choice is not null && !candidate.Evidence.IsDefaultOrEmpty)
            .ToArray();

        if (supported.Length != 1)
        {
            return false;
        }

        message = $"{Capitalize(supported[0].Choice!.Label)} is a species of {category.Label}.";
        evidence = ImmutableArray.Create(ToEvidence(
            supported[0].Evidence,
            LatticeEvidenceKind.Derived));
        return true;
    }

    internal static bool TryAskMembership(
        string individual,
        string type,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        evidence = ImmutableArray<LatticeEvidence>.Empty;
        var item = model.FindConcept(individual, SemanticConceptKind.Individual);
        var targetType = model.FindConcept(type, SemanticConceptKind.Type);
        if (item is null || targetType is null)
        {
            return false;
        }

        var sources = reasoner.GetMembershipEvidence(item.Id, targetType.Id);
        if (sources.IsDefaultOrEmpty)
        {
            return false;
        }

        evidence = ImmutableArray.Create(ToEvidence(sources, LatticeEvidenceKind.Derived));
        return true;
    }

    private static bool TryWhy(
        string individualId,
        string typeId,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;
        var individual = model.FindConcept(individualId, SemanticConceptKind.Individual);
        var type = model.FindConcept(typeId, SemanticConceptKind.Type);
        var definition = type is null ? null : model.FindDefinition(type.Id);
        if (individual is null ||
            type is null ||
            definition?.SufficientExpression is null ||
            !TryExplainExpression(
                individual.Id,
                definition.SufficientExpression,
                model,
                reasoner,
                out var phrases,
                out var sources))
        {
            return false;
        }

        message = $"{Capitalize(IndefinitePhrase(individual.Label))} is {IndefinitePhrase(type.Label)} because it {JoinConditions(phrases)}.";
        sources = ImmutableArray.Create(definition.Source).AddRange(sources);
        evidence = ImmutableArray.Create(ToEvidence(sources.Distinct().ToImmutableArray(), LatticeEvidenceKind.Derived));
        return true;
    }

    private static bool TryExplainExpression(
        string individual,
        SemanticClassExpression expression,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out ImmutableArray<string> phrases,
        out ImmutableArray<SemanticWorldModelSource> sources)
    {
        phrases = [];
        sources = [];
        if (expression.Kind == SemanticClassExpressionKind.Some)
        {
            foreach (var candidate in reasoner.GetRelations(individual, expression.Relation))
            {
                if (!reasoner.IsInstanceOf(candidate.Object, expression.TargetType!))
                {
                    continue;
                }

                var nestedPhrases = ImmutableArray<string>.Empty;
                var nestedSources = ImmutableArray<SemanticWorldModelSource>.Empty;
                if (expression.Filler is not null &&
                    !TryExplainExpression(
                        candidate.Object,
                        expression.Filler,
                        model,
                        reasoner,
                        out nestedPhrases,
                        out nestedSources))
                {
                    continue;
                }

                var phrase = FormatCondition(
                    new SemanticRestriction(expression.Relation!, expression.TargetType!),
                    model);
                if (phrase is null)
                {
                    return false;
                }

                if (!nestedPhrases.IsDefaultOrEmpty)
                {
                    phrase += $" that {JoinConditions(nestedPhrases)}";
                }

                phrases = [phrase];
                sources = ImmutableArray.Create(candidate.Source)
                    .AddRange(reasoner.GetMembershipEvidence(candidate.Object, expression.TargetType!))
                    .AddRange(nestedSources);
                return true;
            }

            return false;
        }

        if (expression.Kind == SemanticClassExpressionKind.All)
        {
            var relation = model.FindRelation(expression.Relation!);
            var target = model.FindConcept(expression.TargetType!, SemanticConceptKind.Type);
            if (relation is null || target is null)
            {
                return false;
            }

            var assertionSources = ImmutableArray.CreateBuilder<SemanticWorldModelSource>();
            foreach (var candidate in reasoner.GetRelations(individual, expression.Relation))
            {
                if (!reasoner.IsInstanceOf(candidate.Object, expression.TargetType!))
                {
                    return false;
                }

                assertionSources.Add(candidate.Source);
                assertionSources.AddRange(reasoner.GetMembershipEvidence(candidate.Object, expression.TargetType!));
                if (expression.Filler is not null)
                {
                    if (!TryExplainExpression(candidate.Object, expression.Filler, model, reasoner,
                        out _, out var nestedSources))
                    {
                        return false;
                    }

                    assertionSources.AddRange(nestedSources);
                }
            }

            var phrase = $"{relation.Verb} only elements of type {target.Label}";
            if (expression.Filler is not null)
            {
                var fillerPhrase = FormatExpression(expression.Filler, model);
                if (fillerPhrase is null)
                {
                    return false;
                }

                phrase += $" that {fillerPhrase}";
            }

            phrases = [phrase];
            sources = assertionSources.Distinct().ToImmutableArray();
            return true;
        }

        if (expression.Kind == SemanticClassExpressionKind.Cycle)
        {
            var startRelation = model.FindRelation(expression.StartRelation!);
            var traversalRelation = model.FindRelation(expression.Relation!);
            var targetType = model.FindConcept(expression.TargetType!, SemanticConceptKind.Type);
            var sourceConnection = expression.SourceConnectionRelation is null
                ? null
                : model.FindRelation(expression.SourceConnectionRelation);
            var targetConnection = expression.TargetConnectionRelation is null
                ? null
                : model.FindRelation(expression.TargetConnectionRelation);
            var cycleSources = reasoner.GetCycleEvidence(individual, expression);
            if (startRelation is null || traversalRelation is null ||
                targetType is null || cycleSources.IsDefaultOrEmpty)
            {
                return false;
            }

            phrases =
            [
                $"{startRelation.Verb} {IndefinitePhrase(targetType.Label)} and follows {traversalRelation.Verb} links for at least {expression.MinimumLength} elements before returning to that same element" +
                (sourceConnection is null || targetConnection is null
                    ? string.Empty
                    : $", with each transition matching {sourceConnection.Verb} to {targetConnection.Verb} by identity")
            ];
            sources = cycleSources;
            return true;
        }

        if (expression.Kind == SemanticClassExpressionKind.Or)
        {
            foreach (var operand in expression.Operands)
            {
                if (TryExplainExpression(individual, operand, model, reasoner, out phrases, out sources))
                {
                    return true;
                }
            }

            return false;
        }

        var phraseBuilder = ImmutableArray.CreateBuilder<string>();
        var sourceBuilder = ImmutableArray.CreateBuilder<SemanticWorldModelSource>();
        foreach (var operand in expression.Operands)
        {
            if (!TryExplainExpression(individual, operand, model, reasoner, out var childPhrases, out var childSources))
            {
                return false;
            }

            phraseBuilder.AddRange(childPhrases);
            sourceBuilder.AddRange(childSources);
        }

        phrases = phraseBuilder.ToImmutable();
        sources = sourceBuilder.ToImmutable();
        return true;
    }

    private static bool TryExplainMechanism(
        string individualId,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;
        var individual = model.FindConcept(individualId, SemanticConceptKind.Individual);
        if (individual is null)
        {
            return false;
        }

        var mechanismAssertions = reasoner.GetRelations(individual.Id, "has-sound-production-mechanism");
        if (mechanismAssertions.IsDefaultOrEmpty)
        {
            return false;
        }

        var sentences = new List<string>();
        var sources = new List<SemanticWorldModelSource>();
        var visited = new HashSet<string>(StringComparer.Ordinal);

        foreach (var mechanism in mechanismAssertions)
        {
            var relation = model.FindRelation(mechanism.Relation);
            var target = model.FindConcept(mechanism.Object, SemanticConceptKind.Individual);
            if (relation is null || target is null)
            {
                continue;
            }

            sentences.Add(FormatRelation(individual, target, relation, first: true));
            sources.Add(mechanism.Source);
            AppendMechanismChain(target.Id, model, reasoner, visited, sentences, sources, 0);
        }

        if (sentences.Count == 0)
        {
            return false;
        }

        message = string.Join(" ", sentences);
        evidence = ImmutableArray.Create(ToEvidence(sources.Distinct().ToImmutableArray(), LatticeEvidenceKind.Derived));
        return true;
    }

    private static void AppendMechanismChain(
        string subjectId,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        HashSet<string> visited,
        List<string> sentences,
        List<SemanticWorldModelSource> sources,
        int depth)
    {
        if (depth >= 6 || !visited.Add(subjectId))
        {
            return;
        }

        foreach (var assertion in reasoner.GetRelations(subjectId)
            .Where(assertion => (model.FindRelation(assertion.Relation)?.Order ?? 0) > 0))
        {
            var relation = model.FindRelation(assertion.Relation);
            var subject = model.FindConcept(assertion.Subject, SemanticConceptKind.Individual);
            var target = model.FindConcept(assertion.Object, SemanticConceptKind.Individual);
            if (relation is null || subject is null || target is null)
            {
                continue;
            }

            sentences.Add(FormatRelation(subject, target, relation, first: false));
            sources.Add(assertion.Source);
            AppendMechanismChain(target.Id, model, reasoner, visited, sentences, sources, depth + 1);
        }
    }

    private static bool TryExplainRequiredKnowledge(
        string requestedActivity,
        string requestedKnowledge,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;

        var activity = model.FindConcept(requestedActivity, SemanticConceptKind.Individual);
        var knowledge = model.FindConcept(requestedKnowledge, SemanticConceptKind.Individual);
        if (activity is null ||
            knowledge is null ||
            !reasoner.IsInstanceOf(activity.Id, "creative-activity") ||
            !reasoner.IsInstanceOf(knowledge.Id, "knowledge-domain"))
        {
            return false;
        }

        var entryRequirements = reasoner.GetRelations(activity.Id, "has-entry-requirement");
        var startingMethods = reasoner.GetRelations(activity.Id, "can-begin-through");
        var helpfulTopics = reasoner.GetRelations(activity.Id, "benefits-from");
        var advancedTopics = reasoner.GetRelations(activity.Id, "later-benefits-from");
        if (entryRequirements.Length != 1 ||
            startingMethods.Length != 3 ||
            helpfulTopics.Length != 4 ||
            advancedTopics.Length != 3)
        {
            return false;
        }

        var entryRequirement = model.FindConcept(entryRequirements[0].Object, SemanticConceptKind.Individual);
        var starts = startingMethods
            .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
            .ToArray();
        var helpful = helpfulTopics
            .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
            .ToArray();
        var advanced = advancedTopics
            .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
            .ToArray();
        if (entryRequirement is null ||
            starts.Any(concept => concept is null) ||
            helpful.Any(concept => concept is null) ||
            advanced.Any(concept => concept is null))
        {
            return false;
        }

        var sources = entryRequirements
            .Concat(startingMethods)
            .Concat(helpfulTopics)
            .Concat(advancedTopics)
            .Select(assertion => assertion.Source)
            .Concat(reasoner.GetMembershipEvidence(activity.Id, "creative-activity"))
            .Concat(reasoner.GetMembershipEvidence(knowledge.Id, "knowledge-domain"))
            .Distinct()
            .ToImmutableArray();

        message = $"You do not need {entryRequirement.Label} to start {activity.Label}. You can begin with {JoinNames(starts.Select(concept => concept!.Label).ToArray())}. Some basic {knowledge.Label}, such as {JoinNames(helpful.Select(concept => concept!.Label).ToArray())}, can help you understand and develop what you hear. As your compositions become more ambitious, {JoinNames(advanced.Select(concept => concept!.Label).ToArray())} can become useful, but they are tools to learn as needed rather than prerequisites. Start composing and add theory as it helps you solve musical problems.";
        evidence = ImmutableArray.Create(ToEvidence(sources, LatticeEvidenceKind.Derived));
        return true;
    }

    private static bool TryExplainApplications(
        string requestedMethod,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;

        var method = model.FindConcept(requestedMethod, SemanticConceptKind.Individual);
        if (method is null || !reasoner.IsInstanceOf(method.Id, "limit-evaluation-method"))
        {
            return false;
        }

        var purposeAssertions = reasoner.GetRelations(method.Id, "has-purpose");
        var formAssertions = reasoner.GetRelations(method.Id, "applies-to-indeterminate-form");
        var conditionAssertions = reasoner.GetRelations(method.Id, "requires-condition");
        var applicationAssertions = reasoner.GetRelations(method.Id, "broader-calculus-context");
        if (purposeAssertions.Length != 1 ||
            formAssertions.Length != 2 ||
            conditionAssertions.Length != 3 ||
            applicationAssertions.Length != 4)
        {
            return false;
        }

        var purpose = model.FindConcept(purposeAssertions[0].Object, SemanticConceptKind.Individual);
        var forms = formAssertions
            .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
            .ToArray();
        var conditions = conditionAssertions
            .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
            .ToArray();
        var applications = applicationAssertions
            .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
            .ToArray();
        if (purpose is null ||
            forms.Any(concept => concept is null) ||
            conditions.Any(concept => concept is null) ||
            applications.Any(concept => concept is null))
        {
            return false;
        }

        var formNames = forms.Select(concept => concept!.Label).ToArray();
        var conditionNames = conditions.Select(concept => concept!.Label).ToArray();
        var applicationNames = applications.Select(concept => concept!.Label).ToArray();
        var sources = purposeAssertions
            .Concat(formAssertions)
            .Concat(conditionAssertions)
            .Concat(applicationAssertions)
            .Select(assertion => assertion.Source)
            .Concat(reasoner.GetMembershipEvidence(method.Id, "limit-evaluation-method"))
            .Distinct()
            .ToImmutableArray();

        message = $"{method.Label} is a calculus method for {purpose.Label}. It applies when direct substitution gives {JoinNames(formNames)}. The rule requires that {JoinNames(conditionNames)}; when those conditions hold, the limit can be evaluated using the quotient of the derivatives of the numerator and denominator. In day-to-day life, you are unlikely to apply the rule directly. The broader calculus of limits and derivatives supports {JoinNames(applicationNames)}. These calculations are usually handled in mathematical models or by software; ordinary tasks such as working out a bill or an average travel time do not require L'Hôpital's Rule.";
        evidence = ImmutableArray.Create(ToEvidence(sources, LatticeEvidenceKind.Derived));
        return true;
    }

    private static bool TryWhyUse(
        string requestedAssistant,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;

        var assistant = model.FindConcept(requestedAssistant, SemanticConceptKind.Individual);
        if (assistant is null ||
            !reasoner.IsInstanceOf(assistant.Id, "conversational-ai-assistant"))
        {
            return false;
        }

        var benefitAssertions = reasoner.GetRelations(assistant.Id, "offers-benefit");
        if (benefitAssertions.IsDefaultOrEmpty ||
            !TryGetSingleTarget("has-project-status", out var projectStatus, out var statusSource) ||
            !TryGetSingleTarget("has-access-model", out var accessModel, out var accessSource) ||
            !TryGetSingleTarget("has-licensing-model", out var licensingModel, out var licensingSource) ||
            !TryGetSingleTarget("has-demo-status", out var demoStatus, out var demoSource) ||
            !TryGetSingleTarget("has-model-quality", out var modelQuality, out var qualitySource) ||
            !TryGetSingleTarget("has-hardware-requirement", out var hardwareRequirement, out var hardwareSource))
        {
            return false;
        }

        var benefits = benefitAssertions
            .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
            .Where(concept => concept is not null)
            .Select(concept => concept!.Label)
            .ToArray();
        if (benefits.Length == 0)
        {
            return false;
        }

        var sources = benefitAssertions
            .Select(assertion => assertion.Source)
            .Append(statusSource)
            .Append(accessSource)
            .Append(licensingSource)
            .Append(demoSource)
            .Append(qualitySource)
            .Append(hardwareSource)
            .Distinct()
            .ToImmutableArray();

        message = $"{assistant.Label} is worth exploring for its {JoinNames(benefits)}. It is {IndefinitePhrase(projectStatus)}, and its {demoStatus}. You can use its {accessModel}. Its licensing terms: {licensingModel}. {Capitalize(modelQuality)}; {hardwareRequirement}. It is best suited to research and self-hosted experimentation, not as a maintained hosted chatbot.";
        evidence = ImmutableArray.Create(ToEvidence(sources, LatticeEvidenceKind.Derived));
        return true;

        bool TryGetSingleTarget(
            string relation,
            out string label,
            out SemanticWorldModelSource source)
        {
            label = string.Empty;
            source = new SemanticWorldModelSource(string.Empty);
            var assertions = reasoner.GetRelations(assistant.Id, relation);
            if (assertions.Length != 1)
            {
                return false;
            }

            var target = model.FindConcept(assertions[0].Object, SemanticConceptKind.Individual);
            if (target is null)
            {
                return false;
            }

            label = target.Label;
            source = assertions[0].Source;
            return true;
        }
    }

    private static bool TryExplainSamplingParameters(
        string firstParameterId,
        string secondParameterId,
        string domainId,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;

        var first = model.FindConcept(firstParameterId, SemanticConceptKind.Individual);
        var second = model.FindConcept(secondParameterId, SemanticConceptKind.Individual);
        var domain = model.FindConcept(domainId, SemanticConceptKind.Type);
        if (first is null ||
            second is null ||
            domain is null ||
            !reasoner.IsInstanceOf(first.Id, "llm-sampling-parameter") ||
            !reasoner.IsInstanceOf(second.Id, "llm-sampling-parameter"))
        {
            return false;
        }

        SemanticRelationAssertion? GetFirst(string subject, string relation) =>
            reasoner.GetRelations(subject, relation).FirstOrDefault();

        var firstModel = GetFirst(first.Id, "llm-parameter-of");
        var secondModel = GetFirst(second.Id, "llm-parameter-of");
        if (firstModel is null ||
            secondModel is null ||
            firstModel.Object != secondModel.Object ||
            !reasoner.IsInstanceOf(firstModel.Object, domain.Id))
        {
            return false;
        }

        var firstLowerEffect = GetFirst(first.Id, "llm-lower-setting-effect");
        var secondLowerEffect = GetFirst(second.Id, "llm-lower-setting-effect");
        var temperature = firstLowerEffect is not null ? first : second;
        var topP = firstLowerEffect is not null ? second : first;
        var lowerEffectAssertion = firstLowerEffect ?? secondLowerEffect;
        var higherEffectAssertion = GetFirst(temperature.Id, "llm-higher-setting-effect");
        var temperatureMethodAssertion = GetFirst(temperature.Id, "llm-uses-method");
        var topPMethodAssertion = GetFirst(topP.Id, "llm-uses-method");
        if (lowerEffectAssertion is null ||
            higherEffectAssertion is null ||
            temperatureMethodAssertion is null ||
            topPMethodAssertion is null)
        {
            return false;
        }

        var temperatureMethod = model.FindConcept(temperatureMethodAssertion.Object, SemanticConceptKind.Individual);
        var topPMethod = model.FindConcept(topPMethodAssertion.Object, SemanticConceptKind.Individual);
        if (temperatureMethod is null || topPMethod is null)
        {
            return false;
        }

        var temperatureInputAssertion = GetFirst(temperatureMethod.Id, "llm-method-rescales");
        var temperatureStageAssertion = GetFirst(temperatureMethod.Id, "llm-method-before");
        var topPInputAssertion = GetFirst(topPMethod.Id, "llm-method-acts-on");
        var topPStageAssertion = GetFirst(topPMethod.Id, "llm-method-after");
        var topPOrderAssertion = GetFirst(topPMethod.Id, "llm-method-orders-by");
        var topPNucleusAssertion = GetFirst(topPMethod.Id, "llm-method-retains");
        var topPRenormalizationAssertion = GetFirst(topPMethod.Id, "llm-method-renormalizes");
        if (temperatureInputAssertion is null ||
            temperatureStageAssertion is null ||
            topPInputAssertion is null ||
            topPStageAssertion is null ||
            topPOrderAssertion is null ||
            topPNucleusAssertion is null ||
            topPRenormalizationAssertion is null)
        {
            return false;
        }

        var temperatureInput = model.FindConcept(temperatureInputAssertion.Object, SemanticConceptKind.Individual);
        var temperatureStage = model.FindConcept(temperatureStageAssertion.Object, SemanticConceptKind.Individual);
        var topPInput = model.FindConcept(topPInputAssertion.Object, SemanticConceptKind.Individual);
        var topPStage = model.FindConcept(topPStageAssertion.Object, SemanticConceptKind.Individual);
        var orderedBy = model.FindConcept(topPOrderAssertion.Object, SemanticConceptKind.Individual);
        var nucleus = model.FindConcept(topPNucleusAssertion.Object, SemanticConceptKind.Individual);
        var renormalized = model.FindConcept(topPRenormalizationAssertion.Object, SemanticConceptKind.Individual);
        var lowerEffect = model.FindConcept(lowerEffectAssertion.Object, SemanticConceptKind.Individual);
        var higherEffect = model.FindConcept(higherEffectAssertion.Object, SemanticConceptKind.Individual);
        if (temperatureInput is null ||
            temperatureStage is null ||
            topPInput is null ||
            topPStage is null ||
            orderedBy is null ||
            nucleus is null ||
            renormalized is null ||
            lowerEffect is null ||
            higherEffect is null)
        {
            return false;
        }

        if (!SamplingParameterCalculator.TryCalculateTemperatureDistribution(
                [2d, 1d, 0d],
                0.5d,
                out var lowTemperature,
                out _) ||
            !SamplingParameterCalculator.TryCalculateTemperatureDistribution(
                [2d, 1d, 0d],
                2d,
                out var highTemperature,
                out _) ||
            !SamplingParameterCalculator.TrySelectNucleus(
                [0.6d, 0.3d, 0.1d],
                0.8d,
                out var retainedIndices,
                out var retainedProbabilities,
                out _))
        {
            return false;
        }

        var retainedTokens = JoinNames(retainedIndices.Select(index => $"token {index + 1}").ToArray());
        var lowTemperatureExample = string.Join(", ", lowTemperature.Select(FormatPercentage));
        var highTemperatureExample = string.Join(", ", highTemperature.Select(FormatPercentage));
        var retainedExample = string.Join(" and ", retainedProbabilities.Select(FormatPercentage));
        var temperatureSentence =
            $"{Capitalize(temperature.Label)} uses {temperatureMethod.Label}. " +
            $"{Capitalize(temperatureMethod.Label)} {model.FindRelation(temperatureInputAssertion.Relation)!.Verb} {temperatureInput.Label} by dividing each logit by T " +
            $"and {model.FindRelation(temperatureStageAssertion.Relation)!.Verb} {temperatureStage.Label}. " +
            $"Lower temperature {model.FindRelation(lowerEffectAssertion.Relation)!.Verb}; " +
            $"higher temperature {model.FindRelation(higherEffectAssertion.Relation)!.Verb}.";
        var topPSentence =
            $"{Capitalize(topP.Label)} uses {topPMethod.Label}. " +
            $"{Capitalize(topPMethod.Label)} {model.FindRelation(topPInputAssertion.Relation)!.Verb} {topPInput.Label} " +
            $"and {model.FindRelation(topPStageAssertion.Relation)!.Verb} {topPStage.Label}. " +
            $"It {model.FindRelation(topPOrderAssertion.Relation)!.Verb} {orderedBy.Label}, " +
            $"{model.FindRelation(topPNucleusAssertion.Relation)!.Verb} {nucleus.Label}, " +
            $"then {model.FindRelation(topPRenormalizationAssertion.Relation)!.Verb} {renormalized.Label}.";

        message =
            $"{Capitalize(temperature.Label)} and {topP.Label} are both sampling parameters of a language model, but they affect token selection differently. " +
            $"{temperatureSentence} {topPSentence} " +
            $"For example, with next-token logits [2, 1, 0], temperature 0.5 gives probabilities {lowTemperatureExample}, " +
            $"while temperature 2 gives {highTemperatureExample}. " +
            $"With token probabilities 0.6, 0.3, and 0.1 and top-p = 0.8, nucleus sampling keeps {retainedTokens} " +
            $"because their cumulative probability reaches 0.9, then renormalizes them to {retainedExample}. " +
            "In short, temperature changes how concentrated the probability distribution is; top-p changes which tokens remain eligible.";

        var sources = new[]
        {
            firstModel.Source,
            secondModel.Source,
            temperatureMethodAssertion.Source,
            topPMethodAssertion.Source,
            temperatureInputAssertion.Source,
            temperatureStageAssertion.Source,
            topPInputAssertion.Source,
            topPStageAssertion.Source,
            topPOrderAssertion.Source,
            topPNucleusAssertion.Source,
            topPRenormalizationAssertion.Source,
            lowerEffectAssertion.Source,
            higherEffectAssertion.Source
        }.Distinct().ToImmutableArray();
        evidence = ImmutableArray.Create(ToEvidence(sources, LatticeEvidenceKind.Derived));
        return true;
    }

    private static bool TryCompare(
        string firstId,
        string secondId,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;
        var first = model.FindConcept(firstId, SemanticConceptKind.Individual);
        var second = model.FindConcept(secondId, SemanticConceptKind.Individual);
        if (first is null || second is null)
        {
            return false;
        }

        var clauses = new List<string>();
        var sources = new List<SemanticWorldModelSource>();
        foreach (var relation in model.Relations
            .Where(relation => relation.ComparisonPriority > 0)
            .OrderBy(relation => relation.ComparisonPriority))
        {
            var firstFacts = reasoner.GetRelations(first.Id, relation.Id);
            var secondFacts = reasoner.GetRelations(second.Id, relation.Id);
            if (firstFacts.IsDefaultOrEmpty || secondFacts.IsDefaultOrEmpty)
            {
                continue;
            }

            var firstObject = model.FindConcept(firstFacts[0].Object, SemanticConceptKind.Individual);
            var secondObject = model.FindConcept(secondFacts[0].Object, SemanticConceptKind.Individual);
            if (firstObject is null || secondObject is null)
            {
                continue;
            }

            var firstType = GetMostSpecificType(firstObject.Id, model, reasoner);
            var secondType = GetMostSpecificType(secondObject.Id, model, reasoner);
            if (firstType is null || secondType is null)
            {
                continue;
            }

            if (firstType.Id == secondType.Id)
            {
                var verb = relation.Verb == "has" ? "have" : relation.Verb;
                var role = relation.ObjectRole?.Replace("its", "their", StringComparison.Ordinal) ?? string.Empty;
                clauses.Add($"Both {verb} {IndefinitePhrase(firstType.Label)} {role}".TrimEnd());
            }
            else
            {
                clauses.Add($"{Capitalize(first.Label)} {relation.Verb} {IndefinitePhrase(firstType.Label)}, while {second.Label} {relation.Verb} {IndefinitePhrase(secondType.Label)}");
            }

            sources.Add(firstFacts[0].Source);
            sources.Add(secondFacts[0].Source);
        }

        if (clauses.Count == 0)
        {
            return false;
        }

        message = string.Join(". ", clauses).TrimEnd('.') + ".";
        evidence = ImmutableArray.Create(ToEvidence(sources.Distinct().ToImmutableArray(), LatticeEvidenceKind.Derived));
        return true;
    }

    private static SemanticConcept? GetMostSpecificType(
        string individual,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner)
    {
        var types = reasoner.GetTypes(individual);
        return types
            .Where(type => !types.Any(other =>
                other != type && IsSubtype(other, type, model)))
            .Select(type => model.FindConcept(type, SemanticConceptKind.Type))
            .Where(type => type is not null)
            .OrderBy(type => type!.Label, StringComparer.Ordinal)
            .FirstOrDefault();
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

    private static string? FormatResearchReference(
        string publicationId,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner)
    {
        var publication = model.FindConcept(publicationId, SemanticConceptKind.Individual);
        if (publication is null)
        {
            return null;
        }

        var authors = reasoner.GetRelations(publicationId, "authored-by")
            .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
            .Where(author => author is not null)
            .Select(author => author!.Label.Split(' ', StringSplitOptions.RemoveEmptyEntries)[^1])
            .ToArray();
        var year = reasoner.GetRelations(publicationId, "published-in-year")
            .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
            .FirstOrDefault(concept => concept is not null);
        var journal = reasoner.GetRelations(publicationId, "published-in")
            .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
            .FirstOrDefault(concept => concept is not null);
        var citation = authors.Length == 0
            ? publication.Label
            : year is null
                ? $"{JoinNames(authors)}, {publication.Label}"
                : $"{JoinNames(authors)} ({year.Label}), {publication.Label}";
        if (journal is not null)
        {
            citation += $", {journal.Label}";
        }

        return citation;
    }

    private static ImmutableArray<SemanticWorldModelSource> GetTypeEvidence(
        string individual,
        string type,
        SemanticWorldModel model) =>
        model.RelationAssertions
            .Where(assertion =>
                assertion.Subject == individual &&
                assertion.Relation == "instance-of" &&
                IsSubtype(assertion.Object, type, model))
            .Select(assertion => assertion.Source)
            .ToImmutableArray();

    private static string? FormatExpression(
        SemanticClassExpression? expression,
        SemanticWorldModel model)
    {
        if (expression is null)
        {
            return null;
        }

        if (expression.Kind == SemanticClassExpressionKind.Some)
        {
            var phrase = FormatCondition(
                new SemanticRestriction(expression.Relation!, expression.TargetType!),
                model);
            if (phrase is null || expression.Filler is null)
            {
                return phrase;
            }

            var filler = FormatExpression(expression.Filler, model);
            return filler is null ? null : $"{phrase} that {filler}";
        }

        if (expression.Kind == SemanticClassExpressionKind.All)
        {
            var relation = model.FindRelation(expression.Relation!);
            var target = model.FindConcept(expression.TargetType!, SemanticConceptKind.Type);
            if (relation is null || target is null)
            {
                return null;
            }

            var phrase = $"{relation.Verb} only elements of type {target.Label}";
            if (expression.Filler is null)
            {
                return phrase;
            }

            var filler = FormatExpression(expression.Filler, model);
            return filler is null ? null : $"{phrase} that {filler}";
        }

        if (expression.Kind == SemanticClassExpressionKind.Cycle)
        {
            var start = model.FindRelation(expression.StartRelation!);
            var traversal = model.FindRelation(expression.Relation!);
            var target = model.FindConcept(expression.TargetType!, SemanticConceptKind.Type);
            var sourceConnection = expression.SourceConnectionRelation is null
                ? null
                : model.FindRelation(expression.SourceConnectionRelation);
            var targetConnection = expression.TargetConnectionRelation is null
                ? null
                : model.FindRelation(expression.TargetConnectionRelation);
            return start is null || traversal is null || target is null
                ? null
                : $"{start.Verb} {IndefinitePhrase(target.Label)} and follows {traversal.Verb} links for at least {expression.MinimumLength} elements before returning to that same element" +
                  (sourceConnection is null || targetConnection is null
                      ? string.Empty
                      : $", with each transition matching {sourceConnection.Verb} to {targetConnection.Verb} by identity");
        }

        var phrases = expression.Operands
            .Select(operand => FormatExpression(operand, model))
            .ToArray();
        if (phrases.Any(phrase => phrase is null))
        {
            return null;
        }

        var validPhrases = phrases.Select(phrase => phrase!).ToArray();
        return expression.Kind == SemanticClassExpressionKind.And
            ? JoinConditions(validPhrases)
            : JoinAlternatives(validPhrases);
    }

    private static string JoinAlternatives(IReadOnlyList<string> phrases) =>
        phrases.Count switch
        {
            1 => phrases[0],
            2 => $"{phrases[0]} or {phrases[1]}",
            _ => $"{string.Join(", ", phrases.Take(phrases.Count - 1))}, or {phrases[^1]}"
        };

    private static string? FormatCondition(SemanticRestriction condition, SemanticWorldModel model)
    {
        var relation = model.FindRelation(condition.Relation);
        var target = model.FindConcept(condition.TargetType, SemanticConceptKind.Type);
        if (relation is null || target is null)
        {
            return null;
        }

        var phrase = $"{relation.Verb} {IndefinitePhrase(target.Label)}";
        return relation.ObjectRole is null
            ? phrase
            : $"{phrase} {relation.ObjectRole}";
    }

    private static string FormatRelation(
        SemanticConcept subject,
        SemanticConcept target,
        SemanticRelationDefinition relation,
        bool first)
    {
        var prefix = first ? Capitalize(IndefinitePhrase(subject.Label)) : $"The {subject.Label}";
        var objectPhrase = relation.ObjectRole is null
            ? $"the {target.Label}"
            : $"{IndefinitePhrase(target.Label)} {relation.ObjectRole}";
        var verb = subject.Label.EndsWith('s') && subject.Label.Length > 1
            ? InflectPluralSubjectVerb(relation.Verb)
            : relation.Verb;
        return $"{prefix} {verb} {objectPhrase}.";
    }

    private static string FormatRelationTarget(SemanticRelationDefinition relation, string targetLabel) =>
        relation.ObjectRole is null
            ? $"{relation.Verb} {Indefinite(targetLabel)} {targetLabel}"
            : $"{relation.Verb} {Indefinite(targetLabel)} {targetLabel} {relation.ObjectRole}";

    private static string InflectPluralSubjectVerb(string verbPhrase)
    {
        var separator = verbPhrase.IndexOf(' ');
        var verb = separator < 0 ? verbPhrase : verbPhrase[..separator];
        var remainder = separator < 0 ? string.Empty : verbPhrase[separator..];
        var baseVerb = verb switch
        {
            "has" => "have",
            "does" => "do",
            _ when verb.EndsWith("ies", StringComparison.Ordinal) => $"{verb[..^3]}y",
            _ when verb.EndsWith("s", StringComparison.Ordinal) => verb[..^1],
            _ => verb
        };
        return $"{baseVerb}{remainder}";
    }

    private static string JoinConditions(IReadOnlyList<string> phrases) =>
        phrases.Count switch
        {
            1 => phrases[0],
            2 => $"{phrases[0]} and {phrases[1]}",
            _ => $"{string.Join(", ", phrases.Take(phrases.Count - 1))}, and {phrases[^1]}"
        };

    private static string JoinNames(IReadOnlyList<string> names) =>
        names.Count switch
        {
            0 => string.Empty,
            1 => names[0],
            2 => $"{names[0]} and {names[1]}",
            _ => $"{string.Join(", ", names.Take(names.Count - 1))}, and {names[^1]}"
        };

    private static string Indefinite(string phrase)
    {
        if (IsMassNoun(phrase) || StartsWithPluralNoun(phrase))
        {
            return string.Empty;
        }

        return phrase.StartsWith("useful", StringComparison.OrdinalIgnoreCase) ||
               phrase.StartsWith("user", StringComparison.OrdinalIgnoreCase) ||
               phrase.StartsWith("university", StringComparison.OrdinalIgnoreCase)
            ? "a"
            : phrase.Length > 0 && "aeiou".Contains(char.ToLowerInvariant(phrase[0]))
                ? "an"
                : "a";
    }

    private static bool StartsWithPluralNoun(string phrase)
    {
        var firstWord = phrase.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return firstWord is not null &&
            firstWord.EndsWith('s') &&
            firstWord is not ("business" or "status" or "analysis" or "news" or "series" or "species");
    }

    private static string IndefinitePhrase(string phrase)
    {
        var article = Indefinite(phrase);
        return article.Length == 0 ? phrase : $"{article} {phrase}";
    }

    private static bool IsMassNoun(string phrase) =>
        phrase is "monopsony power" or "buyer market power" or "market power" or "software" or "straightness";

    private static string Capitalize(string value) =>
        value.Length == 0 ? value : char.ToUpperInvariant(value[0]) + value[1..];

    private static string FormatScientific(double value)
    {
        if (value == 0)
        {
            return "0";
        }

        var exponent = (int)Math.Floor(Math.Log10(Math.Abs(value)));
        var coefficient = value / Math.Pow(10, exponent);
        return $"{coefficient.ToString("0.00", CultureInfo.InvariantCulture)} × 10^{exponent}";
    }

    private static string FormatPercentage(double probability) =>
        $"{(probability * 100).ToString("0.0", CultureInfo.InvariantCulture)}%";

    private static LatticeEvidence ToEvidence(
        ImmutableArray<SemanticWorldModelSource> sources,
        LatticeEvidenceKind kind) =>
        new(
            kind,
            LatticeEvidencePolarity.Positive,
            sources.Select(source => new LatticeEvidenceSource(
                LatticeKnowledgeSourceKind.FoundationalKnowledge,
                source.SourceId,
                source.SourceId,
                null,
                null)).ToImmutableArray(),
            ImmutableArray<LatticeEvidence>.Empty);

    private static bool Fail(
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;
        return false;
    }
}
