# Client contract and runtime boundary

Lattice frontends communicate through the public contracts in `Lattice.Client`.

## Contract

- `ILatticeClient` creates independent conversational sessions.
- `ILatticeSession` exposes a stable session ID, accepts text turns, loads validated knowledge packs, provides read-only session counts, and has an explicit asynchronous lifetime.
- `LatticeTurnResult` distinguishes a completed turn from input rejected before processing.
- Per-utterance results represent stored statements, rejected statements, answered questions, clarification requests, and unsupported question forms.
- Answers expose the four-valued truth result and structured supporting/opposing evidence, including provenance and proof premises.
- Knowledge-pack loading returns a transport-neutral result with the pack name, claim count, and validation error when rejected.
- A session can accept user-reviewed external monetary candidate evidence while preserving its source URL, retrieval timestamp, and supporting excerpt.

The contract uses transport-neutral .NET types and has no dependency on Core, a UI framework, a process provider, or an HTTP implementation. It is the shared boundary, not a second domain model: Core remains authoritative for parsing, semantics, context, and reasoning.

## In-process runtime

`Lattice.Runtime` supplies `LatticeRuntimeClient`, the first implementation of `ILatticeClient`. Each created session owns its own parser, conversation context, knowledge base, reasoning engine, and loaded-pack list. Turns and pack loads are serialized so session context and knowledge cannot be mutated concurrently. Session status is a read-only snapshot.

The runtime exposes the native text-parsing and reasoning path plus validated knowledge-pack loading. Tool discovery/invocation, model-assisted translation, and persistence remain outside this first contract slice; they will be added only when their cross-frontend behavior is defined.

## Frontends

`Lattice.Repl` uses the in-process client for normal input, reviewed translation candidates, knowledge-pack loading, and session status. Its tool discovery/invocation and LM Studio composition remain frontend-side for now; tool observations are kept separate from the runtime's reasoning context.

## Future transports and hosts

A future HTTP client can implement the same `ILatticeClient` and `ILatticeSession` contracts, while a server host can expose the in-process runtime over a narrow API. Neither transport needs to change Core or the frontend-facing result model.


## Reviewed page evidence

The REPL can turn the latest `web-page-fetch` observation into a reviewable candidate-price claim without treating retrieved text as knowledge automatically. After fetching a page, the user supplies the candidate, amount, currency symbol, and an exact excerpt from the page with `:review-price`. Lattice displays the source URL, retrieval time, and excerpt and requires the user to type `accept` before submitting the claim through the shared session contract. The excerpt must occur verbatim in the fetched text. The runtime still validates the candidate, active monetary constraint, currency, and evidence request before storing the observation.
