# Architecture direction

## Purpose

Lattice is a symbolic conversational intelligence that can reason with or without external capabilities. Programming is an intended early specialty, not the identity of the system.

The architecture must preserve a strict distinction between **intelligence**, **knowledge**, and **capabilities**.

## Conceptual responsibilities

### Language

Turns user-facing language into structured meaning and renders conclusions back into language. Lattice Controlled English (LCE) 0.4 is the first deliberately small controlled-language subset. It is inspired by ACE's approach but is not full ACE; the exact grammar and decision are recorded in [language.md](language.md).

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

Session context is not automatically durable knowledge.

### Knowledge

Knowledge packs are versioned, declarative data. They may describe concepts, facts, relations, rules, and examples. They do not contain executable plugin code and should not require a special project for each subject domain.

The pack format should follow the semantic model rather than forcing the model to fit an early JSON schema. Packs and individual claims need source/provenance information where applicable.

### Tools

Tools are optional capabilities. They live together under the Core Tools/ area and implement a common, explicit contract describing purpose, inputs, output, and effects/permissions.

Generic infrastructure handles discovery, validation, authorization, invocation, limits, and recording. Reasoning must work from capability descriptions and results; it must not contain tool-specific branches.

A tool result first enters context as a sourced observation. The intelligence decides what it means and whether it supports a conclusion. Adding, removing, or replacing a tool must not require changing cognition.

External systems such as MCP servers are potential ways to supply tools, not foundations of the intelligence and not reasons to create a project per integration.

### CLI

The CLI is the initial user interface and composition root. It gathers input, presents responses, and wires Core services. It must not contain language rules, inference rules, tool-specific workflows, or domain knowledge.

## Dependency direction

The initial dependency direction is intentionally simple:

Lattice.Cli -> Lattice.Core

Core must not depend on the CLI. Tool implementations are called through Core contracts, not concrete references from the reasoning engine. Knowledge packs are data loaded by Core, not separate compiled projects.

## Language strategy

LCE 0.4 is the first native controlled-language subset. It exists to establish a tested boundary between language and semantics, not to claim that Lattice already understands English. Full ACE and APE remain candidates for a later language expansion if the benefit justifies their broader grammar and runtime/integration requirements.

A future optional translator may map ordinary language to the same validated semantic representation. Such a translator is replaceable and untrusted: it cannot add facts, make decisions, invoke tools, or bypass validation.

## Evidence and uncertainty

- Separate asserted information from inferred conclusions and observations.
- Retain provenance for external information and derived conclusions.
- Distinguish true, false, both/contradictory, and unknown where the chosen logic supports those states.
- Absence of a fact is not evidence of its negation.
- Search results and fetched pages are temporary observations by default, not automatic edits to durable knowledge packs.

## Development approach

Build the smallest runnable implementation that demonstrates one real behavior at a time. Each PR should have a clear behavioral result, tests, and an honest verification report. Avoid implementing future layers merely to complete a diagram.
