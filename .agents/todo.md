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

Each unchecked item below is one focused implementation change with its own narrow verification. If code inspection shows an item crosses multiple independent behaviors, split it before implementing it. Keep replacement and deletion separate: prove the generic path first, then remove the old shortcut.

### M-07 — Interpret scalar program requests generically

- [x] **M-07.1 — Inventory scalar task-specific input rules.** Identify the exact rules and fact IDs for fixed comparisons, sign classification, two-number addition, assignments, and conditional output in `python.core.json`; map each to the values it currently captures.
- [x] **M-07.2 — Define request-derived scalar requirements.** Represent numeric/string values, variable names, assignments, comparisons, branch conditions, and output requirements as generic semantic facts. Do not encode benchmark task names as semantic identities.
- [ ] **M-07.3 — Replace fixed-value captures with value captures.**
  - [x] **M-07.3a — Verify generic assignment captures.** Capture changed variable names and numeric/text values with the existing general assignment rules; preserve quoted literal syntax for typed parsing.
  - [x] **M-07.3b — Project generic assignment facts.** Map reusable assignment target/value facts to `AssignmentRequirement` values through the existing sequence fact extractor; do not map task-family fact IDs.
  - [ ] **M-07.3c — Capture comparison operands from requests.** Extract both comparison operands as request-derived values and verify that changing either operand changes the projected requirement.
  - [ ] **M-07.3d — Capture comparison operators from requests.** Extract equality and ordering operators as semantic operators, not as fixed phrase-to-task matches.
  - [ ] **M-07.3e — Capture conditional branch requirements.** Project condition, true-branch, and false-branch requirements independently from request content.
  - [ ] **M-07.3f — Capture output values from requests.** Project requested output text/values without relying on fixed greeting or classification strings.
  - [ ] **M-07.3g — Remove fixed scalar sample captures.** Delete obsolete fixed-value capture rules only after the preceding generic captures have focused tests.
- [ ] **M-07.4 — Preserve multiple requirements and their order.**
  - [ ] **M-07.4a — Preserve multiple scalar requirements.** Ensure two or more assignments/expressions from one request are all projected.
  - [ ] **M-07.4b — Preserve requirement dependencies.** Retain which values feed which expressions, conditions, and outputs.
  - [ ] **M-07.4c — Preserve source-request order where order is meaningful.** Verify ordered statements are not reordered or collapsed during extraction.
- [ ] **M-07.5 — Test scalar interpretation against variation.**
  - [ ] **M-07.5a — Test changed numeric and string literals.** Assert exact extracted values for multiple inputs.
  - [ ] **M-07.5b — Test changed identifiers.** Assert variable and parameter names are request-derived.
  - [ ] **M-07.5c — Test paraphrased scalar requests.** Assert equivalent wording yields equivalent semantic requirements.
  - [ ] **M-07.5d — Test mixed scalar requirements.** Assert a request with assignment, comparison, branches, and output retains the complete ordered requirement set.

### M-08 — Compose scalar programs from reusable operations

- [ ] **M-08.1 — Connect scalar requirements to operation knowledge.**
  - [ ] **M-08.1a — Resolve assignment requirements to the assignment operation.** Select the reusable assignment contract from semantic requirements.
  - [ ] **M-08.1b — Resolve arithmetic requirements to arithmetic operations.** Select the required arithmetic contract from operand/operator semantics.
  - [ ] **M-08.1c — Resolve comparison requirements to comparison operations.** Select the comparison contract from the requested operator.
  - [ ] **M-08.1d — Resolve output requirements to output operations.** Select the output contract and preserve its input binding.
  - [ ] **M-08.1e — Resolve operation prerequisites.** Ensure required values and dependencies are available before each operation is planned.
- [ ] **M-08.2 — Generate assignments and arithmetic expressions.**
  - [ ] **M-08.2a — Realize a scalar assignment statement.** Emit one assignment from a semantic target and expression.
  - [ ] **M-08.2b — Realize a binary arithmetic expression.** Emit one arithmetic expression from typed operands and an operator.
  - [ ] **M-08.2c — Compose multiple assignments and expressions.** Emit a sequence whose bindings preserve data dependencies.
  - [ ] **M-08.2d — Remove fixed assignment and addition fragments.** Delete replaced task-specific output fragments after generic realization is covered.
