# Objectives, constraints, and clarification

## Why this capability is needed

The first-use baseline shows that ordinary task requests are rejected before the runtime can process them. The current language and semantic pipeline supports assertions and questions. It has no semantic act for asking Lattice to achieve an outcome.

The current clarification path is narrower still: it resolves one supported `it` reference when the antecedent is missing or ambiguous. It cannot ask for information needed to carry out a task, retain that task while waiting, or interpret the next turn as an answer to the outstanding question.

This is not a request to add a day-out planner. It is a request to make objectives and missing information representable as general conversational concepts.

## Contract

### 1. An objective is a distinct semantic act

A user request to achieve an outcome must not be represented as:
- a factual assertion (the user has not asserted that the desired outcome is already true);
- a yes/no question (the user is not asking whether a proposition is true);
- a tool intent (the user has not necessarily requested one specific operation).

An objective has its own semantic representation and runtime outcome. Its original wording must be retained for provenance and explanation.

The representation must distinguish the requested outcome from the conditions under which the user would consider an outcome acceptable. It must not assume that the desired outcome is already achievable.

### 2. Conditions retain their role and meaning

Conditions supplied with an objective must preserve at least this distinction:

- **Hard constraints**: conditions an acceptable result must satisfy.
- **Preferences**: desirable properties that may be traded off when no candidate satisfies every preference.

A preference must not silently become a hard constraint, and a hard constraint must not be relaxed merely to produce an answer.

The semantic representation must preserve values and operators rather than reducing them to keywords. For example, a spending limit is not interchangeable with a preference for inexpensive options. Numeric, temporal, and other typed values will be needed as the benchmark grows; the existing identifier-only semantic terms are not sufficient for all of them.

Every extracted condition should retain its source wording. Unsupported or ambiguous interpretations must remain unresolved rather than being guessed.

### 3. Unknown is not false

The system must distinguish:
- a condition known to hold;
- a condition known not to hold;
- conflicting evidence;
- a condition for which there is no information.

The absence of a value is not evidence that a constraint is false, satisfied, or irrelevant. Candidate evaluation must preserve that distinction.

### 4. Clarification is driven by a real information need

The general runtime must not contain domain-specific rules such as “day-out requests require a budget” or “laptop requests require a model number.”

Instead, a capability that is attempting a defined operation may report that a particular input is required and is currently unavailable. The conversation layer can then turn that requirement into a focused question.

The information request must identify what is missing and retain the objective it belongs to. A request should be asked only when the missing information materially blocks safe progress; optional information should not create unnecessary interrogation.

### 5. The session retains the pending interaction

When Lattice asks a clarification question:
- the objective and already supplied conditions remain in session state;
- the outstanding information need is explicit;
- the next user turn can satisfy that need without restating the original objective;
- unrelated new information must not be forced into the outstanding slot;
- a user may explicitly revise an earlier condition, and the revision must replace only the named condition while preserving the rest.

A clarification response is not automatically a new fact in the general knowledge base. It belongs to the objective/session state unless the user separately asserts it as general knowledge.

### 6. Explanations remain evidence-based

An objective, each condition, each user-provided value, and each clarification answer should retain source provenance. Later recommendations must be able to distinguish user requirements from derived conclusions and external observations.

The system must not invent missing candidate attributes or treat an unverified external observation as a confirmed fact.

## Boundaries for the first implementation

The first implementation should establish the semantic and conversational contract, not attempt the complete planning benchmark.

In scope:
- a first-class objective distinct from assertions and questions;
- explicit hard-constraint versus preference representation;
- a generic missing-information outcome;
- session retention and a follow-up answer path;
- tests for preservation, replacement, and safe handling of unknown information.

Out of scope until a concrete end-to-end increment requires them:
- travel-specific or shopping-specific objective types in Core;
- a general planner, optimizer, or numerical scoring framework;
- automatic tool selection or autonomous execution;
- inferring facts or values from silence;
- broad unrestricted-English parsing;
- expanding the existing assertion grammar without a semantic use case.

The initial native-language slice can remain deliberately bounded. It must map supported wording faithfully to the contract and reject unsupported or ambiguous wording. The optional translator may later be extended to produce the same validated semantic representation, but it must not become an alternate source of unchecked meaning.


## Current implementation checkpoint

The initial native objective slice now retains a distinct goal and extracts a deliberately small set of typed conditions from the original request:

- hard constraints and preferences are represented separately;
- a stated limited budget without an amount is represented as an unresolved spending limit, not as zero or as a satisfied constraint;
- an explicit budget amount is represented as a typed money value, retaining its currency symbol rather than guessing a currency code;
- an explicit lack of a car is retained as a transport exclusion;
- simple preference clauses are represented as sets of desired tags while retaining their original source wording.

This is a bounded language slice, not general natural-language understanding. Unrecognized clauses remain in the preserved original source and are not promoted to conditions.

