# Repository guidance

## Product direction

Lattice is a tool-independent symbolic conversational intelligence. It is not an IDE command palette, a collection of coding workflows, or an LLM wrapper.

Keep these concerns independently understandable and changeable:

- Language: parses and expresses meaning.
- Semantics: represents meaning in a language-independent form.
- Intelligence: reasons, maintains goals, plans, and decides what to do.
- Context: holds conversation state, current beliefs, observations, and provenance.
- Knowledge: supplies reusable, declarative knowledge.
- Tools: provide optional capabilities through generic contracts.
- REPL: is an interface and composition root, not a home for reasoning.

## Architectural constraints

- Keep `Lattice.Core` cohesive. Treat `Lattice.Repl` as an ordinary frontend; introduce shared application/client boundaries only when they have concrete responsibilities, and keep every frontend on the same public contract.
- Put tool implementations in the Core `Tools/` area. Do not create a project or adapter for every tool, language, or integration.
- Cognition must not branch on individual tool names or concrete tool classes.
- A tool declares its contract and effects; generic infrastructure discovers, authorizes, invokes, and records it.
- Tool output is untrusted evidence/observation until interpreted. Preserve source and provenance.
- Knowledge packs are declarative data, not executable code. Keep durable knowledge separate from session context and observations.
- No neural model, hosted AI service, or network access is a dependency of Core. Any future language translator is optional, replaceable, and outside the reasoning authority.
- Do not claim full Attempto Controlled English (ACE) support unless the implementation actually conforms to ACE. Clearly name any Lattice-defined subset or derivative.
- Unknown is not false. Do not infer negation from missing information.

## Development workflow

- Develop language and intelligence as coupled, co-evolving tracks; do not complete one before returning to the other.
- Every supported English construction must map faithfully to semantics that Core can represent and reason over. Every meaningful reasoning capability should have a clear, usable expression in the supported native language.
- Prefer alternating small language and reasoning increments where each has a useful next step, but do not treat alternation as a rigid schedule or add capabilities merely to satisfy it. Concrete correctness defects take priority.
- Validate both sides of the boundary with exact semantic tests, rejection tests, and end-to-end reasoning tests or benchmark cases where applicable.
- Prefer small, independently reviewable PRs that leave the repository runnable.
- Make one vertical improvement at a time. Avoid speculative frameworks and empty abstraction layers.
- Add behavioral tests with each meaningful language, semantic, reasoning, context, or tool feature.
- Run the relevant tests and a Release build before opening a PR. Report what was and was not run.
- Update `.agents/todo.md` when work is completed or priorities change.
- Do not port code from the legacy `Vraxo/Lattice` repository without reviewing its fit against this direction.

## Incremental delivery and progress reporting

The default unit of work is a **small, coherent, independently verifiable pull request**. Build Lattice through a sequence of clear steps, not a large batch of loosely related changes.

- **One logical step at a time.** Each PR should solve one focused problem or add one cohesive capability. Split work when parts can be reviewed, tested, and understood independently.
- **Build directly on the current baseline.** Before starting a step, identify the relevant behavior and constraints already present. Extend them without silently changing unrelated behavior; make dependencies on earlier work explicit.
- **Keep every increment understandable.** Prefer a few focused files and a narrow diff. Avoid combining multiple features, broad refactors, speculative groundwork, and cleanup into one PR. Do not create a giant PR merely to avoid several small ones.
- **Verify each step.** Add or update focused tests, run the relevant test suite and Release build, and inspect the resulting diff. Never describe a change as verified unless the corresponding checks actually ran and passed. If a check cannot be run, state that plainly and do not imply success.
- **Commit in reviewable units.** Each commit should represent a coherent piece of progress. After **every commit**, report the commit and immediately provide a progress indicator showing where the current step stands on the way to a reviewable, mergeable PR. Do not go silent between commits or leave the user guessing whether work is still progressing.
- **Keep the path to a PR visible.** Report the remaining work when useful: implementation, tests, build/CI, review, and PR readiness. Open the PR once the focused increment is ready; do not keep expanding its scope while waiting for the next idea.
- **Stop at the merge boundary.** Once a PR is ready and its checks are known, present the result and its verification status. Treat the merged baseline as the starting point for the next step. Do not begin stacking unrelated future work into the same PR.
- **Choose the next step only after the current one is reviewable.** Use the resulting merged state, tests, and any review feedback to decide what comes next. This makes each increment a clear checkpoint and keeps regressions and reasoning errors local.

## Pull-request auto-merge — mandatory

**EVERY pull request opened for this repository MUST have GitHub auto-merge explicitly enabled. This is a standing requirement for all future work, not an optional convenience and not something that depends on the user remembering to request it.**

- Immediately after creating each PR, explicitly enable auto-merge through GitHub. Do not assume repository-level availability, CI workflows, or branch settings automatically enroll a PR.
- Confirm that the auto-merge request succeeded. If GitHub rejects it, investigate and report the exact blocker; do not silently leave the PR waiting for a manual merge.
- Keep the repository's required status checks and review requirements intact. Auto-merge must wait for the configured requirements; it must never be used to bypass checks or reviews.
- Do not manually merge a PR as a workaround unless the user explicitly asks for that PR to be merged manually.
- Apply this rule to documentation-only PRs, maintenance PRs, refactors, and feature PRs alike.
- Before reporting a PR as ready, state whether auto-merge was successfully enabled. A PR with auto-merge unavailable or not enabled is not considered complete under this workflow.

This instruction is intentionally explicit so it remains discoverable and enforceable by future agents even when prior conversation context is unavailable.

## C# conventions

- Target .NET 10.
- Use nullable reference types and implicit usings.
- Use one class or primary type per file.
- Prefer explicit, small types and deterministic behavior.
- Avoid unnecessary dependencies, reflection-driven magic, and premature abstraction.
- Use Result-style outcomes for expected failures; do not use exceptions for ordinary control flow.
