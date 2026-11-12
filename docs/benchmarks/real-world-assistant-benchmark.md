# Real-world assistant benchmark

## Decision

Lattice's first assistant benchmark is deliberately small: **30–40 carefully selected source prompts**, plus a separate diagnostic suite of controlled variations. This is a development benchmark, not a statistically representative estimate of general assistant capability.

The benchmark has three distinct layers:

1. **Real-world instruction** — prompts people actually submitted in conversational settings. OASST1 and WildChat are sources; preserve the original wording and provenance.
2. **2022-era capability** — human-authored instruction examples organized around task categories also represented in the InstructGPT evaluation. Dolly 15k is a useful public proxy, not a copy of InstructGPT's private prompt set.
3. **Diagnostics** — developer-written paraphrases and controlled changes to constraints, negation, ambiguity, references, and follow-ups. These are never counted as real prompts.

The initial seed file is [assistant-benchmark-v1.json](../../data/benchmarks/assistant-benchmark-v1.json). Its status is **candidate seed, not frozen**. It currently contains 34 source prompts: 4 OASST1 prompts and 30 Dolly prompts. The earlier WildChat sample was removed because its conversation hash was not retained, leaving its source locator too weak for a reproducible benchmark. The source mix remains intentionally disclosed rather than presented as balanced: the public viewer material available during this curation pass was insufficient to build the originally proposed 15/15/10 split without inventing or rewriting source examples. The current set is therefore a practical starting point, not the final held-out evaluation set.

Controlled diagnostic cases live in [assistant-benchmark-diagnostics-v1.json](../../data/benchmarks/assistant-benchmark-diagnostics-v1.json). They are synthetic and must be scored separately.

## Why these sources

### OASST1 — real conversational prompts

