# Knowledge packs

Knowledge packs are declarative, versioned JSON data. Version 1 supports ordinary LCE assertions and typed candidate attributes. Loading validates the complete pack before adding any of its contents to a knowledge base.

## Version 1 structure

A pack contains:
- `formatVersion`: the integer `1`;
- `id`: a stable lowercase identifier using letters, numbers, and single hyphens;
- `name`: a human-readable name;
- `claims`: an array of individual LCE assertions (which may be empty when typed attributes are supplied);
- optional `attributes`: an array of typed attributes.

A pack must contain at least one claim or one typed attribute. Each array is limited to 500 entries. Unknown or duplicate fields are rejected rather than silently ignored.

## Typed attributes

Each attribute has a `subject`, an attribute `name`, a typed `value`, and its original `sourceText`. An optional boolean `summary` marks a curated detail to surface in candidate summaries; it defaults to false. An optional boolean `candidate` controls whether the subject participates in objective evaluation; it defaults to true for compatibility with existing candidate packs. Reference metadata in the default world model sets it to false, so concepts, citations, and package catalogues do not become candidates in unrelated comparisons. The loader attaches imported-claim provenance and the pack ID to every attribute.

Supported value forms:

| Type | Required fields | Meaning |
| --- | --- | --- |
| `money` | `amount`, `currencySymbol` | Non-negative decimal amount and explicit symbol |
| `text` | `value` | One non-empty text value |
| `textSet` | `values`, optional `excludedValues` | Explicit positive and negative text-tag evidence; at least one array must contain a value |
| `timeWindow` | `start`, `end` | Start and end times in 24-hour `HH:mm` form |
| `duration` | `minutes` | Non-negative duration in minutes |
| `timeSet` | `times` | Non-empty array of 24-hour `HH:mm` times |
| `temporalPlan` | `stages` | Ordered non-empty sequence of duration-attribute references, optionally constrained by start-time sets and availability windows |
| `sum` | `attributes` | Sum of 2–64 named money or duration attributes belonging to the same candidate |

Example:

```json
{
  "formatVersion": 1,
  "id": "day-out-candidates",
  "name": "Day out candidates",
  "claims": [],
  "attributes": [
    {
      "subject": "Fernbank Nature Reserve",
      "name": "total-cost",
      "value": {
        "type": "money",
        "amount": 6,
        "currencySymbol": "$"
      },
      "sourceText": "Benchmark calculation: $6 return fare plus $0 entry; the user brings lunch."
    },
    {
      "subject": "Fernbank Nature Reserve",
      "name": "tags",
      "value": {
        "type": "textSet",
        "values": ["quiet", "outdoors"]
      },
      "sourceText": "Fernbank is usually quiet and outdoors."
    }
  ]
}
```

A derived value can refer to other attributes on the same subject. For example:

```json
{
  "subject": "Fernbank Nature Reserve",
  "name": "total-cost",
  "value": {
    "type": "sum",
    "attributes": ["return-transport-cost", "entry-cost"]
  },
  "sourceText": "The total outing cost is the return transport cost plus the entry cost."
}
```

Sums resolve lazily when an objective uses the derived attribute. Money components must use the same currency; duration components produce a duration. Nested sums are supported with a bounded depth. Missing components remain Unknown, conflicting component values remain Conflicting, and incompatible types, currencies, cycles, or arithmetic overflow remain Unknown.

For `textSet`, `values` records explicitly supported tags and `excludedValues` records explicitly unsupported tags. An absent tag in either array remains unknown; it is not inferred from omission. If the same tag appears in both arrays, evaluation reports conflicting evidence. The format stores data; it does not execute instructions. Each typed value object must contain exactly the fields defined for its type. Unsupported types, extra fields, malformed values, and invalid source metadata reject the pack. Repeated identical attribute values merge provenance through the knowledge base; distinct values remain separate so later evaluation can report conflicts. If any identical merged declaration marks an attribute for summary display, that selection is retained.

## Current boundary

