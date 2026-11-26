# Code-generation migration inventory (M-01)

> **Audit-only checkpoint, 2026-11-23.** This records the implementation on the current `main`-derived baseline (`8aaffd1`). It does not change the generation implementation or settle M-02's design choice. The queue item remains unchecked until its PR is merged and checks pass.

This is an as-built source inventory for [M-01 in the active migration queue](../.agents/todo.md), not a restatement of the intended architecture. Python remains a supported target; the findings below distinguish legitimate declarative syntax/API knowledge from Python-shaped shared C# logic and task-specific stored solutions.

## Verified baseline

The counts in [`knowledge/sequences/python.core.json`](../knowledge/sequences/python.core.json) are **21 sequence rules, 240 input rules, 30 validations, and one string encoding**. All rule, input-rule, and validation IDs in that file are unique. The module is the only current sequence module. The separate [`knowledge/languages/python.core.json`](../knowledge/languages/python.core.json) has **102 productions, 26 bindings, and 104 operations**. [`knowledge/domains/python-language-foundations.json`](../knowledge/domains/python-language-foundations.json) has **43 type concepts, four individual concepts, 43 primitive type declarations, 19 relation signatures, and 76 relation assertions**; it has no defined types. These are verified against the checked-in JSON, not inferred from older PR descriptions.

One detail in the queue's examples is stale: there is no current fact ID beginning `program.sign-classification.`. The corresponding input facts are `program.assignment.classification` and `program.classification.positive`, `.negative`, and `.zero`; they are still task-specific.

## Actual production paths

### A. Raw-input Python sequence path (first in runtime dispatch)

1. [`LatticeRuntimeSession.Process`](../src/Lattice.Runtime/LatticeRuntimeSession.cs) calls `KnowledgeDrivenGenerationResponder.TryGenerate(input, ...)` **before** `LceParser.ParseText`. A successful result returns immediately; the task-request parser is not consulted.
2. [`KnowledgeDrivenGenerationResponder`](../src/Lattice.Runtime/KnowledgeDrivenGenerationResponder.cs) lazily loads the embedded sequence modules, calls [`SequenceInputFactExtractor`](../src/Lattice.Core/Generation/SequenceInputFactExtractor.cs), and gets `SequenceSemanticFact` IDs, string values, and string collections. Extraction is driven by the English-token match/capture rules in `python.core.json`; it is not a language-neutral program AST or world-model query.
3. It builds `KnowledgeDrivenSequenceGenerationRequest` with the extracted IDs as `ActiveFacts`, the captured values as `FactValues`, an empty context, and a **null** token model. It calls [`KnowledgeDrivenSequenceGenerator`](../src/Lattice.Core/Generation/KnowledgeDrivenSequenceGenerator.cs), then returns that generator's text as a completed runtime result with `semantic-facts(...)` meaning and empty evidence.
4. This path does **not** create a `TaskRequestFormula` or `GenerationGoal`; it does not query the semantic world model, select a `GenerationLanguagePack`, or call `SequenceGenerationPipeline`. Python syntax, task facts, bindings, and many whole outputs are in the sequence module itself.

### B. Parsed task-request / Blender sequence-pipeline path

1. If path A does not complete, [`LceTextParser.Parse`](../src/Lattice.Core/Language/LceTextParser.cs) reaches [`TaskRequestParser.TryParse`](../src/Lattice.Core/Language/TaskRequestParser.cs). The parser tries `CompositionalTaskParser` first, then the general-shaped `ScriptCreationPattern` (`How would I create a script for {application} that {task}?`). That pattern stores a normalized task phrase as `Subject`, the application as `Context`, `source-code` as output format, and `program-generation` as scope. It does not decompose the requested program into operations, typed values, or constraints.
2. [`CompositionalTaskParser`](../src/Lattice.Core/Language/CompositionalTaskParser.cs) extracts the same sequence facts, requires `intent.program-generation` and exactly one `language.*` fact, then calls `KnowledgeDrivenSequenceGenerator` as a preflight before constructing a Code `TaskRequestFormula`. In the in-process runtime this successful parse path is **shadowed**: `LatticeRuntimeSession.Process` already returns if the same extraction and generator call succeeds; if that direct call fails, this parser's equivalent preflight cannot succeed. It remains directly exercised by parser unit tests.
3. A parsed `TaskRequestAction.Code` reaches [`TaskRequestResponder`](../src/Lattice.Runtime/TaskRequestResponder.cs), then `TaskCodeGenerationResponder.TryGenerateCode` — today [`TaskProgramGenerationResponder.TryGenerateProgram`](../src/Lattice.Runtime/TaskProgramGenerationResponder.cs) after M-12.4–M-12.6. As inventoried, this responder accepted only requests whose subject was an individual typed `blender-codegen-code-generation-task` and whose context was a `blender-codegen-software-application`; M-12.6 renamed that vocabulary to `code-generation-task` / `software-application`, so the check is a capability rather than a Blender gate.
4. The responder uses the [`SemanticWorldModelReasoner`](../src/Lattice.Core/Reasoning/SemanticWorldModelReasoner.cs) to resolve the application's language and retrieve the task's already-authored, ordered generation-step assertions, effects, argument kinds, bindings, API references, and safety notes. The runtime loads the domain modules through [`SemanticWorldModelResourceLoader`](../src/Lattice.Runtime/SemanticWorldModelResourceLoader.cs). For each of the 18 Blender steps it creates one `GenerationGoal`: normalized effect, normalized argument kind, the step label as argument value, an empty capability list, and named string bindings taken from the assertions. The request text is not used to plan or infer those steps. The API-reference check confirms that referenced concepts are typed as APIs; it does not validate call signatures or derive the bindings from API knowledge.
5. [`GenerationLanguagePackLoader`](../src/Lattice.Core/Generation/GenerationLanguagePackLoader.cs) loads the embedded language packs; the responder selects the pack whose key equals the language found in the model. Only the Python language pack currently exists. [`SequenceGenerationPipeline`](../src/Lattice.Core/Generation/SequenceGenerationPipeline.cs) selects an operation from effect/argument-kind/prerequisites, resolves the pack's bindings and literal encoding, converts its productions to a `SequenceGrammar`, and invokes [`SequenceGenerator`](../src/Lattice.Core/Generation/SequenceGenerator.cs). `TryGenerateProgram` simply generates each supplied goal in order and joins the statements. The responder then formats a Markdown script, the pre-authored step labels, optional safety notes, and evidence.

**Production-path conclusion:** there are two distinct code-emission mechanisms today. The raw Python route is active for the ordinary Python request rules and uses `KnowledgeDrivenSequenceGenerator`. The sequence-pipeline route uses `SequenceGenerationPipeline` and is reached through the Blender-specific task-plan responder. `SequenceGenerationPipeline` is generic by contract, but that does not make the production request-to-goal path generic. The architecture document describes the destination; the call graph above is the source-of-truth for what is currently executed.

## C# responsibilities and migration classification

