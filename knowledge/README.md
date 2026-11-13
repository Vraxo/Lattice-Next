# Foundational semantic world model

The default semantic world model is authored as independent domain modules under `knowledge/domains/`. The runtime embeds every module, composes them into one validated model, and creates the session reasoner. There is no generated or hand-maintained aggregate source file.

## Module layout

- `core.json`: shared root concepts and model metadata.
- `instruments.json`: instrument kinds, mechanisms, components, and examples.
- `geometry.json`: geometric concepts and the triangle structural example.
- `language.json`: lexical categories, verbs, and semantic content.
- `economics.json`: market structure, labour-market mechanisms, examples, and research references.
- `software.json`: software roles, capabilities, application domains, and documentation links.
- `biology.json`: the animal/fish classification example.

Each module uses the same typed schema and contains only its own concepts, lexical entries, relation signatures, definitions, assertions, and data properties. Concept IDs are globally unique. References across modules are resolved after composition.

## Representation rules

- Concept IDs and labels identify types and individuals; they do not define their meaning.
- Type definitions connect types to parent types and structural restrictions.
- Relation signatures declare domain and range types.
- Assertions state typed memberships and relations between individuals.
- URI-valued data assertions hold documentation and DOI links.
- `sourceText` and `sourceUrl` preserve provenance, never the meaning of a concept.

Foundational meaning must not be stored as English sentences, paragraphs, `text` values, or `textSet` attributes. It belongs in typed concepts, relations, definitions, assertions, and supported data properties.

## Current model size

Counts below describe the modules after composition:

- 194 concepts
- 95 type definitions
- 34 relation signatures
- 116 explicit type assertions
- 87 relation assertions
- 2 typed data-property declarations
- 6 typed URI data assertions

## Reasoning boundary

The current reasoner supports type inheritance and conjunctive existential restrictions. It does not yet implement arbitrary description logic or unrestricted first-order logic.

Modularity improves inspection and maintenance; it does not by itself guarantee that every concept has a sufficiently precise definition. Semantic quality remains a separate modeling and validation task.