The session now selects an unresolved hard constraint that declares an expected value type and emits its missing-value prompt. The request is retained in session status. A matching typed response can fill that condition and records its own source wording; it is not added to the general knowledge base. An unrelated assertion does not consume the request. The response parser now also represents a packed-lunch plan, a starting location, and a typed departure/return time window. A later transport revision can add a bus exclusion and a walking-only mode without discarding the original no-car condition or other objective conditions. Explicit preference revisions (`Actually, I prefer ...`) and budget revisions (`Actually, my budget is ...`) replace only the matching named condition. Compositional preference edits use explicit add/remove wording (`Also, I care about ...` or `Remove ... from my preferences.`): additions are case-insensitively idempotent, removals affect only the named items, and removing the final item removes the preference condition. Candidate preference evidence is recomputed from the retained set, so unknown attributes remain unknown. Unrelated conditions, the objective goal, and the accumulated source history remain in session state. These bounded native-language examples map into reusable typed condition updates; they do not rank options or produce a plan.

## Validation

The motivating benchmark is [the first-use day-out task](benchmarks/first-use-day-out.md). It requires Lattice to retain a user's goal and conditions while asking for a missing spending limit, then accept a follow-up and continue without losing earlier information.

A second, unrelated task now exercises the same objective and condition representation. The laptop prompt is recognized through the same generic objective path: its explicit budget is a typed money constraint and its `I care about ...` clause is a preference. No laptop-specific workflow is introduced. The longevity and gaming-performance clauses remain preserved source text, and the current implementation still cannot compare the three candidates or infer which is more reliable.

## Candidate evaluation and temporal sequences

Candidate attributes are typed, loaded through versioned knowledge packs, and retain source provenance. A set-valued preference is assessed item by item: a supplied matching tag satisfies it, an explicitly excluded tag violates it, and an absent tag remains Unknown because a partial tag set does not prove the property is absent. If the same tag is explicitly included and excluded, its status is Conflicting. Each item carries only evidence that supports its status. This preserves sparse candidate data across unrelated domains without ranking options. The evaluator compares individual conditions such as money limits, exclusions, equality, preferences, and time-window containment.

A `CanFit` condition carries the user's earliest start and latest finish. Candidate knowledge packs can supply a `temporalPlan`: a reusable ordered template whose stages reference duration attributes and may reference allowed start-time sets and/or availability windows. Stages without a start-time set begin immediately after the preceding stage; a stage with a start-time set may wait for one of those events. The evaluator combines the user's time window with each candidate's own template without knowing what the stages represent.

Every attribute used by the plan is retained as evidence, including the plan template itself. Missing attributes yield Unknown, conflicting values yield Conflicting, and a known plan with no valid timing yields Violated. The current representation uses same-day `TimeOnly` values and does not support overnight sequences. When a plan fits, the evaluator also returns one concrete witness containing each stage's start, end, and duration. It is the first feasible path through the supplied event times, not an optimized or ranked schedule. The session includes these stage times in its evidence-backed objective summary only when the candidate has no known or unresolved hard-constraint failure.

The ordinary `I can leave at ... and be back by ...` condition-update path now creates this `CanFit` condition. Session evaluation therefore uses the same candidate-provided templates as direct evaluator tests; no destination-specific planner or timetable parser is introduced.

Conditions that describe the user's own context rather than a candidate property are retained but marked `NotApplicable` during candidate comparison. They do not count as unresolved hard constraints. Candidate-relevant context such as the requested origin is evaluated when the pack supplies a matching origin attribute. Each candidate derives one feasibility disposition from its applicable hard constraints: `Eligible` means all are satisfied, `Excluded` means at least one is violated, `Unresolved` means none is known to fail but at least one is unknown or conflicting, and `NotEvaluated` means there are no applicable hard constraints. Preference mismatches do not make a candidate infeasible. After an objective update, the session returns a concise, explicitly unranked assessment of each candidate, its feasibility disposition, condition statuses, and the source text supporting them. Unknown and conflicting evidence remains visible.

Typed candidate attributes may also define a bounded `sum` over other named attributes. The evaluator resolves money sums only when all components share a currency, and duration sums when all components are durations. Nested sums are bounded and retain the definition plus every component as evidence. Missing components remain Unknown, contradictory values remain Conflicting, and incompatible types or cyclic definitions are not guessed.

The native baseline and its exact original rejection behavior are recorded in [first-use-day-out-baseline.md](benchmarks/first-use-day-out-baseline.md). A sparse adjacent-domain transfer benchmark is recorded in [laptop-candidate-comparison.md](benchmarks/laptop-candidate-comparison.md). It reuses the same objective/evaluation path while deliberately leaving prices, screen quality, and reliability unsupported; Cedar 14's upgrade path is the only supplied product-specific comparison fact.

This contract is intentionally smaller than a planner architecture. Implementation should add further structure only when an executable task demonstrates the need.


## Evidence-backed preference trade-offs

The evaluator compares individual preference evidence between pairs of candidates that are both Eligible under their applicable hard constraints. It reports a distinction only when one candidate has evidence satisfying a stated preference and the other has explicit evidence violating that same preference. Both evidence sets are retained and exposed through the shared client evaluation.

Unknown or conflicting preference evidence does not establish a trade-off. Excluded, Unresolved, and NotEvaluated candidates are not included. Equal outcomes are not reported. These distinctions are not a ranking, aggregate score, or winner selection: candidates may each have advantages on different preferences, and the system does not invent weights to resolve that tension.
