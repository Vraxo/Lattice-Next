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
      "subject": "Fernbank",
      "name": "total-cost",
      "value": {
        "type": "money",
        "amount": 6,
        "currencySymbol": "$"
      },
      "sourceText": "Fernbank costs six dollars including entry and fares."
    },
    {
      "subject": "Fernbank",
      "name": "tags",
      "value": {
        "type": "textSet",
        "values": ["quiet", "outdoors"]
      },
      "sourceText": "Fernbank is quiet and outdoors."
    }
  ]
}
```

The format stores data; it does not execute instructions. Each typed value object must contain exactly the fields defined for its type. Unsupported types, extra fields, malformed values, and invalid source metadata reject the pack. Repeated identical attribute values merge provenance through the knowledge base; distinct values remain separate so later evaluation can report conflicts.

## Current boundary

Typed attributes can now be loaded through the same versioned pack path as LCE claims. The objective evaluator can compare money, text exclusions/equality, time-window containment, and preference tags. Duration and time-set values are preserved as typed data but are not yet used by objective comparisons. Pack unloading, replacement, persistence, and candidate ranking are not implemented.
