# Tools

Tools are optional capabilities registered by the application composition root. Core provides a generic contract and registry; reasoning does not reference individual tool implementations or their providers.

## Core contract

Each `ITool` exposes a `ToolDescriptor` containing a stable ID, display name, description, input descriptors, and required permissions. Input descriptors define names, primitive JSON types, descriptions, and whether each input is required.

`ToolRegistry` validates argument objects against descriptors, checks granted permissions, bounds invocation time, contains tool exceptions, and returns sourced observations. The default execution limit is five seconds and the configured limit cannot exceed one minute. A successful `ToolObservation` is evidence attributed to its producer, not a knowledge-base assertion. It is not automatically persisted or used as a premise.

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
  "permissions": []
}
```

The command may be any executable available on the host. The example uses Python only to illustrate one possible implementation; Lattice does not install, import, or otherwise depend on Python. Fixed command arguments are passed as individual process arguments, without a shell. Relative executable paths must stay inside their tool directory.

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

The response must echo the protocol version and request ID. The `result` may be any JSON value. Lattice preserves it as JSON in the resulting sourced observation and does not infer facts from it.

The configured tool directory is a trust boundary. Invoking a discovered command runs it with the current user's OS permissions; process separation is not a security sandbox. Lattice still enforces descriptor permissions, execution timeouts, and process termination.

## Current invocation surface

- `:tools` lists the registered capabilities and their input descriptors.
- `:tool calculator {"left":12,"operation":"multiply","right":3}` invokes the built-in calculator.
- `:tool <id> <arguments-json>` explicitly invokes any registered capability.

External tools are discovered at CLI startup but are started only when invoked. Natural-language tool selection, observation-to-context integration, durable observation storage, and automatic knowledge promotion are not implemented.
