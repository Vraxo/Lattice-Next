using System.Collections.Immutable;
using Lattice.Core.Language;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;
using Xunit;

namespace Lattice.Core.Tests.Reasoning;

public sealed class ObjectiveEvaluatorTests
{
    private readonly LceParser parser = new();
    private readonly ObjectiveEvaluator evaluator = new();

    [Fact]
    public void Evaluates_budget_transport_and_preferences_without_collapsing_unknowns()
    {
        var objective = CreateDayOutObjective();
        var knowledgeBase = CreateCandidateKnowledgeBase();

        var result = evaluator.Evaluate(objective, knowledgeBase);

        Assert.Equal(3, result.Candidates.Length);

        var fernbank = Assert.Single(result.Candidates, candidate => candidate.Candidate.Value == "Fernbank");
        Assert.Equal(
            ObjectiveConditionAssessmentStatus.Satisfied,
            GetCondition(fernbank, "spending-limit").Status);
        Assert.Equal(
            ObjectiveConditionAssessmentStatus.Satisfied,
            GetCondition(fernbank, "transport").Status);
        Assert.Equal(
            ObjectiveConditionAssessmentStatus.Satisfied,
            GetCondition(fernbank, "preference").Status);
        Assert.All(
            GetCondition(fernbank, "preference").Preferences,
            preference => Assert.Equal(ObjectiveConditionAssessmentStatus.Satisfied, preference.Status));
        Assert.Equal(
            "candidate-fixture",
            GetCondition(fernbank, "spending-limit").Evidence[0].SourceId);
        Assert.False(fernbank.HasHardConstraintViolation);
        Assert.Equal(CandidateFeasibility.Eligible, fernbank.Feasibility);

        var riverside = Assert.Single(result.Candidates, candidate => candidate.Candidate.Value == "Riverside");
        Assert.Equal(
            ObjectiveConditionAssessmentStatus.Satisfied,
            GetCondition(riverside, "spending-limit").Status);
        Assert.Equal(
            ObjectiveConditionAssessmentStatus.PartiallySatisfied,
            GetCondition(riverside, "preference").Status);
        Assert.Collection(
            GetCondition(riverside, "preference").Preferences,
            preference =>
            {
                Assert.Equal("quiet", preference.Preference);
                Assert.Equal(ObjectiveConditionAssessmentStatus.Violated, preference.Status);
                Assert.Single(preference.Evidence);
            },
            preference =>
            {
                Assert.Equal("outdoors", preference.Preference);
                Assert.Equal(ObjectiveConditionAssessmentStatus.Satisfied, preference.Status);
                Assert.Single(preference.Evidence);
            });
        Assert.False(riverside.HasHardConstraintViolation);
        Assert.Equal(CandidateFeasibility.Eligible, riverside.Feasibility);

        var hilltop = Assert.Single(result.Candidates, candidate => candidate.Candidate.Value == "Hilltop");
        Assert.Equal(
            ObjectiveConditionAssessmentStatus.Unknown,
            GetCondition(hilltop, "spending-limit").Status);
        Assert.True(hilltop.HasUnresolvedHardConstraint);
        Assert.Equal(CandidateFeasibility.Unresolved, hilltop.Feasibility);
        Assert.Equal(ObjectiveRecommendationStatus.UniquePreferenceLeader, result.Recommendation.Status);
        Assert.Equal("Fernbank", Assert.Single(result.Recommendation.ShortlistedCandidates).Value);
        Assert.Equal("Riverside", Assert.Single(result.Recommendation.DominatedCandidates).Value);
        Assert.DoesNotContain("Hilltop", result.Recommendation.DominatedCandidates.Select(candidate => candidate.Value));
    }

