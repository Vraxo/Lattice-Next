# Lattice

Lattice is an experiment in building a **tool-independent symbolic conversational intelligence**.

Its central purpose is to understand meaning, maintain context, reason over knowledge, form goals, and decide what to do. Programming is intended to be an early area of application—not Lattice's identity.

## Architectural premise

Lattice keeps three concerns independent:

- **Intelligence:** language interpretation, semantic representation, reasoning, planning, and decision-making.
- **Knowledge:** declarative, versioned knowledge packs, kept separate from conversation context and observations.
- **Capabilities:** tools that the intelligence can discover and invoke through generic contracts.

The console is the initial user interface. Tools are not the intelligence, knowledge packs are not executable plugins, and no neural model is required by the core. An optional language translator may be explored later, but it must not become a reasoning dependency.

## Current language and reasoning slice

The first language implementation is **Lattice Controlled English (LCE) 0.1**, a deliberately small controlled-language subset inspired by the approach taken by Attempto Controlled English. It is not full ACE and does not claim to understand unrestricted English.

It currently parses one sentence per input:

```text
Every dog is an animal.
Fido is a dog.
Is Fido an animal?
```

The CLI retains accepted facts and universal rules in an in-memory knowledge base for the current session. Its reasoning engine performs forward chaining over unary facts and single-premise universal rules, answers supported questions with `Yes`, and returns `Unknown` when it cannot derive an answer. Supported answers include a trace back to the original sentences.

This is intentionally not a general theorem prover. Unknown does not mean false, and session knowledge is discarded when the CLI exits. See [the LCE grammar](docs/language.md) and [the first reasoning slice](docs/reasoning.md).

Use `:help`, `:status`, and `:quit` inside the console.

## Run

Requires the .NET 10 SDK. Build outputs are centralized under the repository's `artifacts/` directory instead of being scattered into project-level `bin/` and `obj/` directories.

```powershell
dotnet run --project src/Lattice.Cli
dotnet test Lattice.slnx --configuration Release
```

The test runner is Microsoft Testing Platform, selected in [global.json](global.json) for the .NET 10 `dotnet test` experience.

## Development principles

- Build small, runnable increments.
- Keep the intelligence independent of individual tool implementations.
- Prefer one cohesive Core project and a simple CLI until a real technical boundary justifies more projects.
- Treat external information as sourced observations before considering it for durable knowledge.
- Distinguish unknown from false, and retain provenance for conclusions.
- Preserve useful ideas from the previous Lattice repository only after reviewing their fit; this repository is a clean implementation.

## Status

The repository has a runnable console shell, a controlled-language parser, a language-independent semantic representation, and a first in-memory symbolic reasoning engine. Persistent conversation context, knowledge-pack loading, and tool execution are not implemented yet.

See [AGENTS.md](AGENTS.md), [the architecture notes](docs/architecture.md), [the language design](docs/language.md), [the reasoning boundary](docs/reasoning.md), and [the development queue](.agents/todo.md) for the current constraints and next steps.
