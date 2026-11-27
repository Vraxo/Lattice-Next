# Code-generation migration — acceptance record

This is the acceptance record for section 15 of `.agents/todo.md`: what was run, what it exercised, and what it
proved. Verification is CI-only — this sandbox has no `dotnet` — so every claim below rests on the
`Build & test (.NET 10)` job in `.github/workflows/ci.yml`, which builds `Lattice.slnx` in **Release** and then runs
`dotnet test Lattice.slnx --configuration Release --no-build`. A green run of that job is the Release build and the
full suite at once.

The suite holds **646** Core test methods, **223** Runtime, **19** LM Studio translator, **8** CLI, **6** server, and
**3** benchmark; theories expand those methods into the executed cases each run records.

## What the acceptance pass changed

Running the checks found one real gap, and closing it changed production behaviour:

* **A word output was still emitted by the rule engine.** Requests that only state what to print ("prints 'Ready'",
  "outputs Hello World", "sets total to 5 and prints 'Done'") carried `program.output`, `program.output.start` and
  `value.output` facts, and `SemanticProgramPlanner` declined any request carrying them, so the token-rule engine in
  `KnowledgeDrivenSequenceGenerator` emitted the program. `SemanticProgramFactMapper.TryProjectCapturedOutput` now
  projects the captured words onto an `OutputRequirement`, and those facts are no longer declared unmapped — so the
  family plans through the shared pipeline like every migrated family. The projection fires only when the request
  states nothing else (a declared loop, an arithmetic expression, or a typed output keeps its own path), which the
  composed-program fixtures check.
* **The answer's meaning changed for that family**, deliberately: it now reads
  `semantic-requirements(write-standard-output)` instead of `semantic-facts(language.python, …)`, because the answer is
  derived from requirements rather than from the extracted fact ids. The runtime fixtures were updated to assert the
  new meaning. This is the one output change of the acceptance pass; the generated code is unchanged.
* **An output stated as an expression** ("prints total + 3", "prints result - 4") stayed on its own path at this point: the
  input rules captured no typed expression for it, so `value.output.expression` was declared unmapped. 15.18 closed that,
  together with declared loops; see §15.18 below.

## 15.1 — scalar interpretation and generation fixtures

| What varies | Where it is exercised |
| --- | --- |
| changed numeric and text literals, identifiers | `ScalarProgramInterpretationTests.Projects_changed_numeric_literals_exactly`, `…Projects_changed_text_literals_and_identifiers_exactly` |
| paraphrased requests, paraphrased conditions | `…Projects_equivalent_requirements_from_paraphrased_requests`, `…Projects_paraphrased_conditions_with_equivalent_meaning` |
| multiple requirements, their order and dependencies | `…Projects_multiple_scalar_requirements_from_one_request`, `…Preserves_requirement_dependencies_as_expression_references`, `…Preserves_source_request_order_of_scalar_requirements`, `…Retains_complete_ordered_requirement_set_for_mixed_scalar_requests` |
| generated and executed programs | `PythonProgramGenerationTests.Generates_and_executes_requested_scalar_programs`, `…Generates_two_number_addition_from_a_natural_language_request`, `…Classifies_positive_negative_and_zero_integers`, `…Generates_two_number_addition_from_declarative_assignment_facts` |

Both changed values and paraphrases are also guarded directly:
`SequenceKnowledgeGuardTests.Generates_different_programs_when_the_request_values_change` and
`…Generates_different_programs_when_the_request_names_change` fail when two different requests collapse onto one
program.

## 15.2 — collection interpretation and generation fixtures

`GeneratedCollectionProgramExecutionTests` runs each generated program and compares what it prints:
`Iterates_a_collection_in_source_order`, `Accumulates_collections_of_varied_size_and_values`,
`Filters_with_varied_predicates_and_names`, `Maps_with_varied_transformations_and_names`,
`Sorts_by_the_requested_key_and_direction`, `Iterates_dictionary_pairs_with_varied_keys_and_values`. Length, contents,
predicate, transformation, and sort direction vary between cases, and the identifiers come from the request.

