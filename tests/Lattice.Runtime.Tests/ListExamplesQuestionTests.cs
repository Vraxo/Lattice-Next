using Xunit;
using Lattice.Client;
using Lattice.Runtime;

namespace Lattice.Runtime.Tests;

public sealed class ListExamplesQuestionTests
{
    [Theory]
    [InlineData("Give me the top 5 golf equipment company names.")]
    [InlineData("Give me five golf equipment company names.")]
    public async Task Lists_requested_number_of_semantic_category_members(string prompt)
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);

        var result = await session.SubmitAsync(prompt, TestContext.Current.CancellationToken);

        var answer = Assert.Single(result.Utterances);
        Assert.Equal(LatticeTurnStatus.Completed, result.Status);
        Assert.Equal(LatticeUtteranceResultKind.QuestionAnswered, answer.Kind);
        Assert.Equal("ask(world-model-list-examples(golf-equipment-company, 5))", answer.Meaning);
        Assert.Contains("Here are 5 prominent golf equipment companies:", answer.Message);
        Assert.Contains("1. Acushnet Holdings Corp.", answer.Message);
        Assert.Contains("2. Callaway Golf Company", answer.Message);
        Assert.Contains("3. PING Inc.", answer.Message);
        Assert.Contains("4. Sumitomo Rubber Industries, Ltd.", answer.Message);
        Assert.Contains("5. TaylorMade Golf Company", answer.Message);
        Assert.Equal(5, answer.Message!.Split('\n').Count(line => line.Length > 2 && char.IsDigit(line[0]) && line[1] == '.'));
        Assert.NotEmpty(answer.SupportingEvidence);
    }

    [Fact]
    public async Task Supports_numbered_names_only_diagnostic_prompt()
    {
        var client = new LatticeRuntimeClient();
        await using var session = await client.CreateSessionAsync(TestContext.Current.CancellationToken);

        var result = await session.SubmitAsync(
            "Give me five golf equipment company names in a numbered list. Names only; no descriptions.",
            TestContext.Current.CancellationToken);

        var answer = Assert.Single(result.Utterances);
        Assert.Equal(LatticeUtteranceResultKind.QuestionAnswered, answer.Kind);
        Assert.Contains("1. Acushnet Holdings Corp.", answer.Message);
        Assert.Contains("5. TaylorMade Golf Company", answer.Message);
        Assert.DoesNotContain("include:", answer.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(" - ", answer.Message);
    }
}
