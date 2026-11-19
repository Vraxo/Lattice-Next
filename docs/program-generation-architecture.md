# Program generation architecture

Program generation is a knowledge-driven token continuation process.

## Core rule

The runtime must not contain target-language knowledge. It must not dispatch a prompt to a target-specific parser, choose a predefined operation family, or render code through a language-specific backend. Language syntax, token forms, API usage, and constraints belong in declarative knowledge.

The same runtime is responsible for natural-language, programming-language, and other sequence generation.

## Request understanding

A request is represented by the semantic facts and constraints extracted from it, not by a single prompt category. One request may express multiple requirements that are concurrent, ordered, or dependent on one another.

The semantic representation must preserve all of those requirements. It must not reduce them to one effect/argument pair or a single generation template.

## Generation

Generation begins from the available semantic context and an empty output sequence.

At each step, the engine:

1. Retrieves candidate next-token knowledge that applies to the current token context and active semantic constraints.
2. Reasons over the candidates using the knowledge base, the requested meaning, and the already-emitted sequence.
3. Selects a continuation and updates the context and semantic state.
4. Repeats until the requirements are expressed and the sequence is complete.

Candidates and constraints are data. The engine is generic and does not know what a particular language's keywords or APIs mean.

## Knowledge

Language modules contain the facts needed to produce valid sequences: token forms, legal continuations, syntax relationships, composition constraints, and any language-specific API or runtime knowledge.

Domain modules can contribute additional concepts and relations. For example, Blender operations and Blender API details belong in domain knowledge, while the target language's syntax belongs in its language module.

Knowledge modules should describe concepts and relationships, not store one complete program per prompt. They should support composition across multiple requirements.

## Design constraints

- No target-language names, keywords, syntax, APIs, or special cases in runtime code.
- No target-specific parser or backend classes.
- No one-prompt-one-operation routing.
- Multiple semantic requirements must remain simultaneously available during generation.
- Each generation step is a next-token decision over candidates grounded in knowledge.
- Preserve deterministic bounds, explainable candidate evidence, and executable benchmark validation.
- Knowledge additions should increase capability without requiring new target-specific C# branches.
