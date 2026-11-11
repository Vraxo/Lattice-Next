using System.Collections.Immutable;
using Lattice.Core.Language;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;
using Xunit;

namespace Lattice.Core.Tests.Reasoning;

public sealed class KnowledgePackLoaderTests
{
    private readonly KnowledgePackLoader loader = new();
    private readonly ReasoningEngine engine = new();
    private readonly LceParser parser = new();

    [Fact]
    public void Loads_facts_and_rules_as_imported_claims_that_can_be_queried()
    {
        const string json = """
            {
              "formatVersion": 1,
              "id": "example-animals",
              "name": "Example animals",
              "claims": [
                "Every dog is an animal.",
                "Fido is a dog."
              ]
            }
            """;
        var knowledgeBase = new KnowledgeBase();

        var loaded = loader.TryLoadAndAdd(json, knowledgeBase, out var pack, out var error);

        Assert.True(loaded, error);
        Assert.Equal(new KnowledgePack(1, "example-animals", "Example animals", 2), pack);
        Assert.Single(knowledgeBase.Facts);
        Assert.Single(knowledgeBase.Rules);
        Assert.Equal(KnowledgeSourceKind.ImportedClaim, knowledgeBase.Facts[0].SourceKind);
        Assert.Equal("example-animals", knowledgeBase.Facts[0].Sources[0].SourceId);
        Assert.Equal("example-animals", knowledgeBase.Rules[0].Sources[0].SourceId);

        var question = parser.Parse("Is Fido an animal?");
        Assert.True(question.IsSuccess, question.Error);
        var predicate = Assert.IsType<PredicateApplication>(question.Utterance!.Meaning);
        var answer = engine.Ask(knowledgeBase, predicate);

        Assert.True(answer.IsSupported);
        Assert.Equal(ProofNodeKind.Derived, answer.Proof!.Kind);
        Assert.Equal("example-animals", answer.Proof.Sources[0].SourceId);
        Assert.Equal("example-animals", answer.Proof.Premise!.Sources[0].SourceId);
    }

    [Fact]
    public void Loads_a_quantified_relational_rule_as_an_imported_claim()
    {
        const string json = """
            {
              "formatVersion": 1,
              "id": "dog-relations",
              "name": "Dog relations",
              "claims": [
                "Every dog likes milk.",
                "Fido is a dog."
              ]
            }
            """;
        var knowledgeBase = new KnowledgeBase();

        var loaded = loader.TryLoadAndAdd(
            json,
            knowledgeBase,
            out var pack,
            out var error);

        Assert.True(loaded, error);
        Assert.Equal(2, pack!.ClaimCount);

        var question = parser.Parse("Does Fido like milk?");
        Assert.True(question.IsSuccess, question.Error);
        var query = Assert.IsType<PredicateApplication>(question.Utterance!.Meaning);
        var answer = engine.Ask(knowledgeBase, query);

        Assert.True(answer.IsSupported);
        Assert.Equal(KnowledgeSourceKind.ImportedClaim, answer.Proof!.Sources[0].Kind);
        Assert.Equal("dog-relations", answer.Proof.Sources[0].SourceId);
        Assert.Equal(KnowledgeSourceKind.ImportedClaim, answer.Proof.Premise!.Sources[0].Kind);
        Assert.Equal("dog-relations", answer.Proof.Premise.Sources[0].SourceId);
    }

    [Fact]
    public void Does_not_partially_add_a_pack_when_a_later_claim_is_invalid()
    {
        const string json = """
            {
              "formatVersion": 1,
              "id": "invalid-pack",
              "name": "Invalid pack",
              "claims": [
                "Fido is a dog.",
                "It is a mammal."
              ]
            }
            """;
        var knowledgeBase = new KnowledgeBase();

        var loaded = loader.TryLoadAndAdd(json, knowledgeBase, out var pack, out var error);

        Assert.False(loaded);
        Assert.Null(pack);
        Assert.NotNull(error);
        Assert.Empty(knowledgeBase.Facts);
        Assert.Empty(knowledgeBase.Rules);
    }

