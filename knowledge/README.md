# Foundational semantic world model

The default ontology is authored in independent modules under `knowledge/domains/`. The runtime composes all modules before validating references and reasoning over the graph.

## One relation graph

Every asserted relationship uses `relationAssertions`. Relation signatures declare the kinds of their arguments:

- `is-a`: **Type → Type**. For example, `fish is-a animal`.
- `instance-of`: **Individual → Type**. For example, `tope instance-of fish`.
- Ordinary domain relations: generally **Individual → Individual**, such as `piano-hammers strikes piano-strings`.

The reasoner uses the same graph for transitive subtype checks, inherited individual membership, structural classification, and relation evidence. There is no separate `parents` hierarchy or `typeAssertions` collection in version 3.

## Type definitions

Each type has an explicit status:

- **Primitive** marks a semantic starting point whose membership is asserted or inherited. A primitive is not defined merely by its place in the taxonomy.
- **Defined** requires a sufficient class expression. Membership is derived only when that expression is satisfied.

Necessary restrictions use positive existential forms: `some(relation, targetType)`. They constrain instances but do not establish membership by themselves. Sufficient expressions support positive existential restrictions combined with `and` and `or`.

The current expression language does not support universal restrictions, negation, cardinality, equality/inequality, inverse-role reasoning, or unrestricted first-order logic.

## Module layout

- `core.json`: shared entity, agent, place, event, process, artifact, material, information, mathematical, and activity concepts, plus general relation signatures.
- `instruments.json`: instrument kinds, mechanisms, components, and examples.
- `geometry.json`: geometric structures, boundaries, directions, and polygon composition.
- `mechanics.json`: forces, loads, mechanical states, deformation, and behavior.
- `language.json`: lexical and grammatical semantics.
- `economics.json`: economics, labour markets, and research references.
- `software.json`: scientific and conversational software, capabilities, and comparison criteria.
- `biology.json`: animal and fish classification.
- `natural-world.json`: camel adaptations, physiology, and Earth/day-night concepts.
- `astronomy.json`: solar radiation and illumination.
- `geography.json`: regions, mountains, the UN headquarters site, and Georgia.
- `mathematics.json`: calculus, limits, derivatives, and indeterminate forms.
- `arts-media.json`: paintings, artists, films, television series, episodes, and characters.
- `sports-food.json`: golf, Olympic records, food, ingredients, and annual produce lists.
- `transportation-activities.json`: vehicles, manual transmissions, running, travel, crafts, reuse, and mobile-device effects.
- `music-theory.json`: musical concepts and their relationship to composition.
- `film-production.json`: production incentives and film production.

Concept IDs are globally unique. Cross-module references are resolved after composition. Version 3 requires explicit primitive/defined declarations and uses only the common relation graph for asserted facts. Version 1 and 2 documents remain readable: the loader translates their `parents` and `typeAssertions` into `is-a` and `instance-of` relations respectively.

The benchmark-to-ontology mapping is maintained in [`knowledge/coverage/assistant-benchmark-v1.json`](coverage/assistant-benchmark-v1.json), with a human-readable summary in [`docs/knowledge-coverage.md`](../docs/knowledge-coverage.md). These files distinguish graph coverage from end-to-end runtime answer capability.

## Representation rules

Foundational knowledge stores semantic structure, not evidence packaging or presentation prose:

- No `parents`, `typeAssertions`, `sourceText`, or `sourceUrl` fields in version 3 modules.
- No quotation or citation metadata attached to concepts, definitions, or assertions.
- No URI-valued data properties in the foundational ontology.
- Relation identifiers, argument kinds, typed concepts, class expressions, and graph assertions carry the meaning.

External evidence and retrieval metadata belong in a separate evidence/ingestion layer. They are not prerequisites for loading or reasoning over the ontology. Explanatory sentences are generated from semantic structure; they are not stored as definitions.

## Model size

Counts after composing all seventeen modules:

- 542 concepts (312 types and 230 individuals)
- 312 type declarations (276 primitive, 36 defined)
- 131 relation signatures
- 811 relation assertions:
  - 342 `is-a`
  - 255 `instance-of`
  - 214 ordinary individual-to-individual relations
- 536 lexical forms

## Audit boundary

276 concepts are explicitly primitive and 36 have sufficient definitions. Primitive status marks a deliberately incomplete or foundational point in the ontology; it does not imply that the concept is inherently irreducible. The expanded seed is a coverage-oriented graph, not an encyclopedia: many prompts are only partially represented, and the benchmark map distinguishes ontology coverage from end-to-end answer capability. The model does not yet represent force magnitudes, dimensions, material constitutive laws, quantitative compliance, yield limits, or elastic recovery.

Relations with positive `order` values participate in sequential mechanism chains; zero-order relations are not traversed as chain steps. Some zero-order links are structural or attribute relations, while others connect a playing interface to its mechanism and remain outside the current chain traversal.