    [Fact]
    public void Reports_only_evidence_backed_preference_trade_offs_between_eligible_candidates()
    {
        var evaluation = evaluator.Evaluate(CreateDayOutObjective(), CreateCandidateKnowledgeBase());

        var tradeOff = Assert.Single(evaluation.PreferenceTradeOffs);
        Assert.Equal("quiet", tradeOff.Preference);
        Assert.Equal("Fernbank", tradeOff.CandidateWithPositiveEvidence.Value);
        Assert.Equal("Riverside", tradeOff.CandidateWithContraryEvidence.Value);
        Assert.Single(tradeOff.PositiveEvidence);
        Assert.Single(tradeOff.ContraryEvidence);
        Assert.DoesNotContain(evaluation.PreferenceTradeOffs, item => item.Preference == "outdoors");
        Assert.DoesNotContain(evaluation.PreferenceTradeOffs, item =>
            item.CandidateWithPositiveEvidence.Value == "Hilltop" ||
            item.CandidateWithContraryEvidence.Value == "Hilltop");
    }

    [Fact]
    public void Does_not_dominate_candidates_when_preference_evidence_is_unknown()
    {
        var knowledgeBase = new KnowledgeBase();
        foreach (var candidate in new[] { "First", "Second" })
        {
            AddAttribute(knowledgeBase, candidate, "total-cost", new ObjectiveMoneyValue(10m, "$"));
            AddAttribute(knowledgeBase, candidate, "transport-mode", new ObjectiveTextValue("walking"));
        }

        var evaluation = evaluator.Evaluate(CreateDayOutObjective(), knowledgeBase);

        Assert.Equal(ObjectiveRecommendationStatus.MultipleNonDominatedCandidates, evaluation.Recommendation.Status);
        Assert.Equal(
            new[] { "First", "Second" },
            evaluation.Recommendation.ShortlistedCandidates.Select(candidate => candidate.Value));
        Assert.Empty(evaluation.Recommendation.DominatedCandidates);
        Assert.True(evaluation.Recommendation.HasIncompletePreferenceEvidence);
    }

    [Fact]
    public void Keeps_candidates_non_dominated_when_they_trade_off_different_preferences()
    {
        var knowledgeBase = new KnowledgeBase();
        foreach (var candidate in new[] { "Quiet Option", "Outdoor Option" })
        {
            AddAttribute(knowledgeBase, candidate, "total-cost", new ObjectiveMoneyValue(10m, "$"));
            AddAttribute(knowledgeBase, candidate, "transport-mode", new ObjectiveTextValue("walking"));
        }

        AddAttribute(knowledgeBase, "Quiet Option", "tags", new ObjectiveTextSetValue(
            ImmutableArray.Create("quiet"))
        {
            ExcludedValues = ImmutableArray.Create("outdoors")
        });
        AddAttribute(knowledgeBase, "Outdoor Option", "tags", new ObjectiveTextSetValue(
            ImmutableArray.Create("outdoors"))
        {
            ExcludedValues = ImmutableArray.Create("quiet")
        });

        var evaluation = evaluator.Evaluate(CreateDayOutObjective(), knowledgeBase);

        Assert.Equal(ObjectiveRecommendationStatus.MultipleNonDominatedCandidates, evaluation.Recommendation.Status);
        Assert.Equal(
            new[] { "Outdoor Option", "Quiet Option" },
            evaluation.Recommendation.ShortlistedCandidates.Select(candidate => candidate.Value));
        Assert.Empty(evaluation.Recommendation.DominatedCandidates);
        Assert.False(evaluation.Recommendation.HasIncompletePreferenceEvidence);
        Assert.Equal(2, evaluation.PreferenceTradeOffs.Length);
    }

    [Fact]
    public void Reports_conflicting_positive_and_negative_evidence_for_the_same_preference()
    {
        var knowledgeBase = new KnowledgeBase();
        AddAttribute(knowledgeBase, "Candidate", "tags", new ObjectiveTextSetValue(
            ImmutableArray.Create("quiet", "outdoors"))
        {
            ExcludedValues = ImmutableArray.Create("quiet")
        });

        var candidate = Assert.Single(evaluator.Evaluate(CreateDayOutObjective(), knowledgeBase).Candidates);
        var preference = GetCondition(candidate, "preference");

        Assert.Equal(ObjectiveConditionAssessmentStatus.Conflicting, preference.Status);
        var quiet = Assert.Single(preference.Preferences, item => item.Preference == "quiet");
        Assert.Equal(ObjectiveConditionAssessmentStatus.Conflicting, quiet.Status);
        Assert.Single(quiet.Evidence);
    }

