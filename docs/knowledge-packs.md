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

Each attribute has a `subject`, an attribute `name`, a typed `value`, and its original `sourceText`. The loader attaches imported-claim provenance and the pack ID to every attribute.

Supported value forms:

| Type | Required fields | Meaning |
| --- | --- | --- |
| `money` | `amount`, `currencySymbol` | Non-negative decimal amount and explicit symbol |
| `text` | `value` | One non-empty text value |
| `textSet` | `values` | Non-empty array of text tags |
| `timeWindow` | `start`, `end` | Start and end times in 24-hour `HH:mm` form |
| `duration` | `minutes` | Non-negative duration in minutes |
| `timeSet` | `times` | Non-empty array of 24-hour `HH:mm` times |

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

The format stores data; it does not execute instructions. Each typed value object must contain exactly the fields defined for its type. Unsupported types, extra fields, malformed values, and invalid source metadata reject the pack. Repeated identical attribute values merge provenance through the knowledge base; distinct values remain separate so later evaluation can report conflicts.

## Current boundary

Typed attributes can be loaded through the same versioned pack path as LCE claims. The objective evaluator can compare money, text exclusions/equality, time-window containment, preference tags, and temporal sequences. A temporal sequence composes ordered duration attributes with optional allowed start-time sets and availability windows. It checks whether at least one sequence fits within a requested earliest-start/latest-end window, and retains the attributes used as evidence. Missing attributes remain Unknown; conflicting values remain Conflicting. The current implementation uses same-day `TimeOnly` values and does not model overnight sequences.

The complete synthetic day-out dataset is in `data/knowledge-packs/day-out-candidates.json`. It includes all four candidates, component and benchmark-calculated costs, access and activity durations, qualitative tags, opening windows, and the supplied bus timetables. Unspecified facilities and services are represented by missing attributes, not invented values. The benchmark's calculated total-cost fields are explicitly identified as calculations from the supplied return fare and entry cost; general arithmetic over attribute components is not implemented.

Pack unloading, replacement, persistence, candidate ranking, and planning are not implemented.
