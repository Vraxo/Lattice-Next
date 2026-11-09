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
- [ ] Add quantified relation arguments and relational pronoun forms deliberately.

## 2. First symbolic reasoning slice

- [x] Represent simple facts and universally quantified rules.
- [x] Derive a conclusion from a short chain of rules.
- [x] Require every unary condition in a conjunctive universal rule to hold for the same individual, retaining all supporting premises in the proof.
- [x] Answer existential questions from one matching named individual or anonymous witness, including facts derived by universal rules.
- [x] Distinguish a supported conclusion from unknown information.
- [x] Explain a conclusion using the premises and rules that support it.
- [x] Demonstrate the slice through the CLI with deterministic tests.
- [x] Store ground relational facts and perform exact direct lookup with provenance.
- [ ] Add relation joins, traversal, and relational rule inference only after concrete use cases and tests exist.

## 3. Conversation and context

- [x] Preserve accepted statements across turns in the current CLI session.
- [x] Resolve the supported singular "it" reference when exactly one individual is known.
- [x] Ask for clarification when the referent is missing or ambiguous.
- [x] Preserve the original utterance and record the grounded referent separately.
- [x] Process multiple sentences in one turn in source order using the context and reasoning layers.
- [x] Observe every concrete individual argument in a relational assertion or question.
- [ ] Support richer antecedent selection, recency/salience, and additional pronouns.
- [ ] Keep user assertions, derived conclusions, and observations distinguishable.

## 4. Knowledge packs

- [ ] Define a versioned declarative pack format based on the proven semantic model.
- [ ] Validate packs and preserve provenance for imported claims.
- [ ] Load and query a pack without compiling or loading domain-specific code.

## 5. Generic capabilities

- [ ] Define a tool contract and descriptor independent of individual implementations.
- [ ] Add generic discovery, argument validation, permission checks, bounded execution, and result recording.
- [ ] Feed tool results into context as sourced observations.
- [ ] Demonstrate that removing or replacing a tool requires no cognition changes.

## 6. Language and knowledge expansion

- [x] Add conservative input normalization for harmless Unicode/formatting artifacts without silently rewriting words or meaning.
- [ ] Evaluate an optional natural-language-to-semantics translator with strict validation.
- [ ] Evaluate web retrieval as a source of temporary, attributable observations.
- [ ] Design a reviewable pipeline for generating and validating knowledge packs.
- [ ] Expand programming knowledge and add programming tools only after the generic foundations work.

## Working rules

- Keep the CLI runnable throughout.
- Prefer tiny PRs and complete one observable behavior at a time.
- Do not mark work complete without tests or a clearly stated reason why tests do not apply.
- Update this file in the PR that changes the status.