    [Fact]
    public void Derives_money_and_duration_sums_from_components_with_evidence()
    {
        var knowledgeBase = new KnowledgeBase();
        AddAttribute(knowledgeBase, "Destination", "return-fare", new ObjectiveMoneyValue(6m, "$"));
        AddAttribute(knowledgeBase, "Destination", "entry-cost", new ObjectiveMoneyValue(4m, "$"));
        AddAttribute(knowledgeBase, "Destination", "total-cost", new ObjectiveSumValue(
            ImmutableArray.Create("return-fare", "entry-cost")));
        AddAttribute(knowledgeBase, "Destination", "outbound-leg", new ObjectiveDurationValue(15));
        AddAttribute(knowledgeBase, "Destination", "access-leg", new ObjectiveDurationValue(20));
        AddAttribute(knowledgeBase, "Destination", "one-way-duration", new ObjectiveSumValue(
            ImmutableArray.Create("outbound-leg", "access-leg")));
        AddAttribute(knowledgeBase, "Destination", "activity-duration", new ObjectiveDurationValue(30));

        var objective = new ObjectiveFormula("fit within budget and time")
        {
            Conditions = ImmutableArray.Create(
                new ObjectiveCondition(
                    "spending-limit",
                    ObjectiveConditionRole.HardConstraint,
                    ObjectiveConditionOperator.AtMost,
                    new ObjectiveMoneyValue(12m, "$"),
                    "The budget is $12.")
                {
                    TargetAttribute = "total-cost"
                },
                new ObjectiveCondition(
                    "itinerary",
                    ObjectiveConditionRole.HardConstraint,
                    ObjectiveConditionOperator.CanFit,
                    new ObjectiveTemporalSequenceValue(
                        new TimeOnly(9, 0),
                        new TimeOnly(12, 0),
                        ImmutableArray.Create(
                            new ObjectiveTemporalStage("one-way-duration"),
                            new ObjectiveTemporalStage("activity-duration"),
                            new ObjectiveTemporalStage("one-way-duration"))),
                    "The sequence must fit between 09:00 and 12:00."))
        };

        var result = evaluator.Evaluate(objective, knowledgeBase);
        var candidate = Assert.Single(result.Candidates);

        Assert.Equal(ObjectiveConditionAssessmentStatus.Satisfied, GetCondition(candidate, "spending-limit").Status);
        Assert.Contains(
            GetCondition(candidate, "spending-limit").Evidence,
            attribute => attribute.Name.Value == "return-fare");
        Assert.Contains(
            GetCondition(candidate, "spending-limit").Evidence,
            attribute => attribute.Name.Value == "entry-cost");

        Assert.Equal(ObjectiveConditionAssessmentStatus.Satisfied, GetCondition(candidate, "itinerary").Status);
        Assert.Contains(
            GetCondition(candidate, "itinerary").Evidence,
            attribute => attribute.Name.Value == "outbound-leg");
        Assert.Contains(
            GetCondition(candidate, "itinerary").Evidence,
            attribute => attribute.Name.Value == "access-leg");
    }

