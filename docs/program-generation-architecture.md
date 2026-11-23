# Program generation architecture

> **Historical migration checkpoint (2026-11-23):** the 28 previously tracked legacy Python dispatch branches were removed and merged. That count covered only those tracked C# dispatch branches; it did **not** complete the Python code-generation migration. Python-specific validation helpers, task-specific request/fact rules, and complete program outputs remain in the current implementation. Finishing that replacement is the active priority. The broader coding-capability and generalization roadmap is deferred until the active queue's migration completion gate is met.

This document is the authoritative direction for the program-generation track. When implementation details or older design notes conflict with it, follow this document and the current code/tests.

## The destination

Lattice should have one general-purpose, output-format-agnostic sequence-generation mechanism. It should be able to generate a program by combining what it knows about the requested meaning, the relevant concepts and operations, and the target language's syntax.

The runtime must not know that it is generating Python. Python syntax and semantics needed for generation belong in declarative knowledge. The same generic machinery should eventually be reusable for other languages and for non-code sequences.

The goal is not to replace a growing list of C# special cases with a growing list of prompt-specific JSON templates. The system must generalize by composing reusable facts, constraints, relationships, and generation rules.

## Intended processing model

A request may express multiple requirements at once. They may be independent, ordered, or dependent on one another. Preserve them as semantic facts and constraints rather than collapsing the request into one task category or handler.

The conceptual work is to interpret the request, validate its meaning, reason over reusable knowledge, realize a program, and validate the result. The concrete single-path decision below assigns those responsibilities without requiring a separate framework for every stage.

## Chosen single code-generation path (M-02 decision)

This is the **target production path**, not a description of the current runtime. The as-built routes and their callers are recorded in the [M-01 inventory](code-generation-migration-inventory.md); they remain to be migrated in later queue items.

1. **Interpret one code request.** The language/request layer preserves the user's intent, values, names, collections, constraints, dependencies, and requested target language. The runtime dispatches it through the existing [`TaskCodeGenerationResponder`](../src/Lattice.Runtime/TaskCodeGenerationResponder.cs), generalized for code requests rather than a new router. There is no raw-input first-refusal emitter and no domain-specific fallback route.
2. **Form language-neutral program goals.** Convert the interpreted request into one or more semantic requirements that do not contain target-language tokens or complete source fragments. Keep values and requirements distinct, and retain ordering/dependency information. An unknown, contradictory, or unsupported requirement stays unresolved or is rejected; it is not guessed from a benchmark prompt.
3. **Plan with reusable programming/domain knowledge.** Reason over reusable concepts and operations, their typed inputs/results, effects, prerequisites, constraints, and relevant domain/API facts to select a realizable operation plan. This semantic planning is independent of the chosen syntax. Knowledge describes reusable operations, not a prompt-specific sequence of solution steps.
4. **Select the requested language pack.** Resolve the single requested language identifier with the [`GenerationLanguagePackLoader`](../src/Lattice.Core/Generation/GenerationLanguagePackLoader.cs) to a [`GenerationLanguagePack`](../src/Lattice.Core/Generation/GenerationLanguagePack.cs). The pack supplies syntax productions, surface symbols, bindings/encodings, and the realization available for each supported semantic operation. It does not determine what the request means. An unsupported language or unrealizable operation fails explicitly rather than falling back to another generator.
5. **Realize the plan through [`SequenceGenerationPipeline`](../src/Lattice.Core/Generation/SequenceGenerationPipeline.cs).** Adapt each planned operation and its request-derived values to the existing [`GenerationGoal`](../src/Lattice.Core/Generation/GenerationGoal.cs) boundary. The pipeline's [`GenerationOperationSelector`](../src/Lattice.Core/Generation/GenerationOperationSelector.cs) checks the selected pack's realization against the goal and available prerequisites, resolves values through the pack's [`GenerationBinding`](../src/Lattice.Core/Generation/GenerationBinding.cs) declarations, converts its [`GenerationProduction`](../src/Lattice.Core/Generation/GenerationProduction.cs) declarations into a [`SequenceGrammar`](../src/Lattice.Core/Generation/SequenceGenerator.cs), and calls the shared [`SequenceGenerator`](../src/Lattice.Core/Generation/SequenceGenerator.cs). `TryGenerateProgram` combines the resulting ordered statement sequences. The pipeline expands syntax; it does not parse the request, infer program semantics, or decide dependencies.
6. **Validate and return.** Apply generic structural and semantic checks to the plan and bindings; use the selected pack's grammar/knowledge for language-specific syntax. Return generated source only when the supported checks succeed. Add behavioral execution checks for supported cases where practical.