    [Fact]
    public void Does_not_add_questions_from_a_pack()
    {
        const string json = """
            {
              "formatVersion": 1,
              "id": "question-pack",
              "name": "Question pack",
              "claims": [
                "Is Fido a dog?"
              ]
            }
            """;
        var knowledgeBase = new KnowledgeBase();

        var loaded = loader.TryLoadAndAdd(json, knowledgeBase, out _, out var error);

        Assert.False(loaded);
        Assert.NotNull(error);
        Assert.Empty(knowledgeBase.Facts);
    }

    [Fact]
    public void Rejects_unknown_format_versions_and_unrecognized_fields()
    {
        const string unsupportedVersion = """
            {
              "formatVersion": 2,
              "id": "future-pack",
              "name": "Future pack",
              "claims": ["Fido is a dog."]
            }
            """;
        const string unknownField = """
            {
              "formatVersion": 1,
              "id": "unknown-field",
              "name": "Unknown field",
              "claims": ["Fido is a dog."],
              "executable": true
            }
            """;

        Assert.False(loader.TryLoadAndAdd(
            unsupportedVersion,
            new KnowledgeBase(),
            out _,
            out var versionError));
        Assert.Contains("not supported", versionError);

        Assert.False(loader.TryLoadAndAdd(
            unknownField,
            new KnowledgeBase(),
            out _,
            out var fieldError));
        Assert.Contains("optional attributes", fieldError);
    }

    [Fact]
    public void Merges_pack_provenance_when_a_claim_already_exists()
    {
        const string json = """
            {
              "formatVersion": 1,
              "id": "canine-facts",
              "name": "Canine facts",
              "claims": ["Fido is a dog."]
            }
            """;
        var knowledgeBase = new KnowledgeBase();
        var userClaim = parser.Parse("Fido is a dog.");
        Assert.True(userClaim.IsSuccess, userClaim.Error);
        Assert.True(knowledgeBase.TryAdd(userClaim.Utterance!, out _, out var addError), addError);

        var loaded = loader.TryLoadAndAdd(json, knowledgeBase, out _, out var error);

        Assert.True(loaded, error);
        var fact = Assert.Single(knowledgeBase.Facts);
        Assert.Equal(2, fact.Sources.Length);
        Assert.Equal(KnowledgeSourceKind.UserAssertion, fact.Sources[0].Kind);
        Assert.Equal(KnowledgeSourceKind.ImportedClaim, fact.Sources[1].Kind);
        Assert.Equal("canine-facts", fact.Sources[1].SourceId);
    }

