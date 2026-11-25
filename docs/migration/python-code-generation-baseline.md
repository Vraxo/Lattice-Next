# Python code-generation migration — baseline and completed work

This document preserves the technical baseline and historical migration details removed from the active checklist. It is a reference, not an additional task queue.

## Historical completion count

The former 28/28 figure records removal of 28 tracked C# dispatch branches. It is not evidence that the Python-specific code-generation migration is complete.

## Verified repository baseline

These counts and observations are a historical snapshot taken before the queued migration work was completed. Re-audit the repository before treating any count as current.

- `knowledge/sequences/python.core.json` contained 136 sequence rules, 231 input-fact rules, 50 validations, and one string encoding.
- Task-specific fact IDs included `program.assignment.classification`, `program.classification.positive`, `program.classification.negative`, `program.classification.zero`, `program.even-list-filter`, `program.manual-list-total`, `program.greeting-function`, `program.non-empty-file-line-count`, `program.dictionary-product-iteration`, `program.integer-conversion-function`, `program.name-length-dictionary-mapping`, and `program.directory-file-listing`.
- The sequence module contained fixed, complete Python outputs for greeting functions with Ada/Grace examples, file-line counting, dictionary iteration, integer conversion, dictionary mappings, sorting, and directory listing.
- The coding benchmark disallows task-specific response templates/task-ID branches as evidence of composition. Storing a complete solution or complete code fragment for a benchmark task invalidates that task as a compositional test.
- M-03 removed the unused `src/Lattice.Core/Language/PythonExpressionParser.cs`.
- M-04 removed `pythonListLiteral` and `IsPythonListLiteral` from shared Core.
- M-05A moved identifier, number-literal, string-literal, and function-call syntax checks to selected language-pack metadata.
- Before M-05B, active Python-shaped expression checks remained in `src/Lattice.Core/Generation/SequenceInputFactValidator.cs`, with corresponding validation-kind handling in `SequenceKnowledgeBaseLoader`.
- `src/Lattice.Runtime/KnowledgeDrivenGenerationResponder.cs` extracted facts from raw input and called `KnowledgeDrivenSequenceGenerator` directly, independently of the M-02 target path.
- `src/Lattice.Runtime/TaskCodeGenerationResponder.cs` contained Blender-specific `blender-codegen-*` concept handling and consumed a pre-authored ordered step plan. `TaskRequestParser` also had a separate `ScriptCreationPattern` path.
- `knowledge/domains/python-language-foundations.json` contained 43 type concepts, four individual concepts, 43 primitive type declarations, 19 relation signatures, and 76 relation assertions, with no defined types.
- `knowledge/languages/python.core.json` contained Python operations and grammar productions. Python syntax knowledge is legitimate when declarative; Python-specific reasoning/validation in shared C# and task-specific solutions are not.
- `knowledge/domains/blender-object-replacement-script.json` contained a task-specific generation-step plan. Blender API facts may be valid domain knowledge; a fixed solution sequence is not a reusable programming model.

## Completed milestones

- **M-01 — Inventory:** identified the Python-specific and task-specific generation paths.
- **M-02 — Shared-path architecture:** established the target of one semantic-to-code generation path.
- **M-03 — Dead parser removal:** removed the unused Python expression parser.
- **M-04 — Semantic collections:** replaced the Python-shaped `pythonListLiteral` representation with semantic collection values.
- **M-05A — Language-pack syntax checks:** moved identifier, number, string, and function-call syntax checks into selected language-pack metadata.
- **M-05B — Structured expressions:** completed the expression inventory, semantic expression model, structured capture, semantic validation, language-pack realization, and removal of the old expression-string validation path.
- **M-06 — Minimum operational programming model:** added language-neutral program structure, typed operation/slot contracts, effects, prerequisites, function/control-flow relationships, module/exception/I/O relationships, and loader/reasoner verification.

For the active remaining work and current completion state, see [`.agents/todo.md`](../../.agents/todo.md).

## Migration principles

- Keep Python syntax, keywords, token spellings, indentation, literal formatting, and Python API facts in declarative language/domain knowledge.
- Shared Core/Runtime must not branch on Python, parse Python-specific syntax, validate Python-specific literals, or contain Python-specific generation behavior.
- Moving a complete program from C# into a knowledge file is still a hard-coded solution.
- Knowledge should describe reusable concepts, operations, typed inputs/outputs, effects, prerequisites, constraints, grammar, and API signatures—not complete benchmark answers or fixed prompt-specific plans.
- Requests should become language-neutral semantic goals and requirements; the selected language pack supplies syntax; one generic generation mechanism realizes the result.
- Keep structural and semantic validation generic. Syntax-specific validity belongs in the relevant language pack or parser.
- Do not add ML, an LLM code generator, a new task router, a second generation engine, or another collection of prompt recognizers.
- Preserve benchmark anti-shortcut requirements and test with changed values, names, collection sizes, and paraphrases.
- Do not remove unrelated Python tools, benchmark runners, fixtures, or the Python language pack merely because they contain the word “Python”.
