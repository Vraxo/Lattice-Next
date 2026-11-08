# Architecture direction

## Purpose

Lattice is a symbolic conversational intelligence that can reason with or without external capabilities. Programming is an intended early specialty, not the identity of the system.

The architecture must preserve a strict distinction between **intelligence**, **knowledge**, and **capabilities**.

## Conceptual responsibilities

### Language

Turns user-facing language into structured meaning and renders conclusions back into language. ACE or an explicitly named ACE-inspired controlled language is an initial candidate, not an assumption that all English is already understood.

Language is an interface to meaning. It must not own reasoning or tool execution.

### Semantics

Provides the canonical, language-independent representation of meaning. The same semantic representation should be usable whether input came from ACE, a future natural-language interpreter, a knowledge pack, a user clarification, or a tool observation.

The exact representation is intentionally not frozen by this scaffold. It must be selected through a small, testable design step rather than an extensive speculative ontology.

### Intelligence

Interprets goals, uses context and knowledge, performs inference, decides whether to answer, ask, reason further, or invoke a capability, and reassesses after new information arrives.

It must remain useful when no tools are registered. It must not depend on a particular parser, knowledge pack, tool, programming language, or user interface.

### Context

Maintains the active conversation and task state. Keep user statements, accepted premises, derived conclusions, questions, and external observations distinguishable. Preserve provenance so Lattice can explain why it believes something and retract or revise conclusions when their support changes.

Session context is not automatically durable knowledge.

### Knowledge

Knowledge packs are versioned, declarative data. They may describe concepts, facts, relations, rules, and examples. They do not contain executable plugin code and should not require a special project for each subject domain.

The pack format should follow the semantic model rather than forcing the model to fit an early JSON schema. Packs and individual claims need source/provenance information where applicable.

### Tools

Tools are optional capabilities. They live together under the Core `Tools/` area and implement a common, explicit contract describing purpose, inputs, output, and effects/permissions.

Generic infrastructure handles discovery, validation, authorization, invocation, limits, and recording. Reasoning must work from capability descriptions and results; it must not contain tool-specific branches.

A tool result first enters context as a sourced observation. The intelligence decides what it means and whether it supports a conclusion. Adding, removing, or replacing a tool must not require changing cognition.

External systems such as MCP servers are potential ways to supply tools, not foundations of the intelligence and not reasons to create a project per integration.

### CLI

The CLI is the initial user interface and composition root. It gathers input, presents responses, and wires Core services. It must not contain language rules, inference rules, tool-specific workflows, or domain knowledge.

## Dependency direction

The initial dependency direction is intentionally simple:

`Lattice.Cli -> Lattice.Core`

Core must not depend on the CLI. Tool implementations are called through Core contracts, not concrete references from the reasoning engine. Knowledge packs are data loaded by Core, not separate compiled projects.

## Language strategy

ACE is a candidate controlled-language front end because it provides constrained, explicit constructions that can be mapped to formal meaning. Before implementation, compare using the actual ACE grammar/parser with defining a smaller Lattice-controlled subset. Never label a derivative as full ACE.

The user should not be permanently required to write controlled language. A future optional translator may map ordinary language to the same validated semantic representation. Such a translator is replaceable and untrusted: it cannot add facts, make decisions, invoke tools, or bypass validation.

## Evidence and uncertainty

- Separate asserted information from inferred conclusions and observations.
- Retain provenance for external information and derived conclusions.
- Distinguish true, false, both/contradictory, and unknown where the chosen logic supports those states.
- Absence of a fact is not evidence of its negation.
- Search results and fetched pages are temporary observations by default, not automatic edits to durable knowledge.

## Development approach

Build the smallest runnable implementation that demonstrates one real behavior at a time. Each PR should have a clear behavioral result, tests, and an honest verification report. Avoid implementing future layers merely to complete a diagram.
