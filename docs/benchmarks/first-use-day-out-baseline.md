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

## Current checkpoint: first-class objective capture

The first objective implementation adds a bounded native construction for a contextual request containing a sentence beginning with **"Help me ..."**. It recognizes the following goals:

- Day out: `put together a good day`
- Laptop comparison: `compare these three options`

For both, the runtime now:
- returns `Completed` with an `ObjectiveReceived` utterance result;
- stores the extracted goal separately from factual assertions and questions;
- retains the complete original request in session state, exposed through `LatticeSessionStatus`;
- leaves the knowledge base unchanged.

The same objective representation is used for both domains. This is only objective capture and retention. Lattice explicitly reports that it has not interpreted the surrounding conditions.

The other two prompts remain unsupported:
- the budget/origin/time follow-up is rejected;
- the walking-only revision is rejected.

The current executable checkpoint is covered by `tests/Lattice.Runtime.Tests/FirstUseBenchmarkBaselineTests.cs` and `ObjectiveSessionTests.cs`. The tests run through the in-process runtime with no translator and no external tools.

## What this establishes—and what it does not

1. **The first natural-language boundary has moved.** Lattice can now identify a requested outcome in two unrelated prompts and retain the full source context.
2. **The surrounding context is not yet understood.** Statements about a free Saturday, budget, transport, preferences, product requirements, and price are preserved as text, not represented as typed constraints.
3. **Clarification is still missing.** The runtime cannot yet ask for task information, associate an answer with an outstanding request, or revise a condition across turns.
4. **Candidate evaluation and planning remain untested.** No feasibility comparison, recommendation, trade-off analysis, or plan has been performed. These downstream capabilities must not be implied by objective recognition.
5. **The laptop prompt is only a transfer check for objective capture.** It does not demonstrate laptop comparison or a general selection capability.

## Next capability

The next increment should add typed hard constraints and preferences, followed by a generic missing-information lifecycle driven by a capability's actual information requirements. It must preserve unknown information, keep the objective across turns, and avoid domain-specific clarification rules. Candidate evaluation should follow only when the necessary conditions and candidate data can be represented faithfully.

See [the objective and clarification contract](../objectives.md) and [the day-out benchmark specification](first-use-day-out.md).
