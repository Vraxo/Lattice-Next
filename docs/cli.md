# Non-interactive CLI

`Lattice.Cli` is the script- and automation-oriented frontend. It uses the shared `ILatticeClient` / `ILatticeSession` contract and defaults to the in-process runtime. Use `--server` to send the same work to a hosted Lattice server.

## Usage

```sh
dotnet run --project src/Lattice.Cli -- "Fido is a dog. Is Fido a dog?"
dotnet run --project src/Lattice.Cli -- --format text "Is Fido a dog?"
dotnet run --project src/Lattice.Cli -- --load-pack knowledge/examples/animals.json "Is Fido a dog?"
cat input.lce | dotnet run --project src/Lattice.Cli
```

### Remote server

```sh
dotnet run --project src/Lattice.Cli -- --server http://127.0.0.1:5080 "Fido is a dog. Is Fido a dog?"
```

When the server requires bearer-token authentication, set `LATTICE_SERVER_API_KEY` in the CLI process environment. Use HTTPS when connecting over an untrusted network.

Input can be supplied as positional text, through `--input-file`, or from redirected standard input. These sources are mutually exclusive. A single invocation creates one isolated session, loads any requested knowledge packs in order, and submits one turn. Multiple sentences in that turn retain the normal source-order and atomic-parse behavior. The selected transport does not change the output contract.

JSON is the default output and is intended for scripts. Its top-level object contains `success`, `sessionId`, `loadedPacks`, `turn`, and `error`. Enums are serialized as strings. Use `--format text` for concise human-readable output; diagnostics go to standard error.

## Exit codes

| Code | Meaning |
|---:|---|
| 0 | The turn completed and every utterance was handled. Unknown is a valid answer. |
| 2 | The turn was rejected, a statement was rejected, or clarification/unsupported input was returned. |
| 3 | A knowledge pack failed validation. |
| 64 | Invalid usage or missing input. |
| 66 | An input or knowledge-pack file could not be read, or a server request failed. |
| 130 | Operation cancelled. |

The CLI does not add tool execution, model-assisted translation, or persistent sessions. The HTTP server currently keeps sessions in memory, so a server restart ends them.