    [Fact]
    public void Reports_missing_conflicting_and_circular_sum_components_without_guessing()
    {
        var knowledgeBase = new KnowledgeBase();
        AddAttribute(knowledgeBase, "Missing", "fare", new ObjectiveMoneyValue(5m, "$"));
        AddAttribute(knowledgeBase, "Missing", "total-cost", new ObjectiveSumValue(
            ImmutableArray.Create("fare", "entry-cost")));

        AddAttribute(knowledgeBase, "Conflicting", "fare", new ObjectiveMoneyValue(5m, "$"));
        AddAttribute(knowledgeBase, "Conflicting", "entry-cost", new ObjectiveMoneyValue(2m, "$"));
        AddAttribute(knowledgeBase, "Conflicting", "entry-cost", new ObjectiveMoneyValue(3m, "$"));
        AddAttribute(knowledgeBase, "Conflicting", "total-cost", new ObjectiveSumValue(
            ImmutableArray.Create("fare", "entry-cost")));

        AddAttribute(knowledgeBase, "Circular", "first", new ObjectiveSumValue(
            ImmutableArray.Create("second", "fare")));
        AddAttribute(knowledgeBase, "Circular", "second", new ObjectiveSumValue(
            ImmutableArray.Create("first", "fare")));
        AddAttribute(knowledgeBase, "Circular", "fare", new ObjectiveMoneyValue(1m, "$"));

        var objective = new ObjectiveFormula("check the total cost")
        {
            Conditions = ImmutableArray.Create(new ObjectiveCondition(
                "spending-limit",
                ObjectiveConditionRole.HardConstraint,
                ObjectiveConditionOperator.AtMost,
                new ObjectiveMoneyValue(10m, "$"),
                "The budget is $10.")
            {
                TargetAttribute = "total-cost"
            })
        };
        var result = evaluator.Evaluate(objective, knowledgeBase);

        Assert.Equal(ObjectiveConditionAssessmentStatus.Unknown,
            GetCondition(Assert.Single(result.Candidates, candidate => candidate.Candidate.Value == "Missing"), "spending-limit").Status);
        Assert.Equal(ObjectiveConditionAssessmentStatus.Conflicting,
            GetCondition(Assert.Single(result.Candidates, candidate => candidate.Candidate.Value == "Conflicting"), "spending-limit").Status);

        var circularObjective = objective with
        {
            Conditions = ImmutableArray.Create(objective.Conditions[0] with { TargetAttribute = "first" })
        };
        var circularResult = evaluator.Evaluate(circularObjective, knowledgeBase);
        Assert.Equal(ObjectiveConditionAssessmentStatus.Unknown,
            GetCondition(Assert.Single(circularResult.Candidates, candidate => candidate.Candidate.Value == "Circular"), "spending-limit").Status);
    }

    [Fact]
    public void Requests_replacement_for_external_evidence_without_a_freshness_window()
    {
        var knowledgeBase = CreateExternalPriceKnowledgeBase(freshUntil: null);

        var evaluation = evaluator.Evaluate(CreateDayOutObjective(), knowledgeBase);
        var candidate = Assert.Single(evaluation.Candidates);

        Assert.Equal(ObjectiveConditionAssessmentStatus.Unverified, GetCondition(candidate, "spending-limit").Status);
        Assert.Equal(CandidateFeasibility.Unresolved, candidate.Feasibility);
        var request = Assert.Single(evaluation.EvidenceRequests);
        Assert.Equal("Candidate", request.Candidate.Value);
        Assert.Equal("spending-limit", request.ConditionName);
    }

    [Fact]
    public void Requests_replacement_for_external_evidence_after_its_freshness_window_expires()
    {
        var knowledgeBase = CreateExternalPriceKnowledgeBase(DateTimeOffset.UtcNow.AddMinutes(-1));

        var evaluation = evaluator.Evaluate(CreateDayOutObjective(), knowledgeBase);
        var candidate = Assert.Single(evaluation.Candidates);

        Assert.Equal(ObjectiveConditionAssessmentStatus.Stale, GetCondition(candidate, "spending-limit").Status);
        Assert.Equal(CandidateFeasibility.Unresolved, candidate.Feasibility);
        var request = Assert.Single(evaluation.EvidenceRequests);
        Assert.Equal("spending-limit", request.ConditionName);
    }

