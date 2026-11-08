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
- [ ] Extend the language deliberately: multi-sentence texts, noun phrase resolution, and relational sentence parsing.

## 2. First symbolic reasoning slice

- [x] Represent simple facts and universally quantified rules.
- [x] Derive a conclusion from a short chain of rules.
- [x] Distinguish a supported conclusion from unknown information.
- [x] Explain a conclusion using the premises and rules that support it.
- [x] Demonstrate the slice through the CLI with deterministic tests.

## 3. Conversation and context

- [x] Preserve accepted statements across turns in the current CLI session.
- [ ] Support references to established entities and prior conversational context.
- [ ] Ask for clarification when ambiguity changes the interpretation.
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

- [ ] Evaluate an optional natural-language-to-semantics translator with strict validation.
- [ ] Evaluate web retrieval as a source of temporary, attributable observations.
- [ ] Design a reviewable pipeline for generating and validating knowledge packs.
- [ ] Expand programming knowledge and add programming tools only after the generic foundations work.

## Working rules

- Keep the CLI runnable throughout.
- Prefer tiny PRs and complete one observable behavior at a time.
- Do not mark work complete without tests or a clearly stated reason why tests do not apply.
- Update this file in the PR that changes the status.
