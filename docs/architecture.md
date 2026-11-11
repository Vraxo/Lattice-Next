# Architecture direction

## Purpose

Lattice is a symbolic conversational intelligence that can reason with or without external capabilities. Programming is an intended early specialty, not the identity of the system.

The architecture must preserve a strict distinction between **intelligence**, **knowledge**, and **capabilities**.

## Conceptual responsibilities

### Language

Turns user-facing language into structured meaning and renders conclusions back into language. Lattice Controlled English (LCE) 0.9 is the first deliberately small controlled-language subset. It is inspired by ACE's approach but is not full ACE; the exact grammar and decision are recorded in [language.md](language.md).

Language is an interface to meaning. It must not own reasoning or tool execution.

### Semantics

Provides the canonical, language-independent representation of meaning. The same semantic representation should be usable whether input came from LCE, a future natural-language interpreter, a knowledge pack, a user clarification, or a tool observation.

The initial model represents terms, unresolved references, predicate applications (including n-ary relations), implications, universal quantification, and assertion/question intent. It is intentionally small and will expand only when a concrete reasoning slice requires more expressive power.

### Intelligence

Interprets goals, uses context and knowledge, performs inference, decides whether to answer, ask, reason further, or invoke a capability, and reassesses after new information arrives.

It must remain useful when no tools are registered. It must not depend on a particular parser, knowledge pack, tool, programming language, or user interface.

### Context

Maintains the active conversation and task state. The first implementation tracks explicitly mentioned individuals from unary and relational assertions or questions, and conservatively resolves the singular "it" reference when exactly one individual is known. Missing or ambiguous references produce a clarification request instead of a guess. Multi-sentence input is parsed completely first, then its utterances are processed in source order so earlier accepted statements can inform later references in the same turn. The exact policy and boundaries are recorded in [context.md](context.md).

Keep user statements, accepted premises, derived conclusions, questions, and external observations distinguishable. Stored facts and rules retain structured source records classified as user assertions, imported claims, or external observations. Duplicate semantic claims are deduplicated while independent sources are retained. Proof nodes distinguish asserted premises from derived conclusions and preserve the sources and premises that support them. This is the initial provenance foundation; retraction and recomputation when support changes remain future work.

Session context is not automatically durable knowledge. It also retains the latest successful tool observation as temporary conversational evidence. Numeric follow-ups may refer to that observation, but it is never inserted into the knowledge base or passed to the symbolic reasoning engine as a premise.

### Knowledge

Knowledge packs are versioned, declarative data. They may describe concepts, facts, relations, rules, and examples. They do not contain executable plugin code and should not require a special project for each subject domain.

The initial version 1 format is JSON metadata plus ordered LCE assertion strings. Core validates the entire pack against the current parser and reasoning boundary before adding any claim. This deliberately reuses the proven language-to-semantics path instead of prematurely serializing internal semantic records. Imported claims retain both their imported-claim classification and the pack ID as a structured source identifier. The format should evolve with demonstrated semantic needs, not dictate them.

### Tools

Tools are optional capabilities implementing Core's generic `ITool` contract. Core owns only the descriptor, primitive argument model, permission flags, invocation result, and registry. It does not discover executables, launch processes, parse manifests, or know any implementation language.

The first external provider is a separate `Lattice.Tooling.Stdio` assembly that references Core. It discovers one-level tool directories under a configured root. Each tool directory contains a `tool.json` manifest describing its identity, inputs, permissions, executable command, and fixed process arguments. Discovery parses and validates metadata but never starts a process. The REPL uses `./tools` by default; `LATTICE_TOOLS_DIR` can select another directory.

Protocol version 1 uses one JSON-lines request and one JSON-lines response per process invocation. Lattice starts the configured executable without a shell, sets the tool directory as its working directory, writes one request to standard input, and reads one response from standard output. Standard error is kept separate. The request carries a protocol version, correlation ID, operation, and validated arguments. The response must echo the version and correlation ID and return either a JSON result or an error. The provider does not interpret the result's domain meaning; it returns the JSON value as a sourced `ToolObservation`.

The process command can be an executable found through the host PATH, an absolute executable path, or a relative path contained within that tool's directory. Arguments are passed separately without shell interpolation. The configured tools directory is a trust boundary: discovering a manifest does not execute it, but invoking a discovered tool runs the command it declares with the current user's OS permissions. Process isolation is not a security sandbox, and descriptor permissions do not restrict the child's OS-level access. The REPL grants no tool permissions by default; the coarse session-wide `LATTICE_TOOL_PERMISSIONS` setting can grant `read`, `write`, or both. Execution timeouts and process termination remain enforced by Lattice's host and registry.

The first web-retrieval capability is an optional external `web-search` tool, not a Core service. It uses the DDGS metasearch library to query public search providers with automatic backend selection; users do not need to register with a search provider, obtain an API key, or run a separate search service. The tool requires the session-wide `read` permission and one-time installation of its pinned Python dependency. Each result includes its title, destination URL, domain, and snippet alongside the retrieval timestamp. Because this approach queries public search pages rather than guaranteed official APIs, providers may rate-limit or temporarily block requests. This first slice returns search results only: it does not fetch arbitrary result pages, make claims part of the knowledge base, or pass observations to the reasoning engine.

