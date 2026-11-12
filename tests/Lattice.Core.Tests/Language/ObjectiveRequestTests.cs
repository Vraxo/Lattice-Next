using Lattice.Core.Language;
using Lattice.Core.Semantics;
using Xunit;

namespace Lattice.Core.Tests.Language;

public sealed class ObjectiveRequestTests
{
    private readonly LceParser parser = new();

    [Theory]
    [InlineData("I have a free Saturday, a limited budget, and no car. I would prefer somewhere quiet and outdoors. Help me put together a good day.", "put together a good day")]
    [InlineData("I'm looking for a laptop that will last a long time. I don't need gaming performance, but I care about screen quality, reliability, and repairability. Help me compare these three options. My budget is $1,000.", "compare these three options")]
    public void Parses_objectives_and_preserves_the_complete_request(string input, string expectedGoal)
    {
        var result = parser.ParseText(input);

        Assert.True(result.IsSuccess, result.Error);
        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(UtteranceKind.Objective, utterance.Kind);
        Assert.Equal(expectedGoal, Assert.IsType<ObjectiveFormula>(utterance.Meaning).Goal);
        Assert.Equal(input, utterance.SourceText);
    }

    [Fact]
    public void Preserves_the_same_day_out_conditions_when_request_information_is_reordered()
    {
        const string baseline = "I have a free Saturday, a limited budget, and no car. I would prefer somewhere quiet and outdoors. Help me put together a good day.";
        var variants = new[]
        {
            "Help me put together a good day. I have a limited budget and no car. I prefer somewhere quiet and outdoors. I have a free Saturday.",
            "I have no car, a free Saturday, and a limited budget. Help me put together a good day. I would prefer somewhere quiet and outdoors.",
            "I would prefer somewhere quiet and outdoors. Help me put together a good day. I have a free Saturday, no car, and a limited budget."
        };

        static ObjectiveFormula ParseObjective(LceParser parser, string input)
        {
            var result = parser.ParseText(input);
            Assert.True(result.IsSuccess, result.Error);
            return Assert.IsType<ObjectiveFormula>(Assert.Single(result.Utterances).Meaning);
        }

        static void AssertDayOutConditions(ObjectiveFormula objective)
        {
            var budget = Assert.Single(objective.Conditions, condition => condition.Name == "spending-limit");
            Assert.Equal(ObjectiveConditionRole.HardConstraint, budget.Role);
            Assert.Equal(ObjectiveConditionOperator.AtMost, budget.Operator);
            Assert.Null(budget.Value);
            Assert.Equal(ObjectiveConditionValueKind.Money, budget.ExpectedValueKind);
            Assert.Equal("total-cost", budget.TargetAttribute);

            var transport = Assert.Single(objective.Conditions, condition => condition.Name == "transport");
            Assert.Equal(ObjectiveConditionRole.HardConstraint, transport.Role);
            Assert.Equal(ObjectiveConditionOperator.Excludes, transport.Operator);
            Assert.Equal(new ObjectiveTextValue("car"), transport.Value);
            Assert.Equal("transport-mode", transport.TargetAttribute);

            var preference = Assert.Single(objective.Conditions, condition => condition.Name == "preference");
            Assert.Equal(ObjectiveConditionOperator.Desires, preference.Operator);
            Assert.Equal("tags", preference.TargetAttribute);
            Assert.Equal(
                new[] { "quiet", "outdoors" },
                Assert.IsType<ObjectiveTextSetValue>(preference.Value).Values);
        }

        AssertDayOutConditions(ParseObjective(parser, baseline));
        foreach (var variant in variants)
        {
            AssertDayOutConditions(ParseObjective(parser, variant));
        }
    }

