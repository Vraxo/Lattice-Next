# Lattice-Next — finish the Python code-generation migration

**Active mission:** finish removing the existing Python-specific and task-specific shortcuts from the code-generation implementation, replacing them with reusable programming semantics, world-model knowledge, and the shared language-neutral generation pipeline.

Do not start broader programming-capability expansion, new assistant reasoning tracks, or additional benchmark domains until this migration is complete. The immediate objective is to finish replacing what is already hard-coded, not to start a new roadmap.

## What “28/28 complete” actually means

The 28/28 figure records the removal of 28 previously tracked C# dispatch branches. It is a narrow historical migration count, **not** evidence that the Python-specific code-generation migration is finished.

The current repository still contains Python-specific expression validation, task-specific request facts, prewritten program outputs in declarative sequence knowledge, a shallow programming ontology, and a separate Blender code-generation path. These are the work tracked below.

## Verified baseline

- `knowledge/sequences/python.core.json` contains 136 sequence rules, 231 input-fact rules, 50 validations, and one string encoding. It includes task-specific facts such as `program.assignment.classification`, `program.classification.positive`, `program.classification.negative`, `program.classification.zero`, `program.even-list-filter`, `program.manual-list-total`, `program.greeting-function`, `program.non-empty-file-line-count`, `program.dictionary-product-iteration`, `program.integer-conversion-function`, `program.name-length-dictionary-mapping`, and `program.directory-file-listing`.
- The same sequence module contains fixed, complete Python outputs for multiple tasks, including greeting functions with Ada/Grace examples, file-line counting, dictionary iteration, integer conversion, dictionary mappings, sorting, and directory listing. Some outputs are literal full programs rather than compositions of request-derived semantic facts.
- The coding benchmark explicitly says that a task-specific response template or task-ID branch cannot earn composition/generalization credit, and that storing a complete solution or complete code fragment for a benchmark task invalidates that task as a compositional test.
- M-03 removed the unused `src/Lattice.Core/Language/PythonExpressionParser.cs`. The active Python-shaped expression checks are in `src/Lattice.Core/Generation/SequenceInputFactValidator.cs`, with corresponding validation-kind handling in `SequenceKnowledgeBaseLoader`.
- M-04 removed `pythonListLiteral` and `IsPythonListLiteral` from shared Core. M-05A moved identifier, number-literal, string-literal, and function-call syntax checks to selected language-pack metadata. Arithmetic, boolean, comparison, and value-expression validation remain for M-05B.
- `src/Lattice.Runtime/KnowledgeDrivenGenerationResponder.cs` still extracts facts from raw input and calls `KnowledgeDrivenSequenceGenerator` directly, before the parsed request path. It is an independent production emitter, not the M-02 target path.
- `src/Lattice.Runtime/TaskCodeGenerationResponder.cs` checks Blender-specific `blender-codegen-*` concepts and reads a pre-authored ordered step plan. `TaskRequestParser` also has a separate `ScriptCreationPattern` path. Both must be brought under the general code-request path.
- `knowledge/domains/python-language-foundations.json` has 43 type concepts, four individual concepts, 43 primitive type declarations, 19 relation signatures, and 76 relation assertions; it has no defined types. It is a starting taxonomy, not yet a sufficient operational model.
- `knowledge/languages/python.core.json` contains Python operations and grammar productions. Language-specific syntax knowledge is legitimate and should remain declarative; the problem is Python-specific reasoning/validation in shared C# and task-specific solutions disguised as knowledge.
- `knowledge/domains/blender-object-replacement-script.json` contains a task-specific generation-step plan. Its Blender API facts may be legitimate domain knowledge, but a preselected solution sequence is not a reusable programming model.

## Non-negotiable migration rules

