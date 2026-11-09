# External tools

Place each external capability in its own direct subdirectory:

```text
tools/
  README.md
  calculator/
    tool.json
    main.py
  unit-converter/
    tool.json
    main.py
  example-tool/
    tool.json
    main.py
```

Lattice reads every `tool.json` when the CLI starts. It does not execute tools during discovery. A tool starts only when selected and invoked. The default discovery root is `./tools`, relative to the CLI's working directory. Set `LATTICE_TOOLS_DIR` to use another root.

## Bundled examples

The calculator and unit converter are ordinary external tools, not C# implementations compiled into Core or the CLI. Their Python source and manifests live in this directory and use only Python's standard library:

- `calculator`: exact decimal addition, subtraction, multiplication, and division.
- `unit-converter`: length (mm, cm, m, km), mass (mg, g, kg), volume (ml, l), and time (s, min, h).

To use these bundled examples, install Python 3 and make its `python` command available on `PATH`. They have no pip dependencies. Lattice itself still builds without Python; other external tools can use any executable and implementation language.

The CLI enables the corresponding built-in natural-language recognizer only when it discovers the matching tool ID. For example, the natural-language calculator requests work when the `calculator` manifest is present and its Python interpreter can be started.

## Protocol

External tools communicate through protocol version 1, a language-neutral JSON-lines protocol over standard input and output. The manifest's `command` may name any executable available on the host; Python is used by the bundled examples, not required by the protocol. See [the full manifest and protocol specification](../src/Lattice.Core/Tools/README.md).

Tools requiring permissions are denied by default. Set `LATTICE_TOOL_PERMISSIONS` to `read`, `write`, or `read,write` to grant those capabilities to registered tools for that CLI session. This is a coarse host-level grant, not a per-tool policy or an OS sandbox. External tools run with the current user's OS permissions, so only load and invoke tools you trust.
