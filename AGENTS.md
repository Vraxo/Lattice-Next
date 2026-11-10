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
- CLI: is an interface and composition root, not a home for reasoning.

## Architectural constraints

- Keep one cohesive `Lattice.Core` project and one `Lattice.Cli` project until a concrete technical boundary justifies more projects.
- Put tool implementations in the Core `Tools/` area. Do not create a project or adapter for every tool, language, or integration.
- Cognition must not branch on individual tool names or concrete tool classes.
- A tool declares its contract and effects; generic infrastructure discovers, authorizes, invokes, and records it.
- Tool output is untrusted evidence/observation until interpreted. Preserve source and provenance.
- Knowledge packs are declarative data, not executable code. Keep durable knowledge separate from session context and observations.
- No neural model, hosted AI service, or network access is a dependency of Core. Any future language translator is optional, replaceable, and outside the reasoning authority.
- Do not claim full Attempto Controlled English (ACE) support unless the implementation actually conforms to ACE. Clearly name any Lattice-defined subset or derivative.
- Unknown is not false. Do not infer negation from missing information.

## Development workflow

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

Progress indicators are status reports, not estimates of elapsed time or lines of code. Base them on explicit milestones (for example: scope agreed → implementation committed → tests/build verified → PR opened and ready). Mark incomplete or blocked milestones honestly; do not manufacture a percentage or imply that CI passed before it has. In ChatGPT conversations, render a compact visual progress bar after every commit, alongside the commit identifier and a brief statement of the next milestone. This presentation requirement is specific to ChatGPT and does not apply to other agents or tools.

## C# conventions

- Target .NET 10.
- Use nullable reference types and implicit usings.
- Use one class or primary type per file.
- Prefer explicit, small types and deterministic behavior.
- Avoid unnecessary dependencies, reflection-driven magic, and premature abstraction.
- Use Result-style outcomes for expected failures; do not use exceptions for ordinary control flow.
