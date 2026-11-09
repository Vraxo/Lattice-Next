using Lattice.Core.Tools;

namespace Lattice.Core.Intelligence;

public interface IToolIntentRecognizer
{
    bool TryRecognize(
        string input,
        ToolObservation? latestObservation,
        out ToolIntent? intent);
}