    [Fact]
    public void Loads_typed_candidate_attributes_with_pack_provenance()
    {
        const string json = """
            {
              "formatVersion": 1,
              "id": "day-out-candidates",
              "name": "Day out candidates",
              "claims": [],
              "attributes": [
                {
                  "subject": "Fernbank",
                  "name": "total-cost",
                  "value": { "type": "money", "amount": 6, "currencySymbol": "$" },
                  "sourceText": "Fernbank costs six dollars including entry and fares."
                },
                {
                  "subject": "Fernbank",
                  "name": "transport-mode",
                  "value": { "type": "text", "value": "bus" },
                  "sourceText": "Fernbank requires a bus."
                },
                {
                  "subject": "Fernbank",
                  "name": "tags",
                  "value": { "type": "textSet", "values": ["quiet", "outdoors"] },
                  "sourceText": "Fernbank is quiet and outdoors."
                },
                {
                  "subject": "Fernbank",
                  "name": "opening-window",
                  "value": { "type": "timeWindow", "start": "08:00", "end": "19:00" },
                  "sourceText": "Fernbank is open from 08:00 to 19:00."
                },
                {
                  "subject": "Fernbank",
                  "name": "activity-duration",
                  "value": { "type": "duration", "minutes": 180 },
                  "sourceText": "The visit takes three hours."
                },
                {
                  "subject": "Fernbank",
                  "name": "bus-departures",
                  "value": { "type": "timeSet", "times": ["09:10", "10:40"] },
                  "sourceText": "Buses depart at 09:10 and 10:40."
                }
              ]
            }
            """;
        var knowledgeBase = new KnowledgeBase();

        var loaded = loader.TryLoadAndAdd(json, knowledgeBase, out var pack, out var error);

        Assert.True(loaded, error);
        Assert.Equal(new KnowledgePack(1, "day-out-candidates", "Day out candidates", 0)
        {
            AttributeCount = 6
        }, pack);
        Assert.Equal(6, knowledgeBase.Attributes.Count);
        Assert.All(knowledgeBase.Attributes, attribute =>
        {
            Assert.Equal(KnowledgeSourceKind.ImportedClaim, attribute.Sources[0].Kind);
            Assert.Equal("day-out-candidates", attribute.Sources[0].SourceId);
        });

        Assert.Equal(
            new ObjectiveMoneyValue(6m, "$"),
            Assert.IsType<ObjectiveMoneyValue>(Assert.Single(
                knowledgeBase.Attributes,
                attribute => attribute.Name.Value == "total-cost").Value));
        Assert.Equal(
            new ObjectiveTextValue("bus"),
            Assert.IsType<ObjectiveTextValue>(Assert.Single(
                knowledgeBase.Attributes,
                attribute => attribute.Name.Value == "transport-mode").Value));
        Assert.Equal(
            new[] { "quiet", "outdoors" },
            Assert.IsType<ObjectiveTextSetValue>(Assert.Single(
                knowledgeBase.Attributes,
                attribute => attribute.Name.Value == "tags").Value).Values);
        Assert.Equal(
            new ObjectiveTimeWindowValue(new TimeOnly(8, 0), new TimeOnly(19, 0)),
            Assert.IsType<ObjectiveTimeWindowValue>(Assert.Single(
                knowledgeBase.Attributes,
                attribute => attribute.Name.Value == "opening-window").Value));
        Assert.Equal(
            new ObjectiveDurationValue(180),
            Assert.IsType<ObjectiveDurationValue>(Assert.Single(
                knowledgeBase.Attributes,
                attribute => attribute.Name.Value == "activity-duration").Value));
        Assert.Equal(
            new[] { new TimeOnly(9, 10), new TimeOnly(10, 40) },
            Assert.IsType<ObjectiveTimeSetValue>(Assert.Single(
                knowledgeBase.Attributes,
                attribute => attribute.Name.Value == "bus-departures").Value).Times);
    }

    [Fact]
    public void Loads_ordered_temporal_plan_templates_with_optional_stage_references()
    {
        const string json = """
            {
              "formatVersion": 1,
              "id": "temporal-plan",
              "name": "Temporal plan",
              "claims": [],
              "attributes": [{
                "subject": "Destination",
                "name": "temporal-plan",
                "value": {
                  "type": "temporalPlan",
                  "stages": [
                    {
                      "durationAttribute": "journey-duration",
                      "startTimesAttribute": "departure-times"
                    },
                    {
                      "durationAttribute": "activity-duration",
                      "availabilityWindowAttribute": "opening-window"
                    },
                    {
                      "durationAttribute": "journey-duration",
                      "startTimesAttribute": "return-times"
                    }
                  ]
                },
                "sourceText": "The plan is an outbound journey, activity, and return journey."
              }]
            }
            """;
        var knowledgeBase = new KnowledgeBase();

        Assert.True(loader.TryLoadAndAdd(json, knowledgeBase, out _, out var error), error);

        var plan = Assert.IsType<ObjectiveTemporalPlanValue>(
            Assert.Single(knowledgeBase.Attributes).Value);
        Assert.Equal(
            new[]
            {
                new ObjectiveTemporalStage("journey-duration", "departure-times"),
                new ObjectiveTemporalStage("activity-duration", AvailabilityWindowAttribute: "opening-window"),
                new ObjectiveTemporalStage("journey-duration", "return-times")
            },
            plan.Stages);
    }

