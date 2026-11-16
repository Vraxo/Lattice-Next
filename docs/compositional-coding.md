# Compositional Coding Track

## Objective

Establish whether Lattice can construct programs from independently learned concepts rather than retrieve complete scripts or select task-specific code fragments.

The coding track is separate from the general assistant benchmark. Its initial benchmark is `data/benchmarks/coding-benchmark-v1.json` (30 prompts: 8 Python fundamentals, 8 compositional Python, 6 Blender foundations, and 8 unseen cross-domain holdouts).

## Repository audit: current code-generation path

The existing Blender object-replacement example is not general code generation:

- `knowledge/domains/blender-object-replacement-script.json` stores a task entity, ordered task steps, operation IDs, and Blender API labels.
- `TaskRequestResponder.TryGenerateCode` accepts only the exact combination `python-script` + `blender` + `blender-object-replacement` + `object-to-point-light`.
- `TaskRequestParser.TryParseBlenderScript` also recognizes one Blender-script request shape and only classifies the task as supported when its text contains the specific replacement/object/name/point-light terms; other tasks become `unsupported-blender-script`.
- That method contains a dictionary mapping operation IDs directly to complete Python source fragments.
- It also hardcodes imports, variable setup, regular-expression setup, explanatory prose, and the final script layout.

This path is a useful prototype of semantic task decomposition followed by code-fragment assembly, but it is not evidence of general program construction. It must not be used to score the new compositional benchmark.

## Design constraints

1. **Independent domains:** Python knowledge describes the language; Blender knowledge describes the application, its data model, and its API. Neither domain contains combined benchmark-task solutions.
2. **No task-keyed rendering:** No prompt, task ID, subject/scope tuple, or benchmark case may select a prewritten program or task-specific fragment set.
3. **Reusable program representation:** Represent expressions, values, assignments, calls, attribute access, collections, conditions, loops, functions, imports, and error handling as general constructs.
4. **Separate planning from rendering:** Task reasoning selects and connects operations; a Python renderer translates a general program representation into syntax. The renderer knows Python grammar, not task answers.
5. **Ground API calls:** Blender API knowledge supplies names, signatures, types, and effects. Unknown API details must remain unknown rather than be guessed.
6. **Validation:** Check structural validity, Python syntax, API compatibility where practical, and execution in an isolated Blender process for integration cases.
7. **Provenance:** Retain evidence for concepts and API facts used to construct a program.
8. **Honest scoring:** A stored solution, task-specific branch, or unvalidated plausible script cannot earn compositional/generalization/execution credit.

## Implementation sequence

### Milestone 1 — Benchmark and baseline (this change)

- Add the 30-prompt benchmark and explicit scoring rules.
- Record the current special-case generator as a known baseline limitation.
- Establish holdout rules before building new knowledge or code-generation capability.

### Milestone 2 — Python language foundations

The first independent Python pack now models the language, programs, statements, expressions, values, variables, assignments, control flow, functions, parameters, calls, collections, modules, exceptions, classes, objects, interpreters, libraries, APIs, and output. It contains conceptual relationships only; it does not contain complete scripts or benchmark answers.

Continue expanding the graph only where the benchmark demonstrates a missing concept or relationship. Start with literals, expressions, assignments, function calls, and `print`; then add variables, types, collections, conditions, loops, functions, modules, file operations, classes, and exceptions. Each addition must be justified by benchmark coverage.

### Milestone 3 — General program representation and renderer

Create a typed, composable representation for programs. Keep construction independent of Python text formatting. Add deterministic rendering and syntax validation. The renderer may contain grammar rules and syntax tokens, but no complete benchmark solutions.

The first language-neutral `ProgramPlan` and Python lowering pass now exist. Plans represent imports, assignments, expression evaluation, returns, iteration, conditions, functions, values, member/index access, calls, and operators without embedding Python source strings. `PythonProgramLowerer` maps those generic structures into the Python syntax model; the existing renderer then emits source. Tests verify both a composed loop/condition program and a generic API invocation. This is a construction pipeline, not yet a semantic task planner: the runtime does not infer a plan from a user's request at this stage.

### Milestone 4 — Independent Blender knowledge

Model Blender's application concepts, scene/object/collection relationships, object data, transforms, materials, cameras, lights, and a curated subset of its Python API. API facts should include signatures and effects, not task recipes.

### Milestone 5 — Cross-domain composition

Connect Python's general module/API concepts to Blender's independently modeled API. Let task decomposition select relevant concepts and operations. Do not add a Blender-Python task pack.

### Milestone 6 — Holdout evaluation

Run all eight cross-domain prompts only after the foundations are frozen for that evaluation. Execute generated scripts in a disposable Blender background process with fixture scenes. Add paraphrases and altered fixture data to measure generalization.

## Scoring

Each prompt is scored independently on task understanding, decomposition, composition, syntax, API correctness, execution, and generalization. Each applicable dimension receives 0, 1, or 2. Report both per-stage scores and the weighted aggregate. Keep "answered" separate from "working program".

The holdout stage is the decisive measure: it must remain free of task-specific solution knowledge and special-case routing.
