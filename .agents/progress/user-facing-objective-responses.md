# User-facing objective response progress

**Target:** ordinary objective requests should receive a concise, actionable answer by default, while detailed assessments remain available on demand through `:why`.

**Current progress:** 5 of 6 acceptance milestones complete (83%). Update this checklist in the same pull request as implementation changes.

## Acceptance milestones

- [x] Inspect the current objective evaluation and session flow.
- [x] Build a concise response formatter from the existing generic evaluation model.
- [x] Preserve the full evidence assessment in session status and expose it through the REPL `:why` command.
- [x] Cover unique winners, non-dominated ties, preference trade-offs, unresolved evidence, and infeasible candidate sets with regression tests.
- [x] Pass the complete GitHub Actions test suite and merge the pull request.
- [ ] Run the original first-use day-out request in the REPL and compare the result with the approved response shape.

## Response contract

The default response should:

- Lead with the useful conclusion, not a candidate-by-candidate internal assessment.
- Present only supported shortlist options and relevant distinguishing details.
- Show useful costs, activities, and feasible schedules when the evidence provides them.
- Give a conditional tie-break suggestion only when the data supports it; do not silently assign preference weights.
- Mention unresolved alternatives briefly without blocking a supported shortlist.
- Clearly disclose when times or prices are supplied data rather than live-verified facts.
- Avoid exposing evidence excerpts, internal status labels, and decision bookkeeping by default.

The `:why` view retains the detailed assessment, condition statuses, evidence excerpts, provenance, preference trade-offs, temporal witnesses, and outstanding evidence requests.

## Approved day-out response shape

The intended experience is the concise two-option answer agreed in the implementation discussion: explain that Fernbank and Quarry both fit, conditionally lean toward Fernbank for a cheaper and shorter outing, show each option's cost/activity/timetable, mention Hilltop's unresolved cost, and state that transport details have not been live-verified.
