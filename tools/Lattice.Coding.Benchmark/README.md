# Lattice Python coding benchmark runner

Run the 16 Python tasks in `data/benchmarks/coding-benchmark-v1.json` through the in-process production runtime:

```powershell
dotnet run --project tools/Lattice.Coding.Benchmark
```

The runner creates a fresh session for each exact benchmark prompt, extracts the generated Python block, and checks syntax with `python -m py_compile`. It performs a bounded smoke execution in an isolated temporary directory only for tasks whose benchmark definition sets `executionRequired` to `true`; other tasks are reported as syntax-valid but not executed.

It writes:

- `artifacts/coding-benchmark-python-results.json` — per-task prompts, responses, generated code, syntax status, exit codes, and diagnostics.
- `artifacts/coding-benchmark-python-results.md` — readable summary and generated scripts.

Python 3 must be available as `python` on Windows or `python3` elsewhere.

## Interpreting the report

This is an evidence-gathering runner, not an automatic semantic grader. A successful smoke execution proves only that the generated program ran in the empty fixture environment; it does not prove that it satisfies the task. A failure may indicate a missing input fixture rather than incorrect generated logic. Semantic scoring, task-specific fixtures, generalization variants, and human review remain separate work. The runner intentionally does not convert generation or execution counts into a benchmark accuracy score.

The benchmark's Blender and cross-domain holdout tasks are not included in this Python-only runner.
