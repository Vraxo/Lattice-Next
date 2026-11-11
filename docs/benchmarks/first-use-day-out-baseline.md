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

For the day-out request, the runtime now:
- retains the objective and original request;
- identifies the unresolved spending-limit condition;
- returns `ClarificationRequired` with the condition's missing-value prompt;
- retains the pending request in session state;
- accepts a standalone typed amount such as `$25 total.` on the next turn;
- attaches the value and answer source to the original condition, clears the pending request, and leaves the general knowledge base unchanged.

An unrelated assertion can be processed while the request remains pending. A currency amount supplied without an outstanding information request is not stored as a fact.

This is a generic session mechanism driven by the condition's expected value kind and prompt metadata. The follow-up now accepts the full benchmark turn: the spending limit is bound to the pending money request, while lunch, origin, and the departure/return window become additional typed objective conditions. The subsequent walking-only revision adds a bus exclusion and a walking transport mode while preserving the original no-car condition and all previously supplied constraints. Each answer remains in objective/session state rather than the general knowledge base. This is still bounded parsing and condition retention—not candidate evaluation or planning.

## What this establishes—and what it does not

1. **The first natural-language boundary has moved.** Lattice can now identify a requested outcome in two unrelated prompts and retain the full source context.
2. **Condition understanding is partial.** Budget, transport exclusion, and one preference clause are represented; most surrounding details and all laptop requirements remain preserved source text rather than typed conditions.
3. **Clarification and revision now cover the benchmark conversation.** A missing money value can be requested and supplied, other supplied details are retained as typed conditions, and transport restrictions can be revised without losing unrelated conditions. This does not yet establish a general multi-slot clarification queue or unrestricted natural-language revision.
4. **Candidate evaluation and planning remain untested.** No feasibility comparison, recommendation, trade-off analysis, or plan has been performed. These downstream capabilities must not be implied by objective recognition.
5. **The laptop prompt now verifies cross-domain condition capture.** The same generic path retains its explicit $1,000 spending limit and its screen-quality/reliability/repairability preference. No laptop-specific workflow is used. Longevity and the lack of gaming need remain in preserved source text; no candidate comparison or reliability conclusion has been performed.

## Candidate-data representation boundary

The current knowledge-pack path can store unary categorical claims and binary relations about named individuals. For example, it can store `Fernbank is a reserve.` and `Fernbank costs six-dollars.`, then answer an exact query for that same symbolic relation. The second argument in the cost relation is an `IndividualTerm` identifier—not a decimal or money value.

The current assertion path rejects a money literal such as `Fernbank costs $6.`, and the native parser does not accept a comparative query such as `Does Fernbank cost less than ten-dollars?`. The reasoning engine performs exact ground-predicate lookup and its existing unary derivation; it has no typed numeric comparison or general candidate evaluation.

Consequently, the candidate table cannot yet be represented faithfully enough to compare transport cost, entry price, journey duration, activity duration, and time windows against an objective. Qualitative facts can be stored symbolically, but that is not sufficient to establish feasibility or rank options. No destination recommendation has been produced by Lattice.

## Next capability

The next increment should introduce only the smallest reusable representation and reasoning mechanism needed to express candidate attributes with typed values and evaluate them against objective conditions. It should preserve unknown attributes, explain which conditions are supported or unresolved, and be tested on the day-out candidates before attempting any planner or numerical scoring framework. A cross-domain check should follow before the mechanism is considered general.

See [the objective and clarification contract](../objectives.md) and [the day-out benchmark specification](first-use-day-out.md).