- [ ] **M-08.3 — Generate comparisons and conditional branches.**
  - [ ] **M-08.3a — Realize a comparison expression.** Emit the requested comparison from semantic operands/operator.
  - [ ] **M-08.3b — Realize an if statement.** Emit a conditional with a semantic condition and true branch.
  - [ ] **M-08.3c — Realize an if/else statement.** Emit both branches from independent branch requirements.
  - [ ] **M-08.3d — Compose branch bodies with preceding assignments.** Preserve bindings from earlier statements into branch conditions and bodies.
  - [ ] **M-08.3e — Remove fixed sign-classification solutions.** Delete the replaced sign-task facts and complete output only after generic conditional behavior passes.
- [ ] **M-08.4 — Generate output from semantic values.**
  - [ ] **M-08.4a — Bind a computed value to an output operation.** Pass the result of an earlier expression to output without converting it to a fixed sample.
  - [ ] **M-08.4b — Bind a requested literal to an output operation.** Preserve request-derived output content through realization.
  - [ ] **M-08.4c — Remove fixed greeting/output samples.** Delete complete or near-complete fixed output examples after generic output tests pass.
- [ ] **M-08.5 — Verify scalar composition behaviorally.**
  - [ ] **M-08.5a — Execute assignment and arithmetic examples.** Verify computed values for multiple operand sets.
  - [ ] **M-08.5b — Execute comparison and branch examples.** Verify both true and false paths for each supported comparison family.
  - [ ] **M-08.5c — Execute composed scalar programs.** Verify values flow correctly across assignments, expressions, branches, and output.

### M-09 — Compose collection and iteration programs generically

- [ ] **M-09.1 — Inventory collection task shortcuts.**
  - [ ] **M-09.1a — Inventory list filtering rules and templates.** Record fixed predicates, captured values, and emitted fragments.
  - [ ] **M-09.1b — Inventory iteration and accumulation rules and templates.** Record loop/total task facts and emitted fragments.
  - [ ] **M-09.1c — Inventory dictionary mapping rules and templates.** Record key/value assumptions and emitted fragments.
  - [ ] **M-09.1d — Inventory sorting and record rules and templates.** Record fixed fields, sort keys, and emitted fragments.
- [ ] **M-09.2 — Represent collection and element semantics.**
  - [ ] **M-09.2a — Represent collection values and element types.** Model a collection binding and its element type without fixed contents.
  - [ ] **M-09.2b — Represent iteration item bindings.** Model the relationship between an iteration source and its item variable.
  - [ ] **M-09.2c — Represent dictionary key/value bindings.** Model key and value variables as distinct typed bindings.
  - [ ] **M-09.2d — Test collection and element projections.** Verify different element types, names, and collection sizes.
- [ ] **M-09.3 — Represent iteration and accumulation.**
  - [ ] **M-09.3a — Represent a generic iteration requirement.** Model source collection, item binding, and loop body.
  - [ ] **M-09.3b — Represent accumulator initialization.** Model initial value and accumulator type.
  - [ ] **M-09.3c — Represent accumulator updates.** Model the update expression and its dependency on the current accumulator/item.
  - [ ] **M-09.3d — Represent aggregate results.** Bind the completed accumulator to a result used by later operations.
- [ ] **M-09.4 — Represent filter, map, and sort requirements.**
  - [ ] **M-09.4a — Represent a filter predicate.** Reuse expression semantics to describe which elements are retained.
  - [ ] **M-09.4b — Represent a map transformation.** Describe the output expression for each input element.
  - [ ] **M-09.4c — Represent a sort key.** Bind the key expression to the element being sorted.
  - [ ] **M-09.4d — Represent sort direction and ordering.** Model ascending/descending and any supported ordering requirement.
- [ ] **M-09.5 — Generate collection operations through the shared pipeline.**
  - [ ] **M-09.5a — Realize iteration in the Python language pack.** Emit a loop from the generic iteration structure.
  - [ ] **M-09.5b — Realize filtering in the Python language pack.** Emit a filter operation from source and predicate semantics.
  - [ ] **M-09.5c — Realize mapping in the Python language pack.** Emit a transformation from source and map expression.
  - [ ] **M-09.5d — Realize accumulation in the Python language pack.** Emit initialization, update, and result binding from accumulator semantics.
  - [ ] **M-09.5e — Realize sorting in the Python language pack.** Emit a sort operation from key and direction semantics.
