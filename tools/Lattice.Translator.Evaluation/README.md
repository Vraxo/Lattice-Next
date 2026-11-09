# Lattice translator evaluation

Run the configured LM Studio translator against the repository's natural-language-to-LCE benchmark. The runner uses the production `SemanticTranslationService`, LCE parser, and semantic validator. It never accepts proposals or writes to the knowledge base.

## Requirements

- .NET 10 SDK.
- LM Studio and its local server; the adapter can start the server and load the configured model through `lms`.
- A valid `lattice.config.json` created by running `:translator setup` in the Lattice CLI.

## Run

From the repository root:

```powershell
dotnet run --project tools/Lattice.Translator.Evaluation -- --repeat 5
```

Options:

```powershell
dotnet run --project tools/Lattice.Translator.Evaluation -- --category unsupported --repeat 10
dotnet run --project tools/Lattice.Translator.Evaluation -- --config .\lattice.config.json --output .\artifacts\gemma-results.json
dotnet run --project tools/Lattice.Translator.Evaluation -- --help
```

The default benchmark is `tests/Lattice.Core.Tests/Intelligence/translator-benchmark.json`. The default output is `translator-evaluation-results.json`. The process exits 0 only when all executed cases match their expected canonical meaning or safely abstain; it exits 1 for evaluation failures and 2 for configuration/invocation errors. Ctrl+C writes the results collected so far.

## Report and interpretation

The JSON report records the UTC run time, configured model key/identifier, API URL, reasoning setting, decoding settings, benchmark SHA-256, every case/repetition, expected and actual canonical meanings, response message, outcome, latency, and pass/fail.

Comparison uses `SemanticFormatter.Format` after the real adapter/parser/validator pipeline, so harmless LCE surface differences are accepted when the canonical meaning is identical. A supported case passes only with exactly one expected candidate. An unsupported or ambiguous case passes only when the service returns no interpretation.

The benchmark includes context-dependent pronoun cases. This runner tests only translation-boundary preservation of unresolved `it`; it does not simulate conversation history or test ConversationContext's referent resolution/clarification. Those require separate integration tests.

The report records the model key and identifier as supplied by LM Studio. For meaningful comparisons, also record the model revision and quantization, since these identifiers can be mutable. The benchmark is a specification, not a claim that every expected case is currently supported; failures are reported, never repaired or hidden.