    [Fact]
    public void Uses_external_evidence_while_its_freshness_window_is_valid()
    {
        var knowledgeBase = CreateExternalPriceKnowledgeBase(DateTimeOffset.UtcNow.AddMinutes(10));

        var evaluation = evaluator.Evaluate(CreateDayOutObjective(), knowledgeBase);
        var candidate = Assert.Single(evaluation.Candidates);

        Assert.Equal(ObjectiveConditionAssessmentStatus.Satisfied, GetCondition(candidate, "spending-limit").Status);
        Assert.Equal(CandidateFeasibility.Eligible, candidate.Feasibility);
        Assert.DoesNotContain(evaluation.EvidenceRequests, request => request.ConditionName == "spending-limit");
    }

    [Fact]
    public void Treats_imported_attributes_marked_as_freshness_required_as_unverified_without_expiry()
    {
        var knowledgeBase = new KnowledgeBase();
        AddAttribute(knowledgeBase, "Candidate", "transport-mode", new ObjectiveTextValue("walking"));
        AddAttribute(knowledgeBase, "Candidate", "tags", new ObjectiveTextSetValue(
            ImmutableArray.Create("quiet", "outdoors")));

        var added = knowledgeBase.TryAddAttribute(
            new KnowledgeAttribute(
                new SemanticIdentifier("Candidate"),
                new SemanticIdentifier("total-cost"),
                new ObjectiveMoneyValue(6m, "$"),
                "The current price must be checked before use.",
                KnowledgeSourceKind.ImportedClaim,
                SourceId: "time-sensitive-pack")
            {
                RequiresFreshness = true
            },
            out var error);
        Assert.True(added, error);

        var evaluation = evaluator.Evaluate(CreateDayOutObjective(), knowledgeBase);
        var candidate = Assert.Single(evaluation.Candidates);

        Assert.Equal(ObjectiveConditionAssessmentStatus.Unverified, GetCondition(candidate, "spending-limit").Status);
        Assert.Equal(CandidateFeasibility.Unresolved, candidate.Feasibility);
        Assert.Contains(evaluation.EvidenceRequests, request => request.ConditionName == "spending-limit");
    }

    [Fact]
    public void Keeps_external_preferences_unverified_without_a_freshness_window()
    {
        var knowledgeBase = new KnowledgeBase();
        AddAttribute(knowledgeBase, "Candidate", "total-cost", new ObjectiveMoneyValue(6m, "$"));
        AddAttribute(knowledgeBase, "Candidate", "transport-mode", new ObjectiveTextValue("walking"));

        var added = knowledgeBase.TryAddAttribute(
            new KnowledgeAttribute(
                new SemanticIdentifier("Candidate"),
                new SemanticIdentifier("tags"),
                new ObjectiveTextSetValue(ImmutableArray.Create("quiet", "outdoors")),
                "Candidate preferences were read from an external page.",
                KnowledgeSourceKind.ExternalObservation,
                SourceId: "https://example.org/candidate")
            {
                RetrievedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
            },
            out var error);
        Assert.True(added, error);

        var evaluation = evaluator.Evaluate(CreateDayOutObjective(), knowledgeBase);
        var candidate = Assert.Single(evaluation.Candidates);
        var preference = GetCondition(candidate, "preference");

        Assert.Equal(ObjectiveConditionAssessmentStatus.Unverified, preference.Status);
        Assert.All(preference.Preferences, item =>
            Assert.Equal(ObjectiveConditionAssessmentStatus.Unverified, item.Status));
        Assert.All(preference.Preferences, item => Assert.Single(item.Evidence));
    }