- [ ] **M-09.6 — Remove collection-specific solution templates.**
  - [ ] **M-09.6a — Remove fixed list-filter solutions.** Delete replaced task-specific facts and complete output fragments.
  - [ ] **M-09.6b — Remove fixed loop/total solutions.** Delete replaced task-specific facts and complete output fragments.
  - [ ] **M-09.6c — Remove fixed dictionary-mapping solutions.** Delete replaced task-specific facts and complete output fragments.
  - [ ] **M-09.6d — Remove fixed sorting/record solutions.** Delete replaced task-specific facts and complete output fragments.
- [ ] **M-09.7 — Test collection generalization.**
  - [ ] **M-09.7a — Test iteration and accumulation with varied data.** Vary size, values, and identifiers; execute generated programs.
  - [ ] **M-09.7b — Test filter and map with varied expressions.** Change predicates and transformations; verify output collections.
  - [ ] **M-09.7c — Test dictionary and record operations.** Vary keys, values, fields, and sort keys; verify generated behavior.

### M-10 — Compose functions, classes, and error handling generically

- [ ] **M-10.1 — Inventory function and class templates.**
  - [ ] **M-10.1a — Inventory function declaration examples.** Map function task facts, parameters, return types, and emitted fragments.
  - [ ] **M-10.1b — Inventory function-call examples.** Map argument binding, call facts, and emitted fragments.
  - [ ] **M-10.1c — Inventory class examples.** Map class, field, initializer, and method facts/templates.
  - [ ] **M-10.1d — Inventory exception examples.** Map fixed try/except and raise facts/templates.
- [ ] **M-10.2 — Represent function declarations and parameters.**
  - [ ] **M-10.2a — Represent a function declaration.** Model its request-derived name and body requirement.
  - [ ] **M-10.2b — Represent function parameters.** Model parameter names and types as ordered bindings.
  - [ ] **M-10.2c — Represent a function return type.** Bind the declared result type to the function contract.
- [ ] **M-10.3 — Represent calls and return behavior.**
  - [ ] **M-10.3a — Represent a function call.** Model target function and ordered argument expressions.
  - [ ] **M-10.3b — Represent return expressions.** Model the result expression and its relationship to function inputs.
  - [ ] **M-10.3c — Validate call arguments against parameters.** Check argument count and compatible types before realization.
- [ ] **M-10.4 — Represent classes and members.**
  - [ ] **M-10.4a — Represent a class declaration.** Model class identity independently of source syntax.
  - [ ] **M-10.4b — Represent fields and initialization.** Model member values and constructor initialization requirements.
  - [ ] **M-10.4c — Represent methods.** Model method parameters, body, and return behavior.
  - [ ] **M-10.4d — Represent member access.** Bind an access expression to its owning instance and member.
- [ ] **M-10.5 — Represent exception behavior.**
  - [ ] **M-10.5a — Represent a raised error.** Model error type and triggering condition.
  - [ ] **M-10.5b — Represent a handled error.** Model handler type and recovery body.
  - [ ] **M-10.5c — Represent normal and exceptional control flow.** Preserve the distinction between successful result and handled failure.
- [ ] **M-10.6 — Realize functions, classes, and errors; remove templates.**
  - [ ] **M-10.6a — Realize function declarations and parameters.** Add Python pack syntax for the semantic declaration model.
  - [ ] **M-10.6b — Realize function calls and returns.** Add Python pack syntax for call and return structures.
  - [ ] **M-10.6c — Realize classes, fields, and methods.** Add Python pack syntax for the supported class model.
  - [ ] **M-10.6d — Realize exception structures.** Add Python pack syntax for raise and handling structures.
  - [ ] **M-10.6e — Remove replaced function templates.** Delete complete function examples once their generic path is tested.
  - [ ] **M-10.6f — Remove replaced class and exception templates.** Delete complete class/error examples once their generic paths are tested.