## 15.3 — function, class, and error fixtures

`GeneratedFunctionProgramExecutionTests`: `Calls_a_generated_function_with_varied_arguments`,
`Binds_arguments_positionally_to_the_declared_parameters`, `Returns_and_prints_a_text_parameter`,
`Verifies_initialization_method_calls_and_member_values`, `Verifies_a_handled_failure_on_both_paths`,
`Raises_an_unhandled_error`, `Raises_only_when_the_condition_holds`. The planning side of the same families is
`SemanticProgramFunctionTests` and `SemanticProgramClassAndErrorTests`.

## 15.4 — file and serialization fixtures

`PythonProgramGenerationTests` covers varied paths and data plus the failure cases:
`Generates_and_executes_non_empty_file_line_counting_program`, `Generates_and_executes_json_file_round_trip_program`,
`Generalizes_json_round_trip_variable_and_file_names`, `Rejects_incomplete_or_invalid_json_round_trip_requests`,
`Generates_and_executes_directory_file_listing_with_missing_path_handling`.

## 15.5 — multi-operation composition fixtures

`PythonProgramGenerationTests.Generates_and_executes_composed_python_programs` (loops, assignments and outputs
composed in one request), `…Generates_and_executes_ordered_function_composition_declaratively`,
`…Generates_and_executes_grouped_record_totals`, and
`CrossLanguageExecutionTests.Reports_the_same_result_in_each_language`, which composes several operations and then
checks that the Python and JavaScript programs report the same result.

## 15.6 — Blender cross-domain fixtures

`BlenderScriptGenerationTests` holds the domain's acceptance set: `Generates_the_modelled_script_from_domain_knowledge`,
`Fills_the_object_name_and_light_kind_a_request_states`, `Does_not_invent_a_light_kind_the_knowledge_does_not_declare`,
`Generates_nothing_when_the_knowledge_models_no_steps`, `Keeps_the_reusable_Blender_api_and_scene_vocabulary`,
`Keeps_every_modelled_step_ordered_and_realizable`, and `Derives_every_indentation_from_where_a_step_sits`;
`TaskRequestResponseTests` repeats the end-to-end request and asserts the rendered blocks. The domain request travels
the same responder, planner, and pipeline as every other family, which the first test's evidence assertion checks.

## 15.7 — benchmark failures reviewed for shortcut regressions

`Lattice.Benchmark.Tests` passes (`Runs_each_repetition_in_a_fresh_session_and_writes_both_reports`,
`Applies_category_filters_before_running_prompts`, `Rejects_duplicate_prompt_ids_before_executing_anything`), so no
benchmark case regressed. No prompt-specific patch was added during the migration or its acceptance pass — the
anti-shortcut guards below are what makes that checkable rather than merely asserted. The scheduled
`Assistant benchmark` workflow (`.github/workflows/assistant-benchmark.yml`) needs a live model endpoint; it is not
part of the required check and was not run here.

## 15.8 — the Release build

Green: CI builds `Lattice.slnx` with `--configuration Release` before it tests, and the job that carries this record
succeeded. Warnings are reported as annotations and none is an error.

## 15.9 — the full .NET test suite

Green in the same run: every assembly in `Lattice.slnx` reported zero failures — Core, Runtime, Server, CLI,
Benchmark, and the LM Studio translators.

## 15.10 — external-tool and generation tests

| Check | Result |
| --- | --- |
| `CrossLanguageExecutionTests` (runs `python3` and `node` on the generated programs) | passes on the CI runner |
| `PythonProgramGenerationTests` / `Generated*ProgramExecutionTests` (run `python3`) | pass on the CI runner |
| `Lattice.Translators.LmStudio.Tests` (19 translator tests) | pass; they exercise the translation layer offline |
| `Lattice.Assistant.Benchmark` and its scheduled workflow | **not run**: needs a live model endpoint, which this environment does not provide |
| external page/evidence review tests | pass against their recorded fixtures; the suite needs no network |

The only recorded unavailability is the assistant benchmark: a scheduled, model-backed tool
(`tools/Lattice.Assistant.Benchmark`), outside the build-and-test job. Every migrated capability is covered by the
deterministic fixtures above, so acceptance does not depend on it.

