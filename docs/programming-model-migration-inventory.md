# Minimum programming-model gap inventory (M-06.1, M-06.3a)

**Scope:** map the checked-in Python language-foundations ontology, generic world-model loader/reasoner, and current generation contracts to the currently queued M-07 through M-11 requirements. This is an inventory, not an ontology expansion. It deliberately lists only gaps those work groups need.

## Existing model and runtime capabilities

`knowledge/domains/python-language-foundations.json` currently contains 49 concepts, 24 relation signatures, 45 type definitions, and 76 relation assertions (after M-06.2 added two generic types and five typed relation signatures). The type vocabulary already includes program, statement, expression, value, literal, identifier, variable, assignment/expression/conditional/loop statements, function definition/call, parameter, argument, return statement, collection/sequence/mapping and primitive scalar types, module/import, exception/handler, class/object/method, API/library/interpreter, output, iteration, and program condition. Python-specific individuals and type facts are legitimate target-language knowledge.

The relation signatures cover language syntax, program-to-statement, statement-to-expression, expression-to-value, assignment-to-variable, variable-to-value, function-to-parameter, call-to-argument/callable, function definition/return, imports, exception handling, interpreter execution, library/API provision, collection elements, and class methods. M-06.2 added individual-level typed links for program-goal-to-statement, statement-to-expression, assignment-to-variable, and binding-to-variable/value. These provide a useful vocabulary and typed edges, but most assertions describe individual concepts or type taxonomy; they do not form a queryable program plan.

The generic world-model loader validates and stores concepts, lexemes, relation signatures/assertions, primitive/defined type expressions, and data properties. `SemanticWorldModelReasoner` computes `is-a`/`instance-of` closure, class-expression membership, evidence, and outgoing relation assertions for an individual. It does **not** perform operation selection, infer missing program steps, unify operation argument/result types, or order program statements from dependencies.

`GenerationGoal` is a realization request with string `Effect`, `ArgumentKind`, and `ArgumentValue`, an array of string capabilities, and string/string-collection bindings. `GenerationOperation` is language-pack knowledge with `Id`, `Symbol`, string effect/argument-kind, string prerequisites, and optional start symbol. `GenerationOperationSelector` selects by exact effect/argument kind and available prerequisite strings. These contracts generate language forms but do not model typed input/output slots, result identities, side effects, or dependency edges between goals.

## Minimum gaps required by the queued migration

| Queued requirement | Existing foundation | Missing model/relation needed (only current queue scope) |
|---|---|---|
| M-07 scalar requirements and M-08 scalar composition | Scalar types, value/expression/variable/assignment/condition/output concepts; assignment stores-in-variable and expression-evaluates-to-value signatures; language-pack operations for assignment, arithmetic, comparison, branches, output | A request-derived statement/requirement identity and its ordered dependencies; assignment target/value slots; expression operator/operand edges; condition-to-branch/body edges; output-content binding. Existing broad type-level edges cannot distinguish an individual request's values or order. |
| M-09 collections and iteration | Collection, sequence, mapping, iteration, loop, element concepts and collection-contains-element signature | Typed collection element/key/value relationships; iteration source and item/key/value bindings; filter predicate, map transformation, sort key/direction, and accumulator initialization/update/result relationships. Current collection relation is only a generic contains-element edge. |
| M-10 functions, classes, and error handling | Function definition/call, parameter, argument, return, class/object/method, exception/handler concepts; parameter/argument/return and class-method signatures | Function parameter order/type and call-argument-to-parameter binding; function body/return-expression links; field/init/member-access links; raise/handling/recovery and control-flow relations. Existing signatures do not express arity, result type, scope, or body dependencies. |
| M-11 file, directory, and serialization effects | Module/API/library, input/output, exception and data concepts; `library-provides-api` and print-output signatures; Python pack declares API operations | Reusable typed operation contracts for read/write/list/encode/decode: input slots, result type, effects, prerequisites, failure modes, and result-to-consumer links. Existing relation assertions name APIs but do not define operation contracts or effect dependencies. |
| All groups: operation planning and generation | Generic type/relation graph, `GenerationGoal`, `GenerationOperation`, language-pack selector and sequence pipeline | A bridge from semantic operation contracts and request-derived goal nodes to selected operations, typed bindings, and a dependency-ordered program. It must be reusable and language-neutral; the Python pack remains responsible for syntax/API spellings. |

## M-06.3a minimal operation-set inventory

This is a scoped inventory for M-06.3a, not a new operation taxonomy. The Python pack currently declares 56 operations; the world model declares no reusable operation contract instances. For M-06.3, only the existing migration task families need the following contract shapes:

| Current pack/model family | Semantic input slots | Result | Effect / prerequisite / constraint need |
|---|---|---|---|
| Scalar assignment (`assign-variable*`) | Target variable binding and typed value/expression | Updated variable binding / assigned value | Mutation effect; target must be in the relevant scope. |
| Arithmetic, comparison, Boolean expressions | Ordered typed operands, operator identity, variable/function references | Numeric, text, or Boolean expression value | Pure operation; operand compatibility and name/scope checks; operator syntax stays in the language pack. |
| Output (`write-standard-output*`) | Text or typed value/expression | No program value required; observable output | Standard-output effect; text literal formatting belongs to the language pack. |
| Conditional/loop/control flow (`begin-conditional*`, `begin-loop*`, `emit-else`, `emit-continue`) | Boolean condition or iterable, body statement requirements, loop bindings | Control-flow result / updated bindings | Branch/iteration dependency; condition/body/loop-variable constraints. |
| Function/class/exception structures (M-10) | Ordered parameters/arguments, body, return/field/member requirements, exception types | Return value or constructed object | Scope, arity, control-flow and recovery requirements. |
| File/API/serialization operations (M-11: `open-file`, `read-standard-input`, `json.dump`, imports) | Path/data/format/API arguments | Handle, read data, serialized output or side effect | File/console/serialization effects, module/API prerequisites, missing/invalid data failures. |

The current `GenerationOperation` contracts represent many of these inputs only as a string `ArgumentKind` and a single string value; `Prerequisites` are string labels and do not identify typed result producers. The world model needs reusable operation and slot concepts plus relations for accepted inputs, produced results, effects, prerequisites, and constraints. This inventory intentionally does not propose storing Python spellings, task-specific values, or a fixed execution order in the world model.

## Explicit non-gaps / scope limits

- Do not add another broad programming taxonomy: most concepts listed above already exist and should be reused.
- Do not duplicate Python keywords, syntax, operator token spellings, or library API names into shared world-model C#; those belong in declarative language/domain packs.
- Do not add classes, exception variants, modules, or I/O concepts without a current M-10/M-11 consumer.
- The semantic expression tree and operator identities are now modeled in `Lattice.Core.Generation` (M-05B); this inventory does not propose a duplicate expression AST in the ontology. The knowledge model needs only the operation/statement relationships required to connect request-derived expressions to generation.
- `SemanticWorldModelReasoner` is a graph/type reasoner, not a program planner. M-06 should add only the minimal relations/contracts needed to make the already queued goals operational; it must not silently introduce a second generation engine.

The gap map records M-06.1; the operation-set mapping records M-06.3a. Neither inventory section itself changes runtime behavior. M-06.2's ontology additions are summarized above and are tested by the merged runtime loader test.
