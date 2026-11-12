# Development queue

This is the active queue for the clean-slate Lattice repository. Keep it short, ordered, and tied to runnable outcomes.

## Development goal: compounding capability through meaningful tasks

> Lattice should start out doing a few simple things reliably, but its capabilities should compound. Eventually, we should be able to give it a task it wasn't specifically programmed to perform, and have it solve it by combining things it already knows how to do.

- Start from meaningful end-to-end tasks, not from a desire to increase the number of grammar constructions, commands, or feature checkboxes.
- For each task, identify the general capability that is missing. Build that reusable capability rather than a task-specific handler, intent, or hardcoded workflow.
- Prefer increments that make a genuinely useful task possible and also strengthen mechanisms that can be recombined in other domains.
- Treat example tasks as executable product benchmarks: demonstrate what Lattice can do by composing existing capabilities, not by adding bespoke code for each example.
- Keep language and intelligence co-evolving. Every supported construction must map faithfully to semantics Core can represent and reason over; meaningful reasoning capabilities must be expressible through ordinary interaction.
- Alternate language and reasoning work when each side has a useful next step, but do not follow an artificial schedule. Correctness defects and concrete task requirements take priority.
- Validate exact semantics, safe rejection, unknown/contradictory cases, provenance, and end-to-end task behavior as applicable.

> Current priority: broaden the first-use acceptance gate, starting with ambiguous candidate references and targeted clarification. A bare `It costs ...` answer now requests a candidate choice when multiple candidates still need a cost, without storing the value; continue broadening ambiguous references and test the unambiguous single-candidate case. Conflicting budget assertions also have a distinct clarification path, while an explicitly signalled `Actually, ...` correction replaces the prior value.


## Bootstrap

- [x] Create a separate clean-slate repository; preserve the legacy repository unchanged.
- [x] Record the product direction and architectural constraints.
- [x] Establish the .NET 10 Core + REPL scaffold and CI build.
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
- [x] Answer supported universal class questions from direct universal rules and explicit counterexamples while preserving open-world Unknown.
- [x] Distinguish a supported conclusion from unknown information.
- [x] Explain a conclusion using the premises and rules that support it.
- [x] Demonstrate the slice through the REPL with deterministic tests.
- [x] Store ground relational facts and perform exact direct lookup with provenance.
- [x] Infer fixed-object relational conclusions from unary premises and retain their proofs.
- [x] Expose facts and rules through live read-only views so callers cannot bypass knowledge-base validation, deduplication, or provenance handling.
- [x] Preserve explicit positive and negative ground evidence separately and report True, False, Both, or Unknown without allowing contradictions to trigger unrelated inferences.
- [x] Derive explicit negative universal conclusions from positive unary premises without feeding negative conclusions back into positive inference.
- [x] Support explicit negative unary premises in universal rules; match them against explicit or safely derived negative ground unary evidence, never absence.
- [x] Reuse positive and negative unary rule conclusions in iterative, polarity-separated inference closure with cycle-safe proof ancestry.
- [x] Support at most one fixed-object positive or explicitly negative relational premise alongside unary conditions in universal rules, matching explicit same-polarity evidence for the same subject and exact object while preserving proof provenance.
- [x] Reject universal rules with multiple fixed-object relational premises rather than implicitly expanding the supported relational language.
- [ ] Add relation joins, traversal, multi-variable rules, and broader relational premises only after concrete use cases and tests exist.

## 3. Conversation and context

- [x] Preserve accepted statements across turns in the current REPL session.
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
- [x] Prompt for declared tool permissions on first use; support remembered per-tool approvals, session-only grants, revocation, and environment-variable pre-grants for non-interactive runs.
- [x] Define a versioned, language-neutral JSON-lines protocol for external tool processes.
- [x] Discover external tools from per-tool manifests and invoke them through a stdio provider outside Core.
- [x] Keep process launch, transport, and manifest parsing out of Core; discovery does not execute them.
- [x] Retain the latest sourced tool observation in conversation context for numeric follow-ups without promoting it to knowledge.
- [x] Demonstrate provider-independent invocation through a registry and explicit REPL composition; cognition has no provider-specific branches.
- [x] Move bundled calculator and unit-conversion implementations into independently discovered external Python tools; keep their binaries and runtimes out of Core.
- [x] Add a second independent capability before introducing autonomous tool selection.
- [x] Select capabilities through generic matchers built from typed, manifest-declared phrase patterns, argument bindings, and response templates.
- [x] Execute bounded, deterministic follow-up chains declared through manifest bindings against successive sourced numeric observations.
- [x] Extend manifest intent patterns with optional literal phrases and bounded multiword text captures while retaining conservative matching.

