namespace Lattice.Client;

/// <summary>
/// Creates isolated Lattice sessions through a transport-independent client boundary.
/// </summary>
public interface ILatticeClient
{
    ValueTask<ILatticeSession> CreateSessionAsync(
        CancellationToken cancellationToken = default);
}