    [Fact]
    public void Treats_context_only_conditions_as_not_applicable_to_candidate_comparison()
    {
        var knowledgeBase = new KnowledgeBase();
        AddAttribute(knowledgeBase, "Park", "total-cost", new ObjectiveMoneyValue(5m, "$"));
        var objective = new ObjectiveFormula("visit with lunch packed")
        {
            Conditions = ImmutableArray.Create(new ObjectiveCondition(
                "meal-plan",
                ObjectiveConditionRole.HardConstraint,
                ObjectiveConditionOperator.Equals,
                new ObjectiveTextValue("bring-lunch"),
                "I'll bring lunch.")
            {
                TargetAttribute = null
            })
        };

        var result = evaluator.Evaluate(objective, knowledgeBase);
        var candidate = Assert.Single(result.Candidates);

        Assert.Equal(
            ObjectiveConditionAssessmentStatus.NotApplicable,
            GetCondition(candidate, "meal-plan").Status);
        Assert.False(candidate.HasHardConstraintViolation);
        Assert.False(candidate.HasUnresolvedHardConstraint);
        Assert.Equal(CandidateFeasibility.NotEvaluated, candidate.Feasibility);
    }

    [Fact]
    public void Walking_only_revision_rejects_bus_candidates_and_keeps_walking_candidates()
    {
        var objective = CreateDayOutObjective();
        var update = parser.ParseText("Actually, I don't want to take a bus. Walking only.");
        Assert.True(update.IsSuccess, update.Error);

        var conditions = objective.Conditions.ToBuilder();

        foreach (var utterance in update.Utterances)
        {
            var formula = Assert.IsType<ObjectiveConditionUpdateFormula>(utterance.Meaning);

            foreach (var condition in formula.Conditions)
            {
                conditions.Add(condition);
            }
        }

        objective = objective with { Conditions = conditions.ToImmutable() };
        var result = evaluator.Evaluate(objective, CreateCandidateKnowledgeBase());

        var fernbank = Assert.Single(result.Candidates, candidate => candidate.Candidate.Value == "Fernbank");
        Assert.Equal(
            ObjectiveConditionAssessmentStatus.Violated,
            GetCondition(fernbank, "transport-bus").Status);
        Assert.Equal(
            ObjectiveConditionAssessmentStatus.Violated,
            GetCondition(fernbank, "transport-mode").Status);
        Assert.True(fernbank.HasHardConstraintViolation);
        Assert.Equal(CandidateFeasibility.Excluded, fernbank.Feasibility);

        var riverside = Assert.Single(result.Candidates, candidate => candidate.Candidate.Value == "Riverside");
        Assert.Equal(
            ObjectiveConditionAssessmentStatus.Satisfied,
            GetCondition(riverside, "transport-bus").Status);
        Assert.Equal(
            ObjectiveConditionAssessmentStatus.Satisfied,
            GetCondition(riverside, "transport-mode").Status);
        Assert.False(riverside.HasHardConstraintViolation);
        Assert.Equal(CandidateFeasibility.Eligible, riverside.Feasibility);
    }

    [Fact]
    public void Evaluates_time_window_containment_without_inventing_a_match()
    {
        var objective = new ObjectiveFormula("visit within the available time")
        {
            Conditions = ImmutableArray.Create(new ObjectiveCondition(
                "time-window",
                ObjectiveConditionRole.HardConstraint,
                ObjectiveConditionOperator.Within,
                new ObjectiveTimeWindowValue(new TimeOnly(9, 0), new TimeOnly(18, 0)),
                "I can leave at 9:00 and be back by 18:00.")
            {
                TargetAttribute = "opening-window"
            })
        };
        var knowledgeBase = new KnowledgeBase();
        AddAttribute(
            knowledgeBase,
            "WideHours",
            "opening-window",
            new ObjectiveTimeWindowValue(new TimeOnly(8, 0), new TimeOnly(20, 0)));
        AddAttribute(
            knowledgeBase,
            "ShortHours",
            "opening-window",
            new ObjectiveTimeWindowValue(new TimeOnly(10, 0), new TimeOnly(17, 0)));

        var result = evaluator.Evaluate(objective, knowledgeBase);

        var wide = Assert.Single(result.Candidates, candidate => candidate.Candidate.Value == "WideHours");
        var shortHours = Assert.Single(result.Candidates, candidate => candidate.Candidate.Value == "ShortHours");
        Assert.Equal(
            ObjectiveConditionAssessmentStatus.Satisfied,
            GetCondition(wide, "time-window").Status);
        Assert.Equal(
            ObjectiveConditionAssessmentStatus.Violated,
            GetCondition(shortHours, "time-window").Status);
    }

