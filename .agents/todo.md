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

- [x] **1–2 — Inventory and target architecture.** Recorded the existing shortcuts and established one semantic-to-code generation path.
- [x] **3–4 — Remove obsolete parser and Python collection literal.** Removed the dead expression parser and replaced `pythonListLiteral` with semantic collection values.
- [x] **5 — Move basic syntax checks into language packs.** Identifier, number, string, and function-call syntax checks now use selected language-pack metadata.
- [x] **5 — Make expression input language-neutral.** Completed expression inventory, semantic model, structured capture, semantic validation, pack-based realization, and removal of the old expression-string path.
- [x] **6 — Add the minimum operational programming model.** Added program structure, typed operation contracts/slots, effects, prerequisites, function/control-flow, module, exception, and I/O relationships with loader/reasoner tests.
- [x] **7 — Interpret scalar program requests generically.** Added request-order positions to semantic facts, projected comparison operands/operators, independent true/false branches, and request-derived output expressions into ordered `SemanticProgram` requirements, removed the fixed comparison sample captures, and added changed-value, changed-identifier, paraphrase, ordering, dependency, and mixed-requirement tests.

## Remaining work

Each numbered item is one focused implementation change with its own narrow verification. If code inspection shows an item crosses multiple independent behaviors, split it before implementing it. Keep replacement and deletion separate: prove the generic path first, then remove the old shortcut.

### 7. Interpret scalar program requests generically

- [x] **7.1 — Inventory scalar task-specific input rules.** Identify the exact rules and fact IDs for fixed comparisons, sign classification, two-number addition, assignments, and conditional output in `python.core.json`; map each to the values it currently captures.
- [x] **7.2 — Define request-derived scalar requirements.** Represent numeric/string values, variable names, assignments, comparisons, branch conditions, and output requirements as generic semantic facts. Do not encode benchmark task names as semantic identities.
- [x] **7.3 — Verify generic assignment captures.** Capture changed variable names and numeric/text values with the existing general assignment rules; preserve quoted literal syntax for typed parsing.
- [x] **7.4 — Project generic assignment facts.** Map reusable assignment target/value facts to `AssignmentRequirement` values through the existing sequence fact extractor; do not map task-family fact IDs.
- [x] **7.5 — Capture comparison operands from requests.** Extract both comparison operands as request-derived values and verify that changing either operand changes the projected requirement.
- [x] **7.6 — Capture comparison operators from requests.** Extract equality and ordering operators as semantic operators, not as fixed phrase-to-task matches.
- [x] **7.7 — Capture conditional branch requirements.** Project condition, true-branch, and false-branch requirements independently from request content.
- [x] **7.8 — Capture output values from requests.** Project requested output text/values without relying on fixed greeting or classification strings.
- [x] **7.9 — Remove fixed scalar sample captures.** Deleted the three marker-only fixed-comparison captures and their complete-program fragments; the addition and sign-classification facts remain only because their consumers are still required by the M-08 cutover (8.9, 8.14).
- [x] **7.10 — Preserve multiple scalar requirements.** Ensure two or more assignments/expressions from one request are all projected.
- [x] **7.11 — Preserve requirement dependencies.** Retain which values feed which expressions, conditions, and outputs.
- [x] **7.12 — Preserve source-request order where order is meaningful.** Verify ordered statements are not reordered or collapsed during extraction.
- [x] **7.13 — Test changed numeric and string literals.** Assert exact extracted values for multiple inputs.
- [x] **7.14 — Test changed identifiers.** Assert variable and parameter names are request-derived.
- [x] **7.15 — Test paraphrased scalar requests.** Assert equivalent wording yields equivalent semantic requirements.
- [x] **7.16 — Test mixed scalar requirements.** Assert a request with assignment, comparison, branches, and output retains the complete ordered requirement set.

### 8. Compose scalar programs from reusable operations