    [Fact]
    public void Rejects_empty_temporal_plan_templates_atomically()
    {
        const string json = """
            {
              "formatVersion": 1,
              "id": "invalid-temporal-plan",
              "name": "Invalid temporal plan",
              "claims": [],
              "attributes": [{
                "subject": "Destination",
                "name": "temporal-plan",
                "value": { "type": "temporalPlan", "stages": [] },
                "sourceText": "An empty temporal plan."
              }]
            }
            """;
        var knowledgeBase = new KnowledgeBase();

        Assert.False(loader.TryLoadAndAdd(json, knowledgeBase, out _, out var error));
        Assert.NotNull(error);
        Assert.Empty(knowledgeBase.Attributes);
    }

    [Fact]
    public void Loads_declarative_sums_of_named_attributes()
    {
        const string json = """
            {
              "formatVersion": 1,
              "id": "derived-cost",
              "name": "Derived cost",
              "claims": [],
              "attributes": [{
                "subject": "Destination",
                "name": "total-cost",
                "value": { "type": "sum", "attributes": ["return-fare", "entry-cost"] },
                "sourceText": "The total cost is the return fare plus entry."
              }]
            }
            """;
        var knowledgeBase = new KnowledgeBase();

        Assert.True(loader.TryLoadAndAdd(json, knowledgeBase, out _, out var error), error);

        Assert.Equal(
            new[] { "return-fare", "entry-cost" },
            Assert.IsType<ObjectiveSumValue>(Assert.Single(knowledgeBase.Attributes).Value).Attributes);
    }

    [Fact]
    public void Rejects_invalid_attribute_sums_without_partially_loading_the_pack()
    {
        const string json = """
            {
              "formatVersion": 1,
              "id": "invalid-derived-cost",
              "name": "Invalid derived cost",
              "claims": [],
              "attributes": [{
                "subject": "Destination",
                "name": "total-cost",
                "value": { "type": "sum", "attributes": ["fare", "FARE"] },
                "sourceText": "A sum with a repeated component."
              }]
            }
            """;
        var knowledgeBase = new KnowledgeBase();

        Assert.False(loader.TryLoadAndAdd(json, knowledgeBase, out _, out var error));
        Assert.NotNull(error);
        Assert.Empty(knowledgeBase.Attributes);
    }

    [Fact]
    public void Imported_candidate_attributes_can_be_evaluated_with_pack_provenance()
    {
        const string json = """
            {
              "formatVersion": 1,
              "id": "candidate-costs",
              "name": "Candidate costs",
              "claims": [],
              "attributes": [{
                "subject": "Fernbank",
                "name": "total-cost",
                "value": { "type": "money", "amount": 6, "currencySymbol": "$" },
                "sourceText": "Fernbank costs six dollars including entry and fares."
              }]
            }
            """;
        var knowledgeBase = new KnowledgeBase();
        Assert.True(loader.TryLoadAndAdd(json, knowledgeBase, out _, out var error), error);
        var objective = new ObjectiveFormula("choose within budget")
        {
            Conditions = ImmutableArray.Create(new ObjectiveCondition(
                "spending-limit",
                ObjectiveConditionRole.HardConstraint,
                ObjectiveConditionOperator.AtMost,
                new ObjectiveMoneyValue(25m, "$"),
                "My spending limit is $25.")
            {
                TargetAttribute = "total-cost"
            })
        };

        var evaluation = new ObjectiveEvaluator().Evaluate(objective, knowledgeBase);
        var candidate = Assert.Single(evaluation.Candidates);

        Assert.Equal(
            ObjectiveConditionAssessmentStatus.Satisfied,
            Assert.Single(candidate.Conditions).Status);
        Assert.Equal("candidate-costs", Assert.Single(
            Assert.Single(candidate.Conditions).Evidence).Sources[0].SourceId);
    }

    [Fact]
    public void Rejects_invalid_typed_attributes_without_adding_any_part_of_the_pack()
    {
        const string json = """
            {
              "formatVersion": 1,
              "id": "invalid-attributes",
              "name": "Invalid attributes",
              "claims": ["Fernbank is a reserve."],
              "attributes": [
                {
                  "subject": "Fernbank",
                  "name": "total-cost",
                  "value": { "type": "money", "amount": -6, "currencySymbol": "$" },
                  "sourceText": "An invalid negative cost."
                }
              ]
            }
            """;
        var knowledgeBase = new KnowledgeBase();

        var loaded = loader.TryLoadAndAdd(json, knowledgeBase, out var pack, out var error);

        Assert.False(loaded);
        Assert.Null(pack);
        Assert.NotNull(error);
        Assert.Empty(knowledgeBase.Facts);
        Assert.Empty(knowledgeBase.Rules);
        Assert.Empty(knowledgeBase.Attributes);
    }

