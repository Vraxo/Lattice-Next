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

## C# conventions

- Target .NET 10.
- Use nullable reference types and implicit usings.
- Use one class or primary type per file.
- Prefer explicit, small types and deterministic behavior.
- Avoid unnecessary dependencies, reflection-driven magic, and premature abstraction.
- Use Result-style outcomes for expected failures; do not use exceptions for ordinary control flow.
