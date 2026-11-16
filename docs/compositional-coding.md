# Compositional Coding Track

## Objective

Measure whether Lattice can understand programming concepts, construct programs compositionally, and combine independently learned Python and Blender knowledge on tasks for which no task-specific solution has been supplied.

The coding track is separate from the general assistant benchmark. Its initial benchmark is `data/benchmarks/coding-benchmark-v1.json` (30 prompts: 8 Python fundamentals, 8 compositional Python, 6 Blender foundations, and 8 unseen cross-domain holdouts).

## Architectural principle: one sequence generator

Lattice should have one general-purpose mechanism for generating output as a sequence of symbols. It must not switch between an English renderer, a Python renderer, a Blender generator, or task-specific response templates.

The generator itself is output-format agnostic:

- It expands grammar symbols into terminal tokens.
- It resolves semantic binding slots from the supplied generation context.
- It expands nonterminal grammar symbols to define the currently permissible continuations.
- It scores every emitted terminal or semantic binding token with the same optional context-sensitive token model.
- It emits the resulting sequence without interpreting it as Python, English, Markdown, or any other particular format.

Language and domain knowledge can describe vocabulary, grammar, syntax, and preferred constructions. A generic token model can provide continuation statistics for any kind of output. Both are data consumed by the same engine; neither becomes a specialized rendering implementation. Whitespace and punctuation are ordinary emitted surface tokens.

The initial implementation combines bounded deterministic beam search with an optional smoothed n-gram continuation model:

- **Grammar-constrained generation:** weighted productions restrict possible sequences; the token model scores each emitted token using the longest available preceding-token context, backing off to shorter contexts when necessary.
- **Grammar-free continuation:** the same generator can continue a supplied token context directly from the token model, using beam search over observed next-token candidates and a learned end-of-sequence marker.
- **Corpus training:** token observations can be built from arbitrary token sequences. The training API does not know whether those sequences represent English, Python, Markdown, or another format.
- **Universal tokenization:** one Unicode subword tokenizer learns pair merges from mixed text/code documents. It starts from Unicode scalar values, preserves whitespace and punctuation, and reconstructs the original input exactly. The same tokenizer is used regardless of output format.

The score is format-agnostic and is applied equally to natural-language words, code symbols, whitespace, and punctuation. The token model learns local continuation frequencies, not meaning. It cannot independently decide what a user is asking, plan an answer, or reliably produce unseen long-range structures. It is a small non-neural baseline, **not** an LLM-equivalent model or a claim of open-ended language ability.

The tokenizer and training APIs are currently in-process foundations; there is not yet a curated large-corpus ingest/persistence workflow, semantic conditioning of next-token choices, a learned semantic content planner, robust morphology, or runtime integration. They do not yet replace the current runtime responders. Those are subsequent milestones.

## Repository audit: current code-generation path

The existing Blender object-replacement example is not general code generation:

- `knowledge/domains/blender-object-replacement-script.json` stores a task entity, ordered task steps, operation IDs, and Blender API labels.
- `TaskRequestResponder.TryGenerateCode` accepts only the exact combination `python-script` + `blender` + `blender-object-replacement` + `object-to-point-light`.
- `TaskRequestParser.TryParseBlenderScript` recognizes one Blender-script request shape and only classifies the task as supported when its text contains specific replacement/object/name/point-light terms.
- The responder contains a dictionary mapping operation IDs directly to complete Python source fragments.
- It also hardcodes imports, variable setup, explanatory prose, and the final script layout.

This remains a useful baseline for measuring the limitation, but it is not the target architecture and must not be used to score compositional generalization.

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
- Add context-sensitive continuation scoring and structural constraints without adding language-specific generation engines.
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