## 15.11 — one planning mechanism, one emitter

Program *planning* has one implementation and names no language: `SemanticProgramPlanner`,
`SemanticProgramFactMapper`, `SemanticProgramValidator`, the expression algebra, and `SequenceGenerationPipeline` hold
no language spelling, which
`LanguageNeutralityGuardTests.The_generation_kernel_names_no_language_and_spells_no_language_syntax` enforces.
Realization happens in one place for every family the requirement model covers: the pipeline selects the operation the
pack declares for the goal's effect, argument kind, and capabilities, then composes the statement from the pack's
productions and bindings —
`CrossLanguageGenerationTests.Realizes_the_same_interpretation_in_each_language` and `…Selects_the_requested_language_pack`
run the same plan in both packs.

This acceptance pass found two families that still realized through an older token-rule renderer
(`KnowledgeDrivenSequenceGenerator`): **declared loops** (`program.range-loop`, `program.collection-loop`) and
**outputs stated as expressions** (`value.output.expression`). 15.18 projected them — loops onto `IterationRequirement`,
with the counted loop stating its bounds as a language-neutral `SemanticRangeExpression` the pack spells, and the
expression output onto `OutputRequirement` through the generic output path — and then deleted the renderer, its request
record, its token rules, and the knowledge's `rules` and `encodings` sections. There is now **one** emitter for every
family: `SemanticProgramPlanner` → `SequenceGenerationPipeline` → the pack's operation, productions, and bindings. §15.18
below records the change and its evidence.

## 15.12 — declarative language syntax and API knowledge, no Python-specific shared branches

Syntax, bindings, operations, literal formats, input syntax, conversions, and API spellings live in the packs
(`knowledge/languages/python.core.json`, `javascript.core.json`) and are validated by `GenerationLanguagePackLoader`
(`SequenceGrammarLoaderTests`, `SequenceKnowledgeGuardTests`). API spellings are the `apiExpressionFormats` table: a
pack declares the spelling for each contract, and a contract a pack does not declare stays unrealizable rather than
being emitted with an invented spelling — proved by asking the JavaScript pack for a Blender contract.

`LanguageNeutralityGuardTests.Shared_code_names_a_language_only_where_the_history_records_it` allows exactly one
non-generation site — the software recommendation catalogue, which filters concepts a domain file declares as
`python-package` and neither parses nor validates a program.

## 15.13 — no task-specific complete solutions remain

`SequenceKnowledgeGuardTests.Keeps_benchmark_and_task_identities_out_of_knowledge` fails if a benchmark or task
identity reaches the sequence knowledge; `…Stores_no_token_rule_that_could_emit_a_program` fails when the knowledge
declares a token rule or an encoding at all, and the loader rejects such a document, so a stored fragment has nowhere
left to live; `…Keeps_every_validation_reachable_from_extraction` fails when a validation stops being reachable, so dead
task-specific rules cannot hide.

On the domain side the Blender script is declared as **18 steps** arranged as a requirement tree
(`has-generation-step`, `step-has-body`, `step-has-alternative-branch`), with **8 effect families**, **10 API
references**, and **7 lexemes** — no fragment of the finished script exists outside those declarations, which
`docs/code-generation-migration-inventory.md` records entry by entry. And the word-output family, which was the last
one whose program came from stored token rules rather than from requirements, now plans from request facts
(see "What the acceptance pass changed").

## 15.14 — varied-value, paraphrase, and behavioural coverage

| Claim | Evidence |
| --- | --- |
| changed values change the program | `SequenceKnowledgeGuardTests.Generates_different_programs_when_the_request_values_change` |
| changed names change the program | `…Generates_different_programs_when_the_request_names_change` |
| paraphrases agree | `ScalarProgramInterpretationTests.Projects_equivalent_requirements_from_paraphrased_requests`, `…Projects_paraphrased_conditions_with_equivalent_meaning` |
| behaviour is executed, not string-matched | `GeneratedCollectionProgramExecutionTests`, `GeneratedFunctionProgramExecutionTests`, `PythonProgramGenerationTests` run the generated program and compare what it prints |
| a request's own words reach a domain script | `BlenderScriptGenerationTests.Fills_the_object_name_and_light_kind_a_request_states` |
| the same plan runs in both languages | `CrossLanguageExecutionTests.Reports_the_same_result_in_each_language` |