[OpenAssistant/oasst1](https://huggingface.co/datasets/OpenAssistant/oasst1) is a human-generated and human-annotated conversation corpus collected through a public community platform. It contains conversation trees, not independent flat prompts. For single-turn instruction evaluation, use root prompter messages as independent prompts. Follow-up messages belong in the multi-turn/diagnostic suite with their preceding context.

OASST1 is community-contributed and self-selected; it is not a representative sample of ChatGPT users. The dataset is licensed Apache-2.0.

### WildChat-1M — real ChatGPT prompts

[allenai/WildChat-1M](https://huggingface.co/datasets/allenai/WildChat-1M) contains real user conversations with ChatGPT, but its population is self-selected and its collection period is 2023–2024, not 2022. It is a useful source of actual ChatGPT usage, not a reproduction of the early ChatGPT user population.

WildChat was investigated as a source, but no record is currently retained in the seed: the sample available during initial curation lacked its conversation hash, so it could not be traced reproducibly. Add WildChat examples only when stable record identifiers and source metadata are captured. Check the dataset card for the current license and release details before redistributing data.

### Dolly 15k — 2022-era task-category proxy

[databricks/databricks-dolly-15k](https://huggingface.co/datasets/databricks/databricks-dolly-15k) contains 15,011 human-authored instruction/response examples created by Databricks employees. Contributors were asked to create prompts in task categories informed by InstructGPT, including brainstorming, classification, open/closed QA, generation, information extraction, and summarization. These are elicited instructions, not spontaneous requests from ordinary ChatGPT users. The dataset uses CC BY-SA 3.0.

### InstructGPT — historical reference

The [2022 InstructGPT paper](https://arxiv.org/abs/2203.02155) reports the distribution of request types in real API user prompts, but does not release the original evaluation prompt corpus. Its categories are a useful historical guide; they do not make Dolly or any other public dataset equivalent to that private corpus.

## Curation rules

- Keep original source wording. Do not rewrite source prompts to fit Lattice's current grammar.
- Keep source identity, source URL, and the strongest available source locator with every prompt.
- Use OASST1 root messages for independent single-turn cases. Do not treat follow-up messages as independent prompts.
- Label solicited human-authored instructions (Dolly) separately from naturally submitted conversation prompts (OASST1/WildChat).
- Keep prompts that need a supplied passage paired with that passage; do not silently remove required context.
- Exclude personal data and unsafe material from a public benchmark release.
- Deduplicate exact and near-duplicate prompts.
- Maintain a separate held-out set once the candidate pool is large enough. Do not leak diagnostic paraphrases across development and held-out partitions.
- Freeze and version benchmark releases; record every change.

## Evaluation

Score two outcomes separately.

### 1. Language understanding

Did Lattice correctly identify the user's intent, entities, relations, requested action, output form, constraints, negation, modality, preferences, references, and uncertainty?

A successful parse or recognized keyword is not sufficient. Compare the interpreted meaning against a human-reviewed semantic annotation. Accept equivalent representations.

### 2. Task usefulness

Did Lattice produce a useful, appropriately grounded response? Use task-specific human review. A correct interpretation can still lead to a poor answer, and a plausible answer can conceal a misunderstanding.

Do not collapse these into one score. Report category-level results and important failure types, including unsupported requests, unsafe assumptions, and appropriate abstentions.

## Diagnostics

The diagnostic file contains controlled variations derived from selected source prompts. It probes paraphrasing, added constraints, negation, classification, context references, and follow-ups. Diagnostic results must be reported separately from real-prompt results.

## Acquisition

Do not download entire datasets or all Parquet shards for this benchmark. The initial curation used small public viewer samples and retained only selected prompt text and provenance. The Hugging Face dataset viewer and its rows API can be used to inspect additional candidates in small batches. If a viewer/API route is unavailable, do not substitute invented examples or claim a representative sample.

## Next curation step

Continue expanding the real-world layer only when we can obtain more traceable English root prompts from OASST1 and stable single-turn records from WildChat. Aim for roughly 30–40 prompts overall, but do not pad the set with weakly sourced or synthetic items merely to hit the target. The current 34-prompt seed is useful for early development, but its source imbalance means it must not yet be treated as a frozen benchmark.

The long-term objective remains to test general language understanding without a neural translator, not to hardcode the prompts in this file.


## Automated execution

The candidate benchmark can be executed against the actual production CLI without manually entering prompts. From the repository root:

```powershell
dotnet run --project tools/Lattice.Assistant.Benchmark
```

This runs every source prompt once. Each prompt is submitted through `LatticeCli.RunAsync`, and every repetition receives a fresh in-process session. The runner writes:

- `artifacts/assistant-benchmark-results.json` — full CLI JSON output, standard error, exit status, utterance kinds, response summaries, latency, per-run outcome signatures, benchmark hash, and aggregate metrics.
- `artifacts/assistant-benchmark-results.md` — readable run summary, category/layer breakdowns, per-prompt handling and stability, and individual output summaries.

Useful options:

```powershell
dotnet run --project tools/Lattice.Assistant.Benchmark -- --repeat 5
dotnet run --project tools/Lattice.Assistant.Benchmark -- --category classification
dotnet run --project tools/Lattice.Assistant.Benchmark -- --layer real-world-instruction
dotnet run --project tools/Lattice.Assistant.Benchmark -- --id dolly-001 --id dolly-002
dotnet run --project tools/Lattice.Assistant.Benchmark -- --repeat 5 --fail-on-unhandled
```

The default repeat count is one. Repetitions are useful for detecting instability, but deterministic identical runs do not add evidence of answer quality. The runner reports **handled** when the CLI completes a turn without rejecting it; this is an execution metric, not a correctness score. There are currently no gold answers or human-reviewed semantic annotations for this prompt set, so the runner deliberately does not claim an accuracy percentage. The separate diagnostics JSON is not silently mixed into this source-prompt run.

The runner itself is included in `Lattice.slnx` and has integration tests verifying fresh-session isolation, report generation, filtering, and invalid benchmark rejection.


## Conversational question semantics follow-up

The initial benchmark run accepted 2 of 34 prompts, but neither accepted prompt received a substantive answer. A first language-layer increment now represents simple definition questions such as “What is a polygon?” as a typed `DefinitionQuestionFormula`, preserving the concept and any descriptive modifiers. This is only a parsing result: it does not claim that Lattice can retrieve, formulate, or verify a definition. The benchmark runner's handled count must not be interpreted as answer quality.


## Definition-question retrieval increment

The first definition-question parser recognizes a typed request but does not itself provide an answer. The runtime now looks for an exact `definition` text attribute in the loaded knowledge packs and returns its source provenance. If no exact definition exists, or multiple distinct definitions conflict, the runtime reports that information as unavailable rather than inventing an answer. The source benchmark still contains no supplied knowledge packs, so its definition prompts correctly remain unanswered there; the runtime behavior is covered separately by knowledge-pack integration tests.