## 6. Language and knowledge expansion

- [x] Add conservative input normalization for harmless Unicode/formatting artifacts without silently rewriting words or meaning.
- [x] Add the first compositional native-English grammar slice to the existing parser; map covered constructions directly to semantic formulas and preserve safe rejection of unsupported meanings.
- [x] Reuse one parsed noun-description structure across quantified, existential, definite, and relational sentence positions; support composable adjective and restrictive relative-clause modifiers.
- [x] Extend compositional noun descriptions with explicitly coordinated adjective modifiers and canonical-meaning/rejection tests.
- [x] Add explicit irregular and invariant plural noun forms while keeping unsupported morphology conservative.
- [x] Expand invariant zero-plural nouns and distinguish singular `news` from plural morphology with paired semantic tests and benchmark cases.
- [x] Move native-English lexical tables into a validated, versioned embedded resource while keeping grammar algorithms in code.
- [x] Separate native-English sentence parsing, noun-description parsing, morphology, lexical validation, and semantic construction into independent internal components.
- [ ] Expand native-English grammar coverage by adding further compositional constructions and morphology with benchmarked semantic fidelity.
  - [x] Parse direct unary property assertions and questions about named individuals, with exact semantic and rejection tests.
  - [x] Add the explicit negative universal quantifier `No` with singular/plural agreement, negative class and fixed-object relational conclusions, and end-to-end reasoning tests.
  - [x] Parse coordinated positive properties of named individuals as conjunctions and store each grounded conjunct with shared provenance; verify both through reasoning.
  - [x] Parse coordinated class-membership and adjective predicates of named individuals in either order, with exact semantics, conservative rejection, and end-to-end reasoning tests.
  - [x] Parse explicitly coordinated positive and negative predicates of named individuals, preserve each conjunct's polarity and shared provenance, and verify both through reasoning.
  - [x] Parse explicitly coordinated negative properties of named individuals, preserve each negative polarity independently, and verify negative reasoning with shared provenance.
- [x] Define a replaceable semantics-translator contract and strict structural validation for the currently supported semantic forms; preserve original input provenance and keep candidates untrusted until reviewed.
- [x] Add an optional LM Studio adapter outside Core; require generated text to pass the native LCE parser and semantic validator.
- [x] Expose explicit REPL candidate review; never commit model output without user selection.
- [x] Add a standalone LM Studio benchmark runner that evaluates canonical translations and safe abstentions through the production parser and validator.
- [x] Define a natural-language-to-LCE benchmark covering paraphrases, quantification, relations, descriptions, questions, ambiguity, and unsupported constructions.
- [x] Make LM Studio abstentions explicit with examples and surface invalid model candidates in REPL diagnostics.
- [ ] (Deferred) Compare pinned hosted and local LLM configurations against the benchmark; require exact semantic matches on supported cases and zero unsafe conversions on unsupported cases.
- [x] Resolve the translation contract for parser failures and multi-utterance turns: distinguish unsupported input from invalid LCE output, and keep each request single-utterance.
- [x] Evaluate web retrieval as a source of temporary, attributable observations.
  - [x] Add an optional, manifest-discovered DDGS search capability that queries public search providers without requiring an account, API key, or user-hosted service, and returns retrieval timestamps and per-result source URLs, titles, domains, and snippets.
  - [ ] Add bounded page fetching as a separate capability only after URL-safety and content-extraction requirements are defined.
- [ ] Design a reviewable pipeline for generating and validating knowledge packs.
- [ ] Expand programming knowledge and add programming tools only after the generic foundations work.

## Application interfaces

- [x] Rename the interactive console frontend from `Lattice.Cli` to `Lattice.Repl` without changing its behavior.
- [x] Define a transport-independent public client/session contract and an in-process runtime implementation with isolated sessions and structured turn/evidence results.
- [x] Adapt the REPL to use that contract through an in-process client.
- [x] Add a genuinely non-interactive `Lattice.Cli` for scripts and automation.
- [x] Add an optional long-running host and remote client over a narrow localhost HTTP API; preserve the same contract for every frontend.
- [x] Allow the non-interactive CLI to select the in-process runtime or HTTP server without changing its input/output contract.
- [x] Allow the REPL to select the in-process runtime or HTTP server while retaining its local-only tool and translator composition.

## Working rules

- Keep the REPL runnable throughout.
- Prefer tiny PRs and complete one observable behavior at a time.
- Do not mark work complete without tests or a clearly stated reason why tests do not apply.
- Update this file in the PR that changes the status.
