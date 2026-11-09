# Optional LM Studio translator

Lattice can optionally use a local model served by [LM Studio](https://lmstudio.ai/) to translate one ordinary-language statement or question into a proposed LCE 0.9 sentence.

This integration is deliberately outside `Lattice.Core`. The adapter implements `ISemanticsTranslator` and calls LM Studio's native REST chat endpoint. Each request defaults to `reasoning: off`, so reasoning is disabled for the inference request rather than configured as a model-load option. The model's response is untrusted text: each proposed sentence must pass the native `LceParser`, and the existing `SemanticTranslationService` then applies Core's semantic validator. The CLI asks for explicit acceptance before processing a proposal. The model cannot bypass parsing, validation, context resolution, or the existing knowledge-ingestion path.

## First-time setup

1. Install LM Studio and open it at least once so its `lms` CLI is available.
2. Download or import an instruction-tuned GGUF model, such as Gemma 3 270M IT GGUF.
3. Start Lattice:

   ```powershell
   dotnet run --project src/Lattice.Cli
   ```

4. Enter:

   ```text
   :translator setup
   ```

Lattice checks the default local server at `http://127.0.0.1:1234`. If it is not reachable, Lattice starts it using `lms server start --port 1234 --bind 127.0.0.1`, waits for the API to respond, and lists installed language models from LM Studio's native `GET /api/v1/models` catalog. Embedding and decision models are excluded from the selection list.

Choose a model by number. Lattice writes the choice to `lattice.config.json` in the current working directory. The file is local configuration and is ignored by Git.

## Normal use

After setup, start Lattice normally:

```powershell
dotnet run --project src/Lattice.Cli
```

Then:

```text
:translate Could Fido be a dog?
```

Before inference, Lattice checks the configured server and model. If the model is not loaded, it runs `lms load <model-key> --identifier <model-key>` and waits until the selected identifier appears in `GET /v1/models`. It then sends the source text and system prompt to `POST /api/v1/chat`, including the configured reasoning setting (default: `off`).

A single candidate is displayed as a proposed meaning and requires `y` before it is processed. If the model returns multiple distinct candidates, choose one by number. Press Enter or answer anything other than `y` to discard a single candidate; press Enter at the multi-candidate prompt to discard all candidates. Discarding a proposal leaves session knowledge unchanged.

Ordinary input is not silently sent to the model. Use `:translate` explicitly; the existing native LCE parser, tool-pattern matching, and default CLI behaviour remain unchanged.

## Configuration file

The file is created by `:translator setup`. A default configuration looks like:

```json
{
  "lmStudio": {
    "apiUrl": "http://127.0.0.1:1234/api/v1/chat",
    "modelKey": "publisher/model-name",
    "modelIdentifier": null,
    "lmsExecutable": "lms",
    "apiToken": null,
    "reasoning": "off",
    "serverStartupTimeoutSeconds": 60,
    "modelLoadTimeoutSeconds": 180,
    "requestTimeoutSeconds": 120
  }
}
```

You normally do not need to edit this file. If `lms` is not on PATH, set `lmsExecutable` to the full path of the LM Studio CLI. The API URL must refer to a local loopback HTTP address; Lattice will not start or control a server bound to another machine. `apiToken` is only needed when local API authentication is enabled. The `reasoning` setting accepts `off`, `low`, `medium`, `high`, or `on`; LM Studio reports an error if the selected model does not support the requested setting.

## Scope and limitations

- One input statement or question per `:translate` request. Multi-sentence translation is not supported by this initial adapter.
- The model must produce one complete LCE 0.9 sentence per candidate.
- The current LCE grammar has no negation, disjunction, modality (such as "could" or "might"), unrestricted conditionals, relational joins, or general plural/quantifier handling. The model is instructed to abstain when it cannot preserve the meaning.
- Multiple model candidates remain ambiguous until the user selects one.
- A structurally valid LCE candidate is not proof that the model preserved the source meaning. Review remains necessary.
- The native chat endpoint does not document strict JSON Schema response formatting. The adapter requests the JSON shape in its system prompt, strictly parses the returned JSON, and validates every candidate with the native LCE parser; malformed JSON or an unsupported LCE sentence is rejected.
- The adapter does not add a dependency on LM Studio, a model, an SDK, or a neural runtime to Core.

See the [LM Studio CLI documentation](https://lmstudio.ai/docs/cli), [server start](https://lmstudio.ai/docs/cli/serve/server-start), [installed model catalog](https://lmstudio.ai/docs/developer/rest/list), [model load](https://lmstudio.ai/docs/developer/rest/load), and [native chat API](https://lmstudio.ai/docs/developer/rest/chat).