    [Fact]
    public void Rejects_unknown_attribute_value_fields_and_types()
    {
        const string unknownField = """
            {
              "formatVersion": 1,
              "id": "extra-value-field",
              "name": "Extra value field",
              "claims": [],
              "attributes": [{
                "subject": "Fernbank",
                "name": "total-cost",
                "value": { "type": "money", "amount": 6, "currencySymbol": "$", "guess": true },
                "sourceText": "Fernbank costs six dollars."
              }]
            }
            """;
        const string unknownType = """
            {
              "formatVersion": 1,
              "id": "unknown-value-type",
              "name": "Unknown value type",
              "claims": [],
              "attributes": [{
                "subject": "Fernbank",
                "name": "mystery",
                "value": { "type": "unbounded", "value": "unknown" },
                "sourceText": "An unsupported value."
              }]
            }
            """;

        Assert.False(loader.TryLoadAndAdd(
            unknownField,
            new KnowledgeBase(),
            out _,
            out _));
        Assert.False(loader.TryLoadAndAdd(
            unknownType,
            new KnowledgeBase(),
            out _,
            out _));
    }

    [Fact]
    public void Candidate_attributes_can_be_stored_as_symbolic_relations_and_exactly_retrieved()
    {
        const string json = """
            {
              "formatVersion": 1,
              "id": "candidate-facts",
              "name": "Candidate facts",
              "claims": [
                "Fernbank is a reserve.",
                "Fernbank costs six-dollars."
              ]
            }
            """;
        var knowledgeBase = new KnowledgeBase();

        var loaded = loader.TryLoadAndAdd(json, knowledgeBase, out _, out var error);

        Assert.True(loaded, error);
        var cost = Assert.Single(
            knowledgeBase.Facts,
            fact => fact.Predicate.Predicate.Value == "costs");
        Assert.Equal(2, cost.Predicate.Arguments.Length);
        var costValue = Assert.IsType<IndividualTerm>(cost.Predicate.Arguments[1]);
        Assert.Equal("six-dollars", costValue.Identifier.Value);

        var query = parser.Parse("Does Fernbank cost six-dollars?");
        Assert.True(query.IsSuccess, query.Error);
        var predicate = Assert.IsType<PredicateApplication>(query.Utterance!.Meaning);
        var answer = engine.Ask(knowledgeBase, predicate);

        Assert.Equal(TruthValue.True, answer.TruthValue);
    }

    [Fact]
    public void Candidate_money_literals_and_comparative_queries_are_not_supported_by_the_current_fact_path()
    {
        const string moneyPack = """
            {
              "formatVersion": 1,
              "id": "numeric-candidate-fact",
              "name": "Numeric candidate fact",
              "claims": [
                "Fernbank costs $6."
              ]
            }
            """;
        var knowledgeBase = new KnowledgeBase();

        var loaded = loader.TryLoadAndAdd(moneyPack, knowledgeBase, out _, out var error);

        Assert.False(loaded);
        Assert.NotNull(error);
        Assert.Empty(knowledgeBase.Facts);
        Assert.Empty(knowledgeBase.Rules);

        var comparison = parser.Parse("Does Fernbank cost less than ten-dollars?");
        Assert.False(comparison.IsSuccess);
    }

    [Fact]
    public void Rejects_malformed_json_without_changing_the_knowledge_base()
    {
        var knowledgeBase = new KnowledgeBase();

        var loaded = loader.TryLoadAndAdd("{ invalid", knowledgeBase, out _, out var error);

        Assert.False(loaded);
        Assert.Equal("The knowledge pack is not valid JSON.", error);
        Assert.Empty(knowledgeBase.Facts);
        Assert.Empty(knowledgeBase.Rules);
    }
}
