# First-use task completion checklist

This checklist tracks the work required for Lattice to complete the first-use day-out task reliably, then checks that the same mechanisms transfer to a different domain. It is a product acceptance checklist, not a count of features or PRs.

## Status key

- [x] **Complete** — implemented and verified by automated tests or the recorded runtime benchmark.
- [ ] **In progress** — a partial mechanism exists, but the acceptance criterion is not yet met.
- [ ] **Not started** — no sufficient implementation exists yet.

A checked item must have a repeatable test or benchmark. Passing a synthetic fixture does not establish live-world accuracy.

## 1. Understand and retain the request

- [x] Retain the user's goal and original wording as an objective.
- [x] Distinguish hard constraints from preferences.
- [x] Ask for a missing user-side hard-constraint value, such as the spending limit.
- [x] Accept a typed answer to the outstanding budget clarification.
- [x] Accept a natural-language spending-limit answer such as “I can spend up to $25.” as the same typed money value.
- [x] Accept multiple ordinary-language phrasings for the user's starting location.
- [x] Accept multiple ordinary-language phrasings when the user says lunch is already covered.
- [x] Correct a previously supplied starting location without discarding the meal plan, time window, budget, or other objective conditions.
- [x] Retain origin, availability window, and packed-lunch context from the benchmark exchange.
- [x] Accept “My starting point is …” as an alternative phrasing for the same typed origin condition.
- [x] Accept the availability window when the user states the return time before the departure time.
- [x] Revise a hard constraint without discarding unrelated objective conditions.
- [x] Apply repeated budget and preference corrections in sequence without retaining superseded condition values.
- [x] Add and remove individual preference items without replacing unrelated preferences.
- [x] Preserve the objective across multiple turns in one session.
- [ ] Reliably understand the full range of ordinary phrasings for the same intent, including corrections and ambiguous statements.
- [ ] Ask targeted clarification when a user's answer could refer to more than one condition or candidate.

## 2. Evaluate candidates honestly

- [x] Load candidate facts and typed attributes from declarative knowledge packs.
- [x] Evaluate hard constraints and distinguish Eligible, Excluded, Unresolved, and Not evaluated.
- [x] Keep preferences separate from hard constraints.
- [x] Assess each preference independently and preserve supporting/contrary evidence.
- [x] Keep missing preference evidence Unknown rather than treating it as a mismatch.
- [x] Explain evidence-backed preference trade-offs.
- [x] Produce a conservative non-dominated shortlist without arbitrary weights or scores.
- [x] Evaluate ordered activity/transport stages against time windows and actual timetable events.
- [x] Return concrete feasible timetable witnesses.
- [x] Identify candidate-specific missing hard-constraint evidence and expose a request for it.
- [x] Accept a user's answer to a candidate-specific monetary evidence request and attach it only to the matching candidate and attribute.
- [x] Accept a candidate-named text value for a missing hard-constraint attribute and validate its candidate, attribute, and value type.
- [x] Preserve source wording, source kind, and provenance for user-supplied candidate answers.
- [x] Re-evaluate all affected candidates and withdraw resolved evidence requests without changing unrelated facts.
- [x] Handle conflicting candidate answers explicitly, without silently overwriting earlier evidence.
- [x] Ask for the smallest useful missing fact, not a broad or repetitive questionnaire.

## 3. Complete the day-out task

- [x] Run the initial day-out request through the native runtime using the benchmark knowledge pack.
- [x] Ask for the missing total budget before assessing affordability.
- [x] Accept the $25 total budget and retain lunch, origin, and time-window details.
- [x] Compare all four supplied destinations against the same constraints and preferences.
- [x] Distinguish the feasible options and explain evidence-backed trade-offs.
- [x] Produce unranked feasible outing options and concrete timetable witnesses where the evidence supports them.
- [x] Revise the active plan after the user says “Walking only.”
- [x] Exclude bus-dependent destinations after that revision and retain the other constraints.
- [x] Keep Riverside Gardens' quiet-preference mismatch visible rather than presenting it as a perfect match.
- [x] Demonstrate the full conversation with missing candidate evidence requested and answered during the same session.
- [x] Produce a concise, coherent final day plan that clearly separates evidence-backed details, assumptions, unknowns, and user actions.
- [x] Handle fully infeasible candidate sets by explaining the evidenced blocking constraints and asking whether the user wants to relax one; keep unresolved candidates on the evidence-request path.
- [ ] Verify robustness against paraphrases, reordered information, repeated corrections, contradictions, and unsupported requests.

## 4. Use real-world information safely

- [x] Acquire public candidate page content through reusable search/fetch tools and submit a user-selected monetary claim only after explicit review.
- [x] Preserve the URL/source, retrieval time, and reviewed supporting excerpt for accepted external monetary observations.
- [ ] Distinguish stale, unavailable, conflicting, and unverified information.
- [ ] Treat live schedules, prices, opening hours, weather, and disruptions as time-sensitive; verify them before relying on them.
- [x] Apply URL and tool safety boundaries before fetching or acting on external content.
- [x] Never turn missing data into a positive or negative claim.
- [x] Make clear which parts of a plan require user confirmation or booking.

