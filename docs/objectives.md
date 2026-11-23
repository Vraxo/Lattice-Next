# Objectives and candidate evaluation

Lattice can retain a user's goal as an objective, represent its conditions, compare supplied candidates against those conditions, ask for missing information, and explain the resulting evidence. This is a general evaluation mechanism; the day-out, laptop, and course-selection fixtures are examples, not dedicated workflows.

## Goals and conditions

An objective retains the requested outcome and its conditions. Conditions distinguish:

- **Hard constraints** — requirements a candidate must satisfy to remain feasible.
- **Preferences** — desirable properties used to compare feasible candidates without automatically excluding them.

A condition has a typed value. The current evaluator supports the documented money, text/equality or exclusion, preference-tag, and same-day temporal forms. Unsupported or missing values remain unresolved rather than being guessed.

## Candidate assessment

Each candidate receives one of these hard-constraint dispositions:

- **Eligible** — every applicable hard constraint is supported as satisfied.
- **Excluded** — at least one hard constraint is explicitly violated.
- **Unresolved** — no violation is established, but one or more hard constraints are unknown or conflicting.
- **Not evaluated** — no applicable hard constraint can be assessed.

Preference evidence is assessed separately. Missing preference evidence remains unknown; an explicit mismatch is not turned into a hard-constraint failure. The evaluator can identify a conservative non-dominated shortlist when the supplied evidence supports a comparison. It does not invent weights or a single score to break ties.

## Temporal feasibility

Candidate temporal plans are ordered stages. Stages may reference durations, allowed start events, and availability windows. The evaluator checks whether at least one complete sequence fits the user's same-day availability window and returns a concrete feasible timetable witness with its supporting attributes.

A returned witness proves feasibility against the supplied synthetic or loaded data; it is not an optimized itinerary. The current evaluator uses same-day `TimeOnly` values and does not model overnight sequences or choose among multiple temporal plans for one candidate.

## Evidence review and freshness

User assertions and reviewed external observations retain their source information. Fetched page text is not accepted as a fact automatically.

For the REPL review flows, the user selects the candidate and canonical attribute, supplies a typed value and an exact excerpt from the fetched page, inspects the source URL and retrieval/freshness metadata, and explicitly accepts the claim. The monetary `:review-price` path and typed `:review-attribute` path use this human-review boundary. Stale or unverified external values do not satisfy freshness-required conditions.

Review confirms that the user accepted the cited excerpt as evidence; it does not independently prove that the source is accurate or that the value remains true in the real world.

## Response boundary

A plan summary distinguishes evidence-backed details, assumptions, unresolved information, and remaining user actions. It must not imply that live schedules, prices, opening hours, weather, or availability were checked unless a current source supports that claim. It must not imply that a booking or external action was completed when it was not.

The synthetic benchmark results demonstrate the current mechanisms against supplied fixtures. They do not establish live-world accuracy or unrestricted natural-language understanding. See the [first-use benchmark](benchmarks/first-use-day-out.md), its [completion checklist](benchmarks/first-use-completion-checklist.md), and the [knowledge-pack format](knowledge-packs.md).