- Keep Python syntax, keywords, token spellings, indentation, literal formatting, and actual Python API facts in declarative language/domain knowledge. Do **not** try to remove Python as a supported target language.
- Shared Core/Runtime code must not branch on Python, parse Python-specific syntax, validate Python-specific literals, or contain Python-specific generation behavior.
- Do not replace C# hard-codes with one-prompt-one-rule JSON. Moving a complete program from C# into a knowledge file is still a hard-coded solution.
- Knowledge may describe language-independent programming concepts, reusable operations, typed inputs/outputs, effects, prerequisites, constraints, language grammar, and API signatures. It must not contain complete benchmark answers or fixed solution plans for individual prompts.
- Program requests must become language-neutral semantic goals and requirements. The world model/knowledge base must supply reusable programming meaning; the chosen language pack supplies syntax; one generic sequence-generation mechanism realizes the meaning.
- Keep structural and semantic validation generic. Put syntax-specific validity in the relevant language pack or parser, not in a Python-named branch in shared infrastructure.
- Do not add ML, an LLM code generator, a new task router, a second parallel generation engine, or another pile of prompt recognizers.
- Preserve the existing benchmark's anti-shortcut rules. Use changed values, names, collection sizes, and paraphrases to show that code is composed from semantics rather than retrieved as a memorized answer.
- Do not remove unrelated Python tools, benchmark runners, test fixtures, or the Python language pack simply because they contain the word “Python”.

## Ordered migration work

Complete the groups in order. Each checkbox is one small, concrete implementation task; a group may take several PRs. Keep each change narrow, run its focused tests, and check it only after the change is merged and its checks pass. If inspection shows a task is still too large, split it into adjacent tasks before implementation rather than turning it into one broad PR.

### M-05B — Make expression input language-neutral

#### M-05B.1 — Inventory the existing expression path

- [x] **M-05B.1a — Inventory arithmetic-expression input rules.** Locate each arithmetic-expression input rule in `python.core.json`; record its match conditions, captured values, and emitted fact IDs.
- [x] **M-05B.1b — Inventory comparison-expression input rules.** Record comparison rules, captured operands/operators, and emitted fact IDs.
- [x] **M-05B.1c — Inventory boolean-expression input rules.** Record boolean rules, captured operands/operators, and emitted fact IDs.
- [x] **M-05B.1d — Inventory value-expression input rules.** Record other expression/value rules and the exact values they capture.
- [x] **M-05B.1e — Trace expression facts into validation.** Follow each inventoried fact from extraction into `SequenceInputFactValidator`; identify which validation kinds consume it.
- [x] **M-05B.1f — Trace validation-kind loading.** Find the corresponding validation-kind allow-list/schema handling in `SequenceKnowledgeBaseLoader` and record the relevant tests.
- [x] **M-05B.1g — Trace expression values into generation.** Follow validated values through semantic facts, bindings, and the sequence-generation path; identify where raw source strings are expected.
- [x] **M-05B.1h — Classify the existing checks.** Record each check as syntax parsing, semantic validation, reference/scope validation, or structural validation. Add the findings to the migration inventory without changing behavior.

#### M-05B.2 — Define the semantic expression model

- [x] **M-05B.2a — Choose the expression model location and shape.** Inspect existing semantic value/fact types and define the smallest typed expression representation that fits the current architecture.
- [x] **M-05B.2b — Represent literal expressions.** Add typed literal nodes that preserve the literal's semantic value and type without storing target-language formatting.
- [x] **M-05B.2c — Represent variable references.** Add reference nodes that preserve the referenced semantic name/identity without embedding source syntax.
- [x] **M-05B.2d — Represent arithmetic expressions.** Add arithmetic nodes with language-neutral operator identities and ordered operands.
- [x] **M-05B.2e — Represent comparison expressions.** Add comparison nodes with language-neutral comparison identities and ordered operands.
- [x] **M-05B.2f — Represent boolean expressions.** Add boolean composition nodes for supported logical operations, preserving operand order.
- [x] **M-05B.2g — Represent grouped expressions.** Support explicit grouping/nesting without encoding parentheses as Python text.
- [x] **M-05B.2h — Add model-level tests.** Test construction, nested combinations, operand order, and preservation of literal/reference data. Do not add parsing or generation behavior in this task.

#### M-05B.3 — Capture structured expressions

