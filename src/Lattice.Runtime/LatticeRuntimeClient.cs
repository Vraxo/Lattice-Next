using Lattice.Client;

namespace Lattice.Runtime;

/// <summary>
/// Creates in-process sessions backed by the shared Core implementation.
/// </summary>
public sealed class LatticeRuntimeClient : ILatticeClient
{
    public ValueTask<ILatticeSession> CreateSessionAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ILatticeSession session = new LatticeRuntimeSession();
        return ValueTask.FromResult(session);
    }
}