**Single-emitter decision:** [`SequenceGenerationPipeline`](../src/Lattice.Core/Generation/SequenceGenerationPipeline.cs) backed by [`SequenceGenerator`](../src/Lattice.Core/Generation/SequenceGenerator.cs) is the one code-emission path. [`KnowledgeDrivenSequenceGenerator`](../src/Lattice.Core/Generation/KnowledgeDrivenSequenceGenerator.cs) is to be retired as a program generator; it will not remain as an alternate or “semantic-only” code-generation mechanism. It consumes sequence rules and emits text, so retaining it under that label would preserve the parallel engine. Any reusable input extraction or structural validation may survive only as separate, language-neutral semantic components; it must not emit source or own Python/task-specific solutions. [`OperationSequenceGenerator`](../src/Lattice.Core/Generation/OperationSequenceGenerator.cs) is a test-only overlapping prototype with no production caller; it is not selected or to be promoted as another path.

**Contract boundary and limits:** the existing [`GenerationGoal`](../src/Lattice.Core/Generation/GenerationGoal.cs) is a useful realization request, not the complete language-neutral program model. Its effect, argument kind, and value are strings; it does not represent typed statements/expressions, complete operation signatures, or dependencies between goals. `TryGenerateProgram` accepts a preordered list and generates each goal in turn; it does not plan that order. [`GenerationOperation`](../src/Lattice.Core/Generation/GenerationOperation.cs) and [`GenerationLanguagePack`](../src/Lattice.Core/Generation/GenerationLanguagePack.cs) are reusable starting points, but the current pack schema co-locates operation-selection metadata with language realization. Preserve that limitation explicitly: later semantic-model work may refine the boundary, but should extend these contracts only as needed to keep operation meaning language-independent. The present decision does not claim that the current contracts already provide composition or a sufficient programming ontology.

## Generic generation mechanism

The selected pack's declarative productions define legal surface sequences. The shared `SequenceGenerator` expands nonterminals and resolves already-prepared bindings under deterministic search limits. It does not filter grammar candidates against request facts, advance semantic collection cursors, interpret effects, or plan operations. Those concerns belong before realization in request interpretation, generic validation, and semantic planning. Keep the sequence expander language- and domain-agnostic; add no second generator for a new programming construct.

## Where knowledge belongs

- **Language pack:** token spellings, grammar and syntax constraints, language-specific literal/identifier encoding, and mappings from supported semantic operations to surface productions.
- **Programming/domain knowledge:** reusable operation meanings, typed inputs/results, effects, prerequisites, constraints, concepts, and actual API names/signatures.
- **Request-derived semantic facts:** the values, requirements, entities, target-language choice, and dependencies expressed by this request.
- **Generic runtime:** interpretation infrastructure, semantic representation and validation, operation reasoning/planning, pack selection, binding, and the shared grammar expander. It contains no Python-specific parser, validator, or generator branch.

Knowledge may describe reusable constructions and their constraints. It must not store complete programs for individual prompts, benchmark cases, or task IDs. A language-specific knowledge file is acceptable; a language-specific runtime implementation is not.

## Non-negotiable constraints

- **No Python-specific runtime intelligence.** No Python-specific parser dispatch, task parser, generator, code-fragment assembler, or target-language branch in generic C# runtime code.
- **No task-keyed routing.** Do not map request categories, prompt patterns, benchmark IDs, or operation-family names to dedicated handlers.
- **No hard-coded benchmark solutions.** Benchmarks are tests of generalization, not a source of special cases.
- **No ML-based generation.** The intended system is explicitly symbolic and deterministic/explainable. Do not introduce neural models, corpus training, n-gram prediction, or statistical next-token prediction as a substitute for semantic reasoning.
- **No configuration-shaped overfitting.** Moving each special case into its own declarative template is not sufficient. Rules must express reusable concepts and compose across requirements and wording variations.
- **Keep requirements compositional.** Multiple requirements must remain simultaneously represented; do not reduce them to a single effect/argument pair or one output template.
- **Keep syntax in knowledge.** The generic runtime must not interpret target-language keywords or APIs.
- **Preserve uncertainty and safety.** Reject unsupported or contradictory inputs rather than guessing. Unknown information must remain unknown.
- **Prove behavior.** Add focused extraction, validation, generation, rejection, and end-to-end execution tests as appropriate. A plausible-looking string alone is not evidence of correctness.

## Migration checkpoint

