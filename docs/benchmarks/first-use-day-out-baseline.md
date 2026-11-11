# First-use benchmark: native baseline

## Environment

- Repository revision: `59be64b7e69109ed42bd0520a5239f13fc64c775` (the `main` revision after the benchmark specification merged)
- Runtime: in-process `LatticeRuntimeClient` and a fresh `ILatticeSession`
- Translator: disabled; no model-assisted interpretation
- External tools: not registered or invoked
- Knowledge: empty session
- Test surface: the same runtime session contract consumed by the REPL and CLI

The automated baseline cases live in `tests/Lattice.Runtime.Tests/FirstUseBenchmarkBaselineTests.cs`. They preserve the exact prompts and assert the current rejection result. The repository CI run for the PR containing this report is the execution record; this document does not claim a local .NET run.

## Observed baseline

The initial benchmark request:

> I have a free Saturday, a limited budget, and no car. I would prefer somewhere quiet and outdoors. Help me put together a good day.

is rejected before any utterance is processed:

- Turn status: `Rejected`
- Utterance results: empty
- Error: `Sentence 1: That sentence is outside LCE 0.9. Use :help to see the supported forms.`

The same first-sentence rejection occurs for the supplied budget/origin/time answer, the walking-only revision, and the laptop transfer prompt. These cases are retained as a theory in the baseline test so future changes can show exactly which natural-language tasks have become supported.

## What this establishes

1. **The current native language interface cannot accept the benchmark's initial request.** This is the first blocking gap; the runtime never reaches destination evaluation.
2. **The current clarification mechanism is narrower than the benchmark needs.** Existing clarification is for missing or ambiguous `it` references. The benchmark needs a useful question about missing task information, such as the user's spending limit.
3. **The runtime's turn model is not the blocker.** A session can accept successive submissions and retain its knowledge/context. The limitation is what the parser and intelligence can represent and do with those submissions.
4. **The current runtime result model has no general goal or information-request outcome.** Its utterance outcomes cover stored/rejected statements, answered/unsupported questions, and reference clarification.
5. **The full benchmark remains untested beyond this gate.** Candidate feasibility, cost comparison, timetable reasoning, provenance for a recommendation, and multi-turn constraint revision have not yet been evaluated end to end. The baseline must not be described as passing or failing those downstream behaviors; they are currently unreachable through the benchmark's ordinary opening request.

## Architectural implication

The first implementation increment should investigate a reusable way to interpret and represent a user's objective and identify a relevant missing detail. It should not add a day-out intent, destination type, travel command, or special-case clarification. Before implementation, define the smallest general semantic/runtime contract that can express a goal and ask for missing information, then test it against the day-out prompt and at least one unrelated task.

This is a baseline observation, not a proposed implementation design. It deliberately does not preselect a goal ontology, planner, scoring system, or natural-language architecture.