## 5. Prove the mechanisms transfer

- [x] Run the same objective/evaluator mechanisms against the sparse laptop comparison benchmark.
- [x] Keep unsupported price, screen-quality, and reliability claims Unknown.
- [x] Surface Cedar 14's supplied upgrade-path evidence without inventing a reliability ranking.
- [x] Request the missing total-cost evidence for each unresolved laptop candidate.
- [x] Accept candidate-specific price answers and update the laptop comparison through the same generic path.
- [x] Demonstrate a materially different ceramics-course selection task without adding a domain-specific workflow.
- [x] Show that the system combines objective capture, constraints, preferences, evidence requests, same-session answers, temporal witnesses, and corrections in a task that was not used to implement them.

## 6. Acceptance gate: first-use task

The first-use task is complete when all of the following are true:

- [x] A fresh session can complete the benchmark from the user's initial natural-language request through the final walking-only revision.
- [x] Missing user information and missing candidate evidence are requested at the right level and can be answered naturally.
- [x] The final recommendation is feasible, evidence-backed, and understandable.
- [x] All material claims have traceable provenance or are explicitly labelled as unknown.
- [x] Corrections update only the intended part of the task.
- [x] The same generic mechanisms pass the laptop transfer benchmark.
- [x] Release build, full .NET test suite, and external-tool tests pass in CI.
- [x] No benchmark-specific command, hardcoded candidate rule, or opaque recommendation score is required.

**Current assessment:** the synthetic day-out flow and first laptop transfer checkpoint are implemented. Candidate-specific monetary answers can be supplied as user assertions or reviewed external observations; external observations retain their source URL, retrieval timestamp, and reviewed excerpt. The REPL now connects bounded page retrieval to explicit review: the user selects a candidate, amount, currency, and exact excerpt, inspects the source metadata, and must type `accept` before the shared runtime receives the claim. A third synthetic transfer benchmark now exercises the same mechanisms for ceramics-course selection, including a same-session missing-price answer, temporal feasibility, preferences, and a walking-only correction. Timed plans now end with a concise summary separating evidence-backed options, assumptions, unknowns, and user actions. The day-out benchmark now also requests Hilltop Farm Grounds' missing total cost and accepts the user's $18 answer in the same session before the walking-only revision. Retrieved text is never automatically converted into a fact. Fully excluded candidate sets now report the hard constraints violated by each supplied candidate and ask whether the user wants to relax one; unresolved candidates with pending evidence remain on the evidence-request path instead. The day-out parser also accepts a return-before-departure phrasing and retains the same typed availability window. Repeated explicit budget and preference corrections are covered end to end; each new value replaces the prior condition while preserving unrelated objective details. A conflicting budget assertion without explicit correction language now requests clarification and leaves the current budget unchanged. Explicit budget corrections are covered with `Actually, ...`, `Correction: ...`, `To clarify, ...`, and `I meant ...` phrasings. The runtime also accepts “I can spend up to $25” as a typed answer to the outstanding budget clarification. It also accepts multiple equivalent starting-location phrasings, including “I'm coming from Central Station,” and several ways to state that lunch is already covered. A same-session correction to the starting location now replaces only the `origin` condition while preserving the lunch plan and availability window. Failed page observations now distinguish inaccessible (401/403), unavailable (404/410), and temporarily unavailable (408/429/5xx) sources instead of collapsing them into one generic HTTP error. The fetcher now preserves HTTP freshness expiry from `Cache-Control`/`Age` or `Expires`; already-expired observations are rejected before review, and missing expiry is explicitly shown as unverified. An ambiguous `It costs ...`, `That one costs ...`, or `This one costs ...` answer now requests a specific candidate when multiple candidates still need cost evidence, and regression tests verify that no evidence is stored before clarification. The same reference resolves to the sole remaining candidate when only one still needs a cost. A demonstrative reference to a candidate's text attribute is also tested: when two parks still need transport-mode evidence, `That one's transport mode is walking` asks which park and stores nothing until the candidate is named. The day-out benchmark also makes the boundary between planning and acting explicit: booking requirements are labelled unverified, the user is told to verify live details before acting, and the summary states that no booking has been made. Remaining gaps include broader ordinary-language ambiguity and correction handling, handling stale/unavailable/unverified external data, broadening reviewed evidence beyond monetary values, and completing the full acceptance gate. Missing evidence is explicitly kept Unknown: the laptop regression asserts that preference entries without supporting evidence remain Unknown, candidate prices stay unresolved rather than becoming exclusions, and the system makes no unsupported recommendation. The complete first-use acceptance gate is now supported by the fresh-session day-out integration test, same-session budget and candidate-evidence clarification, the evidence-backed plan and explicit action boundary, isolated correction tests, laptop and ceramics transfer tests, and successful CI covering the Release build, full .NET suite, and external-tool tests. These are synthetic benchmark and regression results, not a claim of live-world accuracy. This checklist should be updated in the same PR that changes any item's status.