The REPL composition root discovers tool manifests and registers the resulting external capabilities; it contains no compiled-in calculator or unit-converter implementation. The repository's calculator and unit converter are examples under `tools/`, implemented as independent Python processes using the same generic manifest and stdio protocol as other tools. Python is not a Core or protocol dependency. A tool manifest may optionally declare intent patterns, argument bindings, and a response template. The generic matcher compiles these constrained declarations into recognizers at startup; it has no knowledge of calculator IDs, unit-converter IDs, or individual argument schemas. Patterns are case-insensitive exact token sequences with single-token `number`/`word` slots, multiword `text` captures, and square-bracketed optional literal phrases, rather than arbitrary regular expressions or executable code. Bindings can refer to a captured slot, a primitive constant, or the latest numeric observation. Response templates can refer to declared tool arguments and the returned result. A tool can therefore add or change its supported phrases alongside its implementation without changing or recompiling C# code. This is deliberately constrained language matching, not unrestricted English understanding or an LLM. The REPL invokes the selected capability through the same registry used by explicit `:tool` diagnostics and renders the sourced result conversationally. A successful observation is retained as the latest conversation-context observation so a numeric follow-up such as `Multiply that by 5.` can resolve it. A narrowly parsed chain of at most eight operations may schedule further invocations, with each step receiving only the preceding successful numeric observation; the host executes the chain sequentially, caps execution at eight invocations, and stops on failure. Observations are not inserted into the knowledge base or used as reasoning premises.

Adding, removing, or replacing a provider must not require changes to cognition. Future providers may use HTTP, MCP, or another transport while exposing the same generic contract. Those transports are adapters, not foundations of the intelligence.

### Application client contract and runtime

`Lattice.Client` defines the transport-independent public boundary shared by frontends: `ILatticeClient` creates isolated `ILatticeSession` instances, and each session accepts turns and returns structured outcomes, truth values, and provenance-bearing evidence. The contract project has no dependency on Core, providers, transports, or user interfaces.

`Lattice.Runtime` is the first in-process implementation. It composes the parser, conversation context, knowledge base, and reasoning engine behind the client/session contracts. Each session owns isolated state and serializes its own turn processing. This is the runtime boundary future hosts will expose; it is not an HTTP server and does not yet include tool execution, translation, or persistence through the client contract. Sessions expose validated knowledge-pack loading and value-only status snapshots.

The REPL now consumes this contract through an in-process client for ordinary turns, accepted translation candidates, and knowledge-pack loading. Its existing tool provider remains a host concern for now. The genuine CLI, GUI, and remote clients should consume this same contract. Transport-specific clients and server adapters belong outside both the contract and Core.

### REPL

The REPL is one frontend. It gathers interactive input and presents responses, but it is not a privileged path into Lattice's application behavior. As additional frontends or a hosted runtime are introduced, they must use the same public client contract. The REPL may use an in-process client when it hosts the runtime itself or a remote client when connecting to another host.

## Dependency direction

The dependency direction keeps providers outside Core:

Frontends depend on `Lattice.Client`. The in-process runtime implements that contract and composes Core with optional providers. Future transport adapters implement the same client contract; neither frontends nor transports call around it.

Core must not depend on the client contract, runtime, REPL, or any provider. External-process launch, stdio transport, and manifest parsing live in the provider assembly. Tool implementations are called through Core contracts, not concrete references from the reasoning engine. Knowledge packs are data loaded by Core, not separate compiled projects.

## Language strategy

LCE 0.9 is the first native controlled-language subset. It exists to establish a tested boundary between language and semantics, not to claim that Lattice already understands English. Full ACE and APE remain candidates for a later language expansion if the benefit justifies their broader grammar and runtime/integration requirements.

Core defines the `ISemanticsTranslator` contract, while concrete translator adapters remain optional implementations outside Core. A translator receives the original input and returns candidate utterance kinds and semantic formulas; it does not return authoritative source records. `SemanticTranslationService` validates every candidate with `SemanticUtteranceValidator` and attaches the exact original input as its `SourceText`. One valid candidate is returned as ready for review; multiple candidates remain ambiguous; no candidate is an explicit no-interpretation result. If any candidate is malformed or outside the supported semantic subset, the complete response is rejected.

This is a structural boundary, not a truth or semantic-faithfulness oracle. Validation cannot establish that a translator understood the user's wording correctly. Translation candidates are never added to the knowledge base, resolved against conversation context, used to make decisions, or sent to tools by this service. A caller must review a candidate and then pass it through the ordinary context and knowledge paths. The default REPL continues to use LCE, and no translator backend or model is selected or required.

## Evidence and uncertainty

- Separate asserted information from inferred conclusions and observations.
- Retain provenance for external information and derived conclusions.
- Distinguish true, false, both/contradictory, and unknown where the chosen logic supports those states.
- Absence of a fact is not evidence of its negation.
- Search results and fetched pages are temporary observations by default, not automatic edits to durable knowledge packs.

## Development approach

Build the smallest runnable implementation that demonstrates one real behavior at a time. Each PR should have a clear behavioral result, tests, and an honest verification report. Avoid implementing future layers merely to complete a diagram.
