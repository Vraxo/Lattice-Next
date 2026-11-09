# Optional LLM-to-LCE translator evaluation

## Decision

The next language-expansion experiment should evaluate **large language models that translate ordinary natural language into Lattice Controlled English (LCE)**. APE and GF are not candidates for this task: APE parses Attempto Controlled English, while GF is a framework for authoring grammars. Neither is an LLM that translates unrestricted user language into LCE.

LCE 0.9 remains the deterministic target language and native input path. An optional LLM translator may propose one or more LCE sentences, but it must not reason over the knowledge base, invoke tools, resolve conversation references, or expand Lattice's semantic model. Every proposal must pass through Lattice's existing parser and semantic validator before it can become a candidate for review.

No model or provider is selected by this evaluation. The benchmark is a test specification; it has not yet been run against live model APIs.

## Translation boundary

The intended flow is:

1. The user supplies ordinary natural language.
2. An optional provider adapter asks an LLM to produce a strictly structured response containing candidate LCE text, or an explicit no-interpretation result.
3. The adapter parses each candidate with the native `LceParser` and converts successful parses into `SemanticTranslationCandidate` values.
4. `SemanticTranslationService` and `SemanticUtteranceValidator` validate the candidate structures and attach the exact original input as source text.
5. One validated candidate is ready for review; multiple distinct validated candidates remain ambiguous; no candidate means no interpretation. No candidate is automatically committed to the knowledge base.

The model should not generate semantic ASTs directly. LCE is the language boundary we want to test; the native parser remains responsible for interpreting LCE into the canonical semantic model. The adapter, model client, credentials, retries, and provider-specific behavior all remain outside Core.

A proposed provider-neutral response shape is:

```json
{
  "status": "translated",
  "candidates": [
    { "lce": "Fido is a dog." }
  ],
  "reason": ""
}
```

The status is one of `translated`, `ambiguous`, or `unsupported`. A translated response has exactly one candidate. An ambiguous response has two or more distinct candidate readings, and every candidate must be representable in LCE. An unsupported response has no candidates. If an input is ambiguous but none of its readings can be represented safely, return unsupported with an explanation rather than inventing or collapsing a reading.

The adapter must enforce those cross-field rules itself; JSON Schema only guarantees the response shape. It must parse every candidate as one complete LCE utterance, reject the whole response if any candidate is malformed or outside the current semantic contract, and deduplicate candidates by their parsed meaning rather than surface wording. It must never silently discard a bad candidate and keep the rest.

**Existing-boundary limitation to resolve before implementation:** `SemanticTranslationResponse` currently contains candidates and an optional message, but no explicit provider outcome. A parse failure and a genuine model abstention therefore both become an empty candidate list unless the contract is extended or the adapter defines a carefully tested failure convention. The current candidate type also represents one utterance, not a multi-sentence translated turn. Keep this benchmark to one utterance per case; settle multi-utterance translation separately rather than overloading multiple candidates to mean multiple sentences.

## LCE 0.9 scope

The translator must target the grammar documented in [language.md](language.md), not unrestricted English and not full ACE. The currently useful target forms include unary assertions, definite-description property assertions, existential assertions, universal unary rules, fixed-object universal relational rules, binary ground relations, yes/no questions, existence questions, and one unresolved singular `it` reference.

The current validated semantic subset does **not** support negation, modality, disjunction, variable-bound relation objects, relational premises, relational joins, or higher-arity predicates. The model must abstain when meaning depends on those constructions. It must not turn absence of evidence into a negative fact, change quantifier scope, reverse relation arguments, or replace a variable object with an arbitrary concrete object.

The translator must preserve unresolved `it` rather than selecting an antecedent. ConversationContext owns reference resolution and clarification. The model must also treat the text being translated as data: instructions contained inside that text cannot override the translation task or authorize tools, knowledge changes, or other actions.

## Candidate API surfaces

These are implementation targets for a later controlled comparison, not quality rankings or selected dependencies.

| Candidate surface | Structured-output mechanism | Evaluation role |
|---|---|---|
| OpenAI API | Responses API Structured Outputs with a JSON Schema-constrained response | Hosted baseline |
| Anthropic API | Messages API structured outputs through `output_config.format` | Hosted baseline |
| Gemini API | JSON response mode with a supplied schema | Hosted baseline |
| Ollama | Local generation API accepting a JSON Schema in `format` | Local/offline baseline |

