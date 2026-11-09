# Tools

Tools are optional capabilities discovered and invoked by the application composition root. Core provides a generic contract and registry; reasoning does not reference individual tool implementations or their providers.

## Core contract

Each `ITool` exposes a `ToolDescriptor` containing a stable ID, display name, description, input descriptors, and required permissions. Input descriptors define names, primitive JSON types, descriptions, and whether each input is required.

`ToolRegistry` validates argument objects against descriptors, checks granted permissions, bounds invocation time, contains tool exceptions, and returns sourced observations. The default execution limit is five seconds and the configured limit cannot exceed one minute. A successful `ToolObservation` is evidence attributed to its producer, not a knowledge-base assertion. It is not automatically persisted or used as a premise.

## Bundled example capabilities

The repository includes two example capabilities under `tools/`. They are ordinary external processes discovered from their manifests, not tool implementations compiled into `Lattice.Core` or `Lattice.Cli`:

- `calculator`: exact decimal addition, subtraction, multiplication, and division, implemented in Python.
- `unit-converter`: converts length (mm, cm, m, km), mass (mg, g, kg), volume (ml, l), and time (s, min, h), implemented in Python.

Both use the same manifest, stdio protocol, generic registry, permission checks, and execution limits as community tools. They have no third-party Python dependencies. The bundled examples require Python 3 with a `python` executable on `PATH`; Lattice Core and the stdio provider do not depend on Python.

## External process provider

The separate `Lattice.Tooling.Stdio` project adapts external processes to the Core contract. Core has no dependency on this provider, process APIs, a transport, or any particular implementation language.

The CLI discovers tools one directory level below `./tools` by default. Set `LATTICE_TOOLS_DIR` to use another root. Each tool directory contains a `tool.json` manifest. Directories without a manifest are ignored. Discovery parses metadata only; it never starts the declared command. Invalid manifests fail discovery as a whole instead of silently loading a partial set.

Example manifest:

```json
{
  "protocolVersion": 1,
  "id": "example.echo",
  "name": "Echo",
  "description": "Returns the supplied text.",
  "command": "python",
  "arguments": ["main.py"],
  "inputs": [
    {
      "name": "text",
      "description": "Text to return.",
      "type": "string",
      "required": true
    }
  ],
  "permissions": [],
  "intents": [
    {
      "pattern": "echo {text:text}",
      "arguments": {
        "text": "$text"
      },
      "response": "Echoed: {result}."
    }
  ]
}
```

The command may be any executable available on the host. Fixed command arguments are passed as individual process arguments, without a shell. Relative executable paths must stay inside their tool directory.

### Declarative intent patterns

The optional `intents` array declares which constrained phrases select a tool. Each entry has:

