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

Each attribute has a `subject`, an attribute `name`, a typed `value`, and its original `sourceText`. An optional boolean `summary` marks a curated detail to surface in candidate summaries; it defaults to false. An optional boolean `candidate` controls whether the subject participates in objective evaluation; it defaults to true for compatibility with existing candidate packs. Foundational concepts, citations, and software catalogues no longer use candidate attributes; they live in the typed semantic world model. Task-specific candidate packs use the `candidate` flag to control which candidate subjects participate in objective evaluation. The loader attaches imported-claim provenance and the pack ID to every attribute.

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


## Foundational semantic world model

The runtime embeds `knowledge/core-semantic-world-model.json`. This is the **only** default store for foundational concepts and world knowledge. It is separate from the version 1 task/candidate pack loader described above.

The semantic model format currently contains:

- **Concepts:** uniquely identified `type` and `individual` nodes. Labels identify nodes for language realization; labels alone do not define them.
- **Lexemes:** word forms associated with declared concept identities and parts of speech.
- **Relations:** named relations with declared domain and range types.
- **Type definitions:** parent types plus conjunctive existential restrictions. Every restriction requires a relation from the candidate individual to a filler of the declared target type.
- **Type assertions:** explicit individual/type memberships.
- **Relation assertions:** explicit subject/relation/object connections.
- **Data properties and assertions:** currently only typed URI values, used for documentation and DOI links.

Example type definition:

```json
{
  "concept": "keyboard-instrument",
  "parents": ["musical-instrument"],
  "conditions": [
    { "relation": "has-playing-interface", "targetType": "keyboard-interface" }
  ],
  "sourceText": "Evidence supporting this curated definition."
}
```

The text in `sourceText` is provenance only. The meaning is the declared parent and relation condition; no explanatory paragraph is stored or retrieved as the definition.

A relation assertion records structure independently of its wording:

```json
{
  "subject": "piano",
  "relation": "has-playing-interface",
  "object": "piano-keyboard",
  "sourceText": "Yamaha instrument documentation supports this assertion.",
  "sourceUrl": "https://www.yamaha.com/en/musical_instrument_guide/piano/"
}
```

An individual may satisfy several definitions at once. A piano qualifies as a keyboard instrument through its playing interface and as a struck-string instrument through its sound-production mechanism. Those are independent semantic dimensions, not mutually exclusive labels.

### Migrated default domains

- **Instruments:** interfaces, mechanisms, components, sound production and overlapping classifications.
- **Geometry:** polygons, boundaries, closed cycles, line segments, endpoints and straightness.
- **Language:** parts of speech, lexical items, semantic content, actions and states.
- **Economics:** monopsony as a buyer-market structure, monopsony power as a separate market-power concept, and structural mechanisms such as employer concentration, job differentiation and search frictions. Illustrative labour markets are modeled as linked entities rather than stored explanatory examples.
- **Software references:** IDAES, BioSTEAM and Pyomo as software individuals, with classifications, capability nodes, application-domain relations and URI-valued documentation links.
- **Research references:** publications, authors and journals as linked concepts, with typed DOI URIs.

### Expressive limits

The current reasoner supports type inheritance and conjunctive existential restrictions. It does not yet support disjunction, universal restrictions, cardinality/inequality constraints, arbitrary recursive expressions, or unrestricted first-order logic. In particular, the polygon slice models the existence of typed boundary elements and endpoints, but does not yet prove arbitrary polygon topology or distinguish all geometric cases.

### Boundary with version 1 task packs

The existing version 1 knowledge-pack format remains supported for candidate/objective data, including money, durations, text and text-set fields. Such values describe a task candidate or user-supplied preference; they are not foundational definitions or world-model facts. Foundational concepts, causal mechanisms, classifications, examples, and reference-catalogue meaning belong in the typed semantic model.

The semantic model loader rejects unknown fields and unsupported data-property value kinds. In particular, it does not accept free-text definition/description fields or text-valued semantic properties. Source text and source URLs remain allowed only as provenance metadata for assertions and definitions.
