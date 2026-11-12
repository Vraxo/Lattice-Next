# Lattice assistant benchmark runner

Run the curated assistant prompt set through Lattice's production CLI without manually entering prompts.

## Requirements

- .NET 10 SDK.
- No LM Studio server or external model is required for the symbolic runtime.

## Run

From the repository root:

```powershell
dotnet run --project tools/Lattice.Assistant.Benchmark
```

By default, this runs all source prompts once, each in a fresh session. It writes:

- `artifacts/assistant-benchmark-results.json` — complete per-run CLI output, errors, status, utterance kinds, latency, output signatures, and aggregate metrics.
- `artifacts/assistant-benchmark-results.md` — readable summary, category and evaluation-layer breakdowns, per-prompt stability, and individual output summaries.

## Options

```powershell
dotnet run --project tools/Lattice.Assistant.Benchmark -- --help
dotnet run --project tools/Lattice.Assistant.Benchmark -- --repeat 5
dotnet run --project tools/Lattice.Assistant.Benchmark -- --category classification
dotnet run --project tools/Lattice.Assistant.Benchmark -- --layer real-world-instruction
dotnet run --project tools/Lattice.Assistant.Benchmark -- --id dolly-001 --id dolly-002
dotnet run --project tools/Lattice.Assistant.Benchmark -- --repeat 5 --fail-on-unhandled
```

The repeat count must be between 1 and 100. The default is one. Every repetition creates a new CLI session, so knowledge and conversation state never leak between cases.

## What the results mean

The runner reports whether the CLI handled a turn, the resulting utterance kinds, the output, latency, and whether repeated turn outputs were identical. A handled turn is **not** proof of factual accuracy or instruction satisfaction. The prompt set does not yet contain human-reviewed expected meanings or reference answers, so the runner intentionally does not manufacture an accuracy score.

Use one repetition for ordinary development checks. Increase repetitions when investigating nondeterminism or session leakage; repeating deterministic cases many times does not improve the quality of the benchmark. The separate synthetic diagnostics file is not automatically mixed into this source-prompt run.