- [ ] **8.1 — Resolve assignment requirements to the assignment operation.** Select the reusable assignment contract from semantic requirements.
- [ ] **8.2 — Resolve arithmetic requirements to arithmetic operations.** Select the required arithmetic contract from operand/operator semantics.
- [ ] **8.3 — Resolve comparison requirements to comparison operations.** Select the comparison contract from the requested operator.
- [ ] **8.4 — Resolve output requirements to output operations.** Select the output contract and preserve its input binding.
- [ ] **8.5 — Resolve operation prerequisites.** Ensure required values and dependencies are available before each operation is planned.
- [ ] **8.6 — Realize a scalar assignment statement.** Emit one assignment from a semantic target and expression.
- [ ] **8.7 — Realize a binary arithmetic expression.** Emit one arithmetic expression from typed operands and an operator.
- [ ] **8.8 — Compose multiple assignments and expressions.** Emit a sequence whose bindings preserve data dependencies.
- [ ] **8.9 — Remove fixed assignment and addition fragments.** Delete replaced task-specific output fragments after generic realization is covered.
- [ ] **8.10 — Realize a comparison expression.** Emit the requested comparison from semantic operands/operator.
- [ ] **8.11 — Realize an if statement.** Emit a conditional with a semantic condition and true branch.
- [ ] **8.12 — Realize an if/else statement.** Emit both branches from independent branch requirements.
- [ ] **8.13 — Compose branch bodies with preceding assignments.** Preserve bindings from earlier statements into branch conditions and bodies.
- [ ] **8.14 — Remove fixed sign-classification solutions.** Delete the replaced sign-task facts and complete output only after generic conditional behavior passes.
- [ ] **8.15 — Bind a computed value to an output operation.** Pass the result of an earlier expression to output without converting it to a fixed sample.
- [ ] **8.16 — Bind a requested literal to an output operation.** Preserve request-derived output content through realization.
- [ ] **8.17 — Remove fixed greeting/output samples.** Delete complete or near-complete fixed output examples after generic output tests pass.
- [ ] **8.18 — Execute assignment and arithmetic examples.** Verify computed values for multiple operand sets.
- [ ] **8.19 — Execute comparison and branch examples.** Verify both true and false paths for each supported comparison family.
- [ ] **8.20 — Execute composed scalar programs.** Verify values flow correctly across assignments, expressions, branches, and output.

### 9. Compose collection and iteration programs generically

- [ ] **9.1 — Inventory list filtering rules and templates.** Record fixed predicates, captured values, and emitted fragments.
- [ ] **9.2 — Inventory iteration and accumulation rules and templates.** Record loop/total task facts and emitted fragments.
- [ ] **9.3 — Inventory dictionary mapping rules and templates.** Record key/value assumptions and emitted fragments.
- [ ] **9.4 — Inventory sorting and record rules and templates.** Record fixed fields, sort keys, and emitted fragments.
- [ ] **9.5 — Represent collection values and element types.** Model a collection binding and its element type without fixed contents.
- [ ] **9.6 — Represent iteration item bindings.** Model the relationship between an iteration source and its item variable.
- [ ] **9.7 — Represent dictionary key/value bindings.** Model key and value variables as distinct typed bindings.
- [ ] **9.8 — Test collection and element projections.** Verify different element types, names, and collection sizes.
- [ ] **9.9 — Represent a generic iteration requirement.** Model source collection, item binding, and loop body.
- [ ] **9.10 — Represent accumulator initialization.** Model initial value and accumulator type.
- [ ] **9.11 — Represent accumulator updates.** Model the update expression and its dependency on the current accumulator/item.
- [ ] **9.12 — Represent aggregate results.** Bind the completed accumulator to a result used by later operations.
- [ ] **9.13 — Represent a filter predicate.** Reuse expression semantics to describe which elements are retained.
- [ ] **9.14 — Represent a map transformation.** Describe the output expression for each input element.
- [ ] **9.15 — Represent a sort key.** Bind the key expression to the element being sorted.
- [ ] **9.16 — Represent sort direction and ordering.** Model ascending/descending and any supported ordering requirement.
- [ ] **9.17 — Realize iteration in the Python language pack.** Emit a loop from the generic iteration structure.
- [ ] **9.18 — Realize filtering in the Python language pack.** Emit a filter operation from source and predicate semantics.
- [ ] **9.19 — Realize mapping in the Python language pack.** Emit a transformation from source and map expression.
- [ ] **9.20 — Realize accumulation in the Python language pack.** Emit initialization, update, and result binding from accumulator semantics.
- [ ] **9.21 — Realize sorting in the Python language pack.** Emit a sort operation from key and direction semantics.
- [ ] **9.22 — Remove fixed list-filter solutions.** Delete replaced task-specific facts and complete output fragments.
- [ ] **9.23 — Remove fixed loop/total solutions.** Delete replaced task-specific facts and complete output fragments.
- [ ] **9.24 — Remove fixed dictionary-mapping solutions.** Delete replaced task-specific facts and complete output fragments.
- [ ] **9.25 — Remove fixed sorting/record solutions.** Delete replaced task-specific facts and complete output fragments.
- [ ] **9.26 — Test iteration and accumulation with varied data.** Vary size, values, and identifiers; execute generated programs.
- [ ] **9.27 — Test filter and map with varied expressions.** Change predicates and transformations; verify output collections.
- [ ] **9.28 — Test dictionary and record operations.** Vary keys, values, fields, and sort keys; verify generated behavior.

