# Real-world assistant benchmark

## Decision

Lattice's first assistant benchmark is deliberately small: **30–40 carefully selected source prompts**, plus a separate diagnostic suite of controlled variations. This is a development benchmark, not a statistically representative estimate of general assistant capability.

The benchmark has three distinct layers:

1. **Real-world instruction** — prompts people actually submitted in conversational settings. OASST1 and WildChat are sources; preserve the original wording and provenance.
2. **2022-era capability** — human-authored instruction examples organized around task categories also represented in the InstructGPT evaluation. Dolly 15k is a useful public proxy, not a copy of InstructGPT's private prompt set.
3. **Diagnostics** — developer-written paraphrases and controlled changes to constraints, negation, ambiguity, references, and follow-ups. These are never counted as real prompts.

The initial seed file is [assistant-benchmark-v1.json](../../data/benchmarks/assistant-benchmark-v1.json). Its status is **candidate seed, not frozen**. It currently contains 33 source prompts: 2 OASST1 prompts, 1 WildChat prompt, and 30 Dolly prompts. This mix is intentionally disclosed rather than presented as balanced: the public viewer material available during this curation pass was insufficient to build the originally proposed 15/15/10 split without inventing or rewriting source examples. The current set is therefore a practical starting point, not the final held-out evaluation set.

Controlled diagnostic cases live in [assistant-benchmark-diagnostics-v1.json](../../data/benchmarks/assistant-benchmark-diagnostics-v1.json). They are synthetic and must be scored separately.

## Why these sources

### OASST1 — real conversational prompts

[OpenAssistant/oasst1](https://huggingface.co/datasets/OpenAssistant/oasst1) is a human-generated and human-annotated conversation corpus collected through a public community platform. It contains conversation trees, not independent flat prompts. For single-turn instruction evaluation, use root prompter messages as independent prompts. Follow-up messages belong in the multi-turn/diagnostic suite with their preceding context.

OASST1 is community-contributed and self-selected; it is not a representative sample of ChatGPT users. The dataset is licensed Apache-2.0.

### WildChat-1M — real ChatGPT prompts

[allenai/WildChat-1M](https://huggingface.co/datasets/allenai/WildChat-1M) contains real user conversations with ChatGPT, but its population is self-selected and its collection period is 2023–2024, not 2022. It is a useful source of actual ChatGPT usage, not a reproduction of the early ChatGPT user population.

The one WildChat seed record was supplied from a small viewer sample. Its conversation hash was not retained, so its provenance is weaker than the OASST1 records and should be replaced with a fully traceable record when a stable identifier is available. Check the dataset card for the current license and release details before redistributing data.

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

Expand the real-world layer only when we can obtain more traceable English root prompts from OASST1 and stable single-turn records from WildChat. Aim for roughly 30–40 prompts overall, but do not pad the set with weakly sourced or synthetic items merely to hit the target. The current 33-prompt seed is useful for early development, but its source imbalance and weak WildChat locator mean it must not yet be treated as a frozen benchmark.

The long-term objective remains to test general language understanding without a neural translator, not to hardcode the prompts in this file.
