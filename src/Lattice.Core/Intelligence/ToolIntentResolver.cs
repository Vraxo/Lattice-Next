namespace Lattice.Core.Intelligence;

public sealed class ToolIntentResolver
{
    private readonly IReadOnlyList<IToolIntentRecognizer> recognizers;

    public ToolIntentResolver(IEnumerable<IToolIntentRecognizer> recognizers)
    {
        ArgumentNullException.ThrowIfNull(recognizers);
        this.recognizers = recognizers.ToArray();
    }

    public ToolIntentResolution Resolve(
        string input,
        Lattice.Core.Tools.ToolObservation? latestObservation = null)
    {
        ArgumentNullException.ThrowIfNull(input);

        ToolIntent? match = null;

        foreach (var recognizer in recognizers)
        {
            if (!recognizer.TryRecognize(input, latestObservation, out var intent))
            {
                continue;
            }

            if (intent is null)
            {
                continue;
            }

            if (match is not null)
            {
                return new ToolIntentResolution(
                    ToolIntentResolutionStatus.Ambiguous,
                    null);
            }

            match = intent;
        }

        return match is null
            ? new ToolIntentResolution(ToolIntentResolutionStatus.NotRecognized, null)
            : new ToolIntentResolution(ToolIntentResolutionStatus.Resolved, match);
    }
}
