# Client contract and runtime boundary

Lattice frontends communicate through the public contracts in `Lattice.Client`.

## Contract

- `ILatticeClient` creates independent conversational sessions.
- `ILatticeSession` exposes a stable session ID, accepts a text turn, and has an explicit asynchronous lifetime.
- `LatticeTurnResult` distinguishes a completed turn from input rejected before processing.
- Per-utterance results represent stored statements, rejected statements, answered questions, clarification requests, and unsupported question forms.
- Answers expose the four-valued truth result and structured supporting/opposing evidence, including provenance and proof premises.

The contract uses transport-neutral .NET types and has no dependency on Core, a UI framework, a process provider, or an HTTP implementation. It is the shared boundary, not a second domain model: Core remains authoritative for parsing, semantics, context, and reasoning.

## In-process runtime

`Lattice.Runtime` supplies `LatticeRuntimeClient`, the first implementation of `ILatticeClient`. Each created session owns its own parser, conversation context, knowledge base, and reasoning engine. Turns are processed in order, and a session serializes concurrent submissions so its context and knowledge cannot be mutated concurrently.

The runtime currently exposes the native text-parsing and reasoning path. Tool discovery/invocation, model-assisted translation, knowledge-pack management, and persistence are not yet part of this first contract slice; they will be added only when their cross-frontend behavior is defined.

## Future transports and hosts

A future HTTP client can implement the same `ILatticeClient` and `ILatticeSession` contracts, while a server host can expose the in-process runtime over a narrow API. Neither transport needs to change Core or the frontend-facing result model. The REPL will be adapted to this contract in a separate step; this PR does not change its current composition or behavior.
