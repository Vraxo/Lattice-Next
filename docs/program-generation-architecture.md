# Program generation architecture

> **Current migration objective:** remove the remaining specialized Python parsing and code-generation branches. Replace them with generic semantic extraction, validation, symbolic reasoning, and declarative token-sequence generation. This is an architectural migration, not a project to move hard-coded templates from C# into JSON.

This document is the authoritative direction for the program-generation track. When implementation details or older design notes conflict with it, follow this document and the current code/tests.

## The destination

Lattice should have one general-purpose, output-format-agnostic sequence-generation mechanism. It should be able to generate a program by combining what it knows about the requested meaning, the relevant concepts and operations, and the target language's syntax.

The runtime must not know that it is generating Python. Python syntax and semantics needed for generation belong in declarative knowledge. The same generic machinery should eventually be reusable for other languages and for non-code sequences.

The goal is not to replace a growing list of C# special cases with a growing list of prompt-specific JSON templates. The system must generalize by composing reusable facts, constraints, relationships, and generation rules.

## Intended processing model

A request may express multiple requirements at once. They may be independent, ordered, or dependent on one another. Preserve them as semantic facts and constraints rather than collapsing the request into one task category or handler.

The intended path is:

1. **Interpret the request:** extract its meaning into reusable semantic facts, values, entities, relationships, and constraints.
2. **Validate meaning:** check types, relationships, dependencies, compatibility, and other semantic constraints using generic validation mechanisms.
3. **Reason over knowledge:** combine the request's requirements with domain concepts, operation knowledge, and language knowledge. Keep all simultaneously relevant requirements active.
4. **Generate a sequence:** use the shared generator to consider valid next-token candidates under the active facts, constraints, and already-emitted sequence.
5. **Validate the result:** test structural/syntactic validity and, where practical, execute generated programs against behavioral expectations.

This is a conceptual pipeline, not a mandate to introduce a separate framework or layer for every stage. Keep the implementation cohesive and add abstractions only when they provide a real reusable capability.

## Generic generation mechanism

At each generation step, the engine should:

1. Retrieve declarative next-token candidates applicable to the current sequence context.
2. Filter and reason over candidates using active semantic facts, completed requirements, forbidden conditions, and other generic constraints.
3. Select a continuation using the generator's deterministic search strategy.
4. Update the sequence and semantic state, including any facts satisfied or collection positions advanced by the emitted token.
5. Continue until the required meaning has been expressed and generation is complete.

The generator must not contain Python keywords, syntax rules, APIs, or task-specific construction logic. It operates on generic facts, token contexts, bindings, constraints, and candidate rules. Language and domain knowledge supply the meaning of those data.

The current implementation already has a declarative next-token knowledge base, semantic fact bindings, collection cursors, input-fact extraction, generic validation, and bounded token-sequence search. Extend these primitives when a genuinely reusable capability is missing; do not add a dedicated generator for the next construct.

## Where knowledge belongs

- **Language knowledge:** token spellings, syntax, legal continuations, grammar relationships, expression forms, and language-specific conventions.
- **Domain knowledge:** concepts, operations, effects, API names/signatures, and domain relationships.
- **Request-derived semantic facts:** the actual values, requirements, entities, and dependencies expressed by the user.
- **Generic runtime:** extraction infrastructure, semantic representation, validation machinery, constraint evaluation, sequence search, token binding, and state transitions.

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

## Migration work in progress

The current cleanup is tracked as 28 legacy Python dispatch branches. As of **2026-11-23**, **27 of 28 (96.4%)** have been removed through small, independently reviewed increments; **1 remains**. This is a count of tracked legacy branches removed, not a claim that the overall architecture is 96.4% complete.

The latest completed increment is PR [#537 — Replace specialized function composition with declarative generation](https://github.com/Vraxo/Lattice-Next/pull/537). It removed the specialized multi-function composition parser and replaced it with generic repeated semantic capture, aligned collections, ordered-scope validation, and declarative generation. Its full CI suite passed, including end-to-end Python execution, and it was merged to `main`.

The preceding increment, PR [#533 — Generate multi-conditional functions declaratively](https://github.com/Vraxo/Lattice-Next/pull/533), removed the specialized multi-conditional function parser and replaced it with aligned semantic collections and compositional token generation for repeatable `if`/`elif` branches.

Earlier knowledge-driven improvements also expanded benchmark coverage without changing the tracked C# dispatch count:

- [PR #522](https://github.com/Vraxo/Lattice-Next/pull/522) recognizes natural-language greeting-function requests through existing declarative knowledge.
- [PR #524](https://github.com/Vraxo/Lattice-Next/pull/524) replaced a fixed product-price dictionary program with captured key/value facts and reusable iteration rules; [PR #525](https://github.com/Vraxo/Lattice-Next/pull/525) made the generated example self-contained without inventing dictionary entries.
- [PR #526](https://github.com/Vraxo/Lattice-Next/pull/526) connected integer-conversion requests to their output semantics.
- [PR #528](https://github.com/Vraxo/Lattice-Next/pull/528) replaced the complete bank-account class template with a class model and staged token-binding rules.

The latest [Python benchmark run](https://github.com/Vraxo/Lattice-Next/actions/runs/45059564042) generated **16/16** programs, validated **16/16** syntactically, and smoke-executed **16/16** successfully. This remains generation/syntax/execution evidence only; semantic correctness and generalization are not yet scored. The bank-account domain mapping itself is still task-specific, even though its code is now composed from semantic facts.

For every remaining increment:

1. Inspect the current `main` branch and identify an actual remaining legacy branch; do not rely on a stale candidate list.
2. Remove one focused branch and its dedicated parser/generator logic while preserving genuinely shared functionality.
3. Express the required input facts, validation, and output sequence declaratively, extending generic infrastructure only when necessary.
4. Add tests for ordinary cases, variations, invalid inputs, and end-to-end behavior.
5. Open a small PR, enable GitHub auto-merge, and wait for required CI checks. Never bypass checks.
6. Verify the merge and the resulting `main` contents before incrementing the progress count.
7. Stop at the merge boundary before starting the next increment.

## Definition of success

The migration is not complete merely because all 28 tracked branches are gone. The resulting system must also demonstrate that:

- new programming constructs can be added through knowledge and existing generic mechanisms, without target-specific C# dispatch or generation code;
- requests with multiple requirements can be represented and composed rather than routed to one task handler;
- paraphrases and changed values work without benchmark-specific rules;
- invalid, incomplete, contradictory, or unsupported requests are rejected or left unresolved safely;
- generated programs are syntactically valid and execute correctly for the supported behavioral cases; and
- the same runtime architecture is not inherently tied to Python.

After the 28-branch cleanup, audit the remaining runtime for hidden target-language assumptions and evaluate compositional generalization independently. Treat branch removal as a migration checkpoint, not the final quality metric.
