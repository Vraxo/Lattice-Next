# Foundational semantic world model

The runtime starts each session with the single embedded resource `core-semantic-world-model.json`. The deleted `core-world-model.json` sentence pack is no longer part of runtime startup or the repository.

The semantic model separates **concept identity, semantic structure, assertions, and provenance**:

- Concept IDs and labels name types and individuals; they do not define their meaning.
- Type definitions connect types to parent types and structural restrictions.
- Relation signatures declare domain and range types.
- Assertions state typed memberships and relations between individuals.
- URI-valued data assertions hold documentation and DOI links.
- `sourceText` and `sourceUrl` preserve provenance, never the meaning of a concept.

## Core domains

The model now includes all the default foundational domains that were previously represented by sentence claims and prose-valued attributes:

- Musical instruments, playing interfaces, sound mechanisms, physical components, and overlapping instrument classifications.
- Geometry, including polygons, boundaries, closed cycles, line segments, endpoints, and straightness.
- Language concepts, including parts of speech, verbs, lexical items, and action/state content.
- Economics, including monopsony buyer-market structure, monopsony power, wage markdowns, employer concentration, job differentiation, search frictions, switching costs, and linked illustrative labour-market structures.
- Software concepts for IDAES, BioSTEAM and Pyomo, represented by typed software roles, capability concepts, application domains, and documentation URIs.
- Research publications connected to authors and journals, with DOI URIs.

## Model size

<text>Counts below describe the current checked-in model, not benchmark results.</text>

- 189 concepts
- 93 type definitions
- 33 relation signatures
- 113 explicit type assertions
- 84 relation assertions
- 2 typed data-property declarations
- 6 typed URI data assertions

## Representation rules

Foundational meaning must not be stored as an English sentence, paragraph, `text` value, or `textSet` attribute. New knowledge belongs in concept types, relation signatures, type definitions, type assertions, relation assertions, or supported typed data properties.

Original text may appear only as evidence/provenance metadata. It is not read as a definition and does not act as a reasoning premise by itself.

The separate version 1 knowledge-pack loader remains available for task-specific candidate data and user-provided objective information. Its text and text-set values are not a substitute for foundational semantic knowledge.

## Reasoning boundary

The current reasoner supports type inheritance and conjunctive existential restrictions. It can derive classifications from a connected graph and retain the evidence supporting the derivation. It does not yet implement arbitrary description logic or unrestricted first-order logic: disjunction, universal restrictions, cardinality/inequality constraints and unrestricted recursive expressions remain unsupported.
