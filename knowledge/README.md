# Foundational semantic world model

The default semantic model is authored in independent modules under `knowledge/domains/`. The runtime composes every module before global validation and reasoning.

## Type-definition semantics

Every type has an explicit status:

- **Primitive** means the ontology intentionally treats the concept as a semantic starting point. Parent types and any necessary restrictions constrain its use, but do not claim to define it sufficiently. Primitive membership is asserted or inherited from a more specific type.
- **Defined** means membership can be derived from an explicit sufficient class expression. A defined type requires at least one parent and a non-empty sufficient expression. The reasoner derives membership only when all parents and that expression are satisfied.

Necessary restrictions use positive existential forms: `some(relation, targetType)`. They constrain instances of a type but do not establish membership by themselves.

Sufficient expressions support positive existential restrictions combined with `and` and `or`. For example, a striking component is a physical component that strikes another physical component. A resonator is a physical component that receives vibration from another physical component and radiates a sound event. These classifications are derived from relation assertions and the types of their participants.

The current expression language deliberately does not claim support for universal restrictions, negation, cardinality, equality/inequality, inverse-role reasoning, or unrestricted first-order logic. These require additional semantics and tests before being used in foundational knowledge.

## Module layout

- `core.json`: shared root concepts and model metadata.
- `instruments.json`: instrument kinds, mechanisms, components, and examples.
- `geometry.json`: geometric structures.
- `language.json`: lexical semantics.
- `economics.json`: economics, labour markets, and research references.
- `software.json`: scientific software, capabilities, and application domains.
- `biology.json`: animal and fish classification.

Concept IDs are globally unique. References between modules are resolved after composition. Version 2 requires an explicit primitive/defined declaration for every type. Version 1 remains loadable for compatibility; new foundational modules must use version 2.

## Provenance

Concept labels, lexical forms, typed relations, restrictions, and assertions carry semantic structure. `sourceText` and `sourceUrl` are provenance only; they never become semantic definitions or reasoning premises. Foundational meaning must not be stored as English paragraphs or prose-valued data properties.

## Model size

Counts below describe the modules after composition:

- 197 concepts
- 98 type declarations
- 36 relation signatures
- 117 explicit type assertions
- 89 relation assertions
- 2 typed data-property declarations
- 6 typed URI data assertions

## Audit boundary

There are 81 primitive types and 17 defined types. Of the primitive types, 80 currently have no necessary restrictions and remain opaque beyond their taxonomy position; only `string-component` has necessary restrictions so far. This is an explicit backlog, not a claim that these concepts are semantically complete. The primitive inventory should be reduced or better constrained over time; modularity and explicit status do not automatically make the ontology complete. Relations with positive `order` values participate in sequential mechanism chains; zero-order relations are not traversed as chain steps. Some zero-order links are structural or attribute relations, while others connect a playing interface to its mechanism and remain outside the current chain traversal.
