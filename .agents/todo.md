# Development queue

This is the active queue for the clean-slate Lattice repository. Keep it short, ordered, and tied to runnable outcomes.

## Bootstrap

- [x] Create a separate clean-slate repository; preserve the legacy repository unchanged.
- [x] Record the product direction and architectural constraints.
- [x] Establish the .NET 10 Core + CLI scaffold and CI build.
- [x] Verify the initial scaffold with a Release build.

## 1. Semantic foundation

- [x] Compare ACE/APE, DRS, and a small Lattice-defined controlled-language subset against concrete examples.
- [x] Choose the smallest canonical semantic representation needed for the first reasoning slice.
- [x] Add tests for parsing and constructing terms, predicates/relations, statements, and questions.
- [x] Keep language syntax separate from semantic data structures.
- [x] Parse multiple complete sentences in one input atomically, preserving source order.
- [x] Parse binary relational assertions and direct yes/no relational questions.
- [x] Parse definite descriptive noun phrases in subject and object positions using stable canonical identifiers.
- [x] Represent definite descriptions with a head noun and ordered modifiers; assert them as separate unary facts on one stable individual.
- [x] Represent indefinite assertions and existence questions as existentially quantified unary conditions.
- [x] Keep anonymous existential witnesses distinct from named individuals and from other indefinite assertions.
- [x] Add quantified relational rules with unary premises and one fixed concrete object.
- [x] Add relational pronoun forms deliberately; resolve one `it` in either argument position without heuristic antecedent selection.

## 2. First symbolic reasoning slice

- [x] Represent simple facts and universally quantified rules.
- [x] Derive a conclusion from a short chain of rules.
- [x] Require every unary condition in a conjunctive universal rule to hold for the same individual, retaining all supporting premises in the proof.
- [x] Answer existential questions from one matching named individual or anonymous witness, including facts derived by universal rules.
- [x] Distinguish a supported conclusion from unknown information.
- [x] Explain a conclusion using the premises and rules that support it.
- [x] Demonstrate the slice through the CLI with deterministic tests.
- [x] Store ground relational facts and perform exact direct lookup with provenance.
- [x] Infer fixed-object relational conclusions from unary premises and retain their proofs.
- [ ] Add relation joins, traversal, multi-variable rules, and relational premises only after concrete use cases and tests exist.

## 3. Conversation and context

- [x] Preserve accepted statements across turns in the current CLI session.
- [x] Resolve the supported singular "it" reference when exactly one individual is known.
- [x] Ask for clarification when the referent is missing or ambiguous.
- [x] Preserve the original utterance and record the grounded referent separately.
- [x] Process multiple sentences in one turn in source order using the context and reasoning layers.
- [x] Observe every concrete individual argument in a relational assertion or question.
- [ ] Support richer antecedent selection, recency/salience, and additional pronouns.
- [x] Keep user assertions, imported claims, external observations, and derived conclusions distinguishable through structured provenance.

## 4. Knowledge packs

- [x] Define a versioned declarative JSON pack format based on LCE assertions and the proven semantic model.
- [x] Validate the complete pack before insertion and preserve imported-claim provenance plus pack IDs.
- [x] Load a pack through Core and query its claims through the existing reasoning engine without domain-specific code.
- [ ] Add pack unloading/replacement and durable knowledge-base persistence only when lifecycle requirements are defined.

## 5. Generic capabilities

- [x] Define a tool contract and descriptor independent of individual implementations.
- [x] Add generic discovery, argument validation, permission checks, bounded execution, and sourced invocation results.
- [x] Define a versioned, language-neutral JSON-lines protocol for external tool processes.
- [x] Discover external tools from per-tool manifests and invoke them through a stdio provider outside Core.
- [x] Keep process launch, transport, and manifest parsing out of Core; discovery does not execute tools.
- [x] Retain the latest sourced tool observation in conversation context for numeric follow-ups without promoting it to knowledge.
- [x] Demonstrate provider-independent invocation through a registry and explicit CLI composition; cognition has no provider-specific branches.
- [x] Move bundled calculator and unit-conversion implementations into independently discovered external Python tools; keep their binaries and runtimes out of Core.
- [x] Add a second independent capability before introducing autonomous tool selection.
- [x] Select capabilities through generic matchers built from typed, manifest-declared phrase patterns, argument bindings, and response templates.
- [x] Execute bounded, deterministic follow-up chains declared through manifest bindings against successive sourced numeric observations.
- [x] Extend manifest intent patterns with optional literal phrases and bounded multiword text captures while retaining conservative matching.

## 6. Language and knowledge expansion

- [x] Add conservative input normalization for harmless Unicode/formatting artifacts without silently rewriting words or meaning.
- [x] Define a replaceable semantics-translator contract and strict structural validation for the currently supported semantic forms; preserve original input provenance and keep candidates untrusted until reviewed.
- [x] Add an optional LM Studio adapter outside Core; require generated text to pass the native LCE parser and semantic validator.
- [x] Expose explicit CLI candidate review; never commit model output without user selection.
- [x] Add a standalone LM Studio benchmark runner that evaluates canonical translations and safe abstentions through the production parser and validator.
- [x] Define a natural-language-to-LCE benchmark covering paraphrases, quantification, relations, descriptions, questions, ambiguity, and unsupported constructions.
- [x] Make LM Studio abstentions explicit with examples and surface invalid model candidates in CLI diagnostics.
- [ ] Compare pinned hosted and local LLM configurations against the benchmark; require exact semantic matches on supported cases and zero unsafe conversions on unsupported cases.
- [ ] Resolve the translation contract for parser failures and multi-utterance turns before implementing an LLM adapter.
- [ ] Evaluate web retrieval as a source of temporary, attributable observations.
- [ ] Design a reviewable pipeline for generating and validating knowledge packs.
- [ ] Expand programming knowledge and add programming tools only after the generic foundations work.

## Working rules

- Keep the CLI runnable throughout.
- Prefer tiny PRs and complete one observable behavior at a time.
- Do not mark work complete without tests or a clearly stated reason why tests do not apply.
- Update this file in the PR that changes the status.