- [x] **M-05B.3a — Add literal capture conversion.** Convert already-captured literal facts into semantic literal nodes without changing how requests are recognized.
- [x] **M-05B.3b — Add variable-reference capture conversion.** Convert captured identifiers/references into reference nodes while preserving their request-derived names.
- [x] **M-05B.3c — Capture arithmetic expressions structurally.** Populate arithmetic nodes from the existing capture path; preserve operator identity and operand order.
- [x] **M-05B.3d — Capture comparison expressions structurally.** Populate comparison nodes from the existing capture path without retaining a Python operator spelling as semantic meaning.
- [x] **M-05B.3e — Capture boolean expressions structurally.** Populate boolean nodes from existing captured facts, preserving composition and order.
- [x] **M-05B.3f — Capture nested/grouped expressions.** Preserve nested expression structure rather than flattening it into a source string.
- [x] **M-05B.3g — Verify captured data.** Add focused extraction tests with changed literals, identifiers, operators, and nested operands. Assert the structured values, not generated code.

#### M-05B.4 — Validate expression semantics

- [x] **M-05B.4a — Validate expression node structure.** Reject missing operands, unsupported node shapes, and invalid child combinations.
- [x] **M-05B.4b — Validate variable references.** Resolve each reference against the available semantic bindings and reject unresolved names.
- [x] **M-05B.4c — Validate reference scope.** Check that references are visible at the point where the expression is used; keep scope rules language-neutral.
- [x] **M-05B.4d — Validate operand and result types.** Check type compatibility for the currently supported arithmetic, comparison, and boolean operations.
- [x] **M-05B.4e — Integrate semantic expression validation.** Route structured expressions through the existing validation flow without parsing target-language source text.
- [x] **M-05B.4f — Add positive and negative validation tests.** Cover valid expressions, missing bindings, out-of-scope references, malformed trees, and incompatible operand types.

#### M-05B.5 — Realize expressions using language packs

- [x] **M-05B.5a — Define language-neutral operator identities.** Settle stable semantic identifiers for the currently supported arithmetic, comparison, and boolean operators.
- [x] **M-05B.5b — Declare arithmetic syntax in language packs.** Describe each supported arithmetic operator's target-language spelling and grammatical form declaratively.
- [x] **M-05B.5c — Declare comparison syntax in language packs.** Describe comparison spellings/forms without putting target-language tokens in shared Core.
- [x] **M-05B.5d — Declare boolean syntax in language packs.** Describe logical operator spellings/forms declaratively.
- [x] **M-05B.5e — Represent precedence and grouping.** Extend the pack grammar/metadata to express operator precedence and explicit grouping for supported expressions.
- [x] **M-05B.5f — Generate arithmetic expressions.** Realize structured arithmetic expressions through the existing sequence-generation mechanism.
- [x] **M-05B.5g — Generate comparison and boolean expressions.** Realize comparison and boolean trees through the same mechanism, including required grouping.
- [x] **M-05B.5h — Test pack-based expression realization.** Verify equivalent semantic trees render correctly using the selected language pack; keep assertions independent of Python-specific logic in shared code.

#### M-05B.6 — Remove the old expression-string path

- [x] **M-05B.6a — Identify obsolete expression-string checks.** Map each existing string-based expression validation branch to its structured-model replacement and tests.
- [x] **M-05B.6b — Remove arithmetic source-string validation.** Delete the obsolete arithmetic expression parsing/validation branch once structured validation covers its behavior.
- [x] **M-05B.6c — Remove comparison source-string validation.** Delete the obsolete comparison expression parsing/validation branch once structured validation covers its behavior.
- [x] **M-05B.6d — Remove boolean source-string validation.** Delete the obsolete boolean expression parsing/validation branch once structured validation covers its behavior.
- [x] **M-05B.6e — Remove value-expression source-string validation.** Delete the obsolete value-expression parsing/validation branch once structured validation covers its behavior.
- [x] **M-05B.6f — Remove obsolete loader entries.** Remove validation-kind allow-list/schema entries that no longer have consumers; retain generic structural validations.
- [x] **M-05B.6g — Remove dead expression-string helpers and tests.** Delete only helpers/tests proven unused after migration; retain tests for language-pack syntax and semantic expression behavior.
- [x] **M-05B.6h — Verify the old path is gone.** Run focused and full relevant tests; confirm no production path still treats Python-shaped expression strings as semantic values.

