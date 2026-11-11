# First-use benchmark: native baseline and current checkpoint

## Original baseline

The initial native baseline was recorded at repository revision `59be64b7e69109ed42bd0520a5239f13fc64c775`, after the benchmark specification merged and before objective support was implemented.

Environment:
- Runtime: in-process `LatticeRuntimeClient` and a fresh `ILatticeSession`
- Translator: disabled; no model-assisted interpretation
- External tools: not registered or invoked
- Knowledge: empty session
- Test surface: the shared runtime session contract consumed by the REPL and CLI

At that revision, all four benchmark prompts were rejected before any utterance was processed, with `That sentence is outside LCE 0.9`. The original CI-backed observation is retained here as the historical baseline.

## Current checkpoint: objective capture and one typed clarification

The native interface recognizes the day-out and laptop objectives. It extracts a small set of typed conditions, distinguishing hard constraints from preferences. A limited budget without an amount is represented as an unresolved spending limit; an explicit amount is represented as a typed money value.

The day-out conversation retains the original objective and preferences, asks for the missing budget, and accepts the benchmark's follow-up details. It also accepts a walking-only revision without losing the earlier budget, time window, origin, or preferences. These remain bounded condition-retention mechanisms, not candidate evaluation or planning.

## Candidate-data representation and evaluation checkpoint

The knowledge base supports typed candidate attributes with source wording and pack provenance. The version 1 pack at `data/knowledge-packs/day-out-candidates.json` now contains all four benchmark candidates and their supplied cost components, transport modes and durations, character/facilities, activity descriptions and durations, opening windows, and published bus departure sets.

The native evaluator compares the supplied total outing costs against the $25 budget, car exclusion against stated transport modes, and quiet/outdoors preference tags. It also evaluates a temporal sequence using departure sets, journey durations, activity duration, the activity's opening window, and return travel.

The shared session status now exposes the evaluator's candidate assessments and their supporting source text/pack IDs. The REPL displays those assessments through `:status`, so loading a candidate pack and retaining an objective makes the currently supported comparisons observable through the same session contract. The user's overall 09:00–18:00 boundary is retained separately from destination opening hours; it is not incorrectly compared as though the destination must remain open for the entire day.

The sequence is represented as ordered duration stages. A stage may begin at a supplied set of event times, must fit within an available time window, or begin immediately after the preceding stage. The evaluator checks whether any complete sequence fits between the user's earliest start and latest return, retaining each referenced attribute as evidence.

For 09:00–18:00, Fernbank, Quarry Lake, and Hilltop each have a feasible bus itinerary. Quarry Lake is not rejected merely because it closes at 17:00: its visit can occur during opening hours, and its 15:45 return bus reaches Central Station by 16:30. Riverside's bus-sequence assessment remains Unknown because no bus timetable is supplied; a separate walking sequence is supported by its journey/activity durations and opening window. A tighter 09:00–16:00 test correctly rejects Quarry Lake because its only listed return service arrives at 16:30.

The dataset deliberately omits attributes for facts the benchmark leaves unspecified: Quarry Lake facilities beyond its picnic area, Hilltop food service, and Riverside bus departures. Missing data is not encoded as a negative claim.

## Remaining benchmark gap

Lattice now has a reusable temporal-sequence feasibility comparison, but it has **not** solved the complete outing task. In particular:
- the user-facing parser/session does not yet construct and evaluate this sequence condition from ordinary conversation;
- the evaluator reports condition assessments, not a recommendation or ranked list;
- the total-cost attribute is explicitly labelled as a benchmark calculation from return fare plus entry; the evaluator does not yet derive sums from component attributes;
- transport-mode alternatives and their corresponding temporal sequences are not yet combined into one unified candidate assessment.

The next increment should be driven by the exact missing general mechanism exposed by timetable feasibility, rather than by adding a destination-specific planner. Do not add ranking or opaque scoring.

See [the benchmark specification](first-use-day-out.md), [the knowledge-pack format](../knowledge-packs.md), and [the objective contract](../objectives.md).