    [Theory]
    [InlineData("Can you help me put together a good day? I can spend up to $25. I don't have a car. I'd like somewhere quiet and outdoors.")]
    [InlineData("Please put together a good day for me. I have $25 to spend. I won't have access to a car. I want somewhere quiet and outdoors.")]
    [InlineData("Let's put together a good day. My maximum spend is $25. I'm without a car. I would like somewhere quiet and outdoors.")]
    [InlineData("I would like to put together a good day. I don't want to spend more than $25. I'm car-free. I would prefer somewhere quiet and outdoors.")]
    public void Parses_common_objective_request_paraphrases_into_the_same_typed_conditions(string input)
    {
        var result = parser.ParseText(input);

        Assert.True(result.IsSuccess, result.Error);
        var objective = Assert.IsType<ObjectiveFormula>(Assert.Single(result.Utterances).Meaning);
        Assert.Equal(UtteranceKind.Objective, Assert.Single(result.Utterances).Kind);

        var budget = Assert.Single(objective.Conditions, condition => condition.Name == "spending-limit");
        Assert.Equal(ObjectiveConditionRole.HardConstraint, budget.Role);
        Assert.Equal(new ObjectiveMoneyValue(25m, "$"), budget.Value);

        var transport = Assert.Single(objective.Conditions, condition => condition.Name == "transport");
        Assert.Equal(ObjectiveConditionOperator.Excludes, transport.Operator);
        Assert.Equal(new ObjectiveTextValue("car"), transport.Value);

        var preference = Assert.Single(objective.Conditions, condition => condition.Name == "preference");
        Assert.Equal(
            new[] { "quiet", "outdoors" },
            Assert.IsType<ObjectiveTextSetValue>(preference.Value).Values);
    }

    [Fact]
    public void Parses_initial_objective_time_window_and_id_prefer_into_typed_conditions()
    {
        const string input = "Help me plan a Saturday day out. I can spend up to $25. I don't have a car. I'd prefer somewhere quiet and outdoors. I'm starting at Central Station, and I can leave at 9:00 and be back by 18:00.";
        var result = parser.ParseText(input);

        Assert.True(result.IsSuccess, result.Error);
        var objective = Assert.IsType<ObjectiveFormula>(Assert.Single(result.Utterances).Meaning);

        var budget = Assert.Single(objective.Conditions, condition => condition.Name == "spending-limit");
        Assert.Equal(new ObjectiveMoneyValue(25m, "$"), budget.Value);

        var transport = Assert.Single(objective.Conditions, condition => condition.Name == "transport");
        Assert.Equal(new ObjectiveTextValue("car"), transport.Value);

        var preference = Assert.Single(objective.Conditions, condition => condition.Name == "preference");
        Assert.Equal(
            new[] { "quiet", "outdoors" },
            Assert.IsType<ObjectiveTextSetValue>(preference.Value).Values);

        var origin = Assert.Single(objective.Conditions, condition => condition.Name == "origin");
        Assert.Equal(ObjectiveConditionRole.HardConstraint, origin.Role);
        Assert.Equal(ObjectiveConditionOperator.Equals, origin.Operator);
        Assert.Equal(new ObjectiveTextValue("Central Station"), origin.Value);
        Assert.Equal("origin", origin.TargetAttribute);

        var time = Assert.Single(objective.Conditions, condition => condition.Name == "time-window");
        Assert.Equal(ObjectiveConditionRole.HardConstraint, time.Role);
        Assert.Equal(ObjectiveConditionOperator.CanFit, time.Operator);
        Assert.Equal("temporal-plan", time.TargetAttribute);
        Assert.Equal(
            new ObjectiveTimeWindowValue(new TimeOnly(9, 0), new TimeOnly(18, 0)),
            time.Value);
    }