### M-06 — Add the minimum operational programming model

- [x] **M-06.1 — Map existing model types and relations to generation needs.** Compare `python-language-foundations.json`, the world-model loader/reasoner, and current `GenerationGoal` / `GenerationOperation` contracts. List only missing concepts or relations required by the queued migration tasks.
- [x] **M-06.2 — Add core program structure concepts.** Represent program goals, values, expressions, statements, variables, and bindings with language-neutral types and relations. Avoid adding concepts that have no current consumer.
- [ ] **M-06.3 — Add reusable operation contracts.** Represent operation inputs and results with types, and describe effects, prerequisites, and constraints in the world model rather than relying only on the current effect/argument-kind string match.
- [ ] **M-06.4 — Add function and control-flow relationships.** Represent functions, parameters, arguments, calls, returns, branches, loops, and their dependencies in a form the reasoner can query.
- [ ] **M-06.5 — Add module, exception, and I/O effect relationships.** Model imports/modules, failure behavior, input/output, and external effects only to the extent needed by M-10 and M-11.
- [ ] **M-06.6 — Verify model loading and reasoning.** Add focused loader/reasoner tests for the new concepts, typed operation contracts, and relationships. Confirm existing domain modules still load without Python-specific behavior in shared code.

### M-07 — Interpret scalar program requests generically

- [ ] **M-07.1 — Inventory scalar task-specific input rules.** Identify the exact rules and fact IDs for fixed comparisons, sign classification, two-number addition, assignments, and conditional output in `python.core.json`; map each to the values it currently captures.
- [ ] **M-07.2 — Define request-derived scalar requirements.** Represent numeric/string values, variable names, assignments, comparisons, branch conditions, and output requirements as generic semantic facts. Do not encode benchmark task names as semantic identities.
- [ ] **M-07.3 — Replace fixed-value captures with value captures.** Capture the actual literals and identifiers from the request; remove fixed sample values and task-specific facts from the active extraction path.
- [ ] **M-07.4 — Preserve multiple requirements and their order.** Ensure a request containing assignment, condition, and output retains each requirement and its dependencies instead of collapsing into one task category.
- [ ] **M-07.5 — Test scalar interpretation against variation.** Add tests with changed numbers, identifiers, output strings, and paraphrased requests; assert the extracted semantic values, not just the final code text.

### M-08 — Compose scalar programs from reusable operations

- [ ] **M-08.1 — Connect scalar requirements to operation knowledge.** Resolve assignments, arithmetic, comparisons, and output requirements to reusable typed operations and prerequisites from M-06.
- [ ] **M-08.2 — Generate assignments and arithmetic expressions.** Build scalar statements from request-derived expression/value nodes and the selected language pack; remove fixed assignment and addition program fragments.
- [ ] **M-08.3 — Generate comparisons and conditional branches.** Compose comparison, if/else, and branch-body structure from semantic conditions; remove sign-classification and fixed conditional-output solutions.
- [ ] **M-08.4 — Generate output from semantic values.** Bind output content from the request and realize it through the language pack; remove fixed greeting/output samples that function as complete solutions.
- [ ] **M-08.5 — Verify scalar composition behaviorally.** Run generated programs with multiple input values and branch outcomes. Assert results and side effects so a syntactically plausible but incorrect composition cannot pass.

### M-09 — Compose collection and iteration programs generically