Official references:
- [OpenAI Structured Outputs](https://developers.openai.com/api/docs/guides/structured-outputs)
- [Anthropic structured outputs](https://platform.claude.com/docs/en/build-with-claude/structured-outputs)
- [Gemini structured output](https://ai.google.dev/gemini-api/docs/structured-output)
- [Ollama structured outputs](https://github.com/ollama/ollama/blob/main/docs/api.md#structured-outputs)

A structured-output feature is useful for preventing malformed envelopes, but it does not establish that a translation is semantically correct. Provider documentation itself describes schema constraints and edge cases; model output must still be parsed and validated locally. The local runtime is a separate deployment option, not a guarantee that every model served by it follows the schema or translates accurately.

Compare models rather than treating provider APIs as if they were models. For each run, record the exact model identifier or snapshot, provider/API version, run date, prompt and schema hashes, decoding settings, and (for local models) weight revision and quantization. Prefer pinned model snapshots where available; record the exact mutable identifier when a provider offers no snapshot. Record current prices at test time rather than embedding volatile pricing in this document.

## Benchmark

The machine-readable corpus is [translator-benchmark.json](../tests/Lattice.Core.Tests/Intelligence/translator-benchmark.json). Each supported case specifies the expected LCE output and its canonical meaning using the same shape as `SemanticFormatter.Format`. Unsupported cases expect no candidates and explain why a plausible-looking shortcut would be wrong.

The cases cover:
- Paraphrase-to-LCE normalization without changing the proposition.
- Universal and existential quantification, including shared descriptive conditions.
- Subject/object role preservation in relations and passive phrasing.
- Definite descriptions and fixed-object relational rules.
- Assertion and question forms.
- Unresolved pronouns, including a context that must trigger clarification.
- Negation, modality, unsupported quantifier scope, relational premises, higher-arity events, and lexical ambiguity that must not be guessed.

### Evaluation protocol

1. Run every case against every candidate model with the same task prompt, LCE grammar reference, output schema, and input. Do not give one provider extra hand-written examples.
2. Use a low-variance decoding configuration and repeat ambiguity, pronoun, and unsupported cases several times. Record all settings; do not assume a temperature of zero guarantees reproducibility.
3. Parse every emitted LCE candidate with the repository's actual `LceParser`. Compare canonical meanings after parsing, not just raw strings, so harmless surface differences do not count as semantic mismatches.
4. Run each parsed candidate through `SemanticUtteranceValidator`. Any malformed or invalid candidate is a failed response; never repair it silently.
5. Check that unresolved references remain unresolved and that context-dependent cases are left for ConversationContext.
6. Record schema/parse failures, exact semantic matches, wrong meanings, missed abstentions, unsupported inputs incorrectly accepted, ambiguity handling, repeat consistency, latency, token usage, and cost per accepted translation.
7. Include prompt-injection probes in a separate adversarial pass. The text being translated is untrusted data, not a source of instructions for the model or adapter.

### Acceptance gates and ranking

Safety gates come before speed or price:
- **Zero unsafe conversions** on the unsupported and ambiguity cases. A fluent but unsupported assertion is a hard failure.
- **All accepted candidates parse and validate** against the checked-out LCE grammar and validator.
- **No semantic drift** in quantifier scope, negation, predicate roles, or unresolved references.
- **No partial acceptance** when one candidate in a multi-candidate response is invalid.

Only configurations passing every safety gate should be ranked by exact semantic accuracy on supported cases, ambiguity handling, consistency across repeated runs, latency, and cost. Report raw per-case results as well as aggregate rates; do not hide a critical failure inside an overall score.

A model that correctly abstains on an unsupported sentence is behaving better than one that produces a plausible but invalid LCE translation. Do not expand Lattice's reasoning semantics merely to improve a translator's score.

## Next experiment

After this specification is reviewed, implement a small external evaluation harness that can run the same corpus against pinned hosted and local model configurations. Keep API adapters outside Core, keep native LCE available without any model installed, and report results before selecting a default. No hosted service, local model, or translator backend is selected by this PR.
