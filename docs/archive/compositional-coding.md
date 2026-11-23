> **Archived design note — 2026-11-23.** This document describes an earlier coding-track plan and benchmark scope. The maintained architecture and current migration checkpoint are in [program-generation-architecture.md](../program-generation-architecture.md); execution details are in the [Python coding benchmark runner](../../tools/Lattice.Coding.Benchmark/README.md).

---

# Compositional Coding Track

## Objective

Measure whether Lattice can understand programming concepts, construct programs compositionally, and combine independently learned Python and Blender knowledge on tasks for which no task-specific solution has been supplied.

The coding track is separate from the general assistant benchmark. Its initial benchmark is `data/benchmarks/coding-benchmark-v1.json` (30 prompts: 8 Python fundamentals, 8 compositional Python, 6 Blender foundations, and 8 unseen cross-domain holdouts).

## Architectural principle: one sequence generator

Lattice uses one output-format-agnostic sequence-generation mechanism. It expands grammar productions, resolves semantic binding slots, and searches for a valid sequence under bounded deterministic limits. The generator does not interpret Python, English, Markdown, or another output format; language knowledge supplies the productions and terminal tokens.

The current generation path is symbolic and deterministic:

- **Grammar-constrained generation:** declarative productions restrict valid continuations.
- **Semantic binding:** named values from a generation goal fill the binding slots required by the selected production.
- **Operation selection:** generic semantic effects, argument kinds, and prerequisites select an applicable language operation.
- **Sequence search:** bounded deterministic search emits the terminal sequence and rejects unresolved or unsupported bindings.

There is no neural model, corpus-trained n-gram model, or statistical next-token prediction in this path. The runtime does not use a separate Python or Blender generator.

The generic sequence engine and language-pack loader are reusable foundations, not evidence of open-ended programming ability. The current knowledge and task models remain limited; broader expression composition, API compatibility checking, semantic planning, and unseen-task generalization still need independent evaluation.

## Repository audit: current code-generation path

The Blender object-replacement example now uses the generic generation path:

- Script-creation requests are parsed into an application and a normalized task phrase, rather than routed by a Blender-specific parser branch.
- The semantic world model resolves the task phrase to a code-generation task and the application to its programming language.
- Ordered generation steps carry generic effects, argument kinds, and named semantic bindings.
- The runtime turns those steps into generic generation goals and uses the same `SequenceGenerationPipeline` and language-pack grammar used by other goals.
- Python statement syntax, indentation, and string-literal escaping live in the language pack; the responder no longer maps task operation IDs to complete source fragments.

This is a meaningful removal of the task-specific C# assembler, but it is not yet proof of compositional generalization. The Blender task still has a curated semantic step plan, and some expression values are represented as language-level expression bindings. The 30-prompt benchmark and independent holdouts must determine what the system can genuinely compose beyond this example.

## Design constraints

1. **One generation mechanism:** All outputs are emitted through the same sequence-generation algorithm.
2. **Knowledge is data:** English, Python, Blender, and other domains may contribute vocabulary, grammar, syntax, API facts, and semantic relationships. They do not get separate generator implementations.
3. **No task-keyed output:** No prompt, task ID, subject/scope tuple, or benchmark case may select a prewritten answer or code-fragment set.
4. **No mandatory AST pipeline:** A complete language-specific object tree is not a prerequisite for output. Internal constraints may guide generation, but the primary output is a sequence.
5. **Meaning guides content:** Semantic reasoning should determine which facts, goals, and constraints need to be expressed. The sequence generator decides how to realize them.
6. **Ground API calls:** Blender API knowledge supplies names, signatures, types, and effects. Unknown API details must remain unknown rather than be guessed.
7. **Validation:** Check generated sequences, syntax, API compatibility where practical, and execution in an isolated Blender process for integration cases.
8. **Provenance:** Retain evidence for semantic facts and API knowledge used to guide generation.
9. **Honest scoring:** A stored solution, task-specific branch, or unvalidated plausible script cannot earn compositional, generalization, or execution credit.

## Implementation sequence

### Milestone 1 — Benchmark and knowledge baseline

- Add the 30-prompt benchmark and explicit scoring rules.
- Record the current special-case generator as a known baseline limitation.
- Add an independent Python conceptual knowledge pack without complete scripts or benchmark answers.

### Milestone 2 — Universal sequence-generation core

- Add a reusable grammar/production representation made from terminal tokens, nonterminal symbols, and semantic binding slots.
- Load grammar productions from a strict, versioned JSON format; keep syntax and language distinctions in grammar knowledge, not generator branches.
- Add one bounded deterministic search algorithm that selects a continuation and emits terminal tokens.
- Prove that the same engine emits both a natural-language sentence and a Python statement.

### Milestone 3 — Generation knowledge and semantic grounding

- Represent language constructions and lexical choices as loadable knowledge rather than renderer code.
- Bind concepts, relations, values, and user-provided constraints to generation slots.
- Add reusable symbolic constraints and cross-step dependency reasoning without adding language-specific generation engines.
- Separate content selection from surface realization while avoiding task-specific answer templates.

The first semantic bridge resolves a graph relation assertion into generic `subject`, `relation`, and `object` bindings, using the shared tokenizer for all three values. A data-defined grammar can express the fact as a sentence without storing that sentence in the knowledge pack. This is direct assertion lexicalization only; multi-hop reasoning, morphology, discourse planning, and contextual lexical choice are not implemented yet.

### Milestone 4 — Runtime integration

- Route supported generation requests through semantic reasoning and the universal sequence generator.
- Retire task-specific code-fragment assembly only when the replacement can demonstrate equivalent or better behavior.
- Keep existing responders intact until their replacements are validated; do not silently degrade unrelated capabilities.

### Milestone 5 — Independent Blender knowledge and cross-domain composition

Model Blender's application concepts, scene/object/collection relationships, object data, transforms, materials, cameras, lights, and a curated subset of its Python API. Connect these facts to general Python knowledge through shared concepts such as modules, callables, values, attributes, and iteration. Do not create a Blender-Python generation backend.

### Milestone 6 — Holdout evaluation

Run the eight cross-domain prompts only after the foundations are frozen for that evaluation. Execute generated scripts in a disposable Blender background process with fixture scenes. Add paraphrases and altered fixture data to measure generalization.

## Scoring

Each prompt is scored independently on task understanding, decomposition, composition, syntax, API correctness, execution, and generalization. Each applicable dimension receives 0, 1, or 2. Report both per-stage scores and the weighted aggregate. Keep "answered" separate from "working program".

The holdout stage is the decisive measure: it must remain free of task-specific solution knowledge and special-case routing.
