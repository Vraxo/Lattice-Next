using Lattice.Core.Language;
using Lattice.Core.Semantics;
using Xunit;

namespace Lattice.Core.Tests.Language;

public sealed class TaskRequestParserTests
{
    private readonly LceParser parser = new();

    [Fact]
    public void Parses_a_counted_potential_uses_request_with_its_object_brand()
    {
        const string input = "What are 5 potential uses for an empty La Croix can?";

        var result = parser.ParseText(input);

        Assert.True(result.IsSuccess, result.Error);
        var utterance = Assert.Single(result.Utterances);
        var request = Assert.IsType<TaskRequestFormula>(utterance.Meaning);

        Assert.Equal(UtteranceKind.TaskRequest, utterance.Kind);
        Assert.Equal(TaskRequestAction.Brainstorm, request.Action);
        Assert.Equal("beverage-can", request.Subject);
        Assert.Equal("la-croix", request.Context);
        Assert.Equal("5-ideas", request.OutputFormat);
        Assert.Equal("potential-uses", request.Scope);
        Assert.Equal(new[] { "potential-uses" }, request.Requirements);
        Assert.Equal(input, utterance.SourceText);
    }

    [Fact]
    public void Parses_a_research_writing_request_as_one_structured_task()
    {
        const string input = "Can you write a short introduction about the relevance of the term \"monopsony\" in economics? Please use examples related to potential monopsonies in the labour market and cite relevant research.";

        var result = parser.ParseText(input);

        Assert.True(result.IsSuccess, result.Error);
        var utterance = Assert.Single(result.Utterances);
        var request = Assert.IsType<TaskRequestFormula>(utterance.Meaning);

        Assert.Equal(UtteranceKind.TaskRequest, utterance.Kind);
        Assert.Equal(TaskRequestAction.Write, request.Action);
        Assert.Equal("monopsony", request.Subject);
        Assert.Equal("economics", request.Context);
        Assert.Equal("short-introduction", request.OutputFormat);
        Assert.Equal("potential monopsonies in the labour market", request.Scope);
        Assert.Equal(
            new[] { "examples", "research-citations" },
            request.Requirements);
        Assert.Equal(input, utterance.SourceText);
        Assert.Contains("task(write, monopsony", SemanticFormatter.Format(utterance));
    }

    [Fact]
    public void Parses_a_manual_transmission_operation_question_as_a_structured_task()
    {
        const string input = "How do you operate a car with a manual transmission?";

        var result = parser.ParseText(input);

        Assert.True(result.IsSuccess, result.Error);
        var utterance = Assert.Single(result.Utterances);
        var request = Assert.IsType<TaskRequestFormula>(utterance.Meaning);

        Assert.Equal(UtteranceKind.TaskRequest, utterance.Kind);
        Assert.Equal(TaskRequestAction.Explain, request.Action);
        Assert.Equal("manual-transmission", request.Subject);
        Assert.Equal("car", request.Context);
        Assert.Equal("procedure", request.OutputFormat);
        Assert.Equal(new[] { "operating-procedure" }, request.Requirements);
        Assert.Equal(input, utterance.SourceText);
    }

    [Fact]
    public void Parses_a_recommendation_question_with_its_preceding_purpose()
    {
        const string input = "I would like to implement simulations of chemical unit operations using Python and use them to optimize certain outputs. Which open-source packages do you recommend for this purpose?";

        var result = parser.ParseText(input);

        Assert.True(result.IsSuccess, result.Error);
        var utterance = Assert.Single(result.Utterances);
        var request = Assert.IsType<TaskRequestFormula>(utterance.Meaning);

        Assert.Equal(UtteranceKind.TaskRequest, utterance.Kind);
        Assert.Equal(TaskRequestAction.Recommend, request.Action);
        Assert.Equal("open-source-packages", request.Subject);
        Assert.Equal(
            "implement simulations of chemical unit operations using Python and use them to optimize certain outputs",
            request.Context);
        Assert.Equal(
            new[] { "open-source", "python", "process-simulation", "optimization" },
            request.Requirements);
        Assert.Equal(input, utterance.SourceText);
    }
    [Fact]
    public void Parses_a_natural_language_hobby_exploration_request()
    {
        const string input = "I want to start doing astrophotography as a hobby, any suggestions what could i do?";

        var result = parser.ParseText(input);

        Assert.True(result.IsSuccess, result.Error);
        var utterance = Assert.Single(result.Utterances);
        var request = Assert.IsType<TaskRequestFormula>(utterance.Meaning);

        Assert.Equal(UtteranceKind.TaskRequest, utterance.Kind);
        Assert.Equal(TaskRequestAction.Explore, request.Action);
        Assert.Equal("astrophotography", request.Subject);
        Assert.Equal("hobby", request.Context);
        Assert.Equal("starter-activities", request.OutputFormat);
        Assert.Empty(request.Requirements);
        Assert.Equal(input, utterance.SourceText);
    }
}
