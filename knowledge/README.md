# Foundational semantic world model

The default ontology is authored in twenty-seven independent modules under `knowledge/domains/`. The runtime composes all modules before validating references and reasoning over the graph.

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

- `core.json`: shared root concepts and the general `is-a` and `instance-of` relation signatures.
- `instruments.json`: instrument kinds, mechanisms, components, and examples.
- `geometry.json`: geometric structures and directions.
- `mechanics.json`: forces, loads, mechanical states, deformation, and behavior.
- `language.json`: lexical semantics.
- `economics.json`: economics, labour markets, and research references.
- `software.json`: scientific software, capabilities, and application domains.
- `biology.json`: animal and fish classification.
- `animal-survival.json`: animal adaptations and survival abilities.
- `un-headquarters.json`: United Nations headquarters location and acquisition contributions.
- `mobile-phones.json`: mobile-phone use, adverse effects, and contributing mechanisms.
- `running.json`: beginner running practices, their purposes, and safety guidance.
- `television.json`: a television series, its season-four episodes, and director credits.
- `diy-curtain-tiebacks.json`: DIY project ideas represented through materials, construction techniques, and fastening methods.
- `business-ideas.json`: business venture ideas represented through customer segments, problems, offerings, and revenue models.
- `conversational-ai-assistants.json`: conversational AI projects, access models, licensing, benefits, and comparable assistants.
- `golf-companies.json`: prominent golf equipment companies classified under a shared company type.
- `olympic-records.json`: Olympic athletes and numeric gold-medal counts for maximum-value reasoning.
- `dutch-artists.json`: Dutch artists, artworks, and painting relations.
- `sunset-and-day-night.json`: the causal link between Earth's rotation, sunset, the end of daylight, and the start of night.
- `calculus-and-lhopitals-rule.json`: L'Hôpital's Rule, its indeterminate forms and conditions, and its connection to broader calculus applications.
- `music-composition-and-theory.json`: music composition, entry-level methods, helpful theory topics, and advanced study areas.
- `us-mountain-elevations.json`: mountain elevations and the distinction between the contiguous 48 states and the broader continental U.S. including Alaska.
- `1980s-comedy-films.json`: notable 1980s comedy films, their release years, and their comedy styles.
- `golf-regional-terms.json`: golf scoring terminology, alternate names by language variety, and score relative to par.
- `full-english-breakfast.json`: common and optional ingredients of a full English breakfast.
- `new-jersey-road-trip.json`: a structured three-day Central New Jersey route, its ordered days, destinations, and activities.
- `calculus-and-lhopitals-rule.json`: L'Hôpital's Rule, its indeterminate forms and conditions, and its connection to broader calculus applications.

Concept IDs are globally unique. Cross-module references are resolved after composition. Version 3 requires explicit primitive/defined declarations and uses only the common relation graph for asserted facts. Version 1 and 2 documents remain readable: the loader translates their `parents` and `typeAssertions` into `is-a` and `instance-of` relations respectively.

## Representation rules

Foundational knowledge stores semantic structure, not evidence packaging or presentation prose:

- No `parents`, `typeAssertions`, `sourceText`, or `sourceUrl` fields in version 3 modules.
- No quotation or citation metadata attached to concepts, definitions, or assertions.
- No URI-valued data properties in the foundational ontology.
- Relation identifiers, argument kinds, typed concepts, class expressions, and graph assertions carry the meaning.

External evidence and retrieval metadata belong in a separate evidence/ingestion layer. They are not prerequisites for loading or reasoning over the ontology. Explanatory sentences are generated from semantic structure; they are not stored as definitions.

## Model size

Counts after composing all twenty-seven modules:

- 535 concepts
- 211 type declarations (174 primitive, 37 defined)
- 118 relation signatures
- 803 relation assertions:
  - 213 `is-a`
  - 340 `instance-of`
  - 302 ordinary individual-to-individual relations
- 361 lexical forms
- 5 numeric data properties
- 30 numeric data assertions

## Audit boundary

121 concepts are explicitly primitive and 37 have sufficient definitions. Primitive status marks a deliberately incomplete or foundational point in the ontology; it does not imply that the concept is inherently irreducible. The model does not yet represent force magnitudes, dimensions, material constitutive laws, quantitative compliance, yield limits, or elastic recovery.

Relations with positive `order` values participate in sequential mechanism chains; zero-order relations are not traversed as chain steps. Some zero-order links are structural or attribute relations, while others connect a playing interface to its mechanism and remain outside the current chain traversal.
