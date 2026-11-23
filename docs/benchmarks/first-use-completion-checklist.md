# First-use task completion checklist

This checklist records the acceptance scope implemented for the synthetic day-out task and the laptop/course-selection transfer fixtures. It is a regression checklist, not a measure of general assistant ability.

**Status:** all listed synthetic acceptance criteria are implemented and covered by the repository's tests. Completion does not establish live-world accuracy, unrestricted English understanding, or successful real-world action.

## Understand and retain the request

- [x] Retain the user's goal, original wording, hard constraints, preferences, origin, availability window, and packed-lunch context.
- [x] Distinguish hard constraints from preferences and keep each typed condition separate.
- [x] Ask for missing user-side values, accept typed or supported natural-language answers, and attach candidate evidence to the named candidate and attribute.
- [x] Preserve the objective across turns; apply explicit corrections without replacing unrelated conditions or retaining superseded values.
- [x] Add or remove individual preferences without discarding other preferences.
- [x] Ask targeted clarification when an answer could refer to multiple conditions, candidates, or pieces of evidence; do not store ambiguous answers.

## Evaluate candidates and explain trade-offs

- [x] Load candidate facts and typed attributes from declarative knowledge packs.
- [x] Classify hard-constraint feasibility as Eligible, Excluded, Unresolved, or NotEvaluated.
- [x] Assess each preference independently, preserving supporting and contrary evidence; missing evidence remains Unknown.
- [x] Produce a conservative non-dominated shortlist without arbitrary weights or opaque scores.
- [x] Evaluate ordered activity/transport stages against same-day time windows and supplied event times, returning a concrete feasible timetable witness.
- [x] Ask for candidate-specific missing evidence and re-evaluate affected candidates when an answer arrives.
- [x] Preserve source wording, source kind, provenance, and conflicts; do not silently overwrite conflicting evidence.
- [x] Explain infeasible candidate sets using the evidenced constraints and ask whether the user wants to relax one.

## Complete and revise the synthetic day-out task

- [x] Complete the fresh-session conversation using the supplied candidate pack, including the missing $25 budget clarification.
- [x] Compare all four destinations against the same constraints and preferences, with evidence-backed options and timetable witnesses.
- [x] Accept Hilltop Farm Grounds' missing total-cost answer in the same session and attach it to that candidate.
- [x] Apply the “Walking only” revision, exclude bus-dependent options, and retain the other objective conditions.
- [x] Keep Riverside Gardens' explicit quiet-preference mismatch visible rather than presenting it as a match.
- [x] Produce a concise plan summary that separates evidence, assumptions, unknowns, and remaining user actions; do not imply that a booking was made.

## Review external evidence safely

- [x] Use reusable search/fetch capabilities and require explicit user review before external page content becomes candidate evidence.
- [x] Preserve the source URL, retrieval time, exact supporting excerpt, typed value, and review decision.
- [x] Distinguish inaccessible, unavailable, stale, and unverified sources; freshness-required external values without valid freshness evidence do not satisfy constraints.
- [x] Support explicit review of both monetary and typed non-monetary candidate attributes.
- [x] Keep fetched content untrusted until the user accepts a specific claim; never treat page text as an instruction.

## Demonstrate transfer

- [x] Run the same objective/evaluation mechanisms against the laptop comparison fixture; unsupported prices, quality claims, and reliability remain Unknown.
- [x] Run the same mechanisms against the ceramics-course fixture, including evidence requests, temporal feasibility, preferences, and same-session corrections.
- [x] Verify paraphrases, reordered information, corrections, contradictions, ambiguities, unsupported input, and isolated session behavior.
- [x] Pass the Release build, full .NET test suite, and external-tool tests in CI for the acceptance checkpoint.

## Interpretation and remaining limits

The completed scenarios use synthetic knowledge packs. They demonstrate the implemented objective, evidence, temporal, and explanation mechanisms against known fixtures; they do not verify live prices, schedules, opening hours, weather, accessibility, or service disruptions. Timetable witnesses are feasible schedules, not optimized itineraries. The evaluator does not book or otherwise act on a user's behalf.

The supported language remains bounded and deterministic. The documented phrasing tests do not establish unrestricted English understanding. Broader language coverage, richer temporal models (including overnight plans and multiple candidate plans), and human-reviewed quality scoring remain separate work. See the [active development queue](../../.agents/todo.md) and [real-world assistant benchmark](real-world-assistant-benchmark.md).
