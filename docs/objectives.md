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

The session now selects an unresolved hard constraint that declares an expected value type and emits its missing-value prompt. The request is retained in session status. A matching typed response can fill that condition and records its own source wording; it is not added to the general knowledge base. An unrelated assertion does not consume the request. The response parser now also represents a packed-lunch plan, a starting location, and a typed departure/return time window. A later transport revision can add a bus exclusion and a walking-only mode without discarding the original no-car condition or other objective conditions. These are bounded native-language examples mapped into reusable typed condition updates; they do not evaluate candidates, compare options, or produce a plan.

## Validation

The motivating benchmark is [the first-use day-out task](benchmarks/first-use-day-out.md). It requires Lattice to retain a user's goal and conditions while asking for a missing spending limit, then accept a follow-up and continue without losing earlier information.

A second, unrelated task now exercises the same objective and condition representation. The laptop prompt is recognized through the same generic objective path: its explicit budget is a typed money constraint and its `I care about ...` clause is a preference. No laptop-specific workflow is introduced. The longevity and gaming-performance clauses remain preserved source text, and the current implementation still cannot compare the three candidates or infer which is more reliable.

The native baseline and its exact current rejection behavior are recorded in [first-use-day-out-baseline.md](benchmarks/first-use-day-out-baseline.md).

This contract is intentionally smaller than a planner architecture. Implementation should add further structure only when an executable task demonstrates the need.
