# Real-world assistant benchmark

## Decision

Lattice's next major language-understanding evaluation should be grounded in **real prompts written by people using conversational AI**, not primarily in prompts invented by the developers.

Use public conversation datasets as source material, curate a reproducible prompt set, and evaluate Lattice's understanding separately from its ability to complete the requested task.

The benchmark is intended to answer a practical question:

> How well can an ordinary person communicate an unfamiliar request to Lattice, in their own words, and have Lattice understand what they want?

The initial target is **single-turn instruction understanding**, with multi-turn continuity evaluated as a later, separate capability. This is deliberately a model-independent benchmark: a neural translator is not required or assumed.

## Why this benchmark

The InstructGPT work is a useful historical reference because its evaluation included prompts submitted by real API users, with held-out customer prompts used for evaluation. The paper reports a distribution of request types rather than evaluating only conventional question-answering tasks. Its published categories include generation, open-ended questions, brainstorming, conversation, rewriting, summarization, classification, other, closed questions, and extraction.

The original InstructGPT user-prompt corpus was not released. The examples in the paper are illustrative, not a downloadable copy of the actual prompts. Therefore, Lattice cannot reproduce the original test set exactly. We can use its published request distribution as a historical reference for the kinds of tasks people brought to early instruction-following assistants.

Primary references:

- Ouyang et al., [Training language models to follow instructions with human feedback](https://arxiv.org/abs/2203.02155), 2022.
- OpenAI, [Introducing ChatGPT](https://openai.com/index/chatgpt/), 2022.

## Prompt sources

### Primary: WildChat-1M

Use [allenai/WildChat-1M](https://huggingface.co/datasets/allenai/WildChat-1M) as the initial primary source.

WildChat contains approximately 838,000 conversations in the currently published dataset viewer, collected from real users interacting with ChatGPT. It includes conversations involving multiple ChatGPT model variants, detected language, timestamps, and structured user/assistant messages. The currently published version excludes conversations flagged as toxic by the dataset's moderation filters and has an ODC-BY license.

Relevant references:

- Zhao et al., [WildChat: 1M ChatGPT Interaction Logs in the Wild](https://arxiv.org/abs/2405.01470).
- [WildChat-1M dataset card](https://huggingface.co/datasets/allenai/WildChat-1M).

WildChat is not a random sample of all assistant users. Its users self-selected into a public ChatGPT access platform, and the conversations date from 2023–2024 rather than 2022. It is nevertheless a large, real-world source of natural requests. Where possible, inspect and report model and date distributions; do not describe it as an exact reproduction of the 2022 ChatGPT user population.

### Historical task distribution: InstructGPT

Use the request-type distribution reported in the 2022 InstructGPT paper as a reference when deciding how to balance the curated benchmark.

The reported proportions are approximate and sum to 99.9% because of rounding:

| Request type | Reported share |
| --- | ---: |
| Generation | 45.6% |
| Open-ended questions | 12.4% |
| Brainstorming | 11.2% |
| Conversation | 8.4% |
| Rewriting | 6.6% |
| Summarization | 4.2% |
| Classification | 3.5% |
| Other | 3.5% |
| Closed questions | 2.6% |
| Extraction | 1.9% |

These categories are a sampling guide, not a claim that WildChat has the same distribution. Preserve the source dataset's original category-free prompts; benchmark categories are annotations added during curation.

### Supplementary: LMSYS-Chat-1M

Use [lmsys/lmsys-chat-1m](https://huggingface.co/datasets/lmsys/lmsys-chat-1m) as a supplementary source for diversity and cross-checking. It contains real conversations collected through the Vicuna demo and Chatbot Arena.

Reference: [LMSYS-Chat-1M paper](https://arxiv.org/abs/2309.11998).

LMSYS is also self-selected and reflects users specifically choosing to interact with or compare chatbots. Do not merge its prompts indiscriminately with WildChat or treat the combined set as a statistically representative sample of the general population. Record each prompt's source and sampling method.

## Initial benchmark construction

Start with a **curated candidate set of approximately 1,000 single-turn English prompts**, not the entire source corpus.

This is an initial working target, not a claim that 1,000 prompts are sufficient for a definitive population-level score. Expand it if category coverage, domain diversity, or confidence intervals require more examples.

Selection principles:

1. Preserve the user's original wording. Do not paraphrase prompts into the controlled language Lattice already supports.
2. For the single-turn benchmark, select the first user message from conversations containing one user/assistant exchange. Keep multi-turn conversations for a later continuity benchmark.
3. Include ordinary, mundane requests as well as difficult, compound, ambiguous, and unusual ones.
4. Cover the InstructGPT request categories, but do not force every prompt into exactly one category when multiple labels are justified.
5. Include variation in length, vocabulary, grammar, explicitness, and domain.
6. Deduplicate exact and near-duplicate prompts, while avoiding over-cleaning natural wording.
7. Exclude or manually review prompts containing personal data, sensitive information, unsafe material, or content unsuitable for a public test corpus. The source's de-identification and moderation are useful safeguards, not a guarantee that every row is safe to republish.
8. Keep source dataset, source row/conversation identifier, model, date, language, selection rationale, and annotation provenance for every retained prompt.
9. Split by conversation/source identity into development and held-out evaluation sets. Do not let paraphrases or near-duplicates leak across the split.
10. Freeze and version benchmark releases. Any prompt additions, removals, or annotation changes must be documented.

The initial set should be sampled from real data, not composed to demonstrate features we already implemented. Developer-written paraphrases and adversarial variants can be added as a clearly separated diagnostic suite; they must not be presented as real user prompts.

## Evaluation dimensions

Measure at least two separate outcomes.

### 1. Language understanding

Did Lattice correctly identify what the user meant?

Evaluate whether it correctly represents, where applicable:

- The requested action, question, or desired outcome.
- Entities, concepts, properties, and relationships.
- Participants and their roles in actions or events.
- Negation, modality, uncertainty, and preference strength.
- Time, quantities, comparisons, and constraints.
- References and implicit information that can be safely resolved.
- Multiple requirements and how they relate.
- Missing information, ambiguity, and cases where it should ask for clarification or abstain.

A parse success, recognized keyword, or syntactically valid semantic object is not by itself evidence of understanding. Compare the resulting meaning against a human-reviewed semantic annotation, and allow multiple equivalent representations where appropriate.

### 2. Task usefulness / completion

Did Lattice respond in a way that actually helps with the user's request?

Use task-appropriate rubrics and human review. A correct interpretation can still lead to a poor answer, and a plausible answer can conceal a misunderstanding. Keep these scores separate.

For prompts that are not answerable from available knowledge, evaluate whether Lattice accurately identifies what is missing rather than inventing facts. Do not penalize a system for refusing to claim knowledge it does not have.

### Generalization safeguards

- Keep development prompts separate from a held-out test set.
- Include domain and vocabulary variation not used to implement a capability.
- Track results by category and difficulty as well as aggregate score.
- Report unsupported inputs, incorrect interpretations, unsafe assumptions, and appropriate abstentions separately.
- Never hide critical failures inside a single average.
- A new capability should improve a family of prompts, including held-out examples, not merely the motivating example.
- Use synthetic knowledge packs only when a task requires supplied facts. Do not confuse a knowledge-pack reasoning test with a language-understanding test.

## Data exploration and acquisition

**Do not download all Parquet shards just to inspect or curate the initial benchmark.** The WildChat dataset is available through Hugging Face Data Studio, including its browser-based SQL Console, which runs DuckDB WASM and can query the Parquet-backed dataset. Query results can be downloaded as CSV or Parquet.

Useful links:

- [WildChat-1M Data Studio](https://huggingface.co/datasets/allenai/WildChat-1M)
- [Hugging Face Data Studio documentation](https://huggingface.co/docs/hub/data-studio)
- [SQL Console documentation](https://huggingface.co/docs/hub/datasets-viewer-sql-console)

Inspect the schema and available model values first. The dataset's `conversation` field is a list of structured messages; `turn` counts user/assistant exchanges, and `language` is the conversation's most frequently detected language.

A starting DuckDB query for inspecting the available model distribution among one-exchange English conversations:

```sql
SELECT model, COUNT(*) AS conversations
FROM train
WHERE turn = 1
  AND language = 'English'
GROUP BY model
ORDER BY conversations DESC;
```

A starting query for viewing first user messages:

```sql
SELECT
    conversation_hash,
    model,
    timestamp,
    language,
    conversation[1].content AS prompt
FROM train
WHERE turn = 1
  AND language = 'English'
  AND conversation[1].role = 'user'
  AND LENGTH(TRIM(conversation[1].content)) BETWEEN 20 AND 2000
LIMIT 100;
```

These are exploratory queries. Confirm the column names and nested-field syntax in the current SQL Console before relying on them. Inspect the returned model values before deciding whether to focus on a particular early ChatGPT-era model. Export only selected candidate rows; retain provenance and review them before adding them to the repository.

The full local dataset may become useful for reproducible large-scale sampling, custom analysis, or offline curation. It is not a prerequisite for beginning.

## Development implications

The benchmark should guide development, not become a list of features to hardcode.

For each cluster of failures:

1. Identify the underlying missing capability.
2. Define the language-independent meaning that needs to be represented.
3. Implement a reusable interpretation or semantic mechanism.
4. Test it against the motivating prompts and unrelated prompts that require the same capability.
5. Evaluate on held-out real prompts.
6. Verify that the interpreted meaning composes with Lattice's existing context, knowledge, and reasoning mechanisms.

The initial milestone is single-turn natural-language instruction understanding without a neural translator. Multi-turn dialogue, conversational memory, and broader task completion remain subsequent milestones. The benchmark does not require Lattice to imitate a human or to answer every request; it requires it to understand ordinary requests reliably, act when it can, and handle its limitations honestly.

## Decision status

This document records the chosen evaluation direction and initial source strategy. Exact sample size, category quotas, model/date filters, annotation schema, and release format remain to be finalized after inspecting the real dataset and reviewing a pilot sample.
