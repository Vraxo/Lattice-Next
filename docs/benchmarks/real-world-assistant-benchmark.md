# Real-world assistant benchmark

## Purpose and status

This is a small, curated development seed for measuring general language understanding and assistant usefulness. It is **not** a statistically representative sample and is **not** a held-out accuracy benchmark.

The current source file is [`assistant-benchmark-v1.json`](../../data/benchmarks/assistant-benchmark-v1.json). It contains 40 source prompts:

| Source | Records | Share |
|---|---:|---:|
| OASST1 | 8 | 20% |
| Dolly 15k | 30 | 75% |
| WildChat | 2 | 5% |
| **Total** | **40** | **100%** |

The source mix is deliberately disclosed: Dolly accounts for three quarters of the current set. The JSON currently marks 37 records with `substantive_answer_reviewed: true`; 3 records remain unreviewed. This marker is a limited coverage-review field, not a complete semantic or factual quality score. The dataset status remains `candidate-seed-not-frozen`.

Synthetic controlled variations are stored separately in [`assistant-benchmark-diagnostics-v1.json`](../../data/benchmarks/assistant-benchmark-diagnostics-v1.json). Never combine diagnostic counts with source-prompt counts.

## Sources and provenance

- **OASST1** — community-submitted conversation prompts. Use root prompter messages for independent single-turn cases; preserve message IDs and source metadata. The source is self-selected, not population-representative.
- **WildChat** — real ChatGPT conversations collected in 2023–2024. Preserve conversation IDs, turn numbers, model, timestamp, language, moderation flags, and license metadata. This benchmark contains only two English single-turn prompts.
- **Dolly 15k** — human-authored instructions solicited from Databricks contributors and organized using task categories informed by InstructGPT. It is a task-category proxy, not a sample of spontaneous ChatGPT usage.
- **InstructGPT** — the 2022 paper provides a historical reference for task categories; its original evaluation prompt corpus is not publicly released.

Keep original prompt wording and the strongest available source locator. Preserve any supplied context needed to answer a prompt. Deduplicate exact and near-duplicate examples, exclude personal data and unsafe material, and retain licensing/source metadata. Do not substitute synthetic prompts for missing real-source examples or claim representativeness.

## Evaluation

Evaluate two distinct questions:

1. **Language understanding:** did Lattice preserve the request's intent, entities, relations, requested action, output form, constraints, negation, modality, preferences, references, and uncertainty? Compare against human-reviewed semantic annotations, accepting equivalent semantic representations.
2. **Task usefulness:** did the response meaningfully satisfy the request while remaining grounded, appropriately uncertain, and traceable to evidence? Use task-specific human review.

Do not collapse these into one score. Report category-level results and failure types, including unsupported requests, unsafe assumptions, and appropriate abstentions. A fluent answer or successful parse is not proof of correct understanding.

## Automated execution

Run the current source set through the production CLI-backed runner:

```powershell
dotnet run --project tools/Lattice.Assistant.Benchmark
```

The runner uses a fresh CLI session for each prompt repetition and writes:

- `artifacts/assistant-benchmark-results.json` — per-run CLI output, errors, status, utterance kinds, latency, output signatures, benchmark hash, and aggregate metrics.
- `artifacts/assistant-benchmark-results.md` — human-readable summary and per-prompt/category breakdowns.

Useful options:

```powershell
dotnet run --project tools/Lattice.Assistant.Benchmark -- --repeat 5
dotnet run --project tools/Lattice.Assistant.Benchmark -- --category classification
dotnet run --project tools/Lattice.Assistant.Benchmark -- --layer real-world-instruction
dotnet run --project tools/Lattice.Assistant.Benchmark -- --id dolly-001 --id dolly-002
dotnet run --project tools/Lattice.Assistant.Benchmark -- --repeat 5 --fail-on-unhandled
```

The runner reports handling, answered utterances, marked substantive answers, stability, and latency. **Handled** means the CLI completed the turn; it does not mean the request was understood or answered well. **Answered** records a `QuestionAnswered` response; it does not prove factual correctness. The substantive-answer marker is a manually maintained coverage indicator, not a semantic rubric or accuracy score. The source benchmark currently has no full criterion-level human-reviewed quality rubric.

## Next evaluation work

- Preserve the current 40-prompt set and its source provenance while improving source balance through traceable additions.
- Add human-reviewed semantic expectations for a stratified subset, including explicit expected abstentions.
- Add task-specific answer-quality rubrics and bind every review to the exact prompt version, output, and system revision.
- Keep the synthetic diagnostics and task-transfer fixtures separately reported.

The active quality-evaluation implementation work is tracked in [`.agents/todo.md`](../../.agents/todo.md). See also the [first-use task benchmarks](first-use-day-out.md) and [product vision](../vision.md).
