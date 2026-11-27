# Code-generation migration — acceptance record

This is the acceptance record for section 15 of `.agents/todo.md`: what was run, what it exercised, and what it
proved. Verification is CI-only — this sandbox has no `dotnet` — so every claim below rests on the
`Build & test (.NET 10)` job in `.github/workflows/ci.yml`, which builds `Lattice.slnx` in **Release** and then runs
`dotnet test Lattice.slnx --configuration Release --no-build`. A green run of that job is the Release build and the
full suite at once.

The suite holds **661** Core test methods, **223** Runtime, **19** LM Studio translator, **8** CLI, **6** server, and
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
* **An output stated as an expression** ("prints total + 3", "prints result - 4") stays on its own path: the input
  rules capture no typed expression for it, so `value.output.expression` remains declared unmapped; that is queue item 15.18.

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

## 15.11 — one planning mechanism, and what still realizes through rules

Program *planning* has one implementation and names no language: `SemanticProgramPlanner`,
`SemanticProgramFactMapper`, `SemanticProgramValidator`, the expression algebra, and `SequenceGenerationPipeline` hold
no language spelling, which
`LanguageNeutralityGuardTests.The_generation_kernel_names_no_language_and_spells_no_language_syntax` enforces.
Realization happens in one place for every family the requirement model covers: the pipeline selects the operation the
pack declares for the goal's effect, argument kind, and capabilities, then composes the statement from the pack's
productions and bindings —
`CrossLanguageGenerationTests.Realizes_the_same_interpretation_in_each_language` and `…Selects_the_requested_language_pack`
run the same plan in both packs.

Recorded residual, found by this acceptance pass and narrowed by it: `KnowledgeDrivenSequenceGenerator` still emits
program text for the families the requirement model does not project — **declared loops** (`program.range-loop`,
`program.collection-loop`) and **outputs stated as expressions** (`value.output.expression`). Its 21 token rules are
generic renderers (a fence, `print(`, a loop header), carry no task or benchmark identity, and are guarded by
`SequenceKnowledgeGuardTests.Keeps_complete_solution_fragments_confined_to_recorded_families`, which allows **no**
multi-line rule. Retiring it needs those two remaining families projected as requirements; that work is recorded as
15.18 and does not gate the definition of done, because the second language is only reachable through the planner.

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
identity reaches the sequence knowledge; `…Keeps_complete_solution_fragments_confined_to_recorded_families` fails when
a multi-line complete-solution fragment appears at all; `…Keeps_every_rule_and_validation_reachable_from_extraction`
fails when a rule stops being reachable, so dead task-specific rules cannot hide.

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
| sequence knowledge | `knowledge/sequences/python.core.json` | 21 rules, 241 input rules, 30 validations, 1 encoding — how a request's facts are recognized |
| language packs | `knowledge/languages/python.core.json`, `javascript.core.json` | productions, bindings, operations, literal and expression formats, API spellings |

## 15.16 — final inventory and counts

| Item | Count |
| --- | --- |
| sequence rules / input rules / validations / encodings | 21 / 241 / 30 / 1 |
| Python pack: productions / bindings / operations / api expression formats | 104 / 26 / 109 / 9 |
| JavaScript pack: productions / bindings / operations / api expression formats | 16 / 9 / 14 / 0 |
| Blender domain: concepts / relations / relation assertions | 117 / 11 / 240 |
| Blender domain: steps / API references / lexemes | 18 / 10 / 7 |
| domain files | 39 |
| test methods (Core / Runtime / LM Studio / CLI / server / benchmark) | 661 / 223 / 19 / 8 / 6 / 3 |

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

One residual is recorded rather than hidden: loops and expression outputs still realize through the token-rule renderer
(15.11 above). It is tracked as **15.18**, it carries no task identity, and it does not affect either language's
generation mechanism.