## 15.15 — the as-built generation path

```
request text
  ├─ KnowledgeDrivenGenerationResponder  (Runtime)    generic code requests: extract facts, plan, realize
  │    └─ SemanticProgramPlanner          (Core)      facts → ProgramRequirement tree → effects → statements
  └─ parser                             (Core)        otherwise: TaskRequestFormula(Action, Subject, …)
       └─ TaskRequestResponder          (Runtime)     dispatch on Action
           └─ TaskProgramGenerationResponder          domain requests: task and application concepts, the
                                                     language the application declares, a slot-bearing phrase
                                                     matched when the subject matches no concept, then the
                                                     requirement tree the domain declares
                └─ SequenceGenerationPipeline         one goal at a time
                   ├─ GenerationOperationSelector     operation by effect + argument kind + capabilities
                   ├─ SequenceGrammar (pack)          productions and bindings for that operation
                   └─ SequenceGenerator               the statement text
       └─ message: script, the step list the goals name, safety notes; evidence: the assertion sources read
```

Two things are worth stating as built. Depth never comes from knowledge: a step names an effect *family*
(`emit-api-call`) and the responder composes the effect it asks the pack for (`emit-triple-indented-api-call`) from the
block the step sits in. And nothing in `Core` or `Runtime` names an application, a task, or a language; those facts
live in three knowledge layers:

| Layer | Files | Holds |
| --- | --- | --- |
| world model | `knowledge/domains/*.json` (39 files, including `blender-object-replacement-script.json`) | concepts, relations, lexemes and slots, definitions, assertions, requirement trees |
| sequence knowledge | `knowledge/sequences/python.core.json` | 245 input rules and 30 validations — how a request's facts are recognized; no token rules and no encodings |
| language packs | `knowledge/languages/python.core.json`, `javascript.core.json` | productions, bindings, operations, literal and expression formats, API spellings |

## 15.16 — final inventory and counts

| Item | Count |
| --- | --- |
| sequence rules / input rules / validations / encodings | 0 / 245 / 30 / 0 |
| Python pack: productions / bindings / operations / api expression formats | 104 / 26 / 109 / 9 |
| JavaScript pack: productions / bindings / operations / api expression formats | 16 / 9 / 14 / 0 |
| Blender domain: concepts / relations / relation assertions | 117 / 11 / 240 |
| Blender domain: steps / API references / lexemes | 18 / 10 / 7 |
| domain files | 39 |
| test methods (Core / Runtime / LM Studio / CLI / server / benchmark) | 646 / 223 / 19 / 8 / 6 / 3 |

The per-ticket record, including every deliberate output change the migration made, is
`docs/code-generation-migration-inventory.md`.

## 15.17 — the migration is complete

Every item in sections 8 through 15 of `.agents/todo.md` is closed, and the definition of done is met:

- requests become language-neutral semantic goals and requirements before any target-language realization — facts
  project to `ProgramRequirement`s, and the packs do the spelling;
- programming operations, relationships, and effects are reusable world-model and knowledge content, not task handlers;
- Python syntax and API facts are declarative, and shared `Core`/`Runtime` has no Python-specific parsing, validation,
  or generation branch;
- no complete or near-complete benchmark solution is stored in sequence rules, packs, code, or task-id templates — the
  guards enforce it, and the last family that relied on stored rules now plans from facts;
- Python and the second language use one production generation mechanism, proven by the same plan running in both;
- changed values, identifiers, collections, and equivalent phrasings generate correct executable programs;
- the Release build, the full .NET suite, the external-tool execution tests, and the generation and behavioural tests
  all pass.

No residual remains. The acceptance pass found that loops and expression outputs still realized through an older
token-rule renderer; **15.18** projected both onto requirements and deleted that renderer, its rules, and its request
record, so one emitter serves every family and the knowledge stores no program text at all (§15.18).