The tracked cleanup covered 28 legacy Python dispatch branches. As of **2026-11-23**, all 28 of those tracked branches had been removed through small, independently reviewed increments. This is a historical count for that specific list, not a measure of all Python-specific code-generation logic. The remaining migration is substantial and is tracked separately in [`.agents/todo.md`](../.agents/todo.md).

The final tracked increment is PR [#541 — Replace Python-specific program request parsing with semantic facts](https://github.com/Vraxo/Lattice-Next/pull/541). It removed `PythonProgramRequestParser` and replaced the English-syntax-specific request gate with generic program-intent and `language.*` fact extraction, followed by declarative generation validation. Its full CI suite passed, including the .NET tests and external Python-tool tests, and it was merged to `main`.

The preceding increment, PR [#537 — Replace specialized function composition with declarative generation](https://github.com/Vraxo/Lattice-Next/pull/537), removed the specialized multi-function composition parser and replaced it with generic repeated semantic capture, aligned collections, ordered-scope validation, and declarative generation. Its full CI suite passed, including end-to-end Python execution, and it was merged to `main`.

PR [#540 — Generate Blender scripts through semantic goals and language grammar](https://github.com/Vraxo/Lattice-Next/pull/540) also removed the task-specific C# source-fragment assembler for the Blender example. It moved the example to ordered semantic generation steps, named bindings, and the shared Python grammar. This was a code-generation architecture improvement, not one of the 28 tracked parser-branch removals.

PR [#533 — Generate multi-conditional functions declaratively](https://github.com/Vraxo/Lattice-Next/pull/533) removed the specialized multi-conditional function parser and replaced it with aligned semantic collections and compositional token generation for repeatable `if`/`elif` branches.

Earlier knowledge-driven improvements also expanded benchmark coverage without changing the tracked C# dispatch count:

- [PR #522](https://github.com/Vraxo/Lattice-Next/pull/522) recognizes natural-language greeting-function requests through existing declarative knowledge.
- [PR #524](https://github.com/Vraxo/Lattice-Next/pull/524) replaced a fixed product-price dictionary program with captured key/value facts and reusable iteration rules; [PR #525](https://github.com/Vraxo/Lattice-Next/pull/525) made the generated example self-contained without inventing dictionary entries.
- [PR #526](https://github.com/Vraxo/Lattice-Next/pull/526) connected integer-conversion requests to their output semantics.
- [PR #528](https://github.com/Vraxo/Lattice-Next/pull/528) replaced the complete bank-account class template with a class model and staged token-binding rules.

The latest [Python benchmark run](https://github.com/Vraxo/Lattice-Next/actions/runs/45059564042) generated **16/16** programs, validated **16/16** syntactically, and smoke-executed **16/16** successfully. This remains generation/syntax/execution evidence only; semantic correctness and generalization are not yet scored. The bank-account domain mapping itself is still task-specific, even though its code is now composed from semantic facts.

For future program-generation work:

1. Begin with a concrete task or a measured failure in the current benchmark; do not invent another migration target to keep the old branch count moving.
2. Inspect the current `main` branch and identify the smallest reusable semantic or generation capability that is missing.
3. Extend the shared mechanism or declarative knowledge without introducing target-language dispatch, task-keyed routing, or stored benchmark solutions.
4. Add tests for ordinary cases, changed values, paraphrases, invalid/incomplete inputs, and end-to-end behavior as applicable.
5. Validate generated behavior, not just plausible text or syntax; keep semantic correctness and generalization separate from generation/execution counts.
6. Open a small PR, enable GitHub auto-merge, and wait for required CI checks. Never bypass checks.
7. Verify the merge and resulting `main` contents before choosing the next increment.

## Definition of success

The migration is not complete merely because all 28 tracked branches are gone. The resulting system must also demonstrate that:

- new programming constructs can be added through knowledge and existing generic mechanisms, without target-specific C# dispatch or generation code;
- requests with multiple requirements can be represented and composed rather than routed to one task handler;
- paraphrases and changed values work without benchmark-specific rules;
- invalid, incomplete, contradictory, or unsupported requests are rejected or left unresolved safely;
- generated programs are syntactically valid and execute correctly for the supported behavioral cases; and
- the same runtime architecture is not inherently tied to Python.

The 28-branch count is not the migration completion gate. Finish the active queue first: remove remaining Python-specific shared-code logic, replace task-specific request facts and full-program sequence templates with reusable semantic structures and operations, unify the production generation path, and verify that Python is supplied as declarative language knowledge rather than embedded in shared C#. Do not start broader ontology expansion or new generalization work before that gate is met.