- [ ] **M-10.7 — Test request-driven function and class generation.**
  - [ ] **M-10.7a — Test functions with varied names and parameters.** Execute generated functions with different argument values.
  - [ ] **M-10.7b — Test return expressions and calls.** Verify returned values and argument binding.
  - [ ] **M-10.7c — Test classes and member access.** Verify initialization, method calls, and member values.
  - [ ] **M-10.7d — Test error behavior.** Verify raised, handled, and unhandled failure paths as supported.

### M-11 — Compose file and serialization programs from API/effect knowledge

- [ ] **M-11.1 — Inventory file and serialization shortcuts.**
  - [ ] **M-11.1a — Inventory file read/write task rules.** Map fixed paths, values, APIs, and emitted code.
  - [ ] **M-11.1b — Inventory directory task rules.** Map directory listing/creation facts and emitted code.
  - [ ] **M-11.1c — Inventory serialization task rules.** Map JSON encode/decode facts, fixed structures, and emitted code.
- [ ] **M-11.2 — Model file and directory operations.**
  - [ ] **M-11.2a — Model file reading.** Define input path, returned content, and read effect.
  - [ ] **M-11.2b — Model file writing.** Define path/content inputs and write effect.
  - [ ] **M-11.2c — Model directory listing.** Define directory input and entry collection result.
  - [ ] **M-11.2d — Model filesystem failure conditions.** Represent missing paths and permission/operation failures.
- [ ] **M-11.3 — Model serialization operations.**
  - [ ] **M-11.3a — Model serialization input and output values.** Define typed source/result relationships.
  - [ ] **M-11.3b — Model encoding.** Define the transformation from semantic value to serialized representation.
  - [ ] **M-11.3c — Model decoding.** Define the transformation from serialized representation to typed value.
  - [ ] **M-11.3d — Model invalid-data outcomes.** Represent decode/validation failures explicitly.
- [ ] **M-11.4 — Keep API signatures declarative.**
  - [ ] **M-11.4a — Move file API signatures into declarative language/domain knowledge.** Remove shared-code API-name conditionals for modeled file operations.
  - [ ] **M-11.4b — Move serialization API signatures into declarative knowledge.** Keep library names and invocation syntax out of shared Core/Runtime.
- [ ] **M-11.5 — Generate file and serialization programs from semantic requests.**
  - [ ] **M-11.5a — Generate a file-read program.** Resolve and realize one read request through the shared pipeline.
  - [ ] **M-11.5b — Generate a file-write program.** Resolve and realize one write request through the shared pipeline.
  - [ ] **M-11.5c — Generate a directory-operation program.** Resolve and realize one directory request through the shared pipeline.
  - [ ] **M-11.5d — Generate a serialization program.** Resolve and realize encode/decode requirements through the shared pipeline.
  - [ ] **M-11.5e — Remove replaced file/serialization recipes.** Delete task-specific ordered steps and complete scripts after generic paths pass.
- [ ] **M-11.6 — Test effects and failure cases.**
  - [ ] **M-11.6a — Test file operations with varied temporary paths and contents.** Verify actual filesystem effects.
  - [ ] **M-11.6b — Test directory operations with varied directory contents.** Verify returned entries and effects.
  - [ ] **M-11.6c — Test serialization round-trips and invalid data.** Verify values and explicit failure behavior.

### M-12 — Remove the Blender-specific code-generation route

- [ ] **M-12.1 — Identify the special-route entry points and callers.**
  - [ ] **M-12.1a — Trace Blender gates in `TaskCodeGenerationResponder`.** Record production callers and route conditions.
  - [ ] **M-12.1b — Trace `ScriptCreationPattern` in `TaskRequestParser`.** Record its callers and parser-specific behavior.
  - [ ] **M-12.1c — Trace Blender runtime dispatch and tests.** Identify the distinct path and assertions that depend on it.
- [ ] **M-12.2 — Generalize code-request dispatch.**
  - [ ] **M-12.2a — Route ordinary code requests through the shared semantic entry point.** Preserve existing supported behavior.
  - [ ] **M-12.2b — Route application-specific script requests through the same entry point.** Pass domain context as knowledge, not as a separate generator.
  - [ ] **M-12.2c — Remove the Blender-only dispatch gate.** Delete the obsolete branch after both request types use the shared entry point.