### 10. Compose functions, classes, and error handling generically

- [ ] **10.1 — Inventory function declaration examples.** Map function task facts, parameters, return types, and emitted fragments.
- [ ] **10.2 — Inventory function-call examples.** Map argument binding, call facts, and emitted fragments.
- [ ] **10.3 — Inventory class examples.** Map class, field, initializer, and method facts/templates.
- [ ] **10.4 — Inventory exception examples.** Map fixed try/except and raise facts/templates.
- [ ] **10.5 — Represent a function declaration.** Model its request-derived name and body requirement.
- [ ] **10.6 — Represent function parameters.** Model parameter names and types as ordered bindings.
- [ ] **10.7 — Represent a function return type.** Bind the declared result type to the function contract.
- [ ] **10.8 — Represent a function call.** Model target function and ordered argument expressions.
- [ ] **10.9 — Represent return expressions.** Model the result expression and its relationship to function inputs.
- [ ] **10.10 — Validate call arguments against parameters.** Check argument count and compatible types before realization.
- [ ] **10.11 — Represent a class declaration.** Model class identity independently of source syntax.
- [ ] **10.12 — Represent fields and initialization.** Model member values and constructor initialization requirements.
- [ ] **10.13 — Represent methods.** Model method parameters, body, and return behavior.
- [ ] **10.14 — Represent member access.** Bind an access expression to its owning instance and member.
- [ ] **10.15 — Represent a raised error.** Model error type and triggering condition.
- [ ] **10.16 — Represent a handled error.** Model handler type and recovery body.
- [ ] **10.17 — Represent normal and exceptional control flow.** Preserve the distinction between successful result and handled failure.
- [ ] **10.18 — Realize function declarations and parameters.** Add Python pack syntax for the semantic declaration model.
- [ ] **10.19 — Realize function calls and returns.** Add Python pack syntax for call and return structures.
- [ ] **10.20 — Realize classes, fields, and methods.** Add Python pack syntax for the supported class model.
- [ ] **10.21 — Realize exception structures.** Add Python pack syntax for raise and handling structures.
- [ ] **10.22 — Remove replaced function templates.** Delete complete function examples once their generic path is tested.
- [ ] **10.23 — Remove replaced class and exception templates.** Delete complete class/error examples once their generic paths are tested.
- [ ] **10.24 — Test functions with varied names and parameters.** Execute generated functions with different argument values.
- [ ] **10.25 — Test return expressions and calls.** Verify returned values and argument binding.
- [ ] **10.26 — Test classes and member access.** Verify initialization, method calls, and member values.
- [ ] **10.27 — Test error behavior.** Verify raised, handled, and unhandled failure paths as supported.

### 11. Compose file and serialization programs from API/effect knowledge

