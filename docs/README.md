# Documentation

This directory contains the maintained product, architecture, language, reasoning, and evaluation documentation. The active implementation queue lives in [`.agents/todo.md`](../.agents/todo.md).

## Start here

- [Product vision](vision.md) — the goal of compounding, reusable capabilities.
- [Architecture](architecture.md) — boundaries between language, semantics, reasoning, knowledge, tools, and frontends.
- [Reasoning](reasoning.md) — the current symbolic reasoning and world-model capabilities, evidence rules, and known limits.
- [Language](language.md) — LCE 0.9 and the supported native-English grammar.

## Core concepts

- [Conversational context](context.md) — session context and conservative reference resolution.
- [Knowledge packs](knowledge-packs.md) — task data and the typed foundational semantic world model.
- [Objectives](objectives.md) — goals, constraints, candidate evaluation, evidence freshness, and plan summaries.
- [Primitive concept audit](primitive-audit.md) — which ontology concepts are currently defined or intentionally primitive.
- [Compositional program generation](program-generation-architecture.md) — the symbolic generation architecture and its migration checkpoint.
- [Code-generation migration inventory](code-generation-migration-inventory.md) — the M-01 as-built production paths, helpers, validation kinds, rules, and knowledge modules.

## Applications and integrations

- [Client contract and runtime](client-api.md) — the shared session API and in-process implementation.
- [HTTP server and remote client](server.md) — host setup, API routes, and remote REPL/CLI usage.
- [Non-interactive CLI](cli.md) — command-line input, output, and exit codes.
- [Optional LM Studio translator](lm-studio.md) — local model setup and candidate-review boundary.

## Benchmarks

- [First-use day-out benchmark](benchmarks/first-use-day-out.md) — the synthetic end-to-end task specification.
- [First-use completion checklist](benchmarks/first-use-completion-checklist.md) — implemented acceptance scope and limitations.
- [First-use baseline record](benchmarks/first-use-day-out-baseline.md) — the original baseline and concise implementation record.
- [Laptop comparison transfer task](benchmarks/laptop-candidate-comparison.md) — cross-domain transfer fixture.
- [Course-selection transfer task](benchmarks/third-task-course-selection.md) — a third synthetic transfer fixture.
- [Real-world assistant benchmark](benchmarks/real-world-assistant-benchmark.md) — source selection, curation, and evaluation protocol.

Benchmark fixtures are development evidence, not statistically representative evaluations. Synthetic scenarios do not establish live-world accuracy. See each benchmark's scope and limitations before interpreting its results.

## Historical design notes

[Archived proposals and superseded implementation notes](archive/) are retained for context only. They are not current architecture or implementation guidance.
