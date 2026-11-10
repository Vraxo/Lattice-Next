# Lattice

Lattice is an experiment in building a **tool-independent symbolic conversational intelligence**.

Its central purpose is to understand meaning, maintain context, reason over knowledge, form goals, and decide what to do. Programming is intended to be an early area of application—not Lattice's identity.

## Architectural premise

Lattice keeps three concerns independent:

- **Intelligence:** language interpretation, semantic representation, reasoning, planning, and decision-making.
- **Knowledge:** declarative, versioned knowledge packs, kept separate from conversation context and observations.
- **Capabilities:** tools that the intelligence can discover and invoke through generic contracts.

The console is the initial user interface. Tools are not the intelligence, knowledge packs are not executable plugins, and no neural model is required by Core. Core defines an optional, provider-neutral semantics-translator boundary. An LM Studio adapter lives in a separate project and can be enabled explicitly in the CLI; no model is bundled or selected by default. Translator output remains an untrusted candidate until reviewed.

## Current language, context, and reasoning slice

The first language implementation is **Lattice Controlled English (LCE) 0.9**, a deliberately small controlled-language subset inspired by the approach taken by Attempto Controlled English. The same native parser is also growing a deterministic English grammar built from reusable noun descriptions, quantifiers, and relation structures. It maps recognized constructions directly into Lattice's semantic model; it does not rewrite input into LCE first. Neither grammar is full ACE or unrestricted English.

A single input can contain multiple complete sentences, including relational facts and questions:

    Every dog is an animal. Fido is a dog. Is Fido an animal?
    Every red ball is a toy. The red ball is shiny. Is the red ball a toy?
    A red ball is shiny. Does a red ball exist?
    Fido is a dog. It is a mammal. Is it a mammal?
    Fido likes milk. Does Fido like milk?
    It likes milk. Does it like milk?
    Fido likes it. Does Fido like it?
    The red ball is a toy. Fido likes the red ball. Does Fido like the red ball?
    Every ball is a toy. The red ball is shiny. Is the red ball red? Is the red ball a toy?

The parser validates the entire text before the CLI changes any session state. Once parsing succeeds, sentences are processed in order, so context and knowledge established by earlier sentences are available to later sentences in the same input.

Unary assertions and single-condition universal rules retain their original behavior. Universal rules can now require several unary conditions: `Every red ball is a toy.` is represented as `and(red(x), ball(x)) => toy(x)`, so both the adjective and head noun must be established for the same individual before the conclusion follows. Binary relational assertions are stored as ordered predicates, so `Fido likes milk.` and `Does Fido like milk?` share the meaning `likes(fido, milk)`. Question verbs use their base form; the controlled grammar applies regular third-person singular spelling and supports the irregular forms `have`, `do`, and `go`.

Definite descriptions such as 'the red ball' are represented as a head noun ('ball') plus modifiers ('red'), while retaining the stable identity 'red-ball'. When a sentence uses that description, the knowledge base stores the head and modifiers as unary facts as well as the sentence's main assertion. This lets rules reason over the head noun and modifiers, including conjunctive class rules such as 'Every red ball is a toy'. Questions such as 'Is the red ball red?' can also retrieve a modifier fact. It is not synonym resolution or unrestricted noun-phrase understanding: 'the red ball' and 'the crimson ball' remain different identifiers.

Indefinite descriptions are also supported in a small existential slice. 'A red ball is shiny.' means that an unnamed individual satisfies red(x), ball(x), and shiny(x). 'Does a red ball exist?' asks whether one known named individual or one anonymous witness satisfies both conditions. Every indefinite assertion introduces its own witness, so separate statements are not silently treated as descriptions of the same object. Anonymous witnesses do not become antecedents for 'it' or get equated with definite descriptions.

For example, 'Every red ball is a toy. A red ball is shiny. Does a toy exist?' can be answered by applying the universal rule to the unnamed red-ball witness.

Universal-rule forward chaining applies to unary predicates, including rules with a conjunction of unary premises. A separate relational inference pass also supports restricted universal rules whose premises are unary and whose conclusion relates the matched individual to one fixed concrete object. It does not perform relational joins, use relations as premises, or traverse relation chains.

Before parsing, Lattice performs conservative formatting normalization: Unicode text is canonically composed, unusual whitespace and zero-width spaces become ordinary separators, a leading byte-order mark is removed, and soft hyphens are discarded. The original sentence is retained for provenance. Words, spelling, articles, capitalization, and punctuation are not corrected or invented.

The parser represents "it" as an unresolved reference. The conversation-context layer resolves it only when exactly one distinct individual has been explicitly mentioned in the current session. With no known individual or multiple known individuals, Lattice asks the user to clarify by naming the intended individual. Relation statements and questions introduce each of their concrete individual arguments into session context, so a turn that names both Fido and milk makes "it" ambiguous until the user clarifies. LCE 0.9 also permits one unresolved "it" in either argument position of a binary relation; a relation cannot contain two unresolved pronouns.