- [ ] **11.1 — Inventory file read/write task rules.** Map fixed paths, values, APIs, and emitted code.
- [ ] **11.2 — Inventory directory task rules.** Map directory listing/creation facts and emitted code.
- [ ] **11.3 — Inventory serialization task rules.** Map JSON encode/decode facts, fixed structures, and emitted code.
- [ ] **11.4 — Model file reading.** Define input path, returned content, and read effect.
- [ ] **11.5 — Model file writing.** Define path/content inputs and write effect.
- [ ] **11.6 — Model directory listing.** Define directory input and entry collection result.
- [ ] **11.7 — Model filesystem failure conditions.** Represent missing paths and permission/operation failures.
- [ ] **11.8 — Model serialization input and output values.** Define typed source/result relationships.
- [ ] **11.9 — Model encoding.** Define the transformation from semantic value to serialized representation.
- [ ] **11.10 — Model decoding.** Define the transformation from serialized representation to typed value.
- [ ] **11.11 — Model invalid-data outcomes.** Represent decode/validation failures explicitly.
- [ ] **11.12 — Move file API signatures into declarative language/domain knowledge.** Remove shared-code API-name conditionals for modeled file operations.
- [ ] **11.13 — Move serialization API signatures into declarative knowledge.** Keep library names and invocation syntax out of shared Core/Runtime.
- [ ] **11.14 — Generate a file-read program.** Resolve and realize one read request through the shared pipeline.
- [ ] **11.15 — Generate a file-write program.** Resolve and realize one write request through the shared pipeline.
- [ ] **11.16 — Generate a directory-operation program.** Resolve and realize one directory request through the shared pipeline.
- [ ] **11.17 — Generate a serialization program.** Resolve and realize encode/decode requirements through the shared pipeline.
- [ ] **11.18 — Remove replaced file/serialization recipes.** Delete task-specific ordered steps and complete scripts after generic paths pass.
- [ ] **11.19 — Test file operations with varied temporary paths and contents.** Verify actual filesystem effects.
- [ ] **11.20 — Test directory operations with varied directory contents.** Verify returned entries and effects.
- [ ] **11.21 — Test serialization round-trips and invalid data.** Verify values and explicit failure behavior.

### 12. Remove the Blender-specific code-generation route

- [ ] **12.1 — Trace Blender gates in `TaskCodeGenerationResponder`.** Record production callers and route conditions.
- [ ] **12.2 — Trace `ScriptCreationPattern` in `TaskRequestParser`.** Record its callers and parser-specific behavior.
- [ ] **12.3 — Trace Blender runtime dispatch and tests.** Identify the distinct path and assertions that depend on it.
- [ ] **12.4 — Route ordinary code requests through the shared semantic entry point.** Preserve existing supported behavior.
- [ ] **12.5 — Route application-specific script requests through the same entry point.** Pass domain context as knowledge, not as a separate generator.
- [ ] **12.6 — Remove the Blender-only dispatch gate.** Delete the obsolete branch after both request types use the shared entry point.
- [ ] **12.7 — Represent Blender object/scene goals as semantic requirements.** Keep requested object identities and desired effects request-derived.
- [ ] **12.8 — Resolve Blender API operations from declarative knowledge.** Select API contracts from goals and prerequisites rather than fixed step order.
- [ ] **12.9 — Bind Blender operation arguments from the request/world model.** Remove fixed object names and values from generation.
- [ ] **12.10 — Remove ordered task-step assertions.** Delete replaced step-sequence rules from `blender-object-replacement-script.json`.
- [ ] **12.11 — Remove obsolete Blender emission concepts.** Delete `blender-codegen-*` concepts no longer used by the shared pipeline.
- [ ] **12.12 — Preserve reusable Blender API/domain facts.** Verify object, scene, and API knowledge remains available after recipe removal.
- [ ] **12.13 — Test Blender request dispatch.** Assert the request reaches the shared production path.
- [ ] **12.14 — Test request-derived Blender bindings.** Vary object names and requested values; assert generated arguments change.
- [ ] **12.15 — Test absence of a Blender fallback.** Assert no separate script-generation route is invoked.

### 13. Remove task-specific solutions from sequence knowledge

- [ ] **13.1 — Classify sequence rules in `python.core.json`.** Label each as syntax, reusable semantic recognition, task-specific recognition, fixed data, or solution.
- [ ] **13.2 — Classify input rules in `python.core.json`.** Apply the same classification to all input/capture rules.
- [ ] **13.3 — Record disposition for each retained/deleted rule.** Link each task-specific rule to its generic replacement or removal rationale.
- [ ] **13.4 — Classify validation rules.** Separate generic structural validation from task-specific checks and language syntax.
- [ ] **13.5 — Classify encoding rules.** Separate semantic value encoding from fixed sample or task output encoding.
- [ ] **13.6 — Identify shared-code language branches.** Record Python-specific parsing/validation/realization branches for removal or pack migration.
- [ ] **13.7 — Remove obsolete scalar task fact IDs and phrase matches.** Keep generic scalar recognition and its tests.
- [ ] **13.8 — Remove obsolete collection task fact IDs and phrase matches.** Keep generic collection recognition and its tests.
- [ ] **13.9 — Remove obsolete function/class/file task fact IDs and phrase matches.** Keep generic recognition and its tests.
- [ ] **13.10 — Remove obsolete Blender task fact IDs and phrase matches.** Keep reusable domain/API knowledge.
- [ ] **13.11 — Remove fixed scalar benchmark data and outputs.** Retain only reusable examples that do not encode solutions.
- [ ] **13.12 — Remove fixed collection benchmark data and outputs.** Retain only generic collection semantics.
- [ ] **13.13 — Remove fixed function/class/file outputs.** Retain declarative operations and syntax knowledge.
- [ ] **13.14 — Remove remaining task-specific generation fragments.** Verify no equivalent copy survives elsewhere.
- [ ] **13.15 — Test against task-ID routing.** Fail if benchmark/task identities select generation solutions.
- [ ] **13.16 — Test against fixed benchmark answers.** Fail if changing request values leaves hard-coded outputs unchanged.
- [ ] **13.17 — Test against complete prompt-specific solutions.** Detect prohibited full-solution templates in sequence knowledge and generation modules.
- [ ] **13.18 — Reconcile rule/fact inventories.** Ensure documented counts match the current knowledge files.
- [ ] **13.19 — Update migration notes.** Document retained reusable knowledge and removed shortcuts with evidence.