    [Fact]
    public void Represents_day_out_budget_transport_and_preference_without_collapsing_their_roles()
    {
        const string input = "I have a free Saturday, a limited budget, and no car. I would prefer somewhere quiet and outdoors. Help me put together a good day.";
        var result = parser.ParseText(input);
        Assert.True(result.IsSuccess, result.Error);
        var objective = Assert.IsType<ObjectiveFormula>(Assert.Single(result.Utterances).Meaning);

        var budget = Assert.Single(objective.Conditions, condition => condition.Name == "spending-limit");
        Assert.Equal(ObjectiveConditionRole.HardConstraint, budget.Role);
        Assert.Equal(ObjectiveConditionOperator.AtMost, budget.Operator);
        Assert.Null(budget.Value);
        Assert.Equal(ObjectiveConditionValueKind.Money, budget.ExpectedValueKind);
        Assert.Equal("What is your spending limit?", budget.MissingValuePrompt);
        Assert.Equal("limited budget", budget.SourceText);
        Assert.Equal("total-cost", budget.TargetAttribute);

        var transport = Assert.Single(objective.Conditions, condition => condition.Name == "transport");
        Assert.Equal(ObjectiveConditionRole.HardConstraint, transport.Role);
        Assert.Equal(ObjectiveConditionOperator.Excludes, transport.Operator);
        Assert.Equal(new ObjectiveTextValue("car"), transport.Value);
        Assert.Equal("transport-mode", transport.TargetAttribute);

        var preference = Assert.Single(objective.Conditions, condition => condition.Role == ObjectiveConditionRole.Preference);
        Assert.Equal(ObjectiveConditionOperator.Desires, preference.Operator);
        Assert.Equal("tags", preference.TargetAttribute);
        Assert.Equal(
            new[] { "quiet", "outdoors" },
            Assert.IsType<ObjectiveTextSetValue>(preference.Value).Values);
    }

    [Fact]
    public void Parses_an_explicit_budget_as_a_typed_money_value()
    {
        const string input = "I'm looking for a laptop that will last a long time. I don't need gaming performance, but I care about screen quality, reliability, and repairability. Help me compare these three options. My budget is $1,000.";
        var result = parser.ParseText(input);
        Assert.True(result.IsSuccess, result.Error);
        var objective = Assert.IsType<ObjectiveFormula>(Assert.Single(result.Utterances).Meaning);

        var budget = Assert.Single(objective.Conditions, condition => condition.Name == "spending-limit");
        Assert.Equal(ObjectiveConditionRole.HardConstraint, budget.Role);
        Assert.Equal(ObjectiveConditionOperator.AtMost, budget.Operator);
        Assert.Equal(new ObjectiveMoneyValue(1000m, "$"), budget.Value);
        Assert.Equal(ObjectiveConditionValueKind.Money, budget.ExpectedValueKind);
    }

    [Fact]
    public void Transfers_the_same_typed_budget_and_preference_model_to_a_laptop_objective()
    {
        const string input = "I'm looking for a laptop that will last a long time. I don't need gaming performance, but I care about screen quality, reliability, and repairability. Help me compare these three options. My budget is $1,000.";
        var result = parser.ParseText(input);

        Assert.True(result.IsSuccess, result.Error);
        var objective = Assert.IsType<ObjectiveFormula>(Assert.Single(result.Utterances).Meaning);
        var budget = Assert.Single(objective.Conditions, condition => condition.Name == "spending-limit");
        var preference = Assert.Single(objective.Conditions, condition => condition.Role == ObjectiveConditionRole.Preference);

        Assert.Equal(new ObjectiveMoneyValue(1000m, "$"), budget.Value);
        Assert.Equal(ObjectiveConditionRole.HardConstraint, budget.Role);
        Assert.Equal(ObjectiveConditionOperator.AtMost, budget.Operator);
        Assert.Equal(
            new[] { "screen quality", "reliability", "repairability" },
            Assert.IsType<ObjectiveTextSetValue>(preference.Value).Values);
        Assert.Equal(ObjectiveConditionOperator.Desires, preference.Operator);
    }

