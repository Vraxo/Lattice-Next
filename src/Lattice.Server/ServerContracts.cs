namespace Lattice.Server;

/// <summary>
/// Response returned when a server-side session is created.
/// </summary>
public sealed record LatticeSessionCreated(Guid SessionId);

/// <summary>
/// Request body for submitting one text turn.
/// </summary>
public sealed record LatticeTurnRequest(string Input);

/// <summary>
/// Request body for loading a versioned knowledge-pack JSON document.
/// </summary>
public sealed record LatticeKnowledgePackRequest(string Json);
