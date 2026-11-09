# External tools

Place each external capability in its own direct subdirectory:

```text
tools/
  README.md
  example-tool/
    tool.json
    main.py
```

Lattice reads `tool.json` when the CLI starts. It does not execute tools during discovery. A tool starts only when explicitly invoked through the current developer-facing `:tool` command.

The default discovery root is `./tools`, relative to the CLI's working directory. Set `LATTICE_TOOLS_DIR` to use another root.

External tools communicate through protocol version 1, a language-neutral JSON-lines protocol over standard input and output. The manifest's `command` may name any executable available on the host; Python is only one possible implementation language. See [the full manifest and protocol specification](../src/Lattice.Core/Tools/README.md).

Tools requiring permissions are denied by default. Set `LATTICE_TOOL_PERMISSIONS` to `read`, `write`, or `read,write` to grant those capabilities to registered tools for that CLI session. This is a coarse host-level grant, not a per-tool policy or an OS sandbox. External tools run with the current user's OS permissions, so only load and invoke tools you trust.