The CLI retains accepted facts, anonymous existential witnesses, and universal rules in an in-memory knowledge base for the current session. Version 1 JSON knowledge packs can be loaded with `:load path/to/pack.json`; every claim is validated before any are accepted, and imported claims retain their pack ID as provenance. Its reasoning engine performs unary forward chaining with conjunctive premises, answers existence questions, performs exact relational lookups and restricted fixed-object relational inference, and returns Unknown when it cannot find a proof. Supported answers include a trace back to the original sentences, including a note when an assertion used a resolved pronoun.

This is intentionally not a general theorem prover or a general co-reference resolver. Unknown does not mean false, and session knowledge and context are discarded when the CLI exits. The CLI accepts LCE and its supported native-English constructions directly. Optional model-assisted interpretation for language outside native coverage remains explicitly invoked with `:translate` after selecting an installed local model with `:translator setup`. See [the LCE grammar](docs/language.md), [LM Studio translation](docs/lm-studio.md), [conversational context](docs/context.md), and [the first reasoning slice](docs/reasoning.md).

Use :help, :status, and :quit inside the console. The generic capability boundary is demonstrated with `:tools` and explicit invocations such as `:tool calculator {"left":12,"operation":"multiply","right":3}`. Every capability, including the bundled calculator and unit converter, is discovered from a one-level subdirectory of `./tools` or the directory selected by `LATTICE_TOOLS_DIR`. Each tool supplies a `tool.json` manifest and communicates through the language-neutral JSON-lines stdio protocol. Manifests may also declare constrained natural-language patterns, typed slots, argument bindings, and response templates. A generic matcher loads these declarations; adding a tool and its supported phrases requires no tool-specific C# recognizer. Patterns are exact token sequences with single-token `number` and `word` slots, multiword `text` captures, and optional literal phrase groups, not unrestricted English or executable code. The calculator and unit converter are standalone Python scripts with no third-party dependencies. Other tools can use any executable language. Adding or replacing an implementation does not require rebuilding Core or the CLI. The bundled Python examples require Python 3 with `python` available on `PATH`, but Core and the stdio provider do not depend on Python. Tools requesting permissions are denied until approved. Before first use, the CLI offers to always allow a tool (remembered in `lattice.config.json`), allow it for the current session, or deny it. Approvals are scoped to individual tool IDs. Use `:permissions` to inspect grants and `:permissions revoke <tool-id>` to forget a remembered approval. For non-interactive runs, `LATTICE_TOOL_PERMISSIONS` can still pre-grant `read`, `write`, or `read,write` globally. Tool outputs are returned as sourced observations and are not automatically added to knowledge. The optional `web-search` tool uses DDGS to query public search providers without an API key, account, or user-hosted search server; its `read` permission can be remembered specifically for that tool. Install its pinned Python dependency once with `python -m pip install -r tools/web-search/requirements.txt`. Search results include their source URLs and retrieval time, and remain temporary observations. Public search providers may rate-limit automated requests. See [the external tool protocol](src/Lattice.Core/Tools/README.md).

## Run

Requires the .NET 10 SDK. Python 3 must be available as `python` on `PATH` to invoke the bundled calculator, unit converter, and web-search tool; other tools may use any runtime. To enable web search, install its Python dependency with `python -m pip install -r tools/web-search/requirements.txt`. The first search asks whether to always remember web-search permission or allow it for the current session. Build outputs are centralized under the repository's artifacts/ directory instead of being scattered into project-level bin/ and obj/ directories.

    dotnet run --project src/Lattice.Cli
    dotnet test Lattice.slnx --configuration Release

The test runner is Microsoft Testing Platform, selected in global.json for the .NET 10 dotnet test experience.

## Development principles

- Build small, runnable increments.
- Keep the intelligence independent of individual tool implementations.
- Prefer one cohesive Core project and a simple CLI until a real technical boundary justifies more projects.
- Treat external information as sourced observations before considering it for durable knowledge.
- Distinguish unknown from false, and retain provenance for conclusions.
- Preserve useful ideas from the previous Lattice repository only after reviewing their fit; this repository is a clean implementation.

## Status

The repository has a runnable console shell, a controlled-language parser with conservative input normalization, a language-independent semantic representation, a conservative session-context layer, an in-memory symbolic reasoning engine with conjunctive unary inference, existential witness lookup, exact relational lookup, and fixed-object relational inference, plus atomic loading of versioned declarative knowledge packs and a generic, permission-aware, time-bounded tool registry and language-neutral stdio provider, with the repository's calculator, unit converter, and optional DDGS-backed web search supplied as dynamically discovered Python tools. Manifest-declared deterministic intent patterns can select discovered capabilities from supported phrases; numeric tool observations remain temporary conversational context and can resolve follow-ups and bounded operation chains. This is not unrestricted English understanding. The translator boundary validates supported semantic structure, not whether an interpretation faithfully captures the user's meaning; candidates therefore remain subject to review. Relational joins and traversal, pack unloading/version replacement, and durable persistence are not implemented yet.

See [AGENTS.md](AGENTS.md), [the architecture notes](docs/architecture.md), [the language design](docs/language.md), [the context policy](docs/context.md), [the reasoning boundary](docs/reasoning.md), and [the development queue](.agents/todo.md) for the current constraints and next steps.