Typed attributes can be loaded through the same versioned pack path as LCE claims. The objective evaluator can compare money, text exclusions/equality, preference tags with explicit positive/negative evidence, and temporal feasibility. A candidate may supply a `temporalPlan` attribute: an ordered list of stages that reference duration attributes and optionally allowed start-time sets and availability windows. The user's `CanFit` time-window condition supplies the earliest start and latest finish; the evaluator combines that window with each candidate's plan and checks whether at least one complete sequence fits. The plan and all referenced attributes are retained as evidence. Missing attributes remain Unknown; conflicting values remain Conflicting. The current implementation uses same-day `TimeOnly` values and does not model overnight sequences.

The complete synthetic day-out dataset is in `data/knowledge-packs/day-out-candidates.json`. It includes all four candidates, cost components and derived total-cost expressions, access and activity durations, qualitative tags, opening windows, and the supplied bus timetables. Unspecified facilities and services are represented by missing attributes, not invented values. The total-cost expressions sum the supplied return fare and entry cost; they are not hardcoded totals.

Pack unloading, replacement, persistence, candidate ranking, and planning are not implemented.


## Concept knowledge

Concept explanations are composed from ordinary LCE claims, not stored definition strings. A concept is represented as a named node in the knowledge graph. Unary predicates describe its classifications or properties; binary predicates connect it to other concept nodes. When asked "What is a polygon?", the runtime traverses the connected facts and presents the resulting structure with the provenance of each claim.

Example:

```json
{
  "formatVersion": 1,
  "id": "geometry-concepts",
  "name": "Geometry concepts",
  "claims": [
    "Polygon is a geometric-shape.",
    "Polygon has Boundary.",
    "The boundary is closed.",
    "The boundary has Line-segment.",
    "Line-segment is a line-segment.",
    "The line-segment is straight."
  ]
}
```

The resulting explanation is composed from the graph:

- polygon is classified as geometric-shape;
- polygon has a boundary;
- the boundary is closed and has a line-segment;
- the line-segment is straight.

The runtime returns the exact imported claims as evidence. It does not use their source sentences as the answer. Relationship traversal is bounded and cycle-safe. A missing specific concept remains unavailable; it does not silently inherit a broader concept's explanation.

Universal rules can now introduce an existentially quantified related individual. For example, the claim `Every polygon has a boundary.` is represented as:

```
forall(x, implies(
  polygon(x),
  exists(y, and(boundary(y), has(x, y)))
))
```

The witness is scoped to the subject that satisfied the rule. If two distinct polygons are known, the rule derives two distinct anonymous boundary witnesses; it does not connect both polygons to one globally shared `boundary` individual. Each witness property and relation carries the rule's provenance. The current increment supports one witness variable, one subject-to-witness binary relation, and one or more positive unary witness conditions. Arbitrary multi-variable joins, nested existential structures, and unrestricted first-order logic remain unsupported and are rejected.

This is an initial graph-backed concept explanation, not yet a complete ontology or natural-language realizer. The graph still uses unary and binary predicates, with a deliberately bounded quantified-rule subset.


## Default world model

Every runtime session starts with the embedded `core-world-model`. It is a
versioned declarative knowledge pack stored at
`knowledge/core-world-model.json`, embedded into the runtime assembly, and
loaded through the normal pack validator. It is available to the CLI, server,
and in-process clients without per-prompt configuration or filesystem access.

Foundational knowledge belongs here. Optional knowledge packs extend this
shared model for specialized domains; they should not be needed for ordinary
concepts that a general assistant is expected to know. The default model is
session-local in the current implementation: it is loaded into each new
session, and user assertions and optional packs enrich that same knowledge
base for the life of the session.

The initial core seed includes basic geometry, a compositional verb concept, and foundational economics knowledge. It also contains a small, sourced reference catalogue for monopsony research and open-source Python process-simulation and optimization tools. Reference attributes are not objective candidates. The geometry and concept claims include:

- a polygon is a geometric shape;
- every polygon has a closed boundary;
- every boundary has a line segment;
- every line segment is straight;
- a verb is a part of speech that expresses an action or state;
- a monopsony is an economic buyer-market structure.

These are stored as assertions and rules, not as a prewritten polygon answer.
Concept explanations inspect direct facts and applicable universal rules,
follow the related concept structure within bounded limits, and retain the
original source claims as evidence. This is an initial seed, not a claim that
the world model is already comprehensive.
