# Non-interactive CLI

`Lattice.Cli` is the script- and automation-oriented frontend. It is intentionally separate from the interactive `Lattice.Repl` and uses the same `ILatticeClient` / `ILatticeSession` contract through the in-process runtime.

## Usage

```sh
dotnet run --project src/Lattice.Cli -- "Fido is a dog. Is Fido a dog?"
dotnet run --project src/Lattice.Cli -- --format text "Is Fido a dog?"
dotnet run --project src/Lattice.Cli -- --load-pack knowledge/examples/animals.json "Is Fido a dog?"
cat input.lce | dotnet run --project src/Lattice.Cli
```

Input can be supplied as positional text, through `--input-file`, or from redirected standard input. These sources are mutually exclusive. A single invocation creates one isolated session, loads any requested knowledge packs in order, and submits one turn. Multiple sentences in that turn retain the normal source-order and atomic-parse behavior.

JSON is the default output and is intended for scripts. Its top-level object contains `success`, `sessionId`, `loadedPacks`, `turn`, and `error`. Enums are serialized as strings. Use `--format text` for concise human-readable output; diagnostics go to standard error.

## Exit codes

| Code | Meaning |
|---:|---|
| 0 | The turn completed and every utterance was handled. Unknown is a valid answer. |
| 2 | The turn was rejected, a statement was rejected, or clarification/unsupported input was returned. |
| 3 | A knowledge pack failed validation. |
| 64 | Invalid usage or missing input. |
| 66 | An input or knowledge-pack file could not be read. |
| 130 | Operation cancelled. |

The first CLI slice deliberately does not add tool execution, model-assisted translation, persistent sessions, or a remote server. Those capabilities can be added when their behavior is defined across frontends.