- [ ] **M-12.3 — Derive Blender operations from domain/API knowledge.**
  - [ ] **M-12.3a — Represent Blender object/scene goals as semantic requirements.** Keep requested object identities and desired effects request-derived.
  - [ ] **M-12.3b — Resolve Blender API operations from declarative knowledge.** Select API contracts from goals and prerequisites rather than fixed step order.
  - [ ] **M-12.3c — Bind Blender operation arguments from the request/world model.** Remove fixed object names and values from generation.
- [ ] **M-12.4 — Remove the Blender task recipe.**
  - [ ] **M-12.4a — Remove ordered task-step assertions.** Delete replaced step-sequence rules from `blender-object-replacement-script.json`.
  - [ ] **M-12.4b — Remove obsolete Blender emission concepts.** Delete `blender-codegen-*` concepts no longer used by the shared pipeline.
  - [ ] **M-12.4c — Preserve reusable Blender API/domain facts.** Verify object, scene, and API knowledge remains available after recipe removal.
- [ ] **M-12.5 — Verify Blender uses the same production emitter.**
  - [ ] **M-12.5a — Test Blender request dispatch.** Assert the request reaches the shared production path.
  - [ ] **M-12.5b — Test request-derived Blender bindings.** Vary object names and requested values; assert generated arguments change.
  - [ ] **M-12.5c — Test absence of a Blender fallback.** Assert no separate script-generation route is invoked.

### M-13 — Remove task-specific solutions from sequence knowledge

- [ ] **M-13.1 — Classify every sequence rule and input rule.**
  - [ ] **M-13.1a — Classify sequence rules in `python.core.json`.** Label each as syntax, reusable semantic recognition, task-specific recognition, fixed data, or solution.
  - [ ] **M-13.1b — Classify input rules in `python.core.json`.** Apply the same classification to all input/capture rules.
  - [ ] **M-13.1c — Record disposition for each retained/deleted rule.** Link each task-specific rule to its generic replacement or removal rationale.
- [ ] **M-13.2 — Classify every validation and encoding.**
  - [ ] **M-13.2a — Classify validation rules.** Separate generic structural validation from task-specific checks and language syntax.
  - [ ] **M-13.2b — Classify encoding rules.** Separate semantic value encoding from fixed sample or task output encoding.
  - [ ] **M-13.2c — Identify shared-code language branches.** Record Python-specific parsing/validation/realization branches for removal or pack migration.
- [ ] **M-13.3 — Remove replaced task-specific fact IDs and match rules.**
  - [ ] **M-13.3a — Remove obsolete scalar task fact IDs and phrase matches.** Keep generic scalar recognition and its tests.
  - [ ] **M-13.3b — Remove obsolete collection task fact IDs and phrase matches.** Keep generic collection recognition and its tests.
  - [ ] **M-13.3c — Remove obsolete function/class/file task fact IDs and phrase matches.** Keep generic recognition and its tests.
  - [ ] **M-13.3d — Remove obsolete Blender task fact IDs and phrase matches.** Keep reusable domain/API knowledge.
- [ ] **M-13.4 — Remove fixed data and complete program outputs.**
  - [ ] **M-13.4a — Remove fixed scalar benchmark data and outputs.** Retain only reusable examples that do not encode solutions.
  - [ ] **M-13.4b — Remove fixed collection benchmark data and outputs.** Retain only generic collection semantics.
  - [ ] **M-13.4c — Remove fixed function/class/file outputs.** Retain declarative operations and syntax knowledge.
  - [ ] **M-13.4d — Remove remaining task-specific generation fragments.** Verify no equivalent copy survives elsewhere.
- [ ] **M-13.5 — Enforce anti-shortcut rules in tests.**
  - [ ] **M-13.5a — Test against task-ID routing.** Fail if benchmark/task identities select generation solutions.
  - [ ] **M-13.5b — Test against fixed benchmark answers.** Fail if changing request values leaves hard-coded outputs unchanged.
  - [ ] **M-13.5c — Test against complete prompt-specific solutions.** Detect prohibited full-solution templates in sequence knowledge and generation modules.
