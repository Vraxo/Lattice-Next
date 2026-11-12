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
  web-search/
    tool.json
    main.py
    requirements.txt
    test_main.py
  web-page-fetch/
    tool.json
    main.py
    test_main.py
    README.md
  example-tool/
    tool.json
    main.py
```

Lattice reads every `tool.json` when the CLI starts. It does not execute tools during discovery. A tool starts only when selected and invoked. The default discovery root is `./tools`, relative to the CLI's working directory. Set `LATTICE_TOOLS_DIR` to use another root.

## Bundled examples

The calculator and unit converter are ordinary external tools, not C# implementations compiled into Core or the CLI. Their Python source and manifests live in this directory and use only Python's standard library:

- `calculator`: exact decimal addition, subtraction, multiplication, and division.
- `unit-converter`: length (mm, cm, m, km), mass (mg, g, kg), volume (ml, l), and time (s, min, h).
- `web-search`: timestamped result titles, source URLs, domains, and snippets from public search providers through DDGS, with no account, API key, or user-hosted service.
- `web-page-fetch`: bounded extraction of public HTML/plain-text pages with URL, retrieval time, DNS/IP, redirect, content-type, and size checks.

To use these bundled examples, install Python 3 and make its `python` command available on `PATH`. The calculator, unit converter, and page fetcher have no pip dependencies. Install the web-search dependency once from the repository root with `python -m pip install -r tools/web-search/requirements.txt`. Web search uses public search pages and may be rate-limited or temporarily unavailable; it does not require an API key or a separate search server.

Each manifest may include an `intents` array declaring supported phrases, typed slots, argument bindings, and response templates. Lattice loads these declarations into a generic matcher; there are no calculator-, unit-converter-, or page-fetch-specific recognizer classes. A newly added tool can declare its own supported phrases in its manifest without changing C# code. Patterns are deliberately constrained exact token sequences: `number` and `word` capture one token, `text` captures a phrase, and square-bracketed literal phrases are optional. They are not arbitrary code or unrestricted English. The bundled calculator's follow-up and chained arithmetic phrases are also declared there.

## Protocol

External tools communicate through protocol version 1, a language-neutral JSON-lines protocol over standard input and output. The manifest's `command` may name any executable available on the host; Python is used by the bundled examples, not required by the protocol. See [the full manifest and protocol specification](../src/Lattice.Core/Tools/README.md).

Tools requiring permissions are denied by default. Before first use, the CLI asks whether to always allow a tool, allow it for the current session, or deny it. Remembered approvals are stored per tool ID in `lattice.config.json`; use `:permissions` to inspect them and `:permissions revoke <tool-id>` to remove one. `LATTICE_TOOL_PERMISSIONS` remains available as a global pre-grant for non-interactive runs. These checks are not an OS sandbox. External tools run with the current user's OS permissions, so only load and invoke tools you trust.


## Repository evaluation tools

- [Lattice assistant benchmark](Lattice.Assistant.Benchmark/README.md): runs the curated source-prompt benchmark through the production CLI and writes JSON and Markdown reports.
- [Lattice translator evaluation](Lattice.Translator.Evaluation/README.md): evaluates the separate LM Studio natural-language-to-LCE translation boundary.
