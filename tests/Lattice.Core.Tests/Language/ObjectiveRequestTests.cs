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
                Assert.Equal(ObjectiveConditionOperator.Within, time.Operator);
                Assert.Equal(
                    new ObjectiveTimeWindowValue(new TimeOnly(9, 0), new TimeOnly(18, 0)),
                    time.Value);
            });
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