- [ ] **M-09.1 — Inventory collection task shortcuts.** Map list filtering, manual totals, loop accumulators, dictionary iteration/mapping, parity filtering, and record sorting rules/templates to their captured facts and emitted fragments.
- [ ] **M-09.2 — Represent collection and element semantics.** Model collection element types, item/key/value access, and collection bindings without embedding Python literals or fixed collection contents.
- [ ] **M-09.3 — Represent iteration and accumulation.** Express iteration sources, loop variables, accumulator initialization/update, and aggregate results as reusable semantic structures.
- [ ] **M-09.4 — Represent filter, map, and sort requirements.** Express predicates, transformations, sort keys, and comparison requirements using the expression and operation model.
- [ ] **M-09.5 — Generate collection operations through the shared pipeline.** Add the language-pack realizations needed for iteration, filtering, mapping, aggregation, and sorting; do not add task-specific generation branches.
- [ ] **M-09.6 — Remove collection-specific solution templates.** Delete the replaced list/dictionary/record task facts and complete or near-complete outputs from the sequence module.
- [ ] **M-09.7 — Test collection generalization.** Vary collection length, item values, variable names, dictionary keys/values, record fields, predicates, and transformations; verify generated behavior.

### M-10 — Compose functions, classes, and error handling generically

- [ ] **M-10.1 — Inventory function and class templates.** Map greeting, integer-conversion, arithmetic-function, conditional-function, and bank-account/class examples to their task facts, bindings, and emitted code.
- [ ] **M-10.2 — Represent function declarations and parameters.** Model function name, parameter names/types, argument bindings, return type, and body requirements independently of source syntax.
- [ ] **M-10.3 — Represent calls and return behavior.** Model call arguments, return expressions, and the relationship between a function's inputs, body, and result.
- [ ] **M-10.4 — Represent classes and members.** Model class declarations, fields, methods, initialization, and member access as reusable semantic relationships.
- [ ] **M-10.5 — Represent exception behavior.** Model raised/handled errors and recovery requirements as effects and control-flow semantics, not fixed Python snippets.
- [ ] **M-10.6 — Add language-pack realizations and remove templates.** Generate the supported function/class/error structures through the shared pipeline, then delete the corresponding complete example programs.
- [ ] **M-10.7 — Test request-driven function and class generation.** Vary names, parameter counts, values, return expressions, members, and error cases; execute the generated programs where supported.

### M-11 — Compose file and serialization programs from API/effect knowledge

- [ ] **M-11.1 — Inventory file and serialization shortcuts.** Map file-line counting, directory listing, JSON round-trip, and related file tasks to their facts, fixed values, API calls, and complete outputs.
- [ ] **M-11.2 — Model file and directory operations.** Add typed read/write/list operations with paths, results, effects, failure conditions, and prerequisites.
- [ ] **M-11.3 — Model serialization operations.** Add reusable encode/decode operations and typed input/result relationships for JSON or other already-supported serialization behavior.
- [ ] **M-11.4 — Keep API signatures declarative.** Put actual library/API names, signatures, and language-specific invocation syntax in the relevant language/domain knowledge; do not place them in shared C# branches.
- [ ] **M-11.5 — Generate file and serialization programs from semantic requests.** Connect the modeled operations to the general planner and sequence pipeline; remove complete scripts and ordered per-task recipes.
- [ ] **M-11.6 — Test effects and failure cases.** Use temporary files/directories and varied paths/data to verify behavior, including missing-file and invalid-data cases where applicable.

### M-12 — Remove the Blender-specific code-generation route

- [ ] **M-12.1 — Identify the special-route entry points and callers.** Trace the Blender concept gates in `TaskCodeGenerationResponder`, the `ScriptCreationPattern` in `TaskRequestParser`, runtime dispatch, and tests. Record which paths are production-active.
- [ ] **M-12.2 — Generalize code-request dispatch.** Route ordinary code requests and application-specific script requests through the M-02 path; remove the separate Blender-only gate without adding another router.
- [ ] **M-12.3 — Derive Blender operations from domain/API knowledge.** Retain Blender object, scene, and API facts, but resolve the requested effects and bindings from reusable operation contracts rather than pre-authored step order.
- [ ] **M-12.4 — Remove the Blender task recipe.** Delete the task-specific ordered generation-step assertions and obsolete `blender-codegen-*` emission concepts from `blender-object-replacement-script.json`; keep reusable domain/API facts.
- [ ] **M-12.5 — Verify Blender uses the same production emitter.** Add or update end-to-end tests proving the Blender request reaches the shared path, uses request-derived values, and does not invoke a separate script-generation fallback.

