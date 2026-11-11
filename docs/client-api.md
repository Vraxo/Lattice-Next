# Client contract and runtime boundary

Lattice frontends communicate through the public contracts in `Lattice.Client`.

## Contract

- `ILatticeClient` creates independent conversational sessions.
- `ILatticeSession` exposes a stable session ID, accepts a text turn, loads a validated knowledge pack, provides a read-only session snapshot, and has an explicit asynchronous lifetime.
- `LatticeTurnResult` distinguishes a completed turn from input rejected before processing.
- Per-utterance results represent stored statements, rejected statements, answered questions, clarification requests, and unsupported question forms.
- Answers expose the four-valued truth result and structured supporting/opposing evidence, including provenance and proof premises.
- Session snapshots expose counts only; they do not expose mutable Core objects.

The contract uses transport-neutral .NET types and has no dependency on Core, a UI framework, a process provider, or an HTTP implementation. Core remains authoritative for parsing, semantics, context, knowledge validation, and reasoning.

## In-process runtime

`Lattice.Runtime` supplies `LatticeRuntimeClient`, the first implementation of `ILatticeClient`. Each created session owns its own parser, conversation context, knowledge base, reasoning engine, and loaded-pack list. Turns and pack loads are serialized per session so context and knowledge cannot be mutated concurrently.

The runtime exposes the native text-parsing and reasoning path plus validated knowledge-pack loading. Tool discovery/invocation, model-assisted translation, and persistence are not yet part of this first contract slice; those remain host/frontend capabilities until their cross-frontend behavior is defined.

## REPL integration

`Lattice.Repl` creates an in-process client and sends ordinary input, accepted translation candidates, and knowledge-pack loads through its session contract. The REPL no longer owns a parser, conversation context, knowledge base, or reasoning engine for ordinary utterance processing. Its separate conversation context is retained only for temporary tool observations used by the existing tool-intent workflow. Status output reads a value-only session snapshot.

## Future transports and hosts

A future HTTP client can implement the same `ILatticeClient` and `ILatticeSession` contracts, while a server host can expose the in-process runtime over a narrow API. Neither transport needs to change Core or the frontend-facing result model.
