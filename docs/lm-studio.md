# Optional LM Studio translator

Lattice can optionally use a local model served by [LM Studio](https://lmstudio.ai/) to translate one ordinary-language statement or question into a proposed LCE 0.9 sentence.

This integration is deliberately outside `Lattice.Core`. The adapter implements `ISemanticsTranslator`, calls LM Studio's OpenAI-compatible chat-completions endpoint, and treats the model's response as untrusted text. Each proposed sentence must pass the native `LceParser`; the existing `SemanticTranslationService` then applies Core's semantic validator. The CLI asks for explicit acceptance before processing a proposal. The model cannot bypass parsing, validation, context resolution, or the existing knowledge-ingestion path.

## 1. Prepare LM Studio

1. Install and open LM Studio at least once.
2. Download or import an instruction-tuned GGUF model, such as Gemma 3 270M IT GGUF.
3. Load the model in LM Studio.
4. Start the local server from the Developer tab, using the default port `1234`.

The adapter uses the OpenAI-compatible `GET /v1/models` and `POST /v1/chat/completions` endpoints. The OpenAI-compatible model list reports loaded instances. To inspect a model that is already loaded, run:

```powershell
Invoke-RestMethod http://127.0.0.1:1234/v1/models
```

Use the exact `id` value returned for the loaded instance.

For the optional auto-load path, first inspect the installed-model catalog instead:

```powershell
Invoke-RestMethod http://127.0.0.1:1234/api/v1/models |
    Select-Object -ExpandProperty models |
    Select-Object key, display_name, type, loaded_instances
```

Set `LATTICE_LMSTUDIO_MODEL` to the exact `key` for the installed LLM. The adapter uses this key to find and load the model, then sends requests using the loaded instance ID. Do not assume that a Hugging Face repository name, downloaded filename, loaded instance ID, and catalog key are interchangeable.

## 2. Configure Lattice

In PowerShell, set the model identifier and start Lattice:

```powershell
$env:LATTICE_LMSTUDIO_MODEL = "the-exact-id-from-v1-models"
$env:LATTICE_LMSTUDIO_URL = "http://127.0.0.1:1234"
dotnet run --project src/Lattice.Cli
```

The URL defaults to `http://127.0.0.1:1234/`, so it can be omitted when the default server port is used. Configuration is read when Lattice starts.

Optional environment settings:

| Variable | Default | Purpose |
|---|---|---|
| `LATTICE_LMSTUDIO_MODEL` | unset | Exact loaded instance `id` from `/v1/models`, or installed model `key` from `/api/v1/models` when auto-load is enabled; required to enable translation |
| `LATTICE_LMSTUDIO_URL` | `http://127.0.0.1:1234/` | Root URL of the local LM Studio server; do not append `/v1` |
| `LATTICE_LMSTUDIO_AUTO_START` | `false` | Run `lms server start --port <port>` on first connection failure; only HTTP loopback addresses are allowed |
| `LATTICE_LMSTUDIO_AUTO_LOAD` | `false` | Load the configured model through LM Studio's native REST API if it is installed but not visible to the OpenAI-compatible endpoint |
| `LATTICE_LMSTUDIO_TIMEOUT_SECONDS` | `120` | Request/startup timeout, from 1 to 3600 seconds |
| `LATTICE_LMSTUDIO_CLI` | `lms` | CLI executable or full path used when automatic server startup is enabled |
| `LATTICE_LMSTUDIO_API_TOKEN` | unset | Optional bearer token when LM Studio API authentication is enabled |

Automatic loading requires an LM Studio version that exposes the `/api/v1/models` and `/api/v1/models/load` endpoints. Lattice does not download models automatically. The model must already exist in LM Studio's model library.

For a fully automatic first-use setup, after installing the LM Studio CLI, set:

```powershell
$env:LATTICE_LMSTUDIO_AUTO_START = "true"
$env:LATTICE_LMSTUDIO_AUTO_LOAD = "true"
```

Automatic server startup uses the configured URL's port and binds only to the local loopback interface. Lattice will not expose LM Studio to the network.

## 3. Use the translator

Start with:

```text
:translator
:translate Could Fido be a dog?
```

A single candidate is displayed as a proposed meaning and requires `y` before it is processed. If the model returns multiple distinct candidates, choose one by number. Press Enter or answer anything other than `y` to discard a single candidate; press Enter at the multi-candidate prompt to discard all candidates. Discarding a proposal leaves session knowledge unchanged.

Ordinary input is not silently sent to the model. Use `:translate` explicitly; the existing native LCE parser, tool-pattern matching, and default CLI behaviour remain unchanged.

## Scope and limitations

- One input statement or question per `:translate` request. Multi-sentence translation is not supported by this initial adapter.
- The model must produce one complete LCE 0.9 sentence per candidate.
- The current LCE grammar has no negation, disjunction, unrestricted conditionals, relational joins, or general plural/quantifier handling. The model is instructed to abstain when it cannot preserve the meaning.
- Multiple model candidates remain ambiguous until the user selects one.
- A structurally valid LCE candidate is not proof that the model preserved the source meaning. Review remains necessary.
- If the model returns malformed JSON, an unsupported LCE sentence, or an API error, no candidate is accepted.
- The adapter does not add a dependency on LM Studio, a model, an SDK, or a neural runtime to Core.

See the [LM Studio OpenAI-compatible API documentation](https://lmstudio.ai/docs/developer/openai-compat), [model-list endpoint](https://lmstudio.ai/docs/developer/openai-compat/models), [model-load endpoint](https://lmstudio.ai/docs/developer/rest/load), and [LM Studio CLI documentation](https://lmstudio.ai/docs/cli).