### M-13 — Remove task-specific solutions from sequence knowledge

- [ ] **M-13.1 — Classify every sequence rule and input rule.** Audit all 136 sequence rules and 231 input rules in `python.core.json` as reusable syntax, reusable semantic recognition, task-specific recognition, fixed sample data, or complete/near-complete solution.
- [ ] **M-13.2 — Classify every validation and encoding.** Audit all 50 validations and the string encoding; retain generic structural checks and language-pack syntax declarations, and identify obsolete or task-specific validation.
- [ ] **M-13.3 — Remove replaced task-specific fact IDs and match rules.** Delete single-prompt fact IDs and one-off phrase matching after their generic replacements are covered by tests. Do not move those rules unchanged to another module.
- [ ] **M-13.4 — Remove fixed data and complete program outputs.** Delete fixed benchmark values and complete or near-complete program fragments once the corresponding compositional paths pass.
- [ ] **M-13.5 — Enforce anti-shortcut rules in tests.** Add checks that reject task-ID routing, fixed benchmark answers, and complete prompt-specific solutions in sequence knowledge or other generation modules.
- [ ] **M-13.6 — Reconcile counts and documentation.** Update the inventory and migration notes to describe the remaining reusable rules accurately; do not treat a lower rule count as proof of completion.

### M-14 — Prove the shared generator is not Python-dependent

- [ ] **M-14.1 — Select a minimal second-language proof scope.** Choose a small set of semantic goals already covered by the migrated model; do not add new programming features for this step.
- [ ] **M-14.2 — Add the second language pack.** Supply only the syntax productions, bindings, literal encoding, and operation realizations needed for that proof scope.
- [ ] **M-14.3 — Run equivalent requests through the same runtime path.** Verify both languages use the same request interpretation, semantic model, operation selection, and sequence-generation pipeline.
- [ ] **M-14.4 — Check shared code for language-specific branches.** Confirm Core/Runtime contains no Python or second-language parser, validator, or generation special case; language differences must remain in declarative packs.

### M-15 — Run acceptance checks and close the migration

- [ ] **M-15.1 — Run the migrated Python fundamentals suite.** Verify all supported fundamentals with changed values, identifiers, collection contents, and paraphrased requests.
- [ ] **M-15.2 — Run compositional and behavioral benchmarks.** Execute generated programs and verify their outputs/effects; include Blender cross-domain fixtures where the environment permits.
- [ ] **M-15.3 — Run repository validation.** Pass the Release build, full .NET test suite, external-tool tests, and applicable generation/behavioral tests. Fix failures before checking this task.
- [ ] **M-15.4 — Verify every migration completion condition.** Confirm one production emitter, language-neutral goals and operations, declarative language syntax/API knowledge, no task-specific complete solutions, and no Python-specific shared parsing/validation/generation.
- [ ] **M-15.5 — Update the architecture and close the queue.** Record the as-built final path and evidence in the architecture/inventory documents. Mark the migration complete only after all acceptance checks pass.

## Migration completion gate

The migration is complete only when all of the following are true:

- Code-generation requests are represented as language-neutral semantic goals and requirements before target-language realization.
- Programming operations and their relationships/effects are represented as reusable world-model or knowledge-base content, not as task handlers or prewritten solutions.
- Python syntax and API facts live in declarative Python language/domain knowledge; shared Core/Runtime contains no Python-specific parsing, validation, or generation branches.
- No complete or near-complete solution for a benchmark prompt is stored in sequence rules, knowledge packs, code, or task-ID templates.
- The production path uses one shared generation mechanism for Python and the minimal second language.
- Changed values, identifiers, collections, and equivalent phrasings still generate correct, executable programs.
- The coding benchmark's own anti-shortcut requirements are satisfied and verified.
- Release build, full .NET tests, external-tool tests, and applicable code-generation/behavioral tests pass.

**Explicitly deferred until this gate is met:** broader programming ontology expansion, new language support beyond the minimal migration proof, additional assistant reasoning domains, and general task/quality benchmark expansion.