### 14. Prove the shared generator is not Python-dependent

- [ ] **14.1 — Select semantic goals already supported by the migrated model.** Choose a small representative subset without adding new capabilities.
- [ ] **14.2 — Define equivalent cross-language fixtures.** Use identical semantic intent and varied request values for both languages.
- [ ] **14.3 — Add second-language identifier and literal syntax.** Supply declarative syntax rules for the proof scope.
- [ ] **14.4 — Add second-language expression and statement realization.** Realize the selected semantic structures through the pack.
- [ ] **14.5 — Add second-language operation/API bindings.** Declare only the operations needed by the proof fixtures.
- [ ] **14.6 — Verify shared request interpretation.** Both languages must produce equivalent language-neutral requirements.
- [ ] **14.7 — Verify shared operation selection.** Both languages must resolve equivalent semantic operations.
- [ ] **14.8 — Verify shared sequence planning and emission entry point.** Only the selected language pack may differ.
- [ ] **14.9 — Execute equivalent generated programs.** Verify both outputs/behaviors match the fixtures.
- [ ] **14.10 — Search shared Core/Runtime for Python-specific parsing and validation.** Remove any remaining branches by moving syntax facts to packs.
- [ ] **14.11 — Search shared Core/Runtime for Python-specific generation.** Remove any remaining realization branches by moving them to packs.
- [ ] **14.12 — Add a regression guard for shared language neutrality.** Fail tests when prohibited language-specific references reappear in shared generation code.

### 15. Run acceptance checks and close the migration

- [ ] **15.1 — Run scalar interpretation and generation fixtures.** Include changed values, identifiers, and paraphrases.
- [ ] **15.2 — Run collection interpretation and generation fixtures.** Include varied lengths, contents, predicates, and transformations.
- [ ] **15.3 — Run function/class/error fixtures.** Include varied signatures, members, return values, and failure paths.
- [ ] **15.4 — Run file/serialization fixtures.** Include varied paths/data and failure cases.
- [ ] **15.5 — Run multi-operation composition fixtures.** Verify data dependencies across statements and control flow.
- [ ] **15.6 — Run Blender cross-domain fixtures.** Verify domain-specific requests use the shared generator.
- [ ] **15.7 — Review benchmark failures for shortcut regressions.** Fix semantic/generation defects rather than adding prompt-specific patches.
- [ ] **15.8 — Pass the Release build.** Resolve all build failures.
- [ ] **15.9 — Pass the full .NET test suite.** Resolve all test failures.
- [ ] **15.10 — Pass external-tool and generation tests.** Run applicable integration checks and record unavailable environment-dependent checks.
- [ ] **15.11 — Verify one production emitter and language-neutral semantic planning.**
- [ ] **15.12 — Verify declarative language syntax/API knowledge and no Python-specific shared branches.**
- [ ] **15.13 — Verify no task-specific complete solutions remain.**
- [ ] **15.14 — Verify varied-value, paraphrase, and behavioral tests cover the migrated capabilities.**
- [ ] **15.15 — Record the as-built generation path and architecture.**
- [ ] **15.16 — Record final inventory/counts and acceptance evidence.**
- [ ] **15.17 — Mark the migration complete only after all acceptance checks pass.**

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