    [Fact]
    public void Conflicting_attribute_values_are_reported_as_conflicting()
    {
        var objective = CreateDayOutObjective();
        var knowledgeBase = new KnowledgeBase();
        AddAttribute(knowledgeBase, "Fernbank", "total-cost", new ObjectiveMoneyValue(6m, "$"));
        AddAttribute(knowledgeBase, "Fernbank", "total-cost", new ObjectiveMoneyValue(8m, "$"));

        var result = evaluator.Evaluate(objective, knowledgeBase);
        var candidate = Assert.Single(result.Candidates);

        Assert.Equal(
            ObjectiveConditionAssessmentStatus.Conflicting,
            GetCondition(candidate, "spending-limit").Status);
        Assert.True(candidate.HasUnresolvedHardConstraint);
    }

    [Fact]
    public void Knowledge_attributes_are_read_only_and_merge_provenance_for_duplicate_values()
    {
        var knowledgeBase = new KnowledgeBase();
        var attributes = Assert.IsAssignableFrom<IList<KnowledgeAttribute>>(knowledgeBase.Attributes);

        Assert.True(attributes.IsReadOnly);

        Assert.True(knowledgeBase.TryAddAttribute(
            new KnowledgeAttribute(
                new SemanticIdentifier("Fernbank"),
                new SemanticIdentifier("total-cost"),
                new ObjectiveMoneyValue(6m, "$"),
                "Fernbank costs six dollars.",
                KnowledgeSourceKind.ImportedClaim,
                SourceId: "day-out"),
            out var firstError), firstError);

        Assert.True(knowledgeBase.TryAddAttribute(
            new KnowledgeAttribute(
                new SemanticIdentifier("Fernbank"),
                new SemanticIdentifier("total-cost"),
                new ObjectiveMoneyValue(6m, "$"),
                "The return fare and entry total six dollars.",
                KnowledgeSourceKind.ExternalObservation,
                SourceId: "cost-check"),
            out var secondError), secondError);

        var attribute = Assert.Single(knowledgeBase.Attributes);
        Assert.Equal(2, attribute.Sources.Length);
        Assert.Equal("day-out", attribute.Sources[0].SourceId);
        Assert.Equal("cost-check", attribute.Sources[1].SourceId);
    }

    [Fact]
    public void Rejects_invalid_typed_attribute_values()
    {
        var knowledgeBase = new KnowledgeBase();
        var attribute = new KnowledgeAttribute(
            new SemanticIdentifier("Fernbank"),
            new SemanticIdentifier("tags"),
            new ObjectiveTextSetValue(ImmutableArray<string>.Empty),
            "Fernbank has no supplied tags.");

        var added = knowledgeBase.TryAddAttribute(attribute, out var error);

        Assert.False(added);
        Assert.Contains("valid typed value", error);
        Assert.Empty(knowledgeBase.Attributes);
    }

    [Fact]
    public void Requests_missing_candidate_evidence_for_unknown_hard_constraints()
    {
        var objective = CreateDayOutObjective();
        var knowledgeBase = new KnowledgeBase();

        foreach (var candidate in new[] { "Aster 14", "Birch 15" })
        {
            AddAttribute(knowledgeBase, candidate, "candidate-type", new ObjectiveTextValue("laptop"));
            AddAttribute(knowledgeBase, candidate, "transport-mode", new ObjectiveTextValue("walking"));
        }

        var evaluation = evaluator.Evaluate(objective, knowledgeBase);

        Assert.Equal(
            new[] { "Aster 14", "Birch 15" },
            evaluation.EvidenceRequests.Select(request => request.Candidate.Value));
        Assert.All(evaluation.EvidenceRequests, request =>
        {
            Assert.Equal("spending-limit", request.ConditionName);
            Assert.Equal("total-cost", request.AttributeName);
            Assert.StartsWith("What is the total cost for ", request.Prompt);
        });
    }