- [ ] **M-13.6 — Reconcile counts and documentation.**
  - [ ] **M-13.6a — Reconcile rule/fact inventories.** Ensure documented counts match the current knowledge files.
  - [ ] **M-13.6b — Update migration notes.** Document retained reusable knowledge and removed shortcuts with evidence.

### M-14 — Prove the shared generator is not Python-dependent

- [ ] **M-14.1 — Select a minimal second-language proof scope.**
  - [ ] **M-14.1a — Select semantic goals already supported by the migrated model.** Choose a small representative subset without adding new capabilities.
  - [ ] **M-14.1b — Define equivalent cross-language fixtures.** Use identical semantic intent and varied request values for both languages.
- [ ] **M-14.2 — Add the second language pack.**
  - [ ] **M-14.2a — Add second-language identifier and literal syntax.** Supply declarative syntax rules for the proof scope.
  - [ ] **M-14.2b — Add second-language expression and statement realization.** Realize the selected semantic structures through the pack.
  - [ ] **M-14.2c — Add second-language operation/API bindings.** Declare only the operations needed by the proof fixtures.
- [ ] **M-14.3 — Run equivalent requests through the same runtime path.**
  - [ ] **M-14.3a — Verify shared request interpretation.** Both languages must produce equivalent language-neutral requirements.
  - [ ] **M-14.3b — Verify shared operation selection.** Both languages must resolve equivalent semantic operations.
  - [ ] **M-14.3c — Verify shared sequence planning and emission entry point.** Only the selected language pack may differ.
  - [ ] **M-14.3d — Execute equivalent generated programs.** Verify both outputs/behaviors match the fixtures.
- [ ] **M-14.4 — Check shared code for language-specific branches.**
  - [ ] **M-14.4a — Search shared Core/Runtime for Python-specific parsing and validation.** Remove any remaining branches by moving syntax facts to packs.
  - [ ] **M-14.4b — Search shared Core/Runtime for Python-specific generation.** Remove any remaining realization branches by moving them to packs.
  - [ ] **M-14.4c — Add a regression guard for shared language neutrality.** Fail tests when prohibited language-specific references reappear in shared generation code.

### M-15 — Run acceptance checks and close the migration

- [ ] **M-15.1 — Run the migrated Python fundamentals suite.**
  - [ ] **M-15.1a — Run scalar interpretation and generation fixtures.** Include changed values, identifiers, and paraphrases.
  - [ ] **M-15.1b — Run collection interpretation and generation fixtures.** Include varied lengths, contents, predicates, and transformations.
  - [ ] **M-15.1c — Run function/class/error fixtures.** Include varied signatures, members, return values, and failure paths.
  - [ ] **M-15.1d — Run file/serialization fixtures.** Include varied paths/data and failure cases.
- [ ] **M-15.2 — Run compositional and behavioral benchmarks.**
  - [ ] **M-15.2a — Run multi-operation composition fixtures.** Verify data dependencies across statements and control flow.
  - [ ] **M-15.2b — Run Blender cross-domain fixtures.** Verify domain-specific requests use the shared generator.
  - [ ] **M-15.2c — Review benchmark failures for shortcut regressions.** Fix semantic/generation defects rather than adding prompt-specific patches.
- [ ] **M-15.3 — Run repository validation.**
  - [ ] **M-15.3a — Pass the Release build.** Resolve all build failures.
  - [ ] **M-15.3b — Pass the full .NET test suite.** Resolve all test failures.
  - [ ] **M-15.3c — Pass external-tool and generation tests.** Run applicable integration checks and record unavailable environment-dependent checks.
- [ ] **M-15.4 — Verify every migration completion condition.**
  - [ ] **M-15.4a — Verify one production emitter and language-neutral semantic planning.**
  - [ ] **M-15.4b — Verify declarative language syntax/API knowledge and no Python-specific shared branches.**
  - [ ] **M-15.4c — Verify no task-specific complete solutions remain.**
  - [ ] **M-15.4d — Verify varied-value, paraphrase, and behavioral tests cover the migrated capabilities.**
- [ ] **M-15.5 — Update the architecture and close the queue.**
  - [ ] **M-15.5a — Record the as-built generation path and architecture.**
  - [ ] **M-15.5b — Record final inventory/counts and acceptance evidence.**
  - [ ] **M-15.5c — Mark the migration complete only after all acceptance checks pass.**

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