## 15.18 — the last two families, and one emitter with nothing left over

The two families the acceptance pass found still rendering outside the planner now plan like every other one, and the
older renderer is gone.

| Gap | What the request used to do | What it does now |
| --- | --- | --- |
| a counted loop — "prints each number from 1 to 5" | `KnowledgeDrivenSequenceGenerator` composed the fence, `for number in range(1, 5):`, and the body from token rules | `program.range-loop` projects onto one `IterationRequirement` whose source is the new `SemanticRangeExpression(Start, End)` and whose body reports the item; each pack spells the range through `rangeExpressionFormat` (Python: `range({0}, {1})`) |
| a collection loop — "prints each number in the list [2, 4, 6]" | the same renderer, walking a stored collection rule | `program.collection-loop` projects onto one `IterationRequirement` over a list literal of the request's own elements (or the pack's declared `exampleData`) with `OutputRequirement(item)` in the body; an empty `[]` is a real, uninhabited collection and still plans |
| an expression output — "sets total to 5, then prints total + 3" | the value was captured as text and the renderer emitted the expression | the Python pack's four `…-and` / `…-then` rules capture the output as a typed `value` expression, so `SemanticProgramFactMapper.TryProjectOutputs` emits one `OutputRequirement(BinaryExpression)`; `SemanticProgramPlanner` no longer declares `value.output.expression` unmapped |

What was deleted with them:

* `src/Lattice.Core/Generation/KnowledgeDrivenSequenceGenerator.cs` (the renderer) and its request record;
* the four token-rule records — `SequenceTokenRule`, `SequenceTokenBinding`, `SequenceTokenEncoding`, `SequenceTokenEscape`
  — so `SequenceKnowledgeBase` is now `(InputRules, Validations)` and no longer carries a rendering rule;
* the loader's rule and encoding readers; `TryLoad` accepts exactly `formatVersion`, `inputRules`, and `validations`, and
  rejects a document that still carries `rules` or `encodings` (or any other unknown field, with the message naming it);
* the `rules` and `encodings` sections of `knowledge/sequences/python.core.json` (245 input rules and 30 validations remain);
* `KnowledgeDrivenGenerationResponder`'s legacy branch (it loads the packs, extracts facts, and calls
  `SemanticProgramPlanner` directly) and `CompositionalTaskParser`'s preflight (it keeps the planner gate alone);
* `tests/Lattice.Core.Tests/Generation/KnowledgeDrivenSequenceGeneratorTests.cs`, whose 56 extraction facts survive as
  `SequenceInputFactExtractionTests`, plus `SequenceKnowledgeGuardTests.Stores_no_token_rule_that_could_emit_a_program`.

Evidence that no output changed:

| Claim | Evidence |
| --- | --- |
| counted and collected loops plan as requirements, not text | `SequenceInputFactExtractionTests.Plans_a_counted_loop_from_the_requests_own_bounds` and `…Plans_a_collection_loop_from_the_requests_own_elements` assert the planned program and the exact emitted text for both families, including `[]`, mixed-type, typed, and single-item collections |
| an expression output is one requirement | `…Plans_an_output_stated_as_an_expression_after_the_assignment_it_reads` and the runtime fixtures that execute `total = 5` / `print(total + 3)` |
| the same text as before | `PythonProgramGenerationTests.Generates_and_executes_composed_python_programs`, `GeneratedCollectionProgramExecutionTests`, and `CrossLanguageExecutionTests` run the generated programs unchanged |
| the knowledge stores no program text | `SequenceKnowledgeGuardTests.Stores_no_token_rule_that_could_emit_a_program` and the loader's field whitelist |
| the loader still loads the shipped document | `SequenceGrammarLoaderTests`, which loads the embedded `python.core.json`, and the guards above, which read the same file |

The one deliberate change in this ticket is structural, not textual: the answer's meaning for an expression output now
carries `semantic-requirements(…)` like every other family, because the output comes from a requirement instead of from
captured facts.
