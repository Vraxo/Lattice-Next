using System.Collections.Concurrent;
using Lattice.Client;
using Lattice.Runtime;

namespace Lattice.Server;

internal sealed class LatticeSessionRegistry : IAsyncDisposable
{
    private readonly ConcurrentDictionary<Guid, ILatticeSession> sessions = new();
    private readonly LatticeRuntimeClient client = new();

    public LatticeSessionRegistry()
    {
    }

    public async ValueTask<ILatticeSession> CreateAsync(CancellationToken cancellationToken)
    {
        var session = await client.CreateSessionAsync(cancellationToken);
        sessions[session.SessionId] = session;
        return session;
    }

    public ILatticeSession? Get(Guid sessionId) =>
        sessions.TryGetValue(sessionId, out var session) ? session : null;

    public async ValueTask<bool> RemoveAsync(Guid sessionId)
    {
        if (!sessions.TryRemove(sessionId, out var session))
        {
            return false;
        }

        await session.DisposeAsync();
        return true;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var session in sessions.Values)
        {
            await session.DisposeAsync();
        }

        sessions.Clear();
    }
}
