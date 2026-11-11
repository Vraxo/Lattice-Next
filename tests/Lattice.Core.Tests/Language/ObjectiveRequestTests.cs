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

        var transport = Assert.Single(objective.Conditions, condition => condition.Name == "transport");
        Assert.Equal(ObjectiveConditionRole.HardConstraint, transport.Role);
        Assert.Equal(ObjectiveConditionOperator.Excludes, transport.Operator);
        Assert.Equal(new ObjectiveTextValue("car"), transport.Value);

        var preference = Assert.Single(objective.Conditions, condition => condition.Role == ObjectiveConditionRole.Preference);
        Assert.Equal(ObjectiveConditionOperator.Desires, preference.Operator);
        Assert.Equal(new ObjectiveTextValue("somewhere quiet and outdoors"), preference.Value);
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
    public void Does_not_treat_a_statement_about_helping_as_an_objective()
    {
        var result = parser.ParseText("Fido helps Mira.");

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(UtteranceKind.Assertion, Assert.Single(result.Utterances).Kind);
    }
}
