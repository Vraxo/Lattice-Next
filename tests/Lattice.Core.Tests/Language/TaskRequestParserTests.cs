using Lattice.Core.Language;
using Lattice.Core.Semantics;
using Xunit;

namespace Lattice.Core.Tests.Language;

public sealed class TaskRequestParserTests
{
    private readonly LceParser parser = new();



    [Fact]
    public void Parses_a_beginner_concept_explanation_as_a_structured_task()
    {
        const string input = "Can you explain contrastive learning in machine learning in simple terms for someone new to the field of ML?";

        var result = parser.ParseText(input);

        Assert.True(result.IsSuccess, result.Error);
        var utterance = Assert.Single(result.Utterances);
        var request = Assert.IsType<TaskRequestFormula>(utterance.Meaning);

        Assert.Equal(UtteranceKind.TaskRequest, utterance.Kind);
        Assert.Equal(TaskRequestAction.Explain, request.Action);
        Assert.Equal("contrastive-learning", request.Subject);
        Assert.Equal("machine learning", request.Context);
        Assert.Equal("conceptual-overview", request.OutputFormat);
        Assert.Equal(new[] { "beginner-friendly" }, request.Requirements);
    }

    [Fact]
    public void Parses_a_beginner_search_optimization_learning_request()
    {
        const string input = "How can I learn to optimize my webpage for search engines?";

        var result = parser.ParseText(input);

        Assert.True(result.IsSuccess, result.Error);
        var utterance = Assert.Single(result.Utterances);
        var request = Assert.IsType<TaskRequestFormula>(utterance.Meaning);

        Assert.Equal(UtteranceKind.TaskRequest, utterance.Kind);
        Assert.Equal(TaskRequestAction.Plan, request.Action);
        Assert.Equal("search-engine-optimization", request.Subject);
        Assert.Equal("webpage", request.Context);
        Assert.Equal("learning-path", request.OutputFormat);
        Assert.Equal(new[] { "beginner", "practical-exercises" }, request.Requirements);
    }

    [Fact]
    public void Parses_a_Blender_object_replacement_script_request()
    {
        const string input = "How would I create a script for blender that replaces all of objects with a specific name with a point light?";

        var result = parser.ParseText(input);

        Assert.True(result.IsSuccess, result.Error);
        var utterance = Assert.Single(result.Utterances);
        var request = Assert.IsType<TaskRequestFormula>(utterance.Meaning);

        Assert.Equal(UtteranceKind.TaskRequest, utterance.Kind);
        Assert.Equal(TaskRequestAction.Code, request.Action);
        Assert.Equal("blender-object-replacement", request.Subject);
        Assert.Equal("blender", request.Context);
        Assert.Equal("python-script", request.OutputFormat);
        Assert.Equal("object-to-point-light", request.Scope);
        Assert.Equal(new[] { "python", "blender-api" }, request.Requirements);
        Assert.Equal(input, utterance.SourceText);
    }

    [Fact]
    public void Parses_an_unsupported_Blender_script_request_without_claiming_its_task_is_known()
    {
        const string input = "How would I create a script for blender that renames every object?";

        var result = parser.ParseText(input);

        Assert.True(result.IsSuccess, result.Error);
        var request = Assert.IsType<TaskRequestFormula>(Assert.Single(result.Utterances).Meaning);
        Assert.Equal(TaskRequestAction.Code, request.Action);
        Assert.Equal("unsupported-blender-script", request.Subject);
    }

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