- `pattern`: an exact sequence of literal tokens and typed placeholders such as `{value:number}`.
- `arguments`: the tool input values. A value beginning with `# Tools

Tools are optional capabilities discovered and invoked by the application composition root. Core provides a generic contract and registry; reasoning does not reference individual tool implementations or their providers.

## Core contract

Each `ITool` exposes a `ToolDescriptor` containing a stable ID, display name, description, input descriptors, and required permissions. Input descriptors define names, primitive JSON types, descriptions, and whether each input is required.

`ToolRegistry` validates argument objects against descriptors, checks granted permissions, bounds invocation time, contains tool exceptions, and returns sourced observations. The default execution limit is five seconds and the configured limit cannot exceed one minute. A successful `ToolObservation` is evidence attributed to its producer, not a knowledge-base assertion. It is not automatically persisted or used as a premise.

## Bundled example capabilities

The repository includes two example capabilities under `tools/`. They are ordinary external processes discovered from their manifests, not tool implementations compiled into `Lattice.Core` or `Lattice.Cli`:

- `calculator`: exact decimal addition, subtraction, multiplication, and division, implemented in Python.
- `unit-converter`: converts length (mm, cm, m, km), mass (mg, g, kg), volume (ml, l), and time (s, min, h), implemented in Python.

Both use the same manifest, stdio protocol, generic registry, permission checks, and execution limits as community tools. They have no third-party Python dependencies. The bundled examples require Python 3 with a `python` executable on `PATH`; Lattice Core and the stdio provider do not depend on Python.

## External process provider

The separate `Lattice.Tooling.Stdio` project adapts external processes to the Core contract. Core has no dependency on this provider, process APIs, a transport, or any particular implementation language.

The CLI discovers tools one directory level below `./tools` by default. Set `LATTICE_TOOLS_DIR` to use another root. Each tool directory contains a `tool.json` manifest. Directories without a manifest are ignored. Discovery parses metadata only; it never starts the declared command. Invalid manifests fail discovery as a whole instead of silently loading a partial set.

Example manifest:

```json
{
  "protocolVersion": 1,
  "id": "example.echo",
  "name": "Echo",
  "description": "Returns the supplied text.",
  "command": "python",
  "arguments": ["main.py"],
  "inputs": [
    {
      "name": "text",
      "description": "Text to return.",
      "type": "string",
      "required": true
    }
  ],
  "permissions": [],
  "intents": [
    {
      "pattern": "echo {text:text}",
      "arguments": {
        "text": "$text"
      },
      "response": "Echoed: {result}."
    }
  ]
}
```

 refers to a captured slot; `$previous` refers to the latest successful numeric observation. Other strings, numbers, and booleans are literal constants.
- `response`: a response template containing one or more tool argument names and `{result}`.

Slot types are `number` (invariant decimal), `word` (letters only), and `text` (one non-whitespace token). Matching is case-insensitive; punctuation at token edges is ignored. Patterns do not support arbitrary regular expressions, optional tokens, or multiword captures. A manifest is rejected when a slot reference, binding type, required input, or response placeholder is invalid.

For example, the declaration above matches `Echo lattice.`, binds the single word to the `text` input, invokes the tool, and formats its returned value as `Echoed: lattice.`. A tool author can add or change these phrases by editing only the manifest.

A pattern may bind an argument to `$previous` to refer to the latest numeric tool observation. This also enables bounded chains: text separated by `then` must match one declared pattern per step, and each subsequent step must bind at least one input to `$previous`. The complete chain is matched before its first invocation and contains at most eight steps. Results remain temporary observations, not knowledge-base facts. These declarations are deterministic phrase matching, not unrestricted English understanding.

### JSON-lines protocol version 1

Each invocation starts one process. Lattice writes exactly one JSON object followed by a newline to standard input, closes standard input, and expects exactly one JSON response line on standard output. Diagnostics belong on standard error.

Request:

```json
{
  "protocolVersion": 1,
  "requestId": "correlation-id",
  "operation": "execute",
  "arguments": {
    "text": "hello"
  }
}
```

Successful response:

```json
{
  "protocolVersion": 1,
  "requestId": "correlation-id",
  "success": true,
  "result": {
    "text": "hello"
  }
}
```

Failed response:

```json
{
  "protocolVersion": 1,
  "requestId": "correlation-id",
  "success": false,
  "error": "A human-readable failure."
}
```

The response must echo the protocol version and request ID. The `result` may be any JSON value. Lattice preserves it as JSON in the resulting sourced observation and does not infer facts from it. Numeric results are represented as JSON numbers so subsequent numeric intents can use the observation directly.

The configured tool directory is a trust boundary. Invoking a discovered command runs it with the current user's OS permissions; process separation is not a security sandbox. Lattice enforces descriptor permission gates, execution timeouts, and process termination, but these gates do not restrict what the child process can access at the OS level. The CLI grants no tool permissions by default; set `LATTICE_TOOL_PERMISSIONS` to `read`, `write`, or `read,write` to grant them for the session. This is a coarse global grant, not a per-tool policy.

## Current invocation surface

- `:tools` lists the registered capabilities and their input descriptors.
- `:tool calculator {"left":12,"operation":"multiply","right":3}` invokes the bundled external calculator when discovered.
- `:tool unit-converter {"value":150,"from":"cm","to":"m"}` invokes the bundled external unit converter when discovered.
- `:tool <id> <arguments-json>` explicitly invokes any registered capability for diagnostics.
- `What is 12 multiplied by 3?` selects the calculator from ordinary language when its manifest is discovered.
- `Convert 150 cm to meters.` selects the unit converter when its manifest is discovered.
- `Multiply that by 5.` resolves the latest numeric tool observation and invokes the calculator again.

The generic intent resolver creates recognizers from discovered manifest declarations; it does not contain a switch over tool IDs or domains. The bundled calculator and unit converter declare their supported arithmetic and conversion phrases in their own manifests. A new tool can declare its own phrases without a matching C# recognizer. Tool observations are kept in session conversation context, and numeric follow-ups may refer to the latest observation. They remain separate from accepted facts and rules. External tools are discovered at CLI startup but are started only when selected and invoked. Durable observation storage and automatic knowledge promotion are not implemented.