    [Fact]
    public void Parses_a_typed_money_answer_as_objective_information()
    {
        var result = parser.ParseText("$25 total.");

        Assert.True(result.IsSuccess, result.Error);
        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(UtteranceKind.ObjectiveInformation, utterance.Kind);
        Assert.Equal(
            new ObjectiveInformationFormula(new ObjectiveMoneyValue(25m, "$")),
            utterance.Meaning);
    }

    [Fact]
    public void Parses_a_natural_language_spending_limit_answer_as_typed_money()
    {
        var result = parser.ParseText("I can spend up to $25.");

        Assert.True(result.IsSuccess, result.Error);
        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(UtteranceKind.ObjectiveInformation, utterance.Kind);
        Assert.Equal(
            new ObjectiveInformationFormula(new ObjectiveMoneyValue(25m, "$")),
            utterance.Meaning);
    }

    [Theory]
    [InlineData("Lunch is covered.")]
    [InlineData("I have lunch with me.")]
    [InlineData("I'll pack my own lunch.")]
    [InlineData("I've got lunch sorted.")]
    [InlineData("I already have lunch.")]
    [InlineData("I'm bringing my own lunch.")]
    [InlineData("I'll have a packed lunch.")]
    [InlineData("I won't need to buy lunch.")]
    public void Parses_equivalent_packed_lunch_phrasings(string input)
    {
        var result = parser.ParseText(input);

        Assert.True(result.IsSuccess, result.Error);
        var utterance = Assert.Single(result.Utterances);
        var update = Assert.IsType<ObjectiveConditionUpdateFormula>(utterance.Meaning);
        var meal = Assert.Single(update.Conditions);
        Assert.Equal("meal-plan", meal.Name);
        Assert.Equal(ObjectiveConditionOperator.Equals, meal.Operator);
        Assert.Equal(new ObjectiveTextValue("bring-lunch"), meal.Value);
    }

    [Theory]
    [InlineData("I'm coming from Central Station.")]
    [InlineData("I'll start from Central Station.")]
    [InlineData("My starting location is Central Station.")]
    [InlineData("I'm starting from Central Station.")]
    [InlineData("I'll be leaving from Central Station.")]
    [InlineData("I'm setting off from Central Station.")]
    public void Parses_additional_origin_phrasings(string input)
    {
        var result = parser.ParseText(input);

        Assert.True(result.IsSuccess, result.Error);
        var utterance = Assert.Single(result.Utterances);
        var update = Assert.IsType<ObjectiveConditionUpdateFormula>(utterance.Meaning);
        var origin = Assert.Single(update.Conditions);
        Assert.Equal("origin", origin.Name);
        Assert.Equal(ObjectiveConditionOperator.Equals, origin.Operator);
        Assert.Equal(new ObjectiveTextValue("Central Station"), origin.Value);
        Assert.Equal("origin", origin.TargetAttribute);
    }

    [Fact]
    public void Parses_lunch_origin_and_time_window_as_objective_condition_updates()
    {
        const string input = "I'm starting at Central Station, and I can leave at 9:00 and be back by 18:00.";
        var result = parser.ParseText(input);

        Assert.True(result.IsSuccess, result.Error);
        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(UtteranceKind.ObjectiveConditionUpdate, utterance.Kind);
        var update = Assert.IsType<ObjectiveConditionUpdateFormula>(utterance.Meaning);
        Assert.Collection(
            update.Conditions,
            origin =>
            {
                Assert.Equal("origin", origin.Name);
                Assert.Equal(ObjectiveConditionOperator.Equals, origin.Operator);
                Assert.Equal(new ObjectiveTextValue("Central Station"), origin.Value);
            },
            time =>
            {
                Assert.Equal("time-window", time.Name);
                Assert.Equal(ObjectiveConditionOperator.CanFit, time.Operator);
                Assert.Equal("temporal-plan", time.TargetAttribute);
                Assert.Equal(
                    new ObjectiveTimeWindowValue(new TimeOnly(9, 0), new TimeOnly(18, 0)),
                    time.Value);
            });
    }

