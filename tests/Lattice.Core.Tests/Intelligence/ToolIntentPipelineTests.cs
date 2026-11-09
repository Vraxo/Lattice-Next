using Lattice.Core.Context;
using Lattice.Core.Intelligence;
using Lattice.Core.Reasoning;
using Lattice.Core.Tools;
using Lattice.Core.Tests.Tools;
using Xunit;

namespace Lattice.Core.Tests.Intelligence;

public sealed class ToolIntentPipelineTests
{
    [Fact]
    public async Task Selects_invokes_and_remembers_tool_observations_without_promoting_them_to_knowledge()
    {
        var registry = BundledToolTestSupport.CreateRegistry();
        var resolver = CreateResolver(registry.Descriptors);
        var context = new ConversationContext();
        var knowledgeBase = new KnowledgeBase();

        var first = resolver.Resolve("What is 12 multiplied by 3?");
        Assert.True(first.IsResolved);

        var firstResult = await registry.TryInvokeAsync(
            first.Intent!.ToolId,
            first.Intent.ArgumentsJson,
            TestContext.Current.CancellationToken);

        Assert.True(firstResult.IsSuccess, firstResult.Error);
        context.Observe(firstResult.Observation!);
        Assert.Equal("12 multiplied by 3 is 36.", first.Intent.FormatResponse(firstResult.Observation!));

        var followUp = resolver.Resolve("Multiply that by 5.", context.LatestToolObservation);
        Assert.True(followUp.IsResolved);

        var followUpResult = await registry.TryInvokeAsync(
            followUp.Intent!.ToolId,
            followUp.Intent.ArgumentsJson,
            TestContext.Current.CancellationToken);

        Assert.True(followUpResult.IsSuccess, followUpResult.Error);
        context.Observe(followUpResult.Observation!);
        Assert.Equal("36 multiplied by 5 is 180.", followUp.Intent.FormatResponse(followUpResult.Observation!));

        var conversion = resolver.Resolve("Convert 180 cm to meters.", context.LatestToolObservation);
        Assert.True(conversion.IsResolved);
        Assert.Equal("unit-converter", conversion.Intent!.ToolId);

        var conversionResult = await registry.TryInvokeAsync(
            conversion.Intent.ToolId,
            conversion.Intent.ArgumentsJson,
            TestContext.Current.CancellationToken);

        Assert.True(conversionResult.IsSuccess, conversionResult.Error);
        context.Observe(conversionResult.Observation!);
        Assert.Equal("180 cm is 1.8 meters.", conversion.Intent.FormatResponse(conversionResult.Observation!));

        Assert.Equal("unit-converter", context.LatestToolObservation!.ToolId);

        var afterConversion = resolver.Resolve("Multiply that by 5.", context.LatestToolObservation);
        Assert.True(afterConversion.IsResolved);

        var afterConversionResult = await registry.TryInvokeAsync(
            afterConversion.Intent!.ToolId,
            afterConversion.Intent.ArgumentsJson,
            TestContext.Current.CancellationToken);

        Assert.True(afterConversionResult.IsSuccess, afterConversionResult.Error);
        context.Observe(afterConversionResult.Observation!);
        Assert.Equal("1.8 multiplied by 5 is 9.", afterConversion.Intent.FormatResponse(afterConversionResult.Observation!));

        var chain = resolver.Resolve(
            "Multiply that by 5, then subtract 10.",
            context.LatestToolObservation);
        Assert.True(chain.IsResolved);

        var chainResponses = new List<string>();
        var current = chain.Intent;

        while (current is not null)
        {
            var result = await registry.TryInvokeAsync(
                current.ToolId,
                current.ArgumentsJson,
                TestContext.Current.CancellationToken);

            Assert.True(result.IsSuccess, result.Error);
            context.Observe(result.Observation!);
            chainResponses.Add(current.FormatResponse(result.Observation!));
            current = current.Continue?.Invoke(result.Observation!);
        }

        Assert.Equal(
            new[] { "9 multiplied by 5 is 45.", "45 minus 10 is 35." },
            chainResponses);
        Assert.Equal("35", context.LatestToolObservation!.Content);

        Assert.Empty(knowledgeBase.Facts);
        Assert.Empty(knowledgeBase.Rules);
    }

    [Fact]
    public void Reports_unrecognized_language_without_selecting_a_tool()
    {
        var registry = BundledToolTestSupport.CreateRegistry();
        var resolver = CreateResolver(registry.Descriptors);

        var result = resolver.Resolve("Tell me something interesting.");

        Assert.Equal(ToolIntentResolutionStatus.NotRecognized, result.Status);
        Assert.Null(result.Intent);
    }
}


static ToolIntentResolver CreateResolver(IEnumerable<ToolDescriptor> descriptors)
{
    var recognizers = descriptors
        .Where(descriptor => !descriptor.Intents.IsDefaultOrEmpty)
        .SelectMany(descriptor => descriptor.Intents.Select(
            definition => new ManifestToolIntentRecognizer(descriptor, definition)));

    return new ToolIntentResolver(recognizers);
}
