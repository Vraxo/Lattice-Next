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

Each attribute has a `subject`, an attribute `name`, a typed `value`, and its original `sourceText`. An optional boolean `summary` marks a curated detail to surface in candidate summaries; it defaults to false. The loader attaches imported-claim provenance and the pack ID to every attribute.

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


## Concept definitions

A knowledge pack can provide a definition using an ordinary typed text attribute whose subject is the exact concept identifier and whose name is `definition`. For example:

```json
{
  "formatVersion": 1,
  "id": "geometry-definitions",
  "name": "Geometry definitions",
  "claims": [],
  "attributes": [
    {
      "subject": "polygon",
      "name": "definition",
      "value": {
        "type": "text",
        "value": "A polygon is a closed plane figure made of straight line segments."
      },
      "sourceText": "A polygon is a closed plane figure made of straight line segments."
    }
  ]
}
```

The request `What is a polygon?` retrieves this text and returns the imported pack ID and source text as evidence. A modified request such as `What is a regular polygon?` requires a definition whose subject is exactly `regular polygon`; it does not silently substitute the broader `polygon` definition. Missing or conflicting definitions are reported as unavailable. Definitions that require freshness are not used after their freshness deadline or when no valid deadline is supplied.