    [Fact]
    public void Parses_starting_location_when_phrased_as_my_starting_point()
    {
        var result = parser.ParseText("My starting point is Central Station.");

        Assert.True(result.IsSuccess, result.Error);
        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(UtteranceKind.ObjectiveConditionUpdate, utterance.Kind);
        var update = Assert.IsType<ObjectiveConditionUpdateFormula>(utterance.Meaning);
        var origin = Assert.Single(update.Conditions);
        Assert.Equal("origin", origin.Name);
        Assert.Equal(ObjectiveConditionOperator.Equals, origin.Operator);
        Assert.Equal(new ObjectiveTextValue("Central Station"), origin.Value);
    }

    [Theory]
    [InlineData("I can be back by 18:00 and leave at 9:00.")]
    [InlineData("I need to be back by 18:00 and can leave at 9:00.")]
    [InlineData("I need to return by 18:00 and leave at 9:00.")]
    [InlineData("I can head out at 9:00 and return by 18:00.")]
    [InlineData("I can set off at 9:00 and be back by 18:00.")]
    public void Parses_time_window_when_departure_and_return_are_phrased_differently(string input)
    {
        var result = parser.ParseText(input);

        Assert.True(result.IsSuccess, result.Error);
        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(UtteranceKind.ObjectiveConditionUpdate, utterance.Kind);
        var update = Assert.IsType<ObjectiveConditionUpdateFormula>(utterance.Meaning);
        var time = Assert.Single(update.Conditions);
        Assert.Equal("time-window", time.Name);
        Assert.Equal(ObjectiveConditionOperator.CanFit, time.Operator);
        Assert.Equal("temporal-plan", time.TargetAttribute);
        Assert.Equal(
            new ObjectiveTimeWindowValue(new TimeOnly(9, 0), new TimeOnly(18, 0)),
            time.Value);
    }

    [Fact]
    public void Parses_packed_lunch_as_a_typed_objective_condition()
    {
        var result = parser.ParseText("I'll bring lunch.");

        Assert.True(result.IsSuccess, result.Error);
        var update = Assert.IsType<ObjectiveConditionUpdateFormula>(
            Assert.Single(result.Utterances).Meaning);
        var meal = Assert.Single(update.Conditions);
        Assert.Equal("meal-plan", meal.Name);
        Assert.Equal(ObjectiveConditionRole.HardConstraint, meal.Role);
        Assert.Equal(new ObjectiveTextValue("bring-lunch"), meal.Value);
        Assert.Null(meal.TargetAttribute);
    }

    [Fact]
    public void Parses_walking_only_revision_as_transport_updates()
    {
        var result = parser.ParseText("Actually, I don't want to take a bus. Walking only.");

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(2, result.Utterances.Length);

        var bus = Assert.IsType<ObjectiveConditionUpdateFormula>(result.Utterances[0].Meaning);
        var exclusion = Assert.Single(bus.Conditions);
        Assert.Equal("transport-bus", exclusion.Name);
        Assert.Equal(ObjectiveConditionOperator.Excludes, exclusion.Operator);
        Assert.Equal(new ObjectiveTextValue("bus"), exclusion.Value);

        var walking = Assert.IsType<ObjectiveConditionUpdateFormula>(result.Utterances[1].Meaning);
        var mode = Assert.Single(walking.Conditions);
        Assert.Equal("transport-mode", mode.Name);
        Assert.Equal(ObjectiveConditionOperator.Equals, mode.Operator);
        Assert.Equal(new ObjectiveTextValue("walking"), mode.Value);
    }

    [Fact]
    public void Does_not_treat_a_statement_about_helping_as_an_objective()
    {
        var result = parser.ParseText("Fido helps Mira.");

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(UtteranceKind.Assertion, Assert.Single(result.Utterances).Kind);
    }
}