    private static KnowledgeBase CreateExternalPriceKnowledgeBase(DateTimeOffset? freshUntil)
    {
        var knowledgeBase = new KnowledgeBase();
        AddAttribute(knowledgeBase, "Candidate", "transport-mode", new ObjectiveTextValue("walking"));
        AddAttribute(knowledgeBase, "Candidate", "tags", new ObjectiveTextSetValue(
            ImmutableArray.Create("quiet", "outdoors")));

        var added = knowledgeBase.TryAddAttribute(
            new KnowledgeAttribute(
                new SemanticIdentifier("Candidate"),
                new SemanticIdentifier("total-cost"),
                new ObjectiveMoneyValue(6m, "$"),
                "Candidate price was retrieved from an external page.",
                KnowledgeSourceKind.ExternalObservation,
                SourceId: "https://example.org/candidate")
            {
                RetrievedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
                FreshUntil = freshUntil
            },
            out var error);
        Assert.True(added, error);
        return knowledgeBase;
    }

    private ObjectiveFormula CreateDayOutObjective()
    {
        const string input = "I have a free Saturday, a limited budget, and no car. I would prefer somewhere quiet and outdoors. Help me put together a good day.";
        var parsed = parser.ParseText(input);
        Assert.True(parsed.IsSuccess, parsed.Error);
        var objective = Assert.IsType<ObjectiveFormula>(Assert.Single(parsed.Utterances).Meaning);

        return objective with
        {
            Conditions = objective.Conditions
                .Select(condition => condition.Name == "spending-limit"
                    ? condition with { Value = new ObjectiveMoneyValue(25m, "$") }
                    : condition)
                .ToImmutableArray()
        };
    }

    private static KnowledgeBase CreateCandidateKnowledgeBase()
    {
        var knowledgeBase = new KnowledgeBase();

        AddAttribute(knowledgeBase, "Fernbank", "total-cost", new ObjectiveMoneyValue(6m, "$"));
        AddAttribute(knowledgeBase, "Fernbank", "transport-mode", new ObjectiveTextValue("bus"));
        AddAttribute(knowledgeBase, "Fernbank", "tags", new ObjectiveTextSetValue(
            ImmutableArray.Create("quiet", "outdoors")));

        AddAttribute(knowledgeBase, "Riverside", "total-cost", new ObjectiveMoneyValue(0m, "$"));
        AddAttribute(knowledgeBase, "Riverside", "transport-mode", new ObjectiveTextValue("walking"));
        AddAttribute(knowledgeBase, "Riverside", "tags", new ObjectiveTextSetValue(
            ImmutableArray.Create("busy", "outdoors"))
        {
            ExcludedValues = ImmutableArray.Create("quiet")
        });

        AddAttribute(knowledgeBase, "Hilltop", "transport-mode", new ObjectiveTextValue("bus"));
        AddAttribute(knowledgeBase, "Hilltop", "tags", new ObjectiveTextSetValue(
            ImmutableArray.Create("quiet", "outdoors")));

        return knowledgeBase;
    }

    private static void AddAttribute(
        KnowledgeBase knowledgeBase,
        string candidate,
        string name,
        ObjectiveConditionValue value)
    {
        var added = knowledgeBase.TryAddAttribute(
            new KnowledgeAttribute(
                new SemanticIdentifier(candidate),
                new SemanticIdentifier(name),
                value,
                $"{candidate} {name} is represented by {value}.",
                KnowledgeSourceKind.ImportedClaim,
                SourceId: "candidate-fixture"),
            out var error);

        Assert.True(added, error);
    }

    private static ObjectiveConditionAssessment GetCondition(
        CandidateAssessment candidate,
        string name) =>
        Assert.Single(candidate.Conditions, condition => condition.ConditionName == name);
}
