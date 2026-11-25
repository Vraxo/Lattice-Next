# Lattice-Next — finish the Python code-generation migration

**Mission:** remove the remaining Python-specific and task-specific code-generation shortcuts, replacing them with reusable programming semantics, world-model knowledge, and one shared language-neutral generation pipeline.

**Scope:** finish this migration before expanding programming capabilities, adding more languages, or starting other reasoning/benchmark domains. Historical baseline and completed-work notes are in [`docs/migration/python-code-generation-baseline.md`](../docs/migration/python-code-generation-baseline.md).

## Working rules

- Complete groups in order. Each checkbox should be one small, independently verifiable implementation task; split it further if inspection shows it is still too large.
- Keep changes narrow, run focused tests, and check tasks only after the change is merged and its checks pass.
- Python syntax and API facts belong in declarative language/domain knowledge. Shared Core/Runtime must remain language-neutral.
- Do not replace C# hard-codes with prompt-specific JSON rules or complete stored solutions.
- Preserve benchmark anti-shortcut rules; test changed values, identifiers, collection contents, and paraphrases.
- Do not add an LLM generator, another router, or a parallel generation engine.

## Completed work

- [x] **M-01–M-02 — Inventory and target architecture.** Recorded the existing shortcuts and established one semantic-to-code generation path.
- [x] **M-03–M-04 — Remove obsolete parser and Python collection literal.** Removed the dead expression parser and replaced `pythonListLiteral` with semantic collection values.
- [x] **M-05A — Move basic syntax checks into language packs.** Identifier, number, string, and function-call syntax checks now use selected language-pack metadata.
- [x] **M-05B — Make expression input language-neutral.** Completed expression inventory, semantic model, structured capture, semantic validation, pack-based realization, and removal of the old expression-string path.
- [x] **M-06 — Add the minimum operational programming model.** Added program structure, typed operation contracts/slots, effects, prerequisites, function/control-flow, module, exception, and I/O relationships with loader/reasoner tests.

## Remaining work

### M-07 — Interpret scalar program requests generically

- [x] **M-07.1 — Inventory scalar task-specific input rules.** Identify the exact rules and fact IDs for fixed comparisons, sign classification, two-number addition, assignments, and conditional output in `python.core.json`; map each to the values it currently captures.
- [x] **M-07.2 — Define request-derived scalar requirements.** Represent numeric/string values, variable names, assignments, comparisons, branch conditions, and output requirements as generic semantic facts. Do not encode benchmark task names as semantic identities.
- [ ] **M-07.3 — Replace fixed-value captures with value captures.** Capture the actual literals and identifiers from the request; remove fixed sample values and task-specific facts from the active extraction path.
  - [x] **M-07.3a — Verify generic assignment captures.** Capture changed variable names and numeric/text values with the existing general assignment rules; preserve quoted literal syntax for typed parsing.
  - [x] **M-07.3b — Project generic assignment facts.** Map the reusable assignment target/value facts to `AssignmentRequirement` values through the existing sequence fact extractor; do not map task-family fact IDs.
  - [ ] **M-07.3c — Retire fixed scalar task facts.** Replace addition/sign/conditional-output sample markers in the active extraction path with generic requirements, without retaining a parallel router or breaking the queued composition work.
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

- [ ] **M-13.1 — Classify every sequence rule and input rule.** Audit all sequence and input rules in `python.core.json` as reusable syntax, reusable semantic recognition, task-specific recognition, fixed sample data, or complete/near-complete solution.
- [ ] **M-13.2 — Classify every validation and encoding.** Audit validations and encodings; retain generic structural checks and language-pack syntax declarations, and identify obsolete or task-specific validation.
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

## Definition of done

The migration is complete only when:

- Requests become language-neutral semantic goals/requirements before target-language realization.
- Programming operations, relationships, and effects are reusable world-model/knowledge-base content, not task handlers or prewritten solutions.
- Python syntax/API facts remain declarative; shared Core/Runtime has no Python-specific parsing, validation, or generation branches.
- No complete or near-complete benchmark solution is stored in sequence rules, packs, code, or task-ID templates.
- Python and the minimal second language use one production generation mechanism.
- Changed values, identifiers, collections, and equivalent phrasings generate correct executable programs.
- Benchmark anti-shortcut requirements are satisfied.
- Release build, full .NET tests, external-tool tests, and applicable generation/behavioral tests pass.

**Deferred until completion:** broader programming ontology expansion, languages beyond the minimal proof, additional reasoning domains, and general benchmark expansion.