| Source | Actual responsibility and classification |
|---|---|
| [`LatticeRuntimeSession.Process`](../src/Lattice.Runtime/LatticeRuntimeSession.cs) / `ProcessTaskRequest` | Runtime coordinator. Gives raw sequence generation first refusal, then parses and routes task requests. The order creates the shadowed compositional parser path described above. |
| [`KnowledgeDrivenGenerationResponder`](../src/Lattice.Runtime/KnowledgeDrivenGenerationResponder.cs) | **Active production route; generic wrapper around a Python-specific declarative sequence module.** Extracts facts and invokes the sequence-token engine directly; bypasses semantic-world-model reasoning and the language-pack path. |
| [`LceTextParser`](../src/Lattice.Core/Language/LceTextParser.cs) | General LCE parser dispatcher. It reaches task-request parsing only after earlier parser categories. Not a code generator. |
| [`TaskRequestParser`](../src/Lattice.Core/Language/TaskRequestParser.cs) | Shared regex-based task recognizers. `ScriptCreationPattern` creates the raw Code task request described above; it does not parse a program. Its `purpose.Contains("python")` recommendation cue is unrelated to code generation and is out of scope; [`TaskRecommendationResponder`](../src/Lattice.Runtime/TaskRecommendationResponder.cs) consumes that requirement for software recommendations. |
| [`TaskRequestFormula`](../src/Lattice.Core/Semantics/TaskRequestFormula.cs) and [`TaskRequestAction`](../src/Lattice.Core/Semantics/TaskRequestAction.cs) | Generic task request contract, but the Code path carries normalized free text, context/output/scope strings, and string requirements—not typed program statements, expressions, or bindings. |
| [`CompositionalTaskParser`](../src/Lattice.Core/Language/CompositionalTaskParser.cs) | **Shadowed production parser / test-only successful use.** It turns extracted fact IDs into a Code request only after the same `KnowledgeDrivenSequenceGenerator` succeeds. It is not the semantic-to-language-pack bridge implied by its name. |
| [`TaskRequestResponder`](../src/Lattice.Runtime/TaskRequestResponder.cs) and [`TaskRequestResponderHelpers`](../src/Lattice.Runtime/TaskRequestResponderHelpers.cs) | Generic task-action dispatch and response/evidence helpers. Code requests dispatch to the shared program-generation entry point; helpers do not provide program semantics. |
| [`TaskProgramGenerationResponder`](../src/Lattice.Runtime/TaskProgramGenerationResponder.cs) | **Shared code-request route.** As inventoried it was `TaskCodeGenerationResponder`, a Blender-specific route; M-12.4–M-12.10 made it application-neutral (capability checks on `code-generation-task` / `software-application`) and removed the stored step order. It turns knowledge-declared steps into goals, selects the language pack, and formats output. Procedure explanation moved to [`TaskOperationExplanationResponder`](../src/Lattice.Runtime/TaskOperationExplanationResponder.cs). |
| [`SemanticWorldModelResourceLoader`](../src/Lattice.Runtime/SemanticWorldModelResourceLoader.cs) and [`SemanticWorldModelReasoner`](../src/Lattice.Core/Reasoning/SemanticWorldModelReasoner.cs) | Generic ontology composition and graph reasoning. In the code-generation route, their data is consumed by the Blender-specific responder; no general programming-operation planner is present in this path. |
| [`SequenceInputFactExtractor`](../src/Lattice.Core/Generation/SequenceInputFactExtractor.cs) | Generic mechanics for phrase matching, capture, collection splitting, raw-value preservation, optional normalization, and fact assembly. The *rules and fact vocabulary it executes* are highly prompt/task-specific; expression payloads remain strings. It tokenizes with `EnglishLexer`. |
| [`SequenceKnowledgeBaseLoader`](../src/Lattice.Core/Generation/SequenceKnowledgeBaseLoader.cs) | Generic strict JSON loader and embedded-resource merger for the sequence format. It explicitly whitelists the Python-only `pythonListLiteral` validation kind. |
| [`SequenceKnowledgeBase`](../src/Lattice.Core/Generation/SequenceKnowledgeBase.cs) | Data-only contracts for semantic facts, captures, collections, validation declarations, encodings, and next-token rules. The types are generic; the active fact IDs/rules and Python source fragments are not. |
| [`SequenceInputFactValidator`](../src/Lattice.Core/Generation/SequenceInputFactValidator.cs) | Mixed shared validation: useful structural constraints (collection alignment, uniqueness, range bounds, name-reference/scope checks) plus Python-shaped identifier, list-literal, arithmetic, function-call, comparison, boolean, and quoted-string parsing. This is active shared Core code, not merely test code. |
| [`BooleanExpressionNormalizer`](../src/Lattice.Core/Generation/BooleanExpressionNormalizer.cs) | Active text-expression parser/normalizer for `and`, `or`, `not`, comparison phrases and arithmetic/string operands. It returns Python-shaped expression text and delegates arithmetic/string checks to `SequenceInputFactValidator`. |
| [`ComparisonOperatorNormalizer`](../src/Lattice.Core/Generation/ComparisonOperatorNormalizer.cs) | Phrase-to-symbol normalization for comparisons. The mapping is mostly language-neutral meaning, but the normalized value is a source-like operator string carried in the current fact model. |
| [`KnowledgeDrivenSequenceGenerator`](../src/Lattice.Core/Generation/KnowledgeDrivenSequenceGenerator.cs) | **Active production generator for path A; separate from `SequenceGenerator`.** A bounded rule/context/fact/collection-cursor search over `SequenceTokenRule`s. Its generic algorithm does not erase the Python/task-specific content of the rule set. Runtime callers pass `model: null`. |
| [`GenerationGoal`](../src/Lattice.Core/Generation/GenerationGoal.cs), [`GenerationOperation`](../src/Lattice.Core/Generation/GenerationOperation.cs), [`GenerationLanguagePack`](../src/Lattice.Core/Generation/GenerationLanguagePack.cs), [`GenerationBinding`](../src/Lattice.Core/Generation/GenerationBinding.cs), [`GenerationProduction`](../src/Lattice.Core/Generation/GenerationProduction.cs), [`GenerationSymbol`](../src/Lattice.Core/Generation/GenerationSymbol.cs), and [`GenerationStringLiteralFormat`](../src/Lattice.Core/Generation/GenerationStringLiteralFormat.cs) | Existing generic goal/operation/grammar/binding contracts. The Blender route uses them, but currently projects each curated step separately; effect and argument kind are strings, there is no typed statement/expression or inter-goal dependency model, and goal bindings do not model API signatures. |
| [`GenerationLanguagePackLoader`](../src/Lattice.Core/Generation/GenerationLanguagePackLoader.cs), [`GenerationOperationSelector`](../src/Lattice.Core/Generation/GenerationOperationSelector.cs), [`SequenceGenerationPipeline`](../src/Lattice.Core/Generation/SequenceGenerationPipeline.cs), and [`SequenceGenerator`](../src/Lattice.Core/Generation/SequenceGenerator.cs) | **Generic reusable language-pack generation mechanism.** The selector checks effect, argument kind and prerequisites; the pipeline binds inputs and invokes the grammar expander. It is used in production through the Blender responder, not through the ordinary Python raw-input path. |
| [`OperationSequenceGenerator`](../src/Lattice.Core/Generation/OperationSequenceGenerator.cs) | Another generic operation/grammar generator with substantially overlapping selection, binding and expansion logic. Search found no production caller; its callers are its unit tests. It is a candidate for M-02's single-path decision, not a current production route. |
| [`SequenceGrammarLoader`](../src/Lattice.Core/Generation/SequenceGrammarLoader.cs) | Generic standalone grammar JSON loader. The production `SequenceGenerationPipeline` converts the already-loaded language-pack productions directly and does not call this loader. Current callers are tests. |
| [`PythonExpressionParser` (M-01 baseline)](https://github.com/Vraxo/Lattice-Next/blob/e31e28e2196176f1de6b20e15b66de3292057f57/src/Lattice.Core/Language/PythonExpressionParser.cs) | **Obsolete/dead Python-specific helper; removed in M-03.** At the time of M-01, source/test search found only its class/method definitions, no production caller and no tests. The active validator has a separate Python-shaped arithmetic parser, so deleting this file does not remove active syntax checks. |
| [`SequenceTokenModel`](../src/Lattice.Core/Generation/SequenceTokenModel.cs), [`SequenceTokenizer`](../src/Lattice.Core/Generation/SequenceTokenizer.cs), [`SemanticSequenceBindings`](../src/Lattice.Core/Generation/SemanticSequenceBindings.cs), and [`SequenceSemanticContext`](../src/Lattice.Core/Generation/SequenceSemanticContext.cs) | Generic token/model or semantic-binding utilities, not the production program-generation path. `SequenceTokenModel` is optional and passed as null by the two raw generation callers; the operation pipeline also does not configure it. Other callers are tests or utility definitions. |

### Active validation kinds

`SequenceKnowledgeBaseLoader` supports 20 validation kinds after M-05B.6f removed the unused `comparisonOperandsReferences` and `booleanExpressionReferences` kinds. The current Python sequence module uses 18 of the remaining kinds (all 50 validation records); `comparisonValueReferences` and `arithmeticExpressionReferences` remain supported and are exercised directly by tests, although not declared in this module. A name regex or expression parser below is a target-language/syntax concern even when its check also enforces a useful generic invariant.

| Validation kind | Current records | Current behavior and classification |
|---|---:|---|
| `identifierValue` | 24 | Validates identifier spelling with `GenerationInputSyntaxParser` and the selected pack's input syntax; scope/name binding remains a separate concern. |
| `identifierCollection` | 4 | Same selected-pack syntax constraint over a collection; combines name validity with collection structure. |
| `uniqueCollection` | 3 | Reusable uniqueness constraint (case-insensitive in implementation). |
| `disjointCollections` | 1 | Reusable scope/no-shadowing constraint between local and parameter collections. |
| `semanticCollection` | 1 | Validates a typed semantic collection's structure and item constraints. |
| `integerValue` | 2 | Generic integer-value constraint, currently limited to non-negative `Int32` parsing. |
| `ascendingIntegerRange` | 2 | Generic range ordering/bounds constraint. |
| `arithmeticCollectionReferences` | 1 | Consumes materialized arithmetic ASTs, enforces local/parameter reference availability and ordered-local scope. Source syntax parsing occurs during extraction via selected `InputSyntax`. |
| `arithmeticValue` | 1 | Checks the already-materialized expression with `SemanticExpressionAnalysis.IsArithmetic`; does not parse source text in the validator. |
| `arithmeticValueReferences` | 1 | Checks typed arithmetic structure and available-name/function-call scope; source parsing occurs before validation. |
| `comparisonValueReferences` | 0 | Supported but unused by this module; validates a structured comparison expression and operand references. |
| `arithmeticExpressionReferences` | 0 | Supported but unused; validates typed arithmetic structure plus references. |
| `booleanExpressionCollectionReferences` | 1 | Validates materialized Boolean trees and parameter references over an expression collection. |
| `valueExpressionReferences` | 1 | Validates a materialized value-expression tree and references; literal/source parsing happens during extraction. |
| `valueExpressionCollectionReferences` | 1 | Same typed-tree checks over an expression collection. |
| `matchingCollections` | 3 | Reusable aligned-collection length constraint. |
| `orderedArithmeticFunctionExpressions` | 1 | Checks ordered scopes/references and function calls from materialized expression trees; signature/parameter collections remain string-named values. |
| `matchingLastNumericFunctionCall` | 1 | Checks a materialized function-call node's name, arity and numeric-literal arguments against the last definition; call syntax is parsed using selected-pack metadata before validation. |
| `matchingFunctionCall` | 1 | Checks a materialized function-call node's name/arity and numeric-or-string literal arguments. |
| `matchingNumericFunctionCall` | 1 | Checks a materialized function-call node's name/arity and numeric arguments. |

The two supported-but-undeclared kinds are nevertheless exercised by shared validator tests. Expression syntax parsing occurs in `SemanticExpressionParser` / `BooleanExpressionNormalizer` using selected-pack `InputSyntax`; `SequenceInputFactValidator` consumes typed nodes and contains no former arithmetic, list-literal, or quoted-string source parsers. `pythonListLiteral` was removed from the active knowledge format and shared validator in M-04. The duplicate dead `PythonExpressionParser` was removed in M-03.

## M-05B semantic expression model decision (M-05B.2a)

**Decision:** keep the expression model in `src/Lattice.Core/Generation/SemanticExpression.cs` for this migration, rather than introducing a parallel `Semantics` AST. The existing public `SemanticExpression` record hierarchy is already consumed by extraction, validation, and generation, and explicitly documents that syntax is supplied by a selected language pack. Reusing it is the smallest architectural change and avoids a second representation/conversion layer.

The current shape provides name, number, text, Boolean, unary, binary, and function-call nodes; `SemanticExpressionOperator` provides language-neutral arithmetic, comparison, Boolean, and unary operator identities. Expression-category analysis rejects incompatible known literal types while allowing unknown variable types to be resolved through bindings. Ordered binary children preserve operand order; nested nodes represent precedence/grouping semantically without retaining parentheses as source text. Number values are canonical invariant decimal strings and text nodes retain their semantic text, not target quoting/escaping. Target formatting belongs in `SemanticExpressionRenderer` and selected language-pack syntax.

**Known fit/gap:** this model was already present on the baseline, so M-05B.2 reuses it rather than creating duplicate node classes. The expression model includes a Boolean literal node whose payload is a `bool`. The Python language pack now declares its true/false spellings, and the shared parser/renderer consume that metadata; shared code contains no Python Boolean token branch. Identifier identity remains a string name, and public constructors can create invalid combinations that are rejected later by `SemanticExpressionAnalysis`. The M-05B.2 model test covers typed literal/reference data, nesting, and operand order. Follow-on M-05B.5 work now parses/renders Boolean literals only through declarative pack metadata, without adding language-specific shared branches.

## M-05B check classification (M-05B.1h)

Classification of the current expression and adjacent checks in `SequenceInputFactValidator` / extraction, without changing behavior:

| Check | Classification | Current responsibility / boundary |
|---|---|---|
| `SemanticExpressionParser.TryParse` called while materializing a tagged capture | **Syntax parsing** | Parses captured source text using selected `GenerationLanguagePack.InputSyntax` and constructs a typed `SemanticExpression`. It still needs to consume source spelling; it is not semantic capture directly from request meaning. |
| `SemanticExpressionAnalysis.IsArithmetic`, `IsBoolean`, `IsValue`, comparison-node/operator shape checks | **Semantic validation** | Rejects expressions whose typed node/operator structure does not satisfy the requested semantic category (for example, an arithmetic expression or Boolean condition). |
| `ReferencesOnlyAvailableNames`, `ReferencesAnyName`, function-name/call checks, ordered local/parameter scopes | **Reference/scope validation** | Checks that identifiers and calls refer to declared parameters, locals, or permitted functions and, in the ordered-local case, that prior values are available. This is useful language-neutral intent but currently operates on the parsed expression tree plus string-named scopes. |
| `uniqueCollection`, `disjointCollections`, `matchingCollections`, non-empty/maximum-size fact checks | **Structural validation** | Enforces uniqueness, disjointness, collection alignment and schema/value bounds; it does not parse expression grammar. |
| `integerValue`, `ascendingIntegerRange`, `semanticCollection` | **Semantic/structural validation** | Enforces numeric/range or collection-shape constraints; integer text parsing is value interpretation rather than target syntax parsing. |
| `GenerationInputSyntaxParser.IsIdentifier` | **Syntax parsing/validation** | Identifier spelling is delegated to selected input syntax metadata; identifier identity/scope remains a separate reference concern. |
| Expression-related `SequenceInputFactValidator` branches | **Semantic/reference/structural validation** | Active branches consume materialized expression nodes. Call-matching kinds check call name, arity and allowed literal argument nodes. The former source-string arithmetic/list/string parsing helpers are no longer in the validator; parsing occurs during extraction against selected-pack syntax. |

**Classification finding:** the active expression capture path has begun separating syntax parsing (language-pack input syntax), typed expression semantics, and name/scope checks. However, a number of validation-kind implementations still combine these responsibilities, and untagged values bypass expression parsing entirely. Structural checks should remain generic; syntax-specific acceptance belongs to the selected language parser/pack. This is a classification of present implementation, not a completion claim for M-05B.

## M-05B expression values through generation (M-05B.1g)

The current extraction/generation path carries both raw and typed forms; the expression metadata does not itself make every value semantic.

1. `SequenceInputFactExtractor` first matches token rules and builds each `SequenceSemanticFact` with scalar `Values` and string `Collections`. For rules/captures tagged with `semanticExpressionKind`, it registers a `(factId, valueId)` capture descriptor. It then resolves a language pack (through the caller-supplied resolver) and `TryMaterializeSemanticExpressions` parses each captured string using `SemanticExpressionParser` and the selected pack's `InputSyntax`. Scalars move from `Values` to typed `Expressions` or `ComparisonOperators`; collections move from `Collections` to `ExpressionCollections`. Raw expression strings are therefore not retained in those same slots after successful materialization.
2. The parser still receives the captured source text. `SemanticExpression` preserves expression structure/operator identity, but literal/reference values are derived by parsing that source and are not independently extracted by the English capture rules. Captures without the metadata (including assignment value and most output values) stay raw strings in `Values`/`Collections`.
3. `SequenceInputFactValidator` validates the typed expression slots: arithmetic/boolean/value kinds and reference checks operate on `SemanticExpression`; comparison-operator facts are materialized into enum identities. These validations run on extracted facts and reject invalid syntax/references, but do not convert raw untagged values.
4. `KnowledgeDrivenSequenceGenerator` receives `FactValues` (the runtime responder passes the extracted facts) and indexes each fact's scalar values, collections, expressions, expression collections, comparison operators, and semantic collections. Its token-rule binding helpers call `SemanticExpressionRenderer` / `TryRenderOperator` with the selected `GenerationLanguagePack` to turn typed expressions back into target-language strings before inserting them into generated output. Thus raw source strings are not required for the tagged expression bindings, but source-like strings are reintroduced at the realization boundary. Untagged raw values are interpolated directly by the sequence-token generator and may contain target-language syntax.
5. In the current runtime raw-input path, `KnowledgeDrivenGenerationResponder` calls the extractor overload whose default resolver is `GenerationLanguagePackSelector.Select`, so extraction can parse expressions when the request facts select a known language pack. It then selects a pack again for the generation request and passes a null token model. The parsed task/sequence path and tests have other pack-resolution arrangements. The expression pipeline is not yet a single, neutral AST-to-language-pack production route.

Relevant implementation: `SequenceInputFactExtractor.TryMaterializeSemanticExpressions` / `TryParseCapturedExpression`, `SequenceSemanticFact` in `SequenceKnowledgeBase.cs`, `SequenceInputFactValidator`, and scalar/collection binding resolution in `KnowledgeDrivenSequenceGenerator`. This records current flow only; no behavior changed.

## M-05B validation-kind loader and test trace (M-05B.1f)

`SequenceKnowledgeBaseLoader.TryReadInputValidations` in `src/Lattice.Core/Generation/SequenceKnowledgeBaseLoader.cs` owns a local, ordinal `supportedKinds` allow-list. It accepts the expression-related kinds `arithmeticCollectionReferences`, `arithmeticValue`, `arithmeticValueReferences`, `comparisonValueReferences`, `comparisonOperandsReferences`, `arithmeticExpressionReferences`, `booleanExpressionReferences`, `booleanExpressionCollectionReferences`, `valueExpressionReferences`, `valueExpressionCollectionReferences`, and `orderedArithmeticFunctionExpressions`. It also accepts adjacent call-matching kinds used by expression/call validation (`matchingLastNumericFunctionCall`, `matchingFunctionCall`, `matchingNumericFunctionCall`). `TryReadInputValidations` rejects a record whose kind is not in this set; this is validation-kind schema handling, separate from validator execution.

Relevant existing coverage is in `tests/Lattice.Core.Tests/Generation/KnowledgeDrivenSequenceGeneratorTests.cs`: `Validates_comparison_conditions_against_declared_parameters` directly exercises `comparisonValueReferences` using a Python language pack and checks valid arithmetic operands, missing references, malformed syntax, and chained comparisons. Nearby conditional-function extraction/generation tests exercise the Boolean capture path and its resulting normalized condition; they do not test every supported loader kind. `tests/Lattice.Core.Tests/Generation/SequenceInputFactValidatorTests.cs` covers validator behavior more generally. `ComparisonOperatorNormalizerTests` covers accepted/rejected phrase-to-operator capture normalization, not the validation-kind allow-list. No behavior changed as part of this trace.

## M-05B expression fact-to-validation trace (M-05B.1e)

The extractor assembles the capture values under their declared `factId`/`valueId`; the validator then resolves the matching `SequenceInputFact` and applies the declared `kind`. The current Python sequence module has seven validations in the expression-related families:

| Validation ID / kind | Fact and value consumed | Related scope/facts | Effect of validation |
|---|---|---|---|
| `function-local-expressions-reference-earlier-names` / `arithmeticCollectionReferences` | `program.function-local-variables.expressions` | Parameters from `program.function-local-signature.parameters`; locals from `program.function-local-variables.locals`; applies when the local-variables fact exists | Parses each raw arithmetic expression and validates references against the allowed earlier parameters/locals and ordering constraints. |
| `function-local-return-references-available-names` / `arithmeticValueReferences` | `program.function-local-return.expression` | Parameters and locals as above; applies when local variables exist | Parses raw arithmetic expression and checks referenced names against available names. |
| `conditional-function-false-return-references-parameters` / `valueExpressionReferences` | `program.conditional-function.false-return.expression` | Parameters from `program.conditional-function.signature.parameters`; applies when signature exists | Accepts the current simplified value-expression forms and checks references. |
| `collection-filter-threshold-is-arithmetic` / `arithmeticValue` | `program.collection-filter.threshold.expression` | Applies when threshold fact exists | Checks that the raw value parses as an arithmetic expression; this is not comparison validation. |
| `conditional-function-branch-conditions-reference-parameters` / `booleanExpressionCollectionReferences` | `program.conditional-function.branches.conditions` | Parameters from `program.conditional-function.signature.parameters`; applies when signature exists | Parses each Boolean expression string and checks referenced names against parameters. |
| `conditional-function-branch-returns-reference-parameters` / `valueExpressionCollectionReferences` | `program.conditional-function.branches.returns` | Parameters from the conditional-function signature; applies when signature exists | Validates each raw return value and its references. |
| `arithmetic-function-expressions-use-ordered-scopes` / `orderedArithmeticFunctionExpressions` | `program.arithmetic-function-return.expression` | Function parameters and name from `program.arithmetic-function-signature`; applies when signature exists | Parses arithmetic expression/call strings and checks ordered scope and call constraints. |

The captured assignment payload `program.assignment.value` has no expression validation declaration in this module. The comparison operator/threshold facts (`program.collection-filter.comparison.operator` and `.threshold.expression`) have no comparison-kind validation; only the threshold gets the arithmetic syntax check above. The fixed conditional-output comparison rules emit task-specific marker facts and have no extracted operands to validate. `comparisonValueReferences`, `comparisonOperandsReferences`, `arithmeticExpressionReferences`, and `booleanExpressionReferences` are supported validator kinds but are not used by the current module's validation declarations. These findings trace the current data path only; they do not alter validation behavior.

## M-05B expression input inventory — value rules (M-05B.1d)

“Value” here means captures tagged `semanticExpressionKind: value`, not every rule whose field happens to be named `value`. The tagged captures are limited to the rules below; the broader print/output capture families store raw text without this metadata and are inventoried in the main 231-rule table.

| Rule IDs | Match conditions | Captured value / emitted fact IDs |
|---|---|---|
| `capture-assignment-components-set`, `-sets`, `-assign`, `-assigns` | Match the corresponding verb token; exclude the `function named` sequence | Capture target text up to `to` as `name` in `program.assignment.target`; capture the text after `to` up to `and` or `then` as `value`, tagged `value`, in `program.assignment.value`. The payload is raw request text, not a typed literal/reference/expression. |
| `recognize-declarative-conditional-function` — branch returns capture | Match `function`; requires `named`, `takes`, `when`, `and otherwise returns`, and `then` | Captures the return text from each `when`-delimited branch field, up to the otherwise clause; removes trailing comma and preserves raw value. Emits a `returns` collection in `program.conditional-function.branches`, tagged `value`. |
| `recognize-declarative-conditional-function` — otherwise return capture | Same outer function conditions | Captures raw expression after `and otherwise returns` up to `then`, trims trailing comma and preserves raw value; emits `program.conditional-function.false-return` with `valueId: expression`, tagged `value`. |

There are no literal/reference sub-kinds in this capture metadata. The separate two-number addition task captures fixed `10`, `20`, and `first_number + second_number` values; these task-specific literal facts are documented in the main rule inventory and are not general value-expression extraction. Likewise, common output rules capture printed/displayed text without an expression-kind tag; those remain raw strings rather than structured values.

## M-05B expression input inventory — boolean rules (M-05B.1c)

This inventory covers input captures tagged `semanticExpressionKind: boolean` and boolean-specific recognizers in `python.core.json`. There is one boolean capture site and no standalone Boolean-operator input rule family.

| Rule ID | Match conditions | Captures / emitted fact IDs |
|---|---|---|
| `recognize-declarative-conditional-function` | Matches `function`; requires `named`, `takes`, `when`, `and otherwise returns`, and `then` | Captures function name and parameters into `program.conditional-function.signature`; captures parallel branch `returns` and `conditions` collections into `program.conditional-function.branches`. Conditions are extracted from each `when`-delimited field, preserving raw text, trimming a trailing comma, and tagged `semanticExpressionKind: boolean`. Branch return values are tagged `value`; the otherwise return and call are likewise captured as `value` and `functionCall`. Other emitted fact IDs are `program.conditional-function`, `.false-return`, and `.call`. No boolean operator or operand is captured separately. |

The match conditions constrain the outer function/branch frame, not the Boolean expression syntax. The Boolean payload is an unparsed string collection; there are no input rules that independently capture logical `and`/`or`/`not`, grouping, or ordered Boolean operands. The collection-filter rules' `or`/`not` tokens belong to comparison-phrase recognition and emit comparison facts, not boolean-expression facts.

## M-05B expression input inventory — comparison rules (M-05B.1b)

This subsection is inventory-only; it does not change recognition or generation. Comparison coverage in `python.core.json` is concentrated in a task-specific collection-filter schema, plus three fully fixed conditional-output recognizers.

| Rule IDs | Match conditions | Captures / emitted fact IDs |
|---|---|---|
| `capture-collection-filter-operator-1` through `-11` | Each requires the phrase `new list containing only`; matches respectively `greater than or equal to`, `at least`, `less than or equal to`, `at most`, `greater than`, `more than`, `less than`, `below`, `not equal to`, `equal to`, or `equals` | Each captures a constant `operator` value tagged `comparisonOperator` into `program.collection-filter.comparison`: `>=`, `>=`, `<=`, `<=`, `>`, `>`, `<`, `<`, `!=`, `==`, `==`. No operand is captured by these rules. |
| `capture-collection-filter-threshold-1` through `-11` | The same comparison phrase family, with the same required list-filter frame; excludes `average` | Each captures the raw `expression` substring after the matched condition through `then`, trims a trailing comma, and tags it `arithmetic`; emits `program.collection-filter.threshold`. This is the threshold/other side of the comparison, but the item/left operand is supplied by a separate fixed schema/template, not represented as a captured comparison node. |
| `recognize-conditional-output-adult-at-least` | Exact token sequence `prints if 20 is at least 18` | Emits `program.conditional-output.adult-at-least`; no captures. Both operands and the relation are embedded in the match condition. |
| `recognize-conditional-output-minor-less-than` | Exact token sequence `displays if 16 is less than 18` | Emits `program.conditional-output.minor-less-than`; no captures. Both operands and the relation are embedded in the match condition. |
| `recognize-conditional-output-match-equals` | Exact token sequence `outputs if 7 equals 7` | Emits `program.conditional-output.match-equals`; no captures. Both operands and the relation are embedded in the match condition. |

Comparison operators are therefore represented as source-symbol strings in captures rather than language-neutral operator identities. The filter rules capture an operator and threshold separately but do not capture ordered left/right operands as an expression; the conditional rules recognize fixed sample comparisons without extracting either value. Related `capture-collection-filter-threshold-*` rules and their arithmetic-kind metadata are detailed in the arithmetic inventory below.

## M-05B expression input inventory — arithmetic rules (M-05B.1a)

This subsection is inventory-only; no matching or generation behavior is changed. It records each arithmetic-oriented input-rule family in `python.core.json` (the exact rule IDs and JSON entries remain authoritative).

| Rule IDs | Match conditions | Captures / emitted fact IDs |
|---|---|---|
| `unsupported-arithmetic-add`, `unsupported-arithmetic-adds` | Any input containing `add` or `adds` respectively | Emit `program.arithmetic`; no operand/operator capture. Despite the ID prefix, these are positive recognition facts. |
| `recognize-arithmetic-plus`, `recognize-arithmetic-minus`, `recognize-arithmetic-multiply`, `recognize-arithmetic-divide` | Match the respective token `+`, `-`, `*`, `/`. The minus rule additionally excludes `file named` and `json file`. | Emit `program.arithmetic`; no operands or operator value are captured. |
| `recognize-arithmetic-word-plus`, `recognize-arithmetic-word-minus`, `recognize-arithmetic-word-times`, `recognize-arithmetic-word-divided` | Match `plus`, `minus`, `times`, or `divided` respectively | Emit `program.arithmetic`; no operands/operator captured. |
| `recognize-print-expression-plus/minus/multiply/divide` and `recognize-prints-expression-plus/minus/multiply/divide` | Match `print` or `prints` together with one of `+`, `-`, `*`, `/` | Emit `value.output.expression`; no expression operand capture. These are eight separate rules. |
| `recognize-declarative-function-local-variables` | Match `function`; requires `and sets` or `and set` | Captures function `name` after `named` up to `that`, parameters after `takes` up to the local assignments, parallel local names and expression strings from the `and sets` clause (`to`-delimited), and return `expression` after `and returns` up to `then`. The locals' expression collection and return are tagged `semanticExpressionKind: arithmetic`; emits `program.function-local-signature`, `program.function-local-variables`, and `program.function-local-return`. It also captures a subsequent call (tagged `functionCall`) into `program.function-local-call`. |
| `recognize-declarative-arithmetic-function-program` | Match `function`; requires `named that takes`; excludes conditional/assignment phrase patterns specified in its rule | Captures function name after `named`, parameter collection after `takes` (through `and returns`), and raw return expression after `returns` up to listed call/function boundaries. The return capture is tagged `arithmetic`; emits `program.arithmetic-function-signature` and `program.arithmetic-function-return`. |
| `capture-collection-filter-threshold-1` through `-11` | Each matches a comparison phrase (`greater than or equal to`, `at least`, `less than or equal to`, `at most`, `greater than`, `more than`, `less than`, `below`, `not equal to`, `equal to`, `equals`) and requires `new list containing only`; excludes `average` | Captures the source substring as `expression` through `then`, trims a trailing comma, preserves the raw value, and tags it `arithmetic`; emits `program.collection-filter.threshold`. The comparison operator is captured separately by the collection-filter operator rules (not part of these arithmetic rules). |

**Finding:** arithmetic recognition is largely operator-presence tagging, with no structured operand/operator capture. The few actual expression captures preserve source-like strings under arithmetic-kind metadata. The task-scoped threshold captures use the same arithmetic tag even when the associated semantic condition is a comparison. The specific literal defaults for the two-number addition task are inventoried in the existing “Two-number addition task facts” row above; they are fixed benchmark facts rather than request-derived arithmetic operands.

## Input-rule inventory: all 231 rules

Every rule below is an entry in [`python.core.json`'s `inputRules`](../knowledge/sequences/python.core.json). “Reusable-shaped” means the captured meaning could be reusable; it does **not** mean the current phrase recognizer is broad, language-neutral, or compliant with the benchmark's anti-shortcut rule.

| Classification | Rule IDs | What the rules currently do |
|---|---|---|
| **Fixed benchmark value / one-off marker (2)** | `canonicalize-hello-world`, `recognize-hello-world-output-start` | The first stores literal `Hello, world!`; the second marks a dedicated Hello World output-start fact. |
| **Generation-intent phrase recognizers (16)** | `recognize-generation-intent-write`, `recognize-generation-intent-write-script`, `recognize-generation-intent-generate`, `recognize-generation-intent-build`, `recognize-generation-intent-define`, `recognize-generation-intent-make`, `recognize-generation-intent-construct`, `recognize-generation-intent-implement`, `recognize-generation-intent-produce`, `recognize-generation-intent-develop`, `recognize-generation-intent-craft`, `recognize-generation-intent-author`, `recognize-generation-intent-create-program`, `recognize-generation-intent-create-script`, `recognize-generation-intent-write-function`, `recognize-generation-intent-create-function` | Map a growing list of English cues to generation intent. The fact is reusable; the recognition is a fixed phrase list. |
| **Broad construct/operation cues (9)** | `recognize-function-requirement`, `recognize-class-requirement`, `recognize-loop-requirement`, `recognize-iteration-requirement`, `recognize-dictionary-requirement`, `recognize-list-requirement`, `recognize-filter-requirement`, `recognize-sort-requirement`, `recognize-aggregate-requirement` | Add high-level `program.*` facts from isolated words. They do not construct typed operations or relationships. |
| **Narrow dictionary-frame capture (1)** | `recognize-dictionary-key-value-iteration` | Captures key/value mentions only under an `each ... with its ...` frame, and supplies the literal collection name `items`. Reusable-looking iteration, but a narrow input rule plus a fixed default. |
| **Language cue (1)** | `recognize-python-language` | Adds `language.python`; Python target selection is encoded in sequence input knowledge. |
| **Program/output and common print cues (14)** | `recognize-program-output`, `recognize-script-output`, `recognize-print-operation`, `recognize-prints-operation`, `recognize-display-operation`, `recognize-output-operation`, `capture-printed-value`, `capture-printed-values`, `capture-displayed-value`, `capture-output-value`, `recognize-output-start-print`, `recognize-output-start-prints`, `recognize-output-start-display`, `recognize-output-start-output` | Capture output as text and add generic-looking output facts, but use a hand-authored cue list and raw string values. |
| **Say/show/emit/echo output cue and capture family (29)** | `recognize-say-output-operation`, `capture-say-value`, `recognize-output-start-say`, `capture-says-value`, `recognize-output-start-says`, `recognize-show-operation`, `capture-shown-value`, `recognize-output-start-show`, `recognize-shows-operation`, `capture-shows-value`, `recognize-output-start-shows`, `recognize-displays-operation`, `capture-displays-value`, `recognize-output-start-displays`, `recognize-outputs-operation`, `capture-outputs-value`, `recognize-output-start-outputs`, `recognize-emit-operation`, `capture-emitted-value`, `recognize-output-start-emit`, `recognize-emits-operation`, `capture-emitted-values`, `recognize-output-start-emits`, `recognize-echo-operation`, `capture-echoed-value`, `recognize-output-start-echo`, `recognize-echoes-operation`, `capture-echoed-values`, `recognize-output-start-echoes` | Synonym families for output verbs/start cues; payloads are text captures rather than typed value/expression semantics. |
| **Overlapping broad tags (10)** | `unsupported-assignment-set`, `unsupported-assignment-sets`, `unsupported-assignment-start`, `unsupported-assignment-assign`, `unsupported-assignment-assigns`, `unsupported-arithmetic-add`, `unsupported-arithmetic-adds`, `unsupported-iteration-each`, `unsupported-iteration-range`, `unsupported-condition-if` | Despite the `unsupported-*` IDs, these rules add ordinary facts such as `program.assignment`, `program.arithmetic`, `program.iteration`, and `program.condition`; they are not a separate unsupported/rejection fact model. |
| **Assignment cue/capture family (12)** | `capture-assignment-components-set`, `capture-assignment-components-sets`, `capture-assignment-components-assign`, `capture-assignment-components-assigns`, `recognize-assignment-operator-set`, `recognize-assignment-operator-sets`, `recognize-assignment-operator-assign`, `recognize-assignment-operator-assigns`, `recognize-assignment-prefix-set`, `recognize-assignment-prefix-sets`, `recognize-assignment-prefix-assign`, `recognize-assignment-prefix-assigns` | Captures target/value text after `set`/`assign`-style cues and emits related assignment markers. The expression remains source-like text; phrase variants are duplicated. |
| **Arithmetic operator aliases (8)** | `recognize-arithmetic-plus`, `recognize-arithmetic-minus`, `recognize-arithmetic-multiply`, `recognize-arithmetic-divide`, `recognize-arithmetic-word-plus`, `recognize-arithmetic-word-minus`, `recognize-arithmetic-word-times`, `recognize-arithmetic-word-divided` | Map symbolic or English operator cues to arithmetic facts. The minus rule carries file/JSON-specific forbidden phrase guards. |
| **Print-plus-arithmetic phrase variants (8)** | `recognize-print-expression-plus`, `recognize-print-expression-minus`, `recognize-print-expression-multiply`, `recognize-print-expression-divide`, `recognize-prints-expression-plus`, `recognize-prints-expression-minus`, `recognize-prints-expression-multiply`, `recognize-prints-expression-divide` | More specific output-expression recognizers layered on the generic operator rules. |
| **Two-number addition task facts (14)** | `recognize-first-addition-assignment`, `recognize-second-addition-assignment`, `recognize-result-addition-assignment`, `capture-first-addition-target`, `capture-first-addition-value`, `capture-second-addition-target`, `capture-second-addition-value`, `capture-result-addition-target`, `capture-result-addition-value`, `recognize-addition-output`, `recognize-first-addition-assignment-operator`, `recognize-second-addition-assignment-operator`, `recognize-result-addition-assignment-operator`, `recognize-addition-variable-output` | One task structure with fixed default variable names `first_number`, `second_number`, `result`, fixed values `10` and `20`, and a fixed result expression `first_number + second_number`. This is not a generic addition request representation. |
| **Sign-classification task facts (12)** | `recognize-sign-classification-assignment-report`, `recognize-sign-classification-assignment-phrase`, `recognize-positive-classification-report`, `recognize-positive-classification-phrase`, `recognize-negative-classification-report`, `recognize-negative-classification-phrase`, `recognize-zero-classification-report`, `recognize-zero-classification-phrase`, `recognize-sign-classification-output-report`, `recognize-sign-classification-output-start-report`, `recognize-sign-classification-output-phrase`, `recognize-sign-classification-output-start-phrase` | Recognize fixed positive/negative/zero categories and output expectations; the request's numeric value/identifier are not captured as reusable semantic operands. |
| **Fixed collection/loop/conditional-output tasks (8)** | `recognize-even-list-filter-request`, `recognize-manual-list-total-request`, `recognize-loop-accumulator-total`, `recognize-loop-accumulator-result`, `recognize-initializes-assignment`, `recognize-conditional-output-adult-at-least`, `recognize-conditional-output-minor-less-than`, `recognize-conditional-output-match-equals` | Task-specific fact IDs for fixed examples: even filtering, manual sum, accumulator examples, and conditionals using 20/18, 16/18, or 7/7 with fixed output text. |
| **Greeting/class/file/dictionary/conversion task recognizers (8)** | `recognize-greeting-function-definition-request`, `recognize-greeting-function-call-request`, `recognize-bank-account-class`, `recognize-non-empty-file-line-count`, `recognize-dictionary-product-iteration`, `recognize-dictionary-name-count-iteration`, `recognize-integer-conversion-function`, `recognize-named-integer-conversion-function` | One-off task-family facts, including a fixed bank-account model, greeting names, file path/use, dictionaries, and conversion examples. |
| **Parity/mapping/directory/multi-sort task recognizers (8)** | `recognize-even-number-list-parity-filter`, `recognize-odd-value-list-parity-filter`, `recognize-number-square-dictionary-mapping`, `recognize-value-offset-dictionary-mapping`, `recognize-name-length-dictionary-mapping`, `recognize-directory-file-listing`, `recognize-multi-field-record-sorting`, `recognize-case-insensitive-multi-field-record-sorting` | Prompt/task-family IDs for specific collection transformations, a directory-listing script, and multi-field sorting. |
| **Reusable-shaped function/collection/range captures (13)** | `recognize-declarative-function-local-variables`, `capture-collection-loop-from`, `capture-collection-loop-in-list`, `recognize-declarative-arithmetic-function-program`, `capture-range-loop-number`, `capture-range-loop-numbers`, `capture-range-loop-value`, `capture-range-loop-values`, `capture-range-loop-item`, `capture-range-loop-items`, `capture-range-loop-count`, `capture-range-loop-counts`, `capture-range-loop-i` | More structured captures for locals, collection loops, arithmetic functions and range loops. Several still accept expressions or lists as language-shaped strings; range synonyms and task framing are hand-authored. |
| **JSON file round-trip task family (8)** | `recognize-json-round-trip-python`, `recognize-json-round-trip-generation-intent`, `capture-json-round-trip-record-default`, `capture-json-round-trip-record-name`, `capture-json-round-trip-file-default`, `capture-json-round-trip-file-name`, `recognize-json-round-trip-loads-back`, `recognize-json-round-trip-load-back` | Detects one JSON round-trip request family and defaults to `records`, `records.json`, and a fixed record shape/sample. These facts select a pre-authored sequence recipe. |
| **Grouped record aggregation task family (4)** | `capture-grouped-record-aggregation`, `recognize-record-aggregation-calculates`, `recognize-record-aggregation-computes`, `recognize-record-aggregation-sums` | Captures record/group/measure labels for one group-and-sum recipe; the output side supplies a sample data set and fixed algorithm. |
| **Single-field record-sort task family (12)** | `recognize-single-field-record-sorting`, `capture-single-field-sort-function-name`, `capture-single-field-sort-records-name`, `capture-single-field-sort-field-name`, `default-single-field-sort-function-name`, `default-single-field-sort-records-name`, `default-single-field-sort-field-name`, `recognize-single-field-sort-case-insensitive`, `recognize-single-field-sort-case-insensitive-spaced`, `recognize-single-field-sort-descending`, `recognize-single-field-sort-reverse`, `recognize-single-field-sort-requirement-sorted` | Captures a few names/options for one sorting template, with defaults and fixed example inputs in output rules. |
| **Conditional-function / collection-filter / function-call task family (34)** | `recognize-declarative-conditional-function`, `recognize-collection-filter-function-name`, `default-collection-filter-function-name`, `default-average-filter-function-name`, `capture-collection-filter-parameter-receives`, `capture-collection-filter-parameter-accepts`, `default-collection-filter-parameter`, `capture-collection-filter-item`, `default-collection-filter-item`, `capture-collection-filter-operator-1`, `capture-collection-filter-operator-2`, `capture-collection-filter-operator-3`, `capture-collection-filter-operator-4`, `capture-collection-filter-operator-5`, `capture-collection-filter-operator-6`, `capture-collection-filter-operator-7`, `capture-collection-filter-operator-8`, `capture-collection-filter-operator-9`, `capture-collection-filter-operator-10`, `capture-collection-filter-operator-11`, `recognize-collection-filter-average-threshold`, `recognize-collection-filter-requirement`, `capture-collection-filter-threshold-1`, `capture-collection-filter-threshold-2`, `capture-collection-filter-threshold-3`, `capture-collection-filter-threshold-4`, `capture-collection-filter-threshold-5`, `capture-collection-filter-threshold-6`, `capture-collection-filter-threshold-7`, `capture-collection-filter-threshold-8`, `capture-collection-filter-threshold-9`, `capture-collection-filter-threshold-10`, `capture-collection-filter-threshold-11`, `recognize-arithmetic-function-composition-call` | Captures branch/return/call expressions and filter names, parameters, comparison operators, thresholds, and an optional composition call. This is the most structured input family, but still a mini phrase recognizer for fixed fact schemas, with string-valued expressions and sample collection output; it is not a language-neutral programming representation. |

The row counts sum to **231**. The file's task-specific fact families include (among others) `program.assignment.first/second/result`, `program.classification.*`, `program.even-list-filter`, `program.manual-list-total`, `program.greeting-function`, `program.bank-account-class`, `program.non-empty-file-line-count`, the dictionary/mapping/file-listing IDs, `program.json-round-trip`, `program.record-aggregation`, `program.single-field-record-sorting`, `program.collection-filter.*`, and `program.conditional-function.*`.

## Sequence-rule inventory: all 136 rules

Every rule below is an entry in [`python.core.json`'s `rules`](../knowledge/sequences/python.core.json). “Syntax fragment” means the rule contains legitimate Python spelling/formatting; it remains tied to this Python-only, fact-ID-based generator. “Solution/template” means its rule or its fixed bindings encode an answer/recipe, not just a language production.

| Classification | Rule IDs | Audit finding |
|---|---|---|
| **Reusable-looking output/assignment syntax fragments (12)** | `python-output-start`, `python-output-open`, `python-output-value`, `python-output-expression`, `python-output-close`, `python-assignment-target`, `python-assignment-operator`, `python-assignment-value`, `python-assignment-line-end`, `python-output-start-after-assignment`, `python-assignment-prefix`, `python-output-call-start` | Python tokens/Markdown fence and generic-looking print/assignment structure. They consume fixed sequence fact IDs and participate only in the raw Python route. |
| **Two-number addition scaffold (13)** | `python-addition-first-target`, `python-addition-first-operator`, `python-addition-first-value`, `python-addition-first-end`, `python-addition-second-target`, `python-addition-second-operator`, `python-addition-second-value`, `python-addition-second-end`, `python-addition-result-target`, `python-addition-result-operator`, `python-addition-result-value`, `python-addition-result-end`, `python-addition-output-start` | Bound operands, but the three-assignment shape and first/second/result fact IDs are task-specific. |
| **Complete/fixed scalar, collection, function and file programs (13)** | `python-sign-classification-assignment`, `python-sign-classification-positive`, `python-sign-classification-negative`, `python-sign-classification-zero`, `python-even-list-filter`, `python-manual-list-total`, `python-loop-accumulator-total`, `python-loop-accumulator-result`, `python-conditional-output-adult-at-least`, `python-conditional-output-minor-less-than`, `python-conditional-output-match-equals`, `python-greeting-function`, `python-non-empty-file-line-count` | Emit complete or near-complete programs with fixed values, sample collections, outputs, names (`Ada`, `Grace`), or file path (`input.txt`). These are stored benchmark-style answers. |
| **Dictionary key/value iteration scaffold (6)** | `python-dictionary-iteration-open`, `python-dictionary-iteration-key`, `python-dictionary-iteration-value`, `python-dictionary-iteration-collection`, `python-dictionary-iteration-print`, `python-dictionary-iteration-print-values` | Some key/value bindings are dynamic, but the opening emits `items = {}` plus a placeholder comment; the structure is fixed to Python `.items()` iteration and `print`. It is a task-family template, not a generic collection operation realization. |
| **Complete fixed dictionary/conversion/parity/mapping/directory/sort outputs (12)** | `python-dictionary-product-iteration`, `python-dictionary-name-count-iteration`, `python-integer-conversion-function`, `python-named-integer-conversion-function`, `python-even-number-list-parity-filter`, `python-odd-value-list-parity-filter`, `python-number-square-dictionary-mapping`, `python-value-offset-dictionary-mapping`, `python-name-length-dictionary-mapping`, `python-directory-file-listing`, `python-multi-field-record-sorting`, `python-case-insensitive-multi-field-record-sorting` | Emit complete programs with fixed dictionaries, names, numbers, collections, expressions, sample records, or directory policy. This includes the directory-listing and mapping answers called out in the queue. |
| **Parameterized function/collection/range syntax fragments (24)** | `python-local-function-open-fence`, `python-local-function-header`, `python-local-function-first-assignment`, `python-local-function-next-assignment`, `python-local-function-return-start`, `python-local-function-return-expression`, `python-local-function-call`, `python-local-function-close-fence`, `python-collection-loop-variable`, `python-collection-loop-values`, `python-collection-loop-output`, `python-collection-loop-output-with-collection`, `python-arithmetic-function-open-fence`, `python-arithmetic-function-header`, `python-arithmetic-function-return-prefix`, `python-arithmetic-function-return-expression`, `python-arithmetic-function-call`, `python-arithmetic-function-close-fence`, `python-arithmetic-function-open-fence-with-expression`, `python-range-loop-open`, `python-range-loop-variable`, `python-range-loop-start`, `python-range-loop-exclusive-end`, `python-range-loop-output` | These bind some values and express real function/loop syntax, but are keyed to narrow `program.function-local-*`, `program.collection-loop`, `program.arithmetic-function-*`, and `program.range-loop` schemas. Raw source expression text is passed through; they are not a language-neutral operation plan. |
| **JSON serialization round-trip recipe (8)** | `python-json-round-trip-open`, `python-json-round-trip-import`, `python-json-round-trip-records`, `python-json-round-trip-write-open`, `python-json-round-trip-dump`, `python-json-round-trip-read-open`, `python-json-round-trip-load`, `python-json-round-trip-output` | A complete task recipe; records are fixed to Ada/Grace examples and a fixed `name`/`age` shape, even though variable and file names can be bound. |
| **Grouped record-aggregation recipe (7)** | `record-aggregation-data`, `record-aggregation-initialize`, `record-aggregation-loop`, `record-aggregation-key`, `record-aggregation-add`, `record-aggregation-iterate-totals`, `record-aggregation-output` | Full ordered group-and-sum algorithm, with fixed sample records and hard-coded `totals`/loop/output scaffolding. |
| **Single-field sort template and fixed sample calls (8)** | `single-field-record-sort-function`, `single-field-record-sort-parameters`, `single-field-record-sort-body-standard`, `single-field-record-sort-body-case-insensitive`, `single-field-record-sort-body-descending`, `single-field-record-sort-body-case-insensitive-descending`, `single-field-record-sort-output`, `single-field-record-sort-output-case-insensitive` | The function name, collection, and field can bind, but the sorted implementation and complete example input/call are template content. |
| **Bank-account class template (6)** | `python-class-model-open`, `python-class-model-definition`, `python-class-model-deposit-header`, `python-class-model-deposit-body`, `python-class-model-withdraw-header`, `python-class-model-withdraw-body` | Parameterized names, but fixed class/member relationships and fixed deposit/withdraw behavior; the semantic model is still the bank-account task. |
| **Collection-filter function template (16)** | `python-collection-filter-open`, `python-collection-filter-open-average`, `python-collection-filter-header`, `python-collection-filter-return-prefix`, `python-collection-filter-item-first`, `python-collection-filter-item-binding`, `python-collection-filter-parameter`, `python-collection-filter-condition-item`, `python-collection-filter-condition-operator`, `python-collection-filter-condition-threshold`, `python-collection-filter-condition-average`, `python-collection-filter-call`, `python-collection-filter-open-arithmetic`, `python-collection-filter-open-average-with-filter`, `python-collection-filter-open-with-filter`, `python-collection-filter-open-arithmetic-with-filter` | A decomposed template rather than one literal program, but the operation family and collection-comprehension structure are fixed; the call contains a hard-coded `[1, 2, 3, 4, 5]` sample. |
| **Conditional-function template (10)** | `python-conditional-function-open`, `python-conditional-function-open-without-arithmetic`, `python-conditional-function-header`, `python-conditional-function-first-branch`, `python-conditional-function-next-branch`, `python-conditional-function-else-return`, `python-conditional-function-print`, `python-conditional-function-print-with-output-start`, `python-conditional-function-call`, `python-conditional-function-call-with-value-output` | Values/branches can be bound from collections, but the `def`/`if`/`elif`/`else`/`return`/`print` layout and Python-specific expression text are hard-wired to one fact schema and one output shape. |
| **Additional-function header composition (1)** | `python-arithmetic-function-next-header` | A parameterized next-function header/collection cursor for the arithmetic-function-specific fact model; not a general function-generation contract. |

The row counts sum to **136**. Complete or near-complete task outputs include the sign-classification, list filtering/totals, conditional output, greeting, file-line counting, dictionary examples, integer conversion, mapping, directory listing, record sorting, JSON, aggregation, and bank-account examples. Moving any of those intact to another JSON file would not make it reusable knowledge.

## Relevant knowledge modules

The Core project embeds all `knowledge/languages/**/*.json` and `knowledge/sequences/**/*.json`; the Runtime embeds all `knowledge/domains/**/*.json`. The modules below are the code-generation-relevant subset; unrelated Python tools, benchmark fixtures, and other ontology domains are not code-generation modules and should not be removed merely because they mention Python.

| Module | Classification and role |
|---|---|
| [`knowledge/sequences/python.core.json`](../knowledge/sequences/python.core.json) | **Mixed: phrase/task recognizers + request fact schema + Python token output + full/near-full task solutions.** 240 input rules and 30 validations produce `program.*`/`value.*` facts; 21 rules output Python text and fences; one `quoted-string` encoding escapes source text. It is the only sequence module and the main source of single-task shortcuts. |
| [`knowledge/languages/python.core.json`](../knowledge/languages/python.core.json) | **Legitimate declarative target-language knowledge.** 102 Python productions contain statement/call/assignment/control-flow spellings and indentation; 26 bindings describe generic argument/goal-binding sources; 104 operations map effects/argument kinds to Python operation symbols and start productions (including standard APIs such as `open`, `input`, `json.dump`, and `sys.stdout.write`). Keep Python syntax and API spellings here. It contains no single benchmark task's full program, but is only exercised in production through the Blender route today. |
| [`knowledge/domains/python-language-foundations.json`](../knowledge/domains/python-language-foundations.json) | **Foundational but shallow programming ontology.** It supplies generic concepts such as program, statement, expression, value, variable, functions, parameters, collections, modules, exceptions, classes and APIs, plus Python individuals and graph assertions. All 43 type declarations are primitive; relations do not yet model reusable typed operation signatures/effects/prerequisites or enough construction dependencies to drive generation. |
| [`knowledge/domains/blender-object-replacement-script.json`](../knowledge/domains/blender-object-replacement-script.json) | **Mixed: legitimate Blender API/domain facts plus a task-specific ordered solution plan.** It has 118 concepts (9 types, 109 individuals), 8 relation signatures, 241 relation assertions, one data property, and 18 step-order assertions. The 18 steps hard-code the object-replacement plan (imports, target name/pattern, snapshot/iteration/match, light creation, transform/collection linking, fallback, removal) and provide generation effects, argument kinds, and literal source-expression bindings. The eight `api-*` concepts (plus `blender-codegen-api-scene-collection-link`) and their API references are legitimate domain knowledge; the task's selected, ordered answer plan is not reusable programming semantics. |
| [`knowledge/domains/core.json`](../knowledge/domains/core.json) | **Generic ontology infrastructure.** Declares shared `is-a`/`instance-of` signatures; not a code-generation recipe. |
| [`knowledge/domains/software.json`](../knowledge/domains/software.json) | **Adjacent non-codegen domain knowledge.** Used by software recommendation, including Python package classification; not a dependency of either program-emission path. Preserve it. |

The queue's pre-authored Blender plan finding is verified in the domain assertions and, as inventoried, in `TaskCodeGenerationResponder`'s `blender-codegen-has-generation-step` / `blender-codegen-generation-step-order` reads; M-12.10 has since deleted the order property, so the steps are planned in assertion order. The general semantic model loader composes the domain modules; that fact does not mean the Core sequence generator reasons over them.

## Tests and benchmark boundary

- [`KnowledgeDrivenGenerationRuntimeTests`](../tests/Lattice.Runtime.Tests/KnowledgeDrivenGenerationRuntimeTests.cs) exercise the raw-input, knowledge-driven route.
- [`PythonProgramGenerationTests`](../tests/Lattice.Runtime.Tests/PythonProgramGenerationTests.cs) exercise named Python task families, including generated-program execution for selected cases; these fixtures are not by themselves proof of task-independent composition.
- [`KnowledgeDrivenSequenceGeneratorTests`](../tests/Lattice.Core.Tests/Generation/KnowledgeDrivenSequenceGeneratorTests.cs) cover the knowledge loader, extractor, validator, token-rule engine, and many fixed task families.
- [`CompositionalTaskParserTests`](../tests/Lattice.Core.Tests/Language/CompositionalTaskParserTests.cs) call the parser directly, including its sequence-generator preflight; they do not remove the runtime shadowing described above.
- [`TaskRequestParserTests`](../tests/Lattice.Core.Tests/Language/TaskRequestParserTests.cs) cover the script-creation regex and normalized Blender request; [`TaskRequestResponseTests`](../tests/Lattice.Runtime.Tests/TaskRequestResponseTests.cs) exercise the Blender step-plan route.
- [`SequenceGenerationPipelineTests`](../tests/Lattice.Core.Tests/Generation/SequenceGenerationPipelineTests.cs) and [`PythonProgramCompositionTests`](../tests/Lattice.Core.Tests/Generation/PythonProgramCompositionTests.cs) test the language-pack pipeline. There is no second language pack in the current tree. [`OperationSequenceGeneratorTests`](../tests/Lattice.Core.Tests/Generation/OperationSequenceGeneratorTests.cs) exercise a generator with no production callers.
- The M-01 baseline `PythonExpressionParser` had no direct tests or non-definition references in the source/tests tree; M-03 removes the unused helper.

The benchmark's own anti-shortcut contract says task-specific response templates/task-ID branches cannot earn composition or generalization credit, and storing a complete solution or complete code fragment invalidates a compositional task ([`coding-benchmark-v1.json`](../data/benchmarks/coding-benchmark-v1.json)). The Python runner selects the **16 Python fundamentals/compositional tasks**, preserves each source prompt verbatim inside `Write a Python program that fulfills this request: {prompt}`, checks `py_compile`, and only smoke-executes cases marked for execution; it explicitly does not establish semantic correctness or generalization ([runner README](../tools/Lattice.Coding.Benchmark/README.md), [`Program.cs`](../tools/Lattice.Coding.Benchmark/Program.cs)). The runner excludes the Blender and cross-domain holdout tasks. Existing exact-prompt output tests and syntax/smoke execution are useful regression checks, but they do not override the anti-shortcut rule.

## Findings carried into M-02 (M-01 inventory only)

1. The two production engines, `KnowledgeDrivenSequenceGenerator` and `SequenceGenerationPipeline`, use distinct knowledge formats; `OperationSequenceGenerator` is a third, currently test-only, overlapping operation/grammar implementation.
2. The raw Python route's input facts need a path into language-neutral goals without retaining a parallel Python solution generator or turning every prompt into a new JSON rule.
3. The Blender-specific handler and its 18-step recipe must be replaced by the same path while preserving reusable API/domain facts and discarding the preselected answer sequence.
4. `GenerationGoal`/operation/binding contracts may be useful at the realization boundary, but current goals are created from pre-authored Blender steps; effect and argument kind are strings, there is no typed statement/expression or inter-goal dependency model, and goal bindings do not model API signatures. M-01 makes no claim that the existing contract is already a sufficient semantic program model.

The target-path decision based on these findings is documented in the [M-02 architecture section](program-generation-architecture.md#chosen-single-code-generation-path-m-02-decision). No implementation was changed for this inventory. M-01 was marked complete after [PR #555](https://github.com/Vraxo/Lattice-Next/pull/555) merged and its required .NET 10 check passed; the [migration queue](../.agents/todo.md) records the current status.

## M-07.1 scalar task-fact inventory

The queued path `python.core.json` refers to the sequence module [`knowledge/sequences/python.core.json`](../knowledge/sequences/python.core.json), not the target-language pack [`knowledge/languages/python.core.json`](../knowledge/languages/python.core.json), which has grammar/bindings/operations but no request input rules. The input rules emit facts and captures as follows:

| Request family | Input rules and emitted facts | Values currently captured |
|---|---|---|
| Fixed comparisons: conditional output | `recognize-conditional-output-adult-at-least` → `program.conditional-output.adult-at-least`; `recognize-conditional-output-minor-less-than` → `program.conditional-output.minor-less-than`; `recognize-conditional-output-match-equals` → `program.conditional-output.match-equals` | Marker-only rules. Match tokens hard-code `20 >= 18`, `16 < 18`, or `7 == 7`; no operands, operator, variable, branch result, or output string are separately captured. |
| Comparison-based collection filters | `capture-collection-filter-operator-1..11` → `program.collection-filter.comparison` (`operator` values `>=`, `<=`, `>`, `<`, `!=`, `==`); `capture-collection-filter-threshold-1..11` → `program.collection-filter.threshold` (`expression`) | Operator is normalized to a fixed operator literal. Threshold captures source-like text from the prompt, tagged as `arithmetic` rather than comparison; they require the `new list containing only` phrase and mostly stop before `then`. |
| Sign classification | Report/phrase pairs `recognize-sign-classification-assignment-*` → `program.assignment.classification`; `recognize-positive-classification-*` → `program.classification.positive`; corresponding negative/zero pairs → `.negative` / `.zero`; output report/phrase pairs → `program.output` and `program.output.start` | Marker facts only. The numeric input and variable name are not captured; positive/negative/zero branches and the reporting expectation are fixed by the rule family. |
| Two-number addition | `recognize-first-addition-assignment`, `recognize-second-addition-assignment`, `recognize-result-addition-assignment` → `program.assignment.first/second/result`; `capture-first-addition-target/value`, `capture-second-addition-target/value`, `capture-result-addition-target/value` → corresponding `.target/.value`; operator rules emit matching `.operator`; output rules emit `value.output` and `value.output.expression` | Hard-coded `first_number`, `second_number`, `result`, literals `10`, `20`, expression `first_number + second_number`, and printed `result`. The “captures” use `literalValue`; request-specific operands/names are not extracted. |
| Generic assignment phrasing | `capture-assignment-components-set/sets/assign/assigns` emit `program.assignment.target` (`name`) and `program.assignment.value` (`value`, `semanticExpressionKind: value`); paired operator/prefix recognizers emit `program.assignment.operator` and `.prefix` | Target and right-hand text are captured from the request, with the value bounded by `to` and `and`/`then`. The value is still source-like text; operator/prefix facts are cue markers. |

These findings inventory only current extraction behavior; they do not change it. The “fixed comparisons” are not general comparison examples with values to extract: their three scalar conditional-output rules match fixed token sequences and emit opaque task markers. M-07.2 must represent the requested operands, comparisons, assignments, branches, and output generically without reusing these task-family facts as semantic identities.

M-07.3 progress: the existing generic assignment captures already produce request-derived variable names and typed number/text expressions. Focused coverage varies the identifier and literal while keeping the same generic capture rules. `SequenceInputFactExtractor.TryExtractProgram` now projects only the generic assignment target/value facts into ordered `AssignmentRequirement` values; repeated assignments of the same generic form preserve their capture order. It does not map task-family fact IDs. Output fact projection is intentionally not included yet because the current `value.output` ID is also used by a fixed addition rule. This does not retire the separate fixed addition, sign-classification, or conditional-output facts; M-07.3 remains incomplete until those are removed from active extraction safely with their consumers migrated.

**Consumer dependency for M-07.3b:** the sequence output rules still require these legacy fact IDs. Addition fragments consume `program.assignment.first`, `.second`, `.result` and their `.target`, `.value`, `.operator` children through `python-addition-*`; sign fragments consume `program.assignment.classification` and `program.classification.positive/.negative/.zero` through `python-sign-classification-*`; the fixed conditional-output fragments consume `program.conditional-output.adult-at-least`, `.minor-less-than`, and `.match-equals`. Removing only the input captures would disable these queued examples before their generic M-08 composition replaces the fragments. The cutover must migrate those consumers to `SemanticProgram` requirements, then remove the legacy facts together—not retain a second active path.

### M-07.2 request-derived requirement model

`Lattice.Core.Generation.SemanticProgram` now carries an ordered collection of generic `ProgramRequirement` values. `AssignmentRequirement` binds a variable name to a `SemanticExpression`; `ConditionalRequirement` carries a condition expression and separate true/false requirement lists; `OutputRequirement` carries a semantic expression. Numeric and text literals and variable references use the existing typed expression nodes, and comparisons are `SemanticBinaryExpression` nodes with language-neutral operator identities. This model contains no task names, fixed sample operands, prompt phrases, or Python syntax. Extraction into this model remains the next task (M-07.3).

### M-07.4 through M-07.16 scalar interpretation (current state)

Scalar requests are now interpreted as ordered, request-derived requirements. No task-family identity, fixed operand, fixed output string, or target-language token is involved.

- **Request order is carried by facts, not by rule order.** `SequenceSemanticFact` gained `ValuePositions` (earliest request token position of each scalar value) and `CollectionPositions` (positions aligned item-by-item with collections and expression collections). `SequenceInputFactExtractor` records a position for every captured scalar and collection item, rebuilds repeated collections in request order, and drops the former assignment-only special case.
- **Comparisons are projected from the request.** Both operands are captured as typed `SemanticExpression` values and the operator is captured as a language-neutral `SemanticExpressionOperator`, parsed through the selected language pack's input syntax. `capture-generic-scalar-condition-when` and the new `capture-generic-scalar-condition-if` rule supply the `when … then …` and `if … then …` phrasings; there is no phrase-to-task rule.
- **Branches are projected independently.** `recognize-generic-branch-separator-otherwise` and `recognize-generic-branch-separator-else` record the requested separator position inside the generic output fact (`program.requirement.output`, value `branch`). `SemanticProgramFactMapper` splits entries after the condition into true-branch and false-branch requirement lists using those positions, so `ConditionalRequirement.ThenRequirements` and `.ElseRequirements` are projected separately.
- **Outputs are request-derived.** The `capture-generic-output-print/prints/display/displays` rules capture the requested output expression up to the sentence end or the next `then`/`otherwise`/`else`. Quoted text, variable references, arithmetic trees, and function calls are retained as typed expressions; no greeting or classification string is hard-coded.
- **Speculative captures no longer reject a request.** `SequenceInputFactCapture.OptionalExpression` (declarative `optionalExpression`) drops a captured value that cannot be materialized as its declared expression kind instead of failing the whole extraction. The arithmetic function-composition capture uses this, so a request such as `… then print total` is interpreted as an output requirement instead of being rejected as an invalid function call.
- **Multiple requirements and dependencies are preserved.** `SemanticProgramFactMapper` projects assignments, outputs, and the conditional in request order, including outputs that precede a later assignment or condition. `SemanticExpressionAnalysis.GetReferencedNames` exposes the variable references that link assignments to conditions and outputs.
- **Focused coverage:** `tests/Lattice.Core.Tests/Generation/ScalarProgramInterpretationTests.cs` asserts changed numeric and text literals, changed identifiers, paraphrased requests, independent branches, request order, dependency references, multi-requirement requests, and the absence of task-family facts. `SemanticProgramTests` continues to cover the requirement model itself.
- **Fixed comparison samples removed (M-07.9 cutover):** the three marker-only captures `recognize-conditional-output-adult-at-least`, `recognize-conditional-output-minor-less-than`, and `recognize-conditional-output-match-equals` and their complete-program fragments `python-conditional-output-adult-at-least`, `python-conditional-output-minor-less-than`, `python-conditional-output-match-equals` are deleted. Requests phrased as `<output verb> 'text' if <fixed comparison>` are no longer answered from stored answers; the generic conditional realization that covers that phrasing is queued in M-08 (8.10–8.12).
- **Deliberately retained for the M-08 cutover:** `program.assignment.first/.second/.result` (two-number addition, benchmark `py-002`) and `program.assignment.classification` plus `program.classification.positive/.negative/.zero` (sign classification, benchmark `py-003`). Their consumers (`python-addition-*`, `python-sign-classification-*`) are still the only active producers for those benchmark tasks, so removing only the captures would disable passing benchmark examples. M-08.9 and M-08.14 remove the facts and fragments together once generic realization covers those programs.

Current module counts after this change: 21 sequence rules, 240 input-fact rules, 30 validations, 1 encoding. The M-07.1 table above is retained as the pre-migration inventory; the fixed-comparison row no longer describes the current file.

### M-08.1 through M-08.20 scalar composition (current state)

Scalar requests whose meaning is fully captured by `SemanticProgram` requirements are now realized by composing declarative operations. No stored program fragment and no target-language token is involved in that path.

- **Planning.** `SemanticProgramPlanner` resolves each requirement to the operation that produces its effect at the depth it occupies: `emit-assignment`, `emit-indented-assignment`, `emit-double-indented-assignment`; `emit-conditional-header`, `emit-indented-conditional-header`, `emit-double-indented-conditional-header`; `emit-else`, `emit-indented-else`, `emit-double-indented-else`; and `write-standard-output`, `write-standard-output-indented-expression`, `write-standard-output-double-indented-expression`. Assignment targets and expressions reach the pack's productions through the `target`/`expression`, `condition`, and `argument-expression` bindings, so `=`/`if`/`else`/`print` spellings come only from the pack.
- **Expressions.** `SemanticExpressionRenderer` renders numbers, names, text (with pack escaping), booleans, arithmetic, comparisons, and calls from the pack's input syntax. A request whose operator or literal the pack cannot express is rejected with an explicit error instead of being approximated (M-08.2, M-08.3).
- **Prerequisites (M-08.5).** Every name a requirement references must be assigned before it is planned. Names defined inside a branch are tracked per branch; a name defined in only one branch is not available after the conditional. Available names are passed to operation selection as `variable:<name>` capabilities.
- **Trailing conditionals.** The `capture-generic-scalar-condition-trailing-if` rule (value `layout` = `trailing`) covers `print 'text' if <comparison>` and `if <comparison> print 'text'`. The mapper then places every requirement of that request in the true branch instead of treating the leading output as an unconditional step. The capture is `optionalExpression`, so an unparsable condition cannot reject the request — it simply yields no conditional requirement.
- **Runtime path.** `KnowledgeDrivenGenerationResponder` first plans requirements with `SemanticProgramPlanner.TryGenerateProgram`; only when that declines does the legacy token path run. `TryGenerateProgram` declines when the facts carry a capability another migration group still owns (functions, collections, files, classes, loops, the stored addition/sign families) or when the request expresses an output or arithmetic intent that its requirements do not carry. This prevents emitting a partial program while the remaining families are still being migrated.
- **Coverage.** `tests/Lattice.Core.Tests/Generation/SemanticProgramPlanningTests.cs` covers operation selection, arithmetic from operand/operator semantics, rejected operators, comparison/if, if/else, branch bindings, computed and literal outputs, unavailable values, invalid targets, and nesting limits. `SemanticProgramGenerationTests` covers facts-to-code planning for changed values, every supported comparison family, dependent assignments, and the decline policy. `tests/Lattice.Runtime.Tests/PythonProgramGenerationTests.cs` executes generated programs for changed values, arithmetic, dependent assignments, both branches of `>=`, the trailing-conditional family, and `==`.
- **Current module counts:** 133 sequence rules, 241 input-fact rules, 50 validations, 1 encoding.

**Still open in this group.** Three items delete stored solutions whose generic replacement does not exist yet, so they remain unchecked and their legacy facts stay active:

- **M-08.9 (addition).** `program.assignment.first/.second/.result` and `python-addition-*` remain. The request (`stores two numbers in variables, adds them, and prints the result`, benchmark `py-002`) supplies no operands or names, so a generic plan cannot be derived without inventing them; the current knowledge schema cannot express "two new variables with default values" without a task-shaped rule. Removing the fragments now would disable a passing benchmark example before any replacement exists.
- **M-08.14 (sign classification).** `program.assignment.classification`, `program.classification.positive/.negative/.zero`, and `python-sign-classification-*` remain. The replacement needs several independent conditionals with alternative branches in one request; extraction currently aligns one condition per request, so the request cannot yet be interpreted generically.
- **M-08.17 (greeting).** `python-greeting-function` remains. Its replacement is function definition and call realization, which belongs to M-10; deleting it now would remove the only producer for benchmark `py-006`.

### M-09.1 through M-09.4 collection, iteration, dictionary, and sorting inventory (current state)

Recorded before any change to these families, as the pre-migration baseline for M-09. Counts are per family; the whole file holds 241 input-fact rules, 133 sequence rules, 50 validations, and 1 encoding.

#### M-09.1 List filtering rules and templates (36 input rules, 19 sequence rules)

| Family | Rules | Facts and captured values | Emitted fragments |
| --- | --- | --- | --- |
| **Parity filters** | `recognize-even-list-filter-request`, `recognize-even-number-list-parity-filter`, `recognize-odd-value-list-parity-filter` | Marker facts only: `program.even-list-filter`, `program.even-number-list-parity-filter`, `program.odd-value-list-parity-filter` | Complete programs: `for number in [1, 2, 3, 4, 5, 6]:` / `if number % 2 == 0:` / `print(number)`; the odd variant uses `[-3, -2, -1, 0, 1, 2, 3]` and `value % 2 != 0` |
| **Filter function signature** | `recognize-collection-filter-function-name`, `default-collection-filter-function-name`, `default-average-filter-function-name`, `capture-collection-filter-parameter-receives`, `capture-collection-filter-parameter-accepts`, `default-collection-filter-parameter`, `capture-collection-filter-item`, `default-collection-filter-item` | `program.collection-filter.signature` → `name`, `parameter`, `item`; defaults are `filter_values`, `values`, `value` (average default `above_average`) | `def <name>(<parameter>):`, `return [`, item binding, call binding |
| **Filter operator** | `capture-collection-filter-operator-1..11` | `program.collection-filter.comparison#operator` as a `comparisonOperator`, one literal per phrase: `>=` (greater than or equal to, at least), `<=` (less than or equal to, at most), `>` (greater than, more than), `<` (less than, below), `!=` (not equal to), `==` (equal to, equals) | Operator token only; the rule family is the phrase-to-operator table |
| **Filter threshold** | `capture-collection-filter-threshold-1..11`, `recognize-collection-filter-average-threshold`, `recognize-collection-filter-requirement` | `program.collection-filter.threshold#expression` captured from the prompt and tagged `arithmetic`; `program.collection-filter.average` is a marker fact | Threshold expression and the `parameter` binding for the average predicate |
| **Filter template** | `python-collection-filter-open`, `-open-average`, `-open-arithmetic`, `-open-with-filter`, `-open-average-with-filter`, `-open-arithmetic-with-filter`, `-header`, `-return-prefix`, `-item-first`, `-item-binding`, `-parameter`, `-condition-item`, `-condition-operator`, `-condition-threshold`, `-condition-average`, `-call` | Consumes the signature, comparison, threshold/average, `program.filter`, `program.collection`, `program.arithmetic`, and intent facts | Composes `def <name>(<parameter>):` + `return [` + `<item> for <item> in <parameter> if <item> <op> <threshold>` + a call with the fixed sample `[1, 2, 3, 4, 5]` |

Fixed predicates: even and odd only, each a single `%` comparison. Captured from the request: function name, parameter, item, operator, threshold. Not captured: collection contents, element type, membership/boolean predicates (`not equal to 3` is an operator, not a predicate), or any composite condition.

#### M-09.2 Iteration and accumulation rules and templates (18 input rules, 12 sequence rules)

| Family | Rules | Facts and captured values | Emitted fragments |
| --- | --- | --- | --- |
| **Collection loops** | `capture-collection-loop-from`, `capture-collection-loop-in-list`, `recognize-loop-requirement`, `recognize-iteration-requirement` | `program.collection-loop#item`, `#collection` | `python-collection-loop-variable`, `-values`, `-output`, `-output-with-collection` bind the item and collection names |
| **Range loops** | `capture-range-loop-number(s)`, `-value(s)`, `-item(s)`, `-count(s)`, `-i`, `unsupported-iteration-each`, `unsupported-iteration-range` | `program.range-loop#item/start/end`, `program.iteration#kind = range`, `program.output.start#kind = print` | `python-range-loop-open/variable/start/exclusive-end/output` |
| **Manual total** | `recognize-manual-list-total-request` | Marker fact `program.manual-list-total` (the "without the built-in sum function" constraint is only this marker) | Complete program: `total = 0` / `for number in [1, 2, 3, 4, 5]:` / `total = total + number` / `print(total)` |
| **Loop accumulator** | `recognize-loop-accumulator-total`, `recognize-loop-accumulator-result` | Marker facts `program.loop-accumulator`, `program.loop-accumulator.result` | Complete programs: `total = 0` / `for number in [1, 2, 3]:` / `total = total + number` / `print(total)`; the result variant uses `result`, `[-2, 4.5, 1.5]` |

Fixed: accumulator name, initial value `0`, collection contents, the `+` update, and the printed name. Not represented: accumulator type, the update expression, whether the aggregate is a sum/min/max, or a result consumed later (M-09.10–M-09.12, M-09.20).

#### M-09.3 Dictionary mapping rules and templates (7 input rules, 11 sequence rules)

| Family | Rules | Facts and captured values | Emitted fragments |
| --- | --- | --- | --- |
| **Key/value iteration** | `recognize-dictionary-requirement`, `recognize-dictionary-key-value-iteration` | `program.dictionary-iteration#key/value/collection` (default collection `items`) | `python-dictionary-iteration-open/key/value/collection/print/print-values`; the open rule emits `items = {}  # Add dictionary entries here.` |
| **Product iteration** | `recognize-dictionary-product-iteration` | Marker fact `program.dictionary-product-iteration` | Complete program with `prices = {'apple': 1.5, 'banana': 0.75}` and `for product, price in prices.items(): print(product, price)` |
| **Name/count iteration** | `recognize-dictionary-name-count-iteration` | Marker fact `program.dictionary-name-count-iteration` | Complete program with `counts = {'Ada': 3, 'Grace': 5, 'Linus': 6}` |
| **Mapping comprehensions** | `recognize-number-square-dictionary-mapping`, `recognize-value-offset-dictionary-mapping`, `recognize-name-length-dictionary-mapping` | Marker facts per task family | Complete functions: `{number: number * number ...}`, `{value: value + 10 ...}`, `{name: len(name) ...}`, each called with a fixed literal list |

Fixed: key expression, value expression, sample dictionary/list contents, function and variable names. Nothing in the request supplies key/value expressions (M-09.7, M-09.14, M-09.24).

#### M-09.4 Sorting and record rules and templates (20 input rules, 18 sequence rules)

| Family | Rules | Facts and captured values | Emitted fragments |
| --- | --- | --- | --- |
| **Single-field record sort** | `recognize-single-field-record-sorting`, `capture-single-field-sort-function-name`, `-records-name`, `-field-name`, `default-single-field-sort-function-name`, `-records-name`, `-field-name`, `recognize-single-field-sort-case-insensitive`, `-case-insensitive-spaced`, `-descending`, `-reverse`, `-requirement-sorted` | `program.single-field-record-sorting` → `name`, `records`, `field`; defaults `sort_records`, `records`, `field`; marker facts `.header`, `.signature`, `.body`, `.case-insensitive`, `.descending`, `program.sort` | `single-field-record-sort-function/parameters/body-standard/body-case-insensitive/body-descending/body-case-insensitive-descending/output/output-case-insensitive` compose a `sorted(...)` function plus a fixed sample call |
| **Multi-field record sort** | `recognize-multi-field-record-sorting`, `recognize-case-insensitive-multi-field-record-sorting` | Marker facts `program.multi-field-record-sorting`, `.case-insensitive` | Complete programs using `sorted(people, key=lambda record: tuple(record[field] for field in sort_keys))` and a fixed record sample |
| **Record aggregation** | `capture-grouped-record-aggregation`, `recognize-record-aggregation-calculates`, `-computes`, `-sums` | `program.record-aggregation#record/group/measure` plus `.intent` | `record-aggregation-data/initialize/loop/key/add/iterate-totals/output` compose the group-and-sum algorithm with `totals = {}` and a fixed sample |
| **Directory listing** | `recognize-directory-file-listing` | Marker fact `program.directory-file-listing` | Fixed script using `os.scandir`, `sorted(...)`, `entry.is_file()`, and a `FileNotFoundError` branch |

Fixed: sort keys and directions are marker facts rather than request-derived key expressions, record samples are literals, and the directory listing is a fixed policy script. M-09.15 and M-09.16 need a request-derived key expression plus direction; M-09.25 removes these templates once that realization exists.

### M-10.1 through M-10.4 function, call, class, and exception inventory (current state)

Recorded before any change to these families, as the pre-migration baseline for M-10. Counts are per family and overlap where one rule feeds two families (the greeting and conversion markers also feed the call family). The module totals stay at 241 input-fact rules, 133 sequence rules, 50 validations, and 1 encoding; nothing is added or removed by this inventory.

#### M-10.1 Function declaration rules and templates (8 input rules, 29 sequence rules)

| Family | Rules | Facts and captured values | Emitted fragments |
| --- | --- | --- | --- |
| **Greeting function** | `recognize-greeting-function-definition-request` | Marker fact `program.greeting-function` (matched from `define/defines function accepts name returns greeting`) | One complete program: `def greet(name):` / `return f"Hello, {name}!"` |
| **Integer conversion** | `recognize-integer-conversion-function`, `recognize-named-integer-conversion-function` | Marker facts `program.integer-conversion-function`, `program.named-integer-conversion-function`, plus `program.output#kind = function-return` and `program.function` | Complete programs: `def convert_to_integer(text)` / `def parse_count(raw_text)`, each wrapping `int(...)` in `try:` / `except ValueError:` and returning the fixed message `"Invalid integer"` |
| **Local-variable composition** | `recognize-declarative-function-local-variables` | `program.function-local-signature#name/parameters`, `program.function-local-variables#locals/expressions` (arithmetic), `program.function-local-return#expression`, `program.function-local-call#call` | `python-local-function-*`: fence, header, one assignment per local, `    return ` + expression, the call, and the fence close |
| **Arithmetic composition** | `recognize-declarative-arithmetic-function-program`, `recognize-arithmetic-function-composition-call` | `program.arithmetic-function-signature#name/parameters`, `.arithmetic-function-return#expression` (arithmetic), `.arithmetic-function-call#call`, `.arithmetic-function-close#required` | `python-arithmetic-function-*`: fence, header, `    return ` + expression, the call, and the close; two open variants (with and without a named call) |
| **Conditional composition** | `recognize-declarative-conditional-function` | `program.conditional-function.signature#name/parameters`, `.branches#returns/conditions` (paired by collection delimiter `, returns` and field delimiter `when`), `.false-return#expression`, `.call#expression` | `python-conditional-function-*`: fence, header, one `if` per returned condition, `    return ` per branch, an optional `else` return (`else-return`), and the call/print variants |

Captured from the request: function name, parameter list, local assignments, the returned expression, and the call. Fixed: every function template assumes a specific arity placement, the greeting's message format and both sample argument lists (`"Ada"`, `"Grace"`, `"42"`, `"not-a-number"`), and the conversion functions' message text and exception type. Not represented: parameter types, return types, a body as an ordered requirement sequence, and the relationship between a return expression and the parameters it depends on.

#### M-10.2 Function-call rules and templates (4 input rules, 6 sequence rules)

| Family | Rules | Facts and captured values | Emitted fragments |
| --- | --- | --- | --- |
| **Call in a composed function** | `recognize-declarative-function-local-variables` (`call`), `recognize-declarative-conditional-function` (`expression`), `recognize-arithmetic-function-composition-call` (`call`, plus `program.arithmetic-function-close#required`) | The call is captured as a `functionCall`- or `value`-kind expression, optionally (`optionalExpression`) | `python-local-function-call`, `python-arithmetic-function-call`, `python-conditional-function-call`, `python-conditional-function-call-with-value-output` |
| **Call as an output** | `recognize-greeting-function-call-request`, `recognize-arithmetic-function-composition-call` | Marker fact `program.greeting-function.calls`; `program.arithmetic-function-call` | `python-conditional-function-print`, `python-conditional-function-print-with-output-start`, the greeting program's two `print(greet(...))` lines |

Captured: the call expression text when the request writes one. Fixed: greeting's two argument values, conversion's two argument values, and which call is printed. Not represented: the callee as a reference to a declared function, ordered argument expressions, argument-to-parameter binding, or any check that a call's arguments match its declaration.

#### M-10.3 Class rules and templates (2 input rules, 6 sequence rules)

| Family | Rules | Facts and captured values | Emitted fragments |
| --- | --- | --- | --- |
| **Class marker** | `recognize-class-requirement` | Marker fact `program.class` | None by itself |
| **Bank-account model** (removed in M-10.23) | `recognize-bank-account-class` (13 captures) — replaced by `recognize-declared-class` | Was `program.class-model#kind/className/attributeName/initializerParameter/depositMethodName/withdrawalMethodName/amountParameter` and five `program.class-model.stage.*` markers, all `literalValue` constants; now `program.requirement.class#name`, `program.requirement.class.field#field/initial`, `program.requirement.class.method#method/parameter/result`, `.method.guard#method/left/right/operator/value`, `.method.update#method/field/operator/value`, and `.method.result#method/value` | `python-class-model-*` deleted; the declaration now renders from the pack's `class`/method/indented-statement productions through `SemanticProgramPlanner.TryPlanClass` |

Before M-10.23 the class name, the attribute, the initializer parameter, both method names, the amount parameter, the method bodies, and the demonstration output were all fixed. Captured from the request now: the class identity. Declared by the recognized rule as the shape the request describes: one field whose initial value is the constructor argument of the same name, two methods with one parameter each, a declared result kind per method, a guard that refuses a withdrawal larger than the balance, and in-place field updates. Still not represented: a request-derived method body expression, member access outside the declaration, and the demonstration call sequence, which the benchmark appends in its own harness rather than in generated code.

#### M-10.4 Exception rules and templates (3 input rules, 3 sequence rules)

| Family | Rules | Facts and captured values | Emitted fragments |
| --- | --- | --- | --- |
| **Conversion failures** | `recognize-integer-conversion-function`, `recognize-named-integer-conversion-function` | Marker facts only (`program.integer-conversion-function`, `program.named-integer-conversion-function`, `program.output#kind = function-return`) | `try:` / `        return int(text)` / `except ValueError:` / `        return "Invalid integer"` inside two complete programs |
| **Missing directory** | `recognize-directory-file-listing` | Marker fact `program.directory-file-listing` | `try:` around the `os.scandir` loop with `except FileNotFoundError:` printing `Directory not found: {directory}` |

Fixed: the exception types (`ValueError`, `FileNotFoundError`), the recovery bodies, and the messages. Captured: nothing. Not represented: a raised error with a type and condition, a handler with a type and recovery body, or the distinction between a successful result and a handled failure.

#### M-10.23 Result: class declarations plan from request-derived facts

`recognize-declared-class` replaces `recognize-bank-account-class`. It matches a class declaration request and captures the identity the request names; the member shape is declared as ordered literal columns on the same rule (16 captures, one per fact/value pair, which is the loader's per-rule budget):

| Fact | Values | Consumed by |
| --- | --- | --- |
| `program.requirement.class#name` | captured identity, prefix `a`/`an`/`the` trimmed | `ClassRequirement.Name` through `TryDeriveClassName` |
| `program.requirement.class.field#field` | `balance` | `ClassField.Name`; its initial value defaults to the constructor parameter of the same name |
| `program.requirement.class.method#method/parameter/result` | `deposit|withdraw`, `amount|amount`, `none|Boolean` | one `MethodRequirement` per method, with `none` as the no-result marker and `Boolean` as the declared result kind |
| `program.requirement.class.method.guard#method/left/right/operator/value` | `withdraw`, `amount`, `balance`, `GreaterThan`, `False` | the guarded `ConditionalRequirement` and its early `ReturnRequirement` |
| `program.requirement.class.method.update#method/field/operator/value` | `deposit|withdraw`, `balance|balance`, `Add|Subtract`, `amount|amount` | one `UpdateRequirement` per method (`this.balance += amount` / `-= amount`) |
| `program.requirement.class.method.result#method/value` | `withdraw`, `True` | the trailing `ReturnRequirement` on the success path |

Deleted with the migration: `recognize-bank-account-class`, the six `python-class-model-*` sequence rules, and the six `bank-account-class-model-*` validations. Added: `declared-class-field-is-identifier`, `declared-class-method-is-identifier`, and `declared-class-method-is-unique`. The module now carries 98 sequence rules and 34 validations.

Two loader limits shaped the rule and are worth recording: an input rule may declare at most 16 captures, and every capture token in `startAfterTokens`/`stopBeforeTokens` must be an identifier (letters, digits, `.`, `-`, `_`, `:`), so punctuation such as `,` cannot terminate a capture. Both failures surface only as a generic "Every input fact rule requires..." error that fails the whole knowledge module, which in turn makes every knowledge-driven request produce no utterance at all — a knowledge-load failure looks like a runtime regression.

#### Consequences for M-10.5 through M-10.27

Every function family emits a *complete program* whose shape (fence, `def`, body, sample call) is baked into the sequence rules, so the generic path has to model four things the families assume implicitly: an ordered function body requirement, parameters with declared types, a declared return type with return expressions, and calls that bind ordered arguments to a declaration. M-10.22 and M-10.23 have since deleted the function, conversion, and class templates: function requests and class declarations now plan from request-derived requirements. Exception fragments survive in one place only — `python-directory-file-listing`, which still emits a whole program because the planner has no import or directory-iteration requirement; M-10.29 removes it once those land.

### M-11.1 through M-11.3 file, directory, and serialization inventory (current state)

Recorded before any change to these families, as the pre-migration baseline for M-11. The module holds 98 sequence rules, 241 input-fact rules, 34 validations, and one encoding; the Python pack holds 91 productions, 20 bindings, and 93 operations. Nothing is added or removed by this inventory.

#### M-11.1 File read and write (1 input rule, 1 sequence rule)

| Family | Rules | Facts and captured values | Emitted fragments |
| --- | --- | --- | --- |
| **Non-empty line count** | `recognize-non-empty-file-line-count` (9 match tokens, no captures) | Marker fact `program.non-empty-file-line-count`, nothing captured | `python-non-empty-file-line-count` (1 rule): `line_count = 0` / `with open("input.txt", "r", encoding="utf-8") as input_file:` / `    for line in input_file:` / `        if line.strip():` / `            line_count += 1` / `print(line_count)` |

Fixed: the path `input.txt`, the file variable `input_file`, the mode and encoding (`"r"`, `"utf-8"`), the accumulator `line_count`, the emptiness predicate `line.strip()`, and the printed result. Captured from the request: nothing. There is **no file-write family at all** — the only write in the module happens inside the JSON round trip (`python-json-round-trip-write-open` and `-dump`) — so M-11.5 and M-11.15 add a capability rather than replacing one.

The pack already carries the syntax the generic path needs here: the `with-statement` and `bound-import-statement` productions, and the operations `python-open` (effect `open-file`, start symbol `call-expression`), `python-with-file` (effect `begin-with`, start symbol `with-statement`), and `python-bound-import` (effect `emit-import`). What is missing is a semantic requirement: there is no import, file-read, or file-write requirement, so the planner cannot reach those operations from facts.

#### M-11.2 Directory listing (1 input rule, 1 sequence rule)

| Family | Rules | Facts and captured values | Emitted fragments |
| --- | --- | --- | --- |
| **Directory file listing** | `recognize-directory-file-listing` (11 match tokens, no captures) | Marker fact `program.directory-file-listing`, nothing captured | `python-directory-file-listing` (1 rule): `import os` / `import sys` / `directory = sys.argv[1]` / `try:` / `    for entry in sorted(os.scandir(directory), key=lambda entry: entry.name):` / `        if entry.is_file():` / `            print(f"{entry.name}: {entry.stat().st_size} bytes")` / `except FileNotFoundError:` / `    print(f"Directory not found: {directory}")` |

Fixed: both imports, the `sys.argv[1]` argument source, `os.scandir`, the `sorted(..., key=lambda entry: entry.name)` policy, the `is_file` filter, the `"{name}: {size} bytes"` message, and the `FileNotFoundError` handler. Captured from the request: nothing. This is the last family whose *whole program* is stored, and it is what M-10.29 waits on: it needs an import requirement, a directory-iteration requirement over a scanned directory, a member access for `entry.name`/`entry.stat().st_size`, and a handler bound to the missing-directory failure.

#### M-11.3 Serialization and record families (8 + 20 input rules, 8 + 20 sequence rules)

| Family | Rules | Facts and captured values | Emitted fragments |
| --- | --- | --- | --- |
| **JSON round trip** | `capture-json-round-trip-record-default`, `capture-json-round-trip-record-name`, `capture-json-round-trip-file-default`, `capture-json-round-trip-file-name`, `recognize-json-round-trip-loads-back`, `recognize-json-round-trip-load-back` (plus the shared `recognize-json-round-trip-python` and `-generation-intent`) | `program.json-round-trip#records/path` (`records` variable name, `records.json` default path, or a captured name), `program.json-round-trip.confirmed`, and six readiness markers `program.json-round-trip.start`, `program.json.import`, `program.json.records`, `program.json.write-open`, `program.json.write`, `program.json.read-open`, `program.json.load` (each `#kind`) | `python-json-round-trip-open/import/records/write-open/dump/read-open/load/output`: `import json` / `records = [{"name": "Ada", "age": 36}, {"name": "Grace", "age": 85}]` / `with open("records.json", "w", encoding="utf-8") as output_file:` / `    json.dump(records, output_file, indent=2)` / `with open("records.json", "r", encoding="utf-8") as input_file:` / `    loaded_records = json.load(input_file)` / `print(loaded_records)` |
| **Single-field record sort** | `recognize-single-field-record-sorting`, `capture-single-field-sort-function-name`, `-records-name`, `-field-name`, the three `default-*` variants, `recognize-single-field-sort-case-insensitive(-spaced)`, `-descending`, `-reverse` | `program.single-field-record-sorting#name/records/field` (all three captured or defaulted), `.header/.signature/.body` and `program.sort` readiness markers, `.case-insensitive`, `.descending` | `single-field-record-sort-*` (10 rules): `def sort_records(records, field):` / `    return sorted(records, key=lambda record: record[field])` / `print(sort_records([{"name": "Ada", "age": 36}, …], "age"))`, with `casefold()` and `reverse=True` variants |
| **Multi-field record sort** | `recognize-multi-field-record-sorting`, `recognize-case-insensitive-multi-field-record-sorting` | Markers `program.multi-field-record-sorting` and `.case-insensitive`; the field list and sample records are literals | `python-multi-field-record-sorting`, `python-case-insensitive-multi-field-record-sorting` (2 rules): `return sorted(people, key=lambda record: tuple(record[field] for field in sort_keys))`, with the `casefold()` variant |
| **Grouped record aggregation** | `capture-grouped-record-aggregation`, `recognize-record-aggregation-calculates/computes/sums` | `program.record-aggregation#record/group/measure` (all three captured), `program.record-aggregation.intent`, plus the `record-aggregation-*` readiness markers | `record-aggregation-*` (7 rules): `records = [{…}]` / `totals = {}` / `for purchase in records:` / `    key = purchase["customer"]` / `    totals[key] = totals.get(key, 0) + purchase["spending"]` / `for key, total in totals.items():` / `    print(key, total)` |

Fixed across the serialization families: the module name `json`, the modes and `encoding="utf-8"`, the dump options (`indent=2`), the loaded variable name (`loaded_records`, or `loaded_<records>`), the sample record literals, the `record[field]` member form, `casefold()`, and the aggregation's `totals.get(key, 0)` accumulator. Captured from the request: record and file names for the JSON round trip, the function/records/field names for single-field sorting, and the record/group/measure names for aggregation.

Validation: `json-round-trip-records-name-is-identifier`, plus the three `record-aggregation-*-is-identifier` and three `single-field-record-sort-*-is-identifier` checks.

The pack already carries `import-statement`, `bound-import-statement`, `python-import-module`, `python-bound-import`, `python-json-dump-indented` (effect `write-expression-indented`), and the `with-statement` production. It does **not** carry `json.load`, a mode/encoding-qualified `open(...)`, `os.scandir`, or `sys.argv`, so M-11.12 and M-11.13 move those signatures into the pack rather than into shared Core.

#### Consequences for M-11.4 through M-11.21

The generic path needs five things these families assume implicitly: an **import requirement** (module name, so `import json`/`import os` are planned, not typed into a template), a **file-read requirement** (path, mode, encoding, and the bound content or line stream), a **file-write requirement** (path, mode, encoding, and the written value), a **directory-listing requirement** (directory expression and an entry collection whose items expose name/size), and a **failure requirement** for missing paths that binds a handler to the operation it guards. M-11.4/M-11.5/M-11.6/M-11.7 define those, and M-11.16 then retires `python-directory-file-listing` — closing M-10.29 — while M-11.18 removes the JSON and record families once their requirements render through the pack.

## M-11.4 through M-11.7, M-11.14, M-11.16, M-10.29 — outcome

The file and directory families now plan from facts:

| Ticket | Model | Pack knowledge | Replaced |
| --- | --- | --- | --- |
| M-11.4 | `FileReadRequirement(path, stream, body)` | `read-text-file` effect, `path`/`file` bindings, `bound-with-open-read-statement` | `recognize-non-empty-file-line-count`, `python-non-empty-file-line-count` |
| M-11.6 | `DirectoryListingRequirement(directory, item, sortKey, body)` | `bound-directory-iteration-statement`, `indented-` variant, `emit-directory-loop-header`, `directory`/`item` bindings | — (new capability) |
| M-11.7 | `TryRequirement` + `HandlerRequirement(errorType, body)` bound to the guarded listing | handler header spellings already present | — (new capability) |
| M-11.12 (partly) | `SemanticProgramArgumentExpression(position)` | `inputSyntax.programArgumentFormat` (`sys.argv[{0}]`) | hard-coded `sys.argv[1]` |
| M-11.12 (partly) | `ImportRequirement(module)` | `emit-import`, `bound-import-statement`, `module` binding | hard-coded `import os` / `import sys` |

Both directory rules (`recognize-directory-file-listing`, `python-directory-file-listing`) are deleted, so
the directory family is one input rule with twelve declared values feeding the same planner as every other
family. Current knowledge counts after M-11.4 and M-10.29: **96 sequence rules** (98 → 97 → 96) and
**241 input rules**; the pack now holds **95 productions**, **24 bindings**, and **97 operations**, plus
`inputSyntax.programArgumentFormat`.

Two model consequences are worth recording for the remaining serialization work:

- Statements may now sit at block depth 3 (`write-standard-output-triple-indented-expression`), because the
  guarded listing nests an output inside `for` inside `try`.
- Request-declared message text is carried as a `{0}`/`{1}` template in the fact, and the mapper turns it
  into text-template fragments, so no message wording lives in shared code.

## M-11.5 through M-11.21 — outcome

The serialization family is planned like every other family:

| Ticket | Model | Pack knowledge | Replaced |
| --- | --- | --- | --- |
| M-11.5 | `FileWriteRequirement(path, stream, body)` | `write-text-file`, `bound-write-with-open-statement` | — (new capability) |
| M-11.8–11.10 | `SerializeRequirement`, `DeserializeRequirement`, `SemanticListLiteralExpression`, `SemanticRecordLiteralExpression` | `indented-serialize-statement`, `indented-deserialize-statement`, `collectionLiteralFormat`, `recordLiteralFormat` | — (new capability) |
| M-11.11 | `TryRequirement` + `HandlerRequirement("json.JSONDecodeError", …)`, qualified error types spaced by the pack | `indented-bound-with-open-read-statement`, `indented-indented-deserialize-statement` | — (new capability) |
| M-11.18 | — | — | all 8 `python-json-round-trip-*` rules, 4 `capture-json-round-trip-*` rules, 2 `recognize-json-round-trip-*` readiness rules |

Two model facilities were added for this family:

- **Operations can declare the argument value they realize** (`GenerationOperation.MatchesArgumentValue`). A
  requirement names a format (`json`) and the pack decides which call spells it, so `json.dump` and
  `json.load` stay in knowledge while the planner stays language-neutral.
- **A request that holds a collection is planned, not refused.** The deferred-capability check now treats a
  collection-naming fact as carried when the requirements already hold a literal, iteration, filter, map, or
  sort, so "a list of dictionaries" no longer blocks the serializer.

The family's gate is knowledge-side: the three round-trip input rules only fire for a request that also reads
the data back, so an incomplete request produces no requirement at all and is rejected as before.

Current knowledge counts after M-13.14: **21 sequence rules** and **240 input rules**, with **30 validations**;
the pack holds **102 productions**, **26 bindings**, and **104 operations**, plus
`inputSyntax.programArgumentFormat`, the pack-level `recordLiteralFormat`, the sort and generator formats, and
the `exampleData` policy.

## M-12.1 — Blender gates in `TaskCodeGenerationResponder`

Production callers, in order:

1. `LatticeRuntimeSession.ProcessTaskRequest` calls `TaskRequestResponder.TryRespond` and maps `false` to
   `LatticeUtteranceResultKind.InformationUnavailable`.
2. `TaskRequestResponder.TryRespond` dispatches `TaskRequestAction.Code` to
   `TaskCodeGenerationResponder.TryGenerateCode`; no other caller exists in `src/`.
3. `TryGenerateCode` gates, in order: `request.Scope == "program-generation"`; the subject concept is an
   individual and `instance-of blender-codegen-code-generation-task`; the context concept is an individual
   and `instance-of blender-codegen-software-application`; the application has exactly one
   `blender-codegen-application-uses-language` relation whose object is `instance-of programming-language`;
   the task's `blender-codegen-has-generation-step` relations all resolve, each carries one parseable
   `blender-codegen-generation-step-order` value, and the orders are distinct; each step has exactly one
   effect and one argument kind of the matching `blender-codegen-generation-*` type; each binding resolves
   to exactly one `blender-codegen-generation-binding-value`; each `blender-codegen-step-references-api`
   object is `instance-of application-programming-interface`.

Fixed in shared code today: all nine `blender-codegen-*` type identifiers, all eight relation identifiers,
the one data property identifier, the `## {task}` / `### Script` / `### Steps` / `### Safety` message shape,
and the assumption that steps are ordered by a stored integer.

## M-12.2 — `ScriptCreationPattern` in `TaskRequestParser`

- Declared at `TaskRequestParser.cs:34` as
  `^\s*How would I create a script for (?<application>[A-Za-z][A-Za-z0-9 -]*?) that (?<task>.+?)\??\s*$`.
- Sole caller: `TaskRequestParser.TryParse` → `TryParseScriptCreationRequest`, which runs *after* the
  compositional task parser and before the hobby, brainstorm, concept, operation, learning, road-trip,
  writing, and recommendation patterns.
- It produces a `TaskRequestFormula(TaskRequestAction.Code, Subject: Normalize(task),
  Context: Normalize(application), OutputFormat: "source-code", Scope: "program-generation",
  Requirements: ["source-code"])`. The task phrase becomes the subject concept id verbatim, so the pattern
  claims nothing about whether the task is modelled — that decision is deferred to the responder.
- Parser tests: `Parses_a_Blender_object_replacement_script_request` (asserts action, subject, context,
  output format, scope, requirements), `Parses_an_unsupported_Blender_script_request_without_claiming_its_task_is_known`
  ("renames every object"), and `Parses_script_creation_requests_for_other_applications_without_task_routing`
  ("Maya … creates a cube") as the application-agnostic control.

## M-12.3 — Blender runtime dispatch and its dependent assertions

The Blender path is distinct in four ways: the concept-type gate (M-12.1), the stored step order, the
domain-specific relation names, and the message assembly that prints the ordered step labels and safety
notes. `knowledge/domains/blender-object-replacement-script.json` holds 118 concepts (109 individuals, 9
types), 241 relation assertions, and 18 data assertions; the 18 `blender-codegen-has-generation-step`
assertions already appear in step order 1–18, so the stored order is redundant with declaration order.

Assertions that depend on this path:

| Test | Depends on |
| --- | --- |
| `TaskRequestResponseTests.Generates_a_Blender_script_that_replaces_matching_objects_with_point_lights` | the task label heading, the three sections, every script line listed in M-12.1's bindings, `target_name = "YourObjectName"`, the ordering of the 18 steps, and non-empty evidence |
| `TaskRequestResponseTests.Does_not_generate_a_Blender_script_for_an_unmodeled_task` | the responder returning `false` when the subject concept has no generation steps, which must keep producing `InformationUnavailable` |
| `TaskRequestParserTests.Parses_a_Blender_object_replacement_script_request` and the two control tests | the parser contract only — unaffected by responder work |

## Consequences for M-12.4 through M-12.15

- **M-12.4/M-12.5/M-12.6** can rename the nine types, eight relations, and one data property to
  application-neutral identifiers in both `TaskCodeGenerationResponder` and the domain knowledge, and delete
  the `blender-codegen-*` gate, without touching any label the runtime test asserts. The gate becomes a
  capability check ("this subject is a code-generation task, this context is a software application, this
  application declares one implementation language"), which is what makes it not Blender-only.
- **M-12.10** can delete the step-order data property and all 18 assertions and order steps by the order of
  their `has-generation-step` assertions, byte-identically, because the two orders already agree.
- **M-12.7/M-12.8/M-12.9** need a knowledge-declared distinction between *API/structure* values (for example
  `list(bpy.data.objects)`, `collection.objects.link`) and *request-derived* values. Only `YourObjectName`
  is request-shaped today: it is the placeholder the request's "with a specific name" leaves open, so the
  request-derived slot must be declared in knowledge and filled from the task phrase, with the declared
  placeholder as the fallback.
- **M-12.11/M-12.12** split the file's concepts into the reusable domain/API facts (`blender-api` instances,
  object/scene vocabulary) and the generation scaffolding (`*-code-generation-task`, `*-step-*`,
  `*-binding-*`), so only the scaffolding can be deleted once the pipeline derives it.

## M-12.4 through M-12.15 — outcome

| Ticket | Outcome |
| --- | --- |
| M-12.4 | `TaskRequestAction.Code` has one entry point, `TaskProgramGenerationResponder.TryGenerateProgram`; the vehicle-procedure explanation moved out to `TaskOperationExplanationResponder` |
| M-12.5 | The entry point is application-neutral: the request names task and application, the domain knowledge supplies steps, effects, argument kinds, bindings, API references, and safety notes |
| M-12.6 | Nine `blender-codegen-*` types, eight relations, and one data property renamed to `code-generation-*` / `step-*` / `binding-*` vocabulary in both code and knowledge; the gate is now a capability check |
| M-12.7 | Blocked — needs early-continue loop, guarded-branch-with-fallback, and API-expression requirements (successors 12.7a–c) |
| M-12.8 | Blocked on 12.7c — the API relations are cited but cannot select the pack's generic `emit-*` operations |
| M-12.9 | Partly satisfied (no name or value in shared code); blocked for named objects, which needs slot-bearing lexemes (successors 12.9a–b) |
| M-12.10 | Step-order property and 18 assertions deleted; steps are planned in assertion order; data-assertion floors 62 → 57 |
| M-12.11 | No `blender-codegen-*` identifier remains; zero unreferenced concepts |
| M-12.12 | Nine `blender-api` individuals, the `blender` application, and all ten API references asserted by `BlenderScriptGenerationTests` |
| M-12.13 | Knowledge-only lines and the cited `step-has-binding` assertion prove the shared path |
| M-12.14 | Blocked on 12.9a/b |
| M-12.15 | Unmodelled task and unmodelled application both answer `InformationUnavailable` with no code fence |

What the section changed in principle: the code-generation route is now a *capability*, not an application.
Any knowledge module that declares a `code-generation-task` instance with `has-generation-step`,
`step-has-generation-effect`, `step-has-argument-kind`, `step-has-binding`, and `binding-has-value`
assertions, on a `software-application` instance with one `application-uses-language`, is answered by the same
entry point and the same language pack. The remaining Blender-specific work is about *what* the requirements
can express (M-12.7/M-12.8) and *how* a request phrase reaches a model (M-12.9/M-12.14), not about routing.

## M-13.1 through M-13.5 — classification of the Python sequence knowledge

Classified from the files themselves (`knowledge/sequences/python.core.json`). Every identifier below is
counted, not estimated.

### M-13.1 — emission rules (88)

All 88 are Python emission rules; the classification separates *reusable statement realization* from
*task-shaped fixed fragments*.

| Family | Rules | Emitted newlines | Multi-line fragments | Classification | Disposition |
| --- | ---: | ---: | ---: | --- | --- |
| `python-collection-*` | 20 | 13 | 0 | one-statement realization of filter/loop steps | retained — feeds blocked 9.22 |
| `python-addition-*` | 13 | 3 | 0 | fixed two-number solution | retained — blocked 8.9 |
| `python-dictionary-*` | 8 | 12 | 3 | fixed dictionary iteration/mapping | retained — blocked 9.24 |
| `single-field-*` | 8 | 7 | 2 | fixed record sorting | retained — blocked 9.25 |
| `python-output-*` | 7 | 2 | 0 | reusable `print` realization | reusable |
| `record-aggregation-*` | 7 | 6 | 1 | fixed grouped aggregation | retained — blocked 9.25 |
| `python-assignment-*` | 5 | 2 | 0 | reusable assignment realization | reusable |
| `python-range-*` | 5 | 2 | 0 | reusable range-loop realization | reusable |
| `python-sign-*` | 4 | 7 | 3 | fixed sign classification | retained — blocked 8.14 |
| `python-loop-*`, `python-manual-*` | 3 | 15 | 3 | fixed loop totals with literal collections | retained — blocked 9.23 |
| `python-even-*`, `python-odd-*` | 3 | 11 | 3 | fixed parity filters | retained — blocked 9.22 |
| `python-number-*`, `python-value-*`, `python-name-*` | 3 | 15 | 3 | fixed dictionary mappings | retained — blocked 9.24 |
| `python-multi-*`, `python-case-*` | 2 | 8 | 2 | fixed multi-field and case-insensitive sorting | retained — blocked 9.25 |

The 20 multi-line fragments are exactly the ids enumerated by the `SequenceKnowledgeGuardTests` exception
table, so any *new* complete-solution fragment fails the build (M-13.17).

### M-13.2 — input rules (238)

| Class | Rules | Examples |
| --- | ---: | --- |
| Feed generic requirement facts (reusable recognition) | 71 | `recognize-declared-file-line-count`, `recognize-declared-directory-listing`, `capture-declared-serialization-records-name`, the `program.requirement.*` facts, `language.python`, `intent.program-generation` |
| Legacy task facts with literal data | 29 | `capture-*-default` rules whose captures carry `literalValue` |
| Legacy task facts capturing from the request | 25 | `capture-*-name` rules whose captures read the phrase |
| Legacy task facts that are phrase markers | 113 | `recognize-*` rules with no captures, one fact per prompt shape |

Disposition: the 71 requirement-feeding rules stay (they are the migrated path); the 167 legacy rules stay
only while their families are blocked (8.9, 8.14, 9.22–9.25), except the four families already migrated in
M-10, M-11, and M-12, whose rules are already deleted.

### M-13.3 — retained and deleted rules

Deleted so far, with the generic replacement that took over:

| Deleted | Replaced by |
| --- | --- |
| `recognize-non-empty-file-line-count`, `python-non-empty-file-line-count` | `program.requirement.file-lines` → `FileReadRequirement` → pack `read-text-file` |
| `recognize-directory-file-listing`, `python-directory-file-listing` | `program.requirement.directory-listing` → `DirectoryListingRequirement` |
| 8 × `python-json-round-trip-*`, 4 × `capture-json-round-trip-*`, 2 × `recognize-json-round-trip-*` | `program.requirement.serialization-round-trip` → `FileWrite`/`FileRead`/`Serialize`/`Deserialize` |
| all `blender-codegen-*` identifiers (M-12.6) | `code-generation-*` vocabulary read by the shared entry point |

Retained with a blocking reason: every row of M-13.1 marked "retained — blocked", plus the 167 legacy input
rules that feed them.

### M-13.4 — validations (33)

All 33 are generic structural or language-neutral checks over one fact's values; the kinds are
`identifierValue` (17), `identifierCollection` (4), `uniqueCollection` (3), `ascendingIntegerRange` (2),
`integerValue` (2), `semanticCollection`, `arithmeticValue`, `disjointCollections`,
`arithmeticCollectionReferences`, `arithmeticValueReferences`, `matchingNumericFunctionCall` (one each). They
name no target language. One task-specific check, `json-round-trip-records-name-is-identifier`, referenced the
deleted `program.json-round-trip` fact and is removed here; the same rule is enforced for the migrated family
by `SemanticProgramValidator` (identifier names) and by the runtime rejection test for `invalid-name`.

### M-13.5 — encodings (1)

`quoted-string` is the only encoding, and it is a semantic value encoding (delimiters plus escape table), not
sample data or a task output. Retained as is.

## M-13.6 — Python-specific branches in shared code

Two sites reference a language name outside the packs:

| Site | What it does | Disposition |
| --- | --- | --- |
| `TaskRequestParser.cs:465` | adds the requirement `"python"` when a recommendation purpose mentions Python | recorded for M-14.10; it is recommendation routing, not program generation |
| `TaskRecommendationResponder.cs:26` | reads `Requirements.Contains("python")` | same |

`src/Lattice.Core/Generation` contains no language-specific parsing, validation, or realization branch: every
Python token lives in `knowledge/languages/python.core.json`.

## M-13.18 — reconciled counts

| File | Count |
| --- | --- |
| sequence rules | 88 |
| input rules | 238 |
| validations | 33 |
| encodings | 1 |
| pack productions | 100 |
| pack bindings | 26 |
| pack operations | 102 |
| pack syntax additions | `inputSyntax.programArgumentFormat`, pack-level `recordLiteralFormat`, `GenerationOperation.MatchesArgumentValue` |
| world-model data assertions | 57 (75 before M-12.10) |

## M-13.7 through M-13.19 — outcome

### M-13.7, M-13.8, M-13.9 — obsolete task fact routing

`SemanticProgramPlanner.DeferredProgramFactPrefixes` held twenty-two task-family prefixes, and ten of them named
facts no extraction rule can produce any more. Those ten are deleted:

`program.directory`, `program.file`, `program.for`, `program.import`, `program.json`, `program.list`,
`program.non-empty`, `program.try`, `program.while`, `program.with`.

The prefixes that remain each still gate a live family and now carry the owning ticket in a comment: 8.14
(`program.classification`, `program.assignment.classification`), 9.22 (`program.collection`),
9.23 (`program.loop`) and 9.24 (`program.dictionary`). `Keeps_deferred_program_prefixes_backed_by_knowledge`
fails if a prefix stops matching a producible fact, so the list can only shrink as families move.

File handling needs no marker at all now: line counts go through `FileReadRequirement` and directory listings
through `DirectoryListingRequirement`, while class programs are planned from `program.requirement.class`.

### M-13.10 — Blender

Completed with M-12.6 and recorded here for the section: no Blender task fact or phrase match survives, and
the domain/API knowledge is reused through `TaskProgramGenerationResponder` with neutral `code-generation-*`
identifiers.

### M-13.11, M-13.12, M-13.14 — fixed data and complete fragments

Twenty rules emitted a multi-line program when this section was written. Every fragment family has been replaced
one ticket at a time, and the exception table in `SequenceKnowledgeGuardTests` is now **empty**:

```
No sequence rule emits a multi-line program any more; a new one fails this guard.
```

The last row was the dictionary-iteration scaffold. It is planned from the request's own key, value, and
collection names, and the two things it needed were small and general: a `SemanticPlaceholderMappingExpression`
carrying the note its reader sees (the pack spells how a note is attached, `placeholderMappingFormat`
`{}  # {0}`), and `exampleData.emptyMappingNote` for the note's words, so the invented text stays policy. With the
six `python-dictionary-iteration-*` rules gone and `program.dictionary` dropped from the deferred prefixes, the
only prefix left is `program.loop`.

### M-13.13 — fixed function, class, and file outputs

No sequence rule emits a function, class, or file program: the retained 50 rules cover assignment, output,
arithmetic, range, filter, loop, dictionary, and sorting statements only, and no rule emits a multi-line
program at all. Function, class, and file programs are planned from requirements, and
`grep -riE '"(create|write|generate) a python' src/` returns nothing, so no prompt literal survives in a
generation module.

### M-13.15, M-13.16, M-13.17 — guards

New `tests/Lattice.Core.Tests/Generation/SequenceKnowledgeGuardTests.cs`, five tests:

| Guard | What it proves |
| --- | --- |
| `Keeps_benchmark_and_task_identities_out_of_knowledge` | no `py-###` identifier and no `benchmark` token in the knowledge; every fact id stays in a generic namespace |
| `Keeps_complete_solution_fragments_confined_to_recorded_families` | the set of rules emitting two or more lines equals the single pinned id |
| `Keeps_every_rule_and_validation_reachable_from_extraction` | no rule and no validation names a fact that extraction cannot produce |
| `Keeps_deferred_program_prefixes_backed_by_knowledge` | no dead entry in the planner's deferred list |
| `Generates_different_programs_when_the_request_values_change` / `…_names_change` | changing a value or a name changes the generated program, so a hard-coded answer fails |

The first three read the embedded knowledge file through its manifest resource, so they guard the data, not a
copy of it.

### M-13.19 — retained knowledge, removed shortcuts

Retained as reusable: the input rules that feed generic requirement facts, the 30 language-neutral
validations, the `quoted-string` encoding, the one guarded deferred prefix, and every pack production,
binding, and operation. Removed: four migrated families (file lines, directory listing, serialization round
trip, Blender scripts) with their replacement operations, one dead validation, and ten dead deferred prefixes.
Blocked remainder: the single fixed fragment listed above, tracked by the open 9.24 ticket.

## M-9.23 — loop totals from request-supplied collections

Two of the three fixed loop-total fragments are deleted. A request that states its own data —
"starts total at 0 and adds each number in [1, 2, 3] to total, then prints total" and "initializes result to 0
and adds each value in [-2, 4.5, 1.5] to result, then displays result" — is now read by eight new input rules
into one `program.accumulation` fact:

| Value id | Captured from | Example |
| --- | --- | --- |
| `accumulator` | the declared total after `starts` / `initializes` | `total`, `result` |
| `initial` | the value after `at` / `to` | `0` |
| `item` | the item after `each`, before `in` | `number`, `value` |
| `collection` | the bracketed list after `in`, as a semantic collection | `[1, 2, 3]`, `[-2, 4.5, 1.5]` |

Every capture rule is scoped with `requiredAllSequences` on the accumulation phrase, so a request that merely
mentions "starts" or "each" cannot create a partial fact. `SemanticProgramFactMapper.TryProjectAccumulations`
projects the fact onto one `AccumulationRequirement` whose source is a `SemanticListLiteralExpression` of the
captured items, plus the reporting output when the request asks for one and supplies no typed output
expression. Signed items become a prefix negation (`SemanticUnaryExpression(Negative, 2)`) because a canonical
number literal is non-negative; that rule is now covered by
`SemanticProgramRequirementTests.Accepts_a_list_literal_source_with_signed_and_decimal_items`.

Deleted: `python-loop-accumulator-total`, `python-loop-accumulator-result`, `recognize-loop-accumulator-total`,
`recognize-loop-accumulator-result`. The generated programs are byte-identical, and the guard exception table
dropped from twenty entries to eighteen.

Still blocked: `python-manual-list-total` — the request names no collection at all, so it needs the shared
example-data policy that 8.9 and 8.14 also wait on.

## M-9.23 — completed: loop totals with request or example data

The third fragment, `python-manual-list-total`, needed one thing the request does not supply: the numbers. That
value now comes from a single declared policy rather than from a task-shaped rule.

### The example-data policy

`GenerationLanguagePack.ExampleData` (new, optional) declares:

```json
"exampleData": { "numbers": ["1", "2", "3", "4", "5"], "numberItemName": "number", "accumulatorInitialValue": "0" }
```

It is the only invented data in the pipeline, and it is constrained on three sides:

| Constraint | Where it is enforced |
| --- | --- |
| It is never task-shaped — keyed by value kind only, never by a request, fact id, or benchmark id | the pack field has no task dimension; `Keeps_benchmark_and_task_identities_out_of_knowledge` still guards the knowledge file |
| Request data always wins | `TryProjectAccumulations` reads the captured collection first and only falls back when it is missing; `Keeps_request_supplied_collections_ahead_of_the_example_data` plans `[4, 9]` while the policy holds `[1, 2, 3, 4, 5]` |
| It is validated, not trusted | `GenerationLanguagePackLoader.IsValidExampleData` requires non-empty canonical numbers, an identifier item name, and a numeric initial value |

Three new input rules read the manual-total request: `recognize-manual-list-accumulation`,
`capture-manual-list-accumulator` (the total after "their"), and `capture-manual-list-output` (the same total as
a typed output expression). All three are scoped with `requiredAllSequences: [["list","of","numbers"]]`.

Two general fixes came out of it:

- `SemanticProgramPlanner.CarriesCollection` now recognises `AccumulationRequirement`, so a request that names a
  list is planned rather than deferred by the `program.collection` marker.
- `recognize-function-requirement` no longer reads "the built-in sum function" as a function declaration
  (`forbiddenAnySequences: [["built"], ["built-in"]]`).

Outcome: `python-manual-list-total` and `recognize-manual-list-total-request` are deleted, the program is
byte-identical (`total = 0` / `for number in [1, 2, 3, 4, 5]:` / `    total = total + number` / `print(total)`),
and the guard exception table is down to seventeen entries.

## M-8.9 — numbered number variables from the request and the example data

The last of the fixed addition fragments is gone. A request such as "stores two numbers in variables, adds
them, and prints the result" names no operands and no variable names, so it needs both the quantity the request
does state and the values it does not. The split is: the request supplies the count, the shape (`in variables`)
and the name of the reported result; the pack's `exampleData` supplies the values and the ordinal names.

`GenerationExampleData` grew two optional fields, validated by the same `IsValidExampleData` as the rest of the
policy:

| Field | Meaning | Python pack |
| --- | --- | --- |
| `numberVariableValues` | the values assigned to numbered variables, in order | `["10", "20", "30"]` |
| `ordinals` | the ordinal words that name them, joined to `numberItemName` | `["first", "second", "third"]` |

Four new input rules read the request into one `program.declaration.numbers` fact:

| Rule | Reads | Example |
| --- | --- | --- |
| `recognize-number-variable-declarations` | the declaration phrase | `stores numbers in variables` |
| `capture-number-variable-count` | the quantity after `stores`, before `numbers` | `two` |
| `capture-number-variable-sum-name` | the reported name after `prints` / `the` | `result` |
| `capture-number-variable-output` | the same name as a typed output expression | `result` |

All four are scoped with `requiredAllSequences: [["in", "variables"]]`, so no other request can enter the
family. `SemanticProgramFactMapper.TryProjectNumberDeclarations` turns the fact into one `AssignmentRequirement`
per variable — named `{ordinal}_{itemName}`, so `first_number`, `second_number` — followed by the sum
assignment, a left-associated chain of `SemanticNameExpression` operands. `TryParseQuantity` accepts a digit or
the words one through ten.

Deleted: thirteen `python-addition-*` rules and fourteen `*addition*` input rules, one stale `forbiddenFacts`
entry and the three `program.assignment.first/second/result` entries in
`SemanticProgramPlanner.DeferredProgramFactPrefixes`. The program is byte-identical:

```
first_number = 10
second_number = 20
result = first_number + second_number
print(result)
```

The count is not special-cased either: asking for three numbers plans `first_number`, `second_number`,
`third_number` and `result = first_number + second_number + third_number`, which
`SemanticProgramGenerationTests` asserts alongside the two-number request. No golden text changed.

## M-8.14 — sign classification as a reusable requirement

The fixed classification program (`number = -5` followed by three branches printing `Positive`, `Negative`, and
`Zero`) is now planned. The gap was not the value — that is example data — but the shape: a request that states
three conditions at once, which the scalar mapper could not express because it aligns one condition per request.

`SignClassificationRequirement(VariableName, Value, PositiveLabel, NegativeLabel, ZeroLabel)` says what a sign
classification is without naming a language: one value, three labels, and boundaries that belong to the kind
because "sign" means the comparison against zero. `SemanticProgramPlanner.ExpandSignClassification` realises it
with the operations that already existed:

| Statement | Built from |
| --- | --- |
| `number = -5` | `AssignmentRequirement` of the example-data integer |
| `if number > 0:` / `print("Positive")` | `ConditionalRequirement` comparing the name with the definitional zero |
| `if number < 0:` / `if number == 0:` | the same construction with `LessThan` and `Equal` |

The request always states its labels ("positive, negative, or zero"), so they are captured rather than invented:
three capture rules read the word between the separators, and the planner reports each as a capitalised display
label (the request's own word, presented the way the fragment did). `GenerationExampleData.IntegerValue` is the
one invented value and is validated by `IsValidExampleData` like the rest of the policy.

Two general fixes came out of it:

- `SemanticProgramValidator` accepts the new kind and requires three non-empty single-line labels, so an
  incomplete capture fails at the projection boundary instead of rendering.
- The sign rules are scoped to the classification phrase *and* to the three label words, because
  "converts text to an integer and returns a clear result" also contains `integer` followed by `is`.

Deleted: `python-sign-classification-assignment`, `-positive`, `-negative`, `-zero`, twelve `*classification*`
input rules, and the two `program.classification` deferred prefixes. The guard exception table drops from
seventeen to fourteen entries. Golden text unchanged.

## M-9.22 — parity filters from request data

The three parity programs are planned. Two of them always named their data; the third never did:

| Request | Data source | Program |
| --- | --- | --- |
| "prints each number in [1, 2, 3, 4, 5, 6] only if it is even" | request | `for number in [1, 2, 3, 4, 5, 6]:` + `if number % 2 == 0:` + `print(number)` |
| "displays each value from [-3, -2, -1, 0, 1, 2, 3] only if it is odd" | request | the same shape with `!= 0` |
| "prints only the even numbers from a list" | example data | the same shape over `[1, 2, 3, 4, 5]` |

New input rules read the family into one `program.parity-filter` fact:

| Rule | Reads |
| --- | --- |
| `capture-parity-filter-predicate` | the word after `only if it is` (`even`, `odd`) |
| `capture-parity-filter-item-in-list` / `-from-list` | the item before `in` / `from` and the bracketed list after it, stopping before `only if` |
| `recognize-even-numbers-from-a-list` / `-odd-numbers-…` | the unnamed form, whose parity word is the word the match already spells |

`SemanticProgramFactMapper.TryCreateParityFilter` projects one `IterationRequirement` whose body is a
`ConditionalRequirement` over `item % 2 == 0` (or `!= 0`) printing the item. The remainder comparison is the
definition of parity, so it belongs to the projection; the item name, the collection, and the parity word come
from the request, and a request that names no list falls back to the single example-data policy.

**One golden text changed.** The unnamed request no longer iterates the fragment's six numbers, so its output is
`2\n4` instead of `2\n4\n6`. The request names no data at all, so those values are invented, and the migration's
rule is that invented values live in one policy — here `exampleData.numbers` — rather than in a fragment. Every
request that states its own data is byte-identical.

Deleted: `python-even-list-filter`, `python-even-number-list-parity-filter`,
`python-odd-value-list-parity-filter`, `recognize-even-list-filter-request`,
`recognize-even-number-list-parity-filter`, `recognize-odd-value-list-parity-filter`, one stale
`forbiddenFacts` entry, and the dead `program.even` deferred prefix. The exception table drops from fourteen
entries to eleven. The function-shaped `python-collection-filter-*` family still needs parameter, return, and
sample-call realisation, so `program.collection` keeps its deferred prefix.

## M-9.24a — declared dictionaries from request entries

Both dictionary-iteration programs are planned. The request states the data:

```
Build a Python script that stores {'Ada': 3, 'Grace': 5, 'Linus': 6} as a dictionary named counts,
then displays each name and its count.
```

```
counts = {'Ada': 3, 'Grace': 5, 'Linus': 6}
for name, count in counts.items():
    print(name, count)
```

Five input rules read it into `program.declaration.dictionary`: the declared name after `named`, the keys and the
values as ordered collections split on the request's own `,` entry separator and `:` field separator, and the two
loop names from `each … and its …`. All five search every sentence, because a request that states decimal values
splits into two sentences at the first decimal point — that was the reason the `prices` twin produced no name.

Two model gaps had to close first:

| Addition | Why |
| --- | --- |
| `SemanticMapLiteralExpression` + pack `mapLiteralFormat` (`'{0}': {1}`) | a Python dictionary is not a record literal: its keys are quoted with single quotes and it is not JSON |
| `SemanticArgumentsExpression` | `print(product, price)` reports two values, and one output requirement carries one expression |

`TryCreateDictionaryIteration` then projects the assignment and a `DictionaryIterationRequirement` whose body
reports both names. Mapping literals also count as carried collections in `CarriesCollection`, and the fact id is
`program.declaration.dictionary` rather than `program.dictionary…` so the mapping family keeps its deferred gate.

Deleted: `python-dictionary-product-iteration`, `python-dictionary-name-count-iteration`,
`recognize-dictionary-product-iteration`, `recognize-dictionary-name-count-iteration`, and two rows of the
exception table (eleven → nine). Both programs are byte-identical.

## M-9.24b — dictionary mappings from request expressions

The three mapping programs are planned. Each request states what the value is and what the mapping consumes:

```
Create a Python program that, from a list of numbers, builds a dictionary named squares that maps each number
to the square of it.
```

```
def map_values(numbers):
    squares = {number: number * number for number in numbers}
    return squares
print(map_values([1, 2, 3]))
```

Five input rules read `program.mapping.dictionary`: a `kind` (`length`, `square`, or `offset`), the loop item
between `each` and `to`, the parameter after the request's own `list of …`, the offset after `+` when the value
is `item + n`, and the declared result name when the request names it. The value itself is not captured as a
fragment of English: the projection builds it from the kind, so `number * number`, `len(name)`, and `value + 10`
are three requirement shapes rather than three benchmark answers.

The sample call is now a declared policy, `GenerationSampleCallPolicy` inside `GenerationExampleData`.

| Field | Value | Why it is policy, not knowledge |
| --- | --- | --- |
| `functionName` | `map_values` | the fragments' function name; the request never states one |
| `resultName` | `mapped_values` | the default result name; a request that says `named squares` overrides it |
| `numbers` | `1`, `2`, `3` | the demonstrated numeric arguments |
| `texts` | `Ada`, `Grace`, `Linus` | the demonstrated text arguments, the same names the rest of the policy already uses |

`TryCreateDictionaryMapping` projects a `FunctionRequirement` whose body is a `DictionaryMapRequirement` (the key
is the loop item and the value must reference it) plus a `ReturnRequirement`, then the `OutputRequirement` that
calls the function with the policy's literals. Two model gaps closed on the way: `SemanticTextLengthExpression`
with a pack `textLengthFormat` (`len({0})`), and the dictionary-comprehension productions
`bound-dictionary-map-assignment` / `indented-bound-dictionary-map-assignment` with their two operations.

Deleted: `python-number-square-dictionary-mapping`, `python-value-offset-dictionary-mapping`,
`python-name-length-dictionary-mapping`, their three marker input rules, and three rows of the exception table
(nine → six). All three programs are byte-identical, including the `squares` result name the request states.

## M-9.25a — single-field record sorts from a subscript and a declared sample

All four single-field sort programs are planned. The request states the names and the ordering; the policy states
the data:

```
Create a Python program that defines a function named order_records that accepts a list of records named people
and a field name named sort_key, then returns the records sorted by that field.
```

```
def order_records(people, sort_key):
    return sorted(people, key=lambda record: record[sort_key])
print(order_records([{"name": "Ada", "age": 36}, {"name": "Grace", "age": 85}, {"name": "Linus", "age": 55}], "age"))
```

Three expressions closed the modelling gap:

| Addition | Pack format | Why a fragment was not enough |
| --- | --- | --- |
| `SemanticSubscriptExpression` | `subscriptFormat` `{0}[{1}]` | the field is a parameter, so the key is not known until planning |
| `SemanticCaseFoldExpression` | `caseFoldFormat` `{0}.casefold()` | case-insensitive ordering is a value transformation, not a variant rule |
| `SemanticSortExpression` | `sortedExpressionFormat` / `sortedExpressionReverseFormat` | the ordering is an expression the function returns, with the descending form selected by the request |

Eight input rules read `program.sort.records`: the kind marker, the function name, the records parameter, the
field parameter, the descending word pair (`descending order` / `reverse order`), and both spellings of
case-insensitivity. Every one of them excludes `those fields` and `list of field names`, which is how the
multi-field family keeps its own facts.

The three `identifierValue` validations that used to guard the function, records, and field names are deleted
rather than moved: a request that names none of them takes them from the policy, and a rule-based validation
cannot see a policy default. The pack's identifier rules still refuse a name no language can spell — the plan is
refused, which the knowledge test now asserts.

`GenerationSampleCallPolicy.Sort` declares the invented data once — the default names, the lambda item `record`,
the record shape (`name` text, `age` number), and one sample per sensitivity:

| Sample | Sorts by | Literal style | Records |
| --- | --- | --- | --- |
| case-sensitive | `age` | `record` | `Ada`/`Grace`/`Linus` with ages 36, 85, 55 |
| case-insensitive | `name` | `map` | `ada`, `BOB`, `Grace` with ages 36, 42, 85 |

`mapLiteralFormat` gained `textFormat` (`'{0}'`), so a dict-style literal spells its own text values and the
case-insensitive sample stays byte-identical.

Deleted: eight `single-field-record-sort-*` rules (header, signature, four bodies, two outputs), twelve
`single-field*` input rules, and two exception-table rows (six → four).

**One deliberate output change, recorded:** the case-insensitive sample's *field argument* is now `"name"`
instead of `'name'`. The records keep their single-quoted dict style, while a call argument takes the language's
canonical string literal; every other byte of all four programs is unchanged, and the executed output is
identical.

## M-9.25c — grouped record totals from an element update

Both grouped-total programs are planned, byte-identically. The request names the loop item and the two fields;
the policy names the dictionary and the names the totals loop reports:

```
Write a small Python program that groups a list of purchase records by customer and calculates each customer's total spending.
```

```python
records = [{"customer": "Ada", "spending": 25.0}, {"customer": "Grace", "spending": 40.0}, {"customer": "Ada", "spending": 15.0}]
totals = {}
for purchase in records:
    key = purchase["customer"]
    totals[key] = totals.get(key, 0) + purchase["spending"]
for key, total in totals.items():
    print(key, total)
```

The plan is composed from requirements, not from a template: a data assignment, an empty mapping, one loop over
the records, one key/value loop over the totals, and an output of the pair. Two model gaps closed:

| Addition | Where | Why a fragment was not enough |
| --- | --- | --- |
| `ElementAssignmentRequirement` | requirement, validator, planner | raising one element by key is an update of the collection the program already holds, not a new binding; the pack's existing `{target} = {expression}` productions spell it |
| an empty mapping literal | `SemanticExpressionAnalysis.IsValidMap` | a mapping a program fills in starts with no entries, and the pack's delimiters already spell `{}` |

`GenerationSampleCallPolicy.GroupedAggregation` declares the invented data: the names (`records`, `totals`,
`key`, `total`) and one group/amount pair per record (`Ada` 25.0, `Grace` 40.0, `Ada` 15.0). The loop item and
the two field names are request data, and the accumulator's starting `0` is the policy's existing
`accumulatorInitialValue`, so the update is arithmetic over the two captured fields rather than a fixed
`totals.get(key, 0) + purchase["spending"]` recipe.

Deleted: the seven `record-aggregation-*` rules, the `record-aggregation-output` exception-table row
(two → one), and the dead `program.record` deferred prefix. Both programs are byte-identical and the executed
outputs are unchanged.

## M-13.8 — function-shaped collection filters from a comparison

All twelve filter programs the runtime suite exercises are planned byte-identically, including the
average-threshold, arithmetic-threshold, and negated-comparison variants:

```
Write a Python function that receives a list of numbers and returns a new list containing only values greater than 2.
```

```python
def filter_values(values):
    return [value for value in values if value > 2]
print(filter_values([1, 2, 3, 4, 5]))
```

The request already supplied every part of the predicate in declarative form — the signature (`name`, `parameter`,
`item`), the comparison operator parsed into `ComparisonOperators`, and the threshold parsed into `Expressions` —
so only the rendering was missing. Three expressions closed it:

| Addition | Pack format | Why a fragment was not enough |
| --- | --- | --- |
| `SemanticFilterExpression` | `filterExpressionFormat` `[{0} for {1} in {2} if {3}]` | the filtered collection is *returned* by the function, not bound to a name first |
| `SemanticTotalExpression` | `totalExpressionFormat` `sum({0})` | the average variant's numerator totals the parameter |
| `SemanticLengthExpression` (renamed from `SemanticTextLengthExpression`) | `lengthFormat` `len({0})` | one count serves a text's characters and a collection's entries, so the expression stops claiming to be about text |

`TryCreateCollectionFilter` composes the function from those parts: the predicate compares the item against either
the request's own threshold expression or `sum(parameter) / len(parameter)` when the request says *average*. The
demonstration call reports the declared `exampleData.numbers`, so no collection literal is left in the knowledge
file.

Deleted: the twenty `python-collection-filter-*` rules and the `program.collection` deferred prefix. The 31 input
rules stay — they are the declarative representation now, not a phrase template.

## The fragments the declared sample call unblocks

The policy is declared (M-9.24b) and the sort and grouped-total families consume it (M-9.25a–c), so what is left
is the requirement the remaining families need. One exception-table row remains — the dictionary scaffold — and
it is a function whose body is generic over its parameters and whose last line prints
one concrete call, for example
`print(sort_records([{…}], "age"))`. That call is no longer the missing piece: `GenerationSampleCallPolicy`
inside `GenerationExampleData` supplies the function name, the result name, and both argument lists once for
every family, never per fragment.

What is still missing is modelling — a subscript expression over the loop item, and a grouped accumulation:

| Ticket | Fragments | Needs |
| --- | --- | --- |
| ~~9.24b~~ | ~~three dictionary mappings~~ | done: `GenerationSampleCallPolicy` and `DictionaryMapRequirement` |
| ~~9.25a~~ | ~~four single-field record sorts~~ | done: `SemanticSubscriptExpression`, `SemanticCaseFoldExpression`, `SemanticSortExpression` |
| ~~9.25b~~ | ~~two multi-field record sorts~~ | done: `SemanticGeneratorExpression`, `SemanticTupleExpression` |
| ~~9.25c~~ | ~~one grouped aggregation~~ | done: `ElementAssignmentRequirement` and an empty mapping literal |

Doing them in that order keeps every replacement separate from its deletion, and each deletion drops the
exception table by exactly the rows it owns.

## M-14 — the second-language proof

The generator is proved language-neutral by realizing one interpretation in two languages. A JavaScript pack
(`knowledge/languages/javascript.core.json`) joins the Python one, and a request that names either language is
planned by the same mapper, validator, and planner; only the selected pack differs.

Three fixtures cover the shapes the proof needs — a statement with its output, a returned literal over a
parameter, and a block with a nested body — and each is worded once, interpreted once, and realized twice:

| # | Request (interpreted once) | Python | JavaScript |
| --- | --- | --- | --- |
| 1 | `Create a Python program that sets total to 7 then print total.` | `total = 7` / `print(total)` | `let total = 7` / `console.log(total)` |
| 2 | `Build a Python function named keep_large that accepts a list of numbers named samples and returns a new list containing only sample values more than 4.` | `def keep_large(samples):` / `    return [sample for sample in samples if sample > 4]` / `print(keep_large([1, 2, 3, 4, 5]))` | `function keep_large(samples) {` / `  return samples.reduce((kept, sample) => sample > 4 ? [...kept, sample] : kept, [])` / `}` / `console.log(keep_large([1, 2, 3, 4, 5]))` |
| 3 | `Write a Python program that fulfills this request: Given a dictionary of product names and prices, print each product with its price.` | `items = {}  # Add dictionary entries here.` / `for product, price in items.items():` / `    print(product, price)` | `let items = {};  // Add dictionary entries here.` / `for (const [product, price] of Object.entries(items)) {` / `  console.log(product, price)` / `}` |

The Python bytes are unchanged by this section: the three programs above are the same text the runtime suite
already asserted. The JavaScript text is new output, not a changed one.

### What the second pack declares

| Part | Python | JavaScript |
| --- | --- | --- |
| productions | 102 | 16 |
| bindings | 26 | 9 |
| operations | 104 | 14 |

The JavaScript pack declares only what the fixtures reach: output and assignment (two depths each), function
headers, indented return, dictionary loop headers, and the four block closes, over the syntax, literal, and
expression formats those statements need. It declares no interpolation, sort, generator, tuple, subscript, or
case-fold format, and no file or standard-input operations, because no fixture asks for one — a request that did
would fail with the pack's own "cannot render" error rather than with invented syntax.

### Two additions the brace language forced

* **Block closing is pack data.** A language whose blocks end by indentation declares nothing and the planner
  plans nothing; a brace language declares `close-block`, `close-indented-block`,
  `close-double-indented-block`, and `close-triple-indented-block`, and the planner asks for the effect matching
  the depth of the block it just finished. The decision is the pack's, taken from whether it declares the
  operation at all.
* **The alternative-branch effects are neutral.** `begin-elif`, `begin-elif-indented`, and
  `begin-elif-double-indented` are now `begin-alternative-branch`, `…-indented`, and `…-double-indented`, so no
  language's keyword survives in the planner's vocabulary. Renaming them changed three operation effects in the
  Python pack and no output.

### Constraints the loader imposed on a second pack

* Every declared binding must be used by the grammar and every grammar binding must be declared; the
  JavaScript pack therefore declares nine bindings, not the Python pack's twenty-six.
* A grammar symbol is a `terminal`, a `nonterminal`, or a binding — `statement` and `expression-call` are
  nonterminals, and pointing one at an undeclared head fails the whole knowledge base.
* `filterExpressionFormat` must carry all four placeholders (`{0}` the kept value, `{1}` the item, `{2}` the
  source, `{3}` the predicate). The Python comprehension uses all four naturally; the JavaScript reduction spells
  the kept value as the array it accumulates into.

### Language selection is a fact

`recognize-javascript-language` (`matchTokens: ["javascript"]`, `factId: "language.javascript"`) joins
`recognize-python-language`, and `GenerationLanguagePackSelector` maps the single `language.` fact onto the pack
that declares that language. `GenerationLanguages` exposes the declared names, and the recommendation parser,
which used to spell `"python"`, now recognises any declared language instead.

### The guards

| Test | What it holds |
| --- | --- |
| `CrossLanguageGenerationTests.Realizes_the_same_interpretation_in_each_language` | three fixtures, each planned twice from one fact set; the programs match their expected bytes and their semantic effects match in order |
| `CrossLanguageGenerationTests.Selects_the_requested_language_pack` | the same request worded for JavaScript reports exactly `language.javascript` and emits the JavaScript program through the same entry point |
| `CrossLanguageExecutionTests.Reports_the_same_result_in_each_language` | `python3` and `node` run the two programs and report the same value (`7`, `[5]`, and nothing for the empty dictionary) |
| `LanguageNeutralityGuardTests.The_generation_kernel_names_no_language_and_spells_no_language_syntax` | no language name and no language spelling in non-comment code under `src/Lattice.Core/Generation` |
| `LanguageNeutralityGuardTests.Shared_code_names_a_language_only_where_the_history_records_it` | the only shared file that names a language is `TaskRecommendationResponder.cs`, the software recommendation catalogue over the domain knowledge's `python-package` concepts |

Current counts after M-14: **21 sequence rules**, **241 input rules** (one added: the JavaScript language
recogniser), **30 validations**, **1 encoding**; two language packs — Python at 102 productions / 26 bindings /
104 operations, JavaScript at 16 / 9 / 14.
