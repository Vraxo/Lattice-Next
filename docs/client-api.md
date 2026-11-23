# Client contract and runtime

Lattice frontends use the transport-independent contracts in `Lattice.Client`. The same session model is available through the in-process runtime and the implemented HTTP transport.

## Contract

- `ILatticeClient` creates independent conversational sessions.
- `ILatticeSession` exposes a stable session ID, accepts text turns, loads validated knowledge packs, accepts explicitly reviewed typed candidate evidence, provides read-only session status, and has an asynchronous lifetime.
- `LatticeTurnResult` distinguishes a completed turn from input rejected before processing.
- Per-utterance results represent stored statements, rejected statements, answered questions, clarification requests, and unsupported question forms.
- Answers expose four-valued truth results and structured supporting/opposing evidence, including provenance and proof premises.
- Knowledge-pack loading returns a transport-neutral result with the pack name, claim count, and validation error when rejected.
- Reviewed external candidate evidence retains its source URL, retrieval timestamp, supporting excerpt, typed value, and review decision.

The contract uses transport-neutral .NET types and has no dependency on Core, a UI framework, a process provider, or HTTP. It is the shared application boundary, not a second domain model: Core remains authoritative for parsing, semantics, context, and reasoning.

## In-process runtime

`Lattice.Runtime` provides `LatticeRuntimeClient`, the in-process implementation of `ILatticeClient`. Each created session owns isolated parser/context state, a knowledge base, a reasoning engine, and its loaded-pack state. Turns and pack loads are serialized within a session; separate sessions remain independent.

The runtime supports the native text and reasoning path, validated knowledge-pack loading, objective/candidate evaluation, and the shared contract for explicitly reviewed typed candidate evidence. It does not make fetched content trusted automatically. Tool discovery/invocation and LM Studio translation remain frontend-side; durable session persistence is not implemented.

## HTTP transport

The HTTP host and remote client are implemented:

- `Lattice.Server` hosts the shared session contract.
- `Lattice.Client.Http` implements `ILatticeClient` over the server API.
- `Lattice.Repl` and `Lattice.Cli` can use either the in-process runtime or the HTTP client.

The HTTP host keeps sessions in memory. Restarting the host ends active sessions; durable persistence and idle-session expiration are not included. Deployment, authentication, and route details are documented in [HTTP server and remote client](server.md).

## Frontends and local capabilities

The REPL and non-interactive CLI use the same public session contract. The REPL may additionally compose local tools and the optional LM Studio adapter. Those capabilities are not silently installed or executed by the remote server; see [server.md](server.md) for the remote boundary.

Reviewed page evidence follows an explicit human-review path. The user identifies the candidate and typed attribute, supplies an exact excerpt from the fetched page, inspects the source and freshness metadata, and explicitly accepts the claim. The runtime validates the candidate, active evidence request, attribute/value type, and provenance before storing it. A fetched page is an untrusted source envelope, not an instruction or fact by itself.
