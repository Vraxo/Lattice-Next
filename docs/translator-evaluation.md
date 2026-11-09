# Optional translator backend evaluation

## Decision

Do not select or integrate a general-purpose backend yet.

The first concrete grammar-based candidate for a runtime experiment is **Attempto Parsing Engine (APE)**. It is a deterministic parser for Attempto Controlled English (ACE), produces a Discourse Representation Structure (DRS), and exposes command-line and service interfaces. It is not an unrestricted-English parser: callers must write ACE, and its semantic output must still be mapped into Lattice's smaller semantic model.

**Grammatical Framework (GF)** is a credible alternative if Lattice decides to author and maintain its own controlled English grammar. GF is a grammar-development framework, not a ready-made English semantic parser. Choosing it means implementing a grammar and abstract semantic syntax rather than installing a backend and immediately translating ordinary sentences.

Native LCE remains the default. The next experiment should run the benchmark corpus against an actual APE executable, map only the representable DRS subset, and reject all other output. No backend is selected by this evaluation.

## Candidate comparison

| Candidate | What it provides | Strengths | Costs and limitations | Assessment |
|---|---|---|---|---|
| Native LCE parser | Lattice's own deterministic controlled grammar | Exact semantic contract, no runtime dependency, fully tested and conservative | Narrow grammar; users must learn LCE | Keep as default and baseline |
| Attempto Parsing Engine (APE) | Deterministic ACE parser with DRS and other output formats | Mature controlled-language grammar; quantification, negation, anaphora, and richer sentence structures; command-line, socket, HTTP, Prolog, and Java interfaces | Requires SWI-Prolog; ACE is controlled English, not unrestricted English; DRS-to-Lattice mapping is a separate adapter; upstream release history is old | Best first external runtime experiment |
| Grammatical Framework (GF) | Typed grammar formalism, compiler, parser and runtime | Designed for explicit grammars and semantic abstract syntax; supports embedding in multiple host environments and multilingual grammars | Not a drop-in English parser; Lattice would need to author and maintain the grammar, lexicon, and semantic mapping | Revisit if Lattice wants to own a larger controlled grammar |

Sources:
- [Attempto/APE repository and interface documentation](https://github.com/Attempto/APE)
- [SWI-Prolog APE package listing](https://www.swi-prolog.org/pack/list?p=ape)
- [Attempto project downloads](https://attempto.ifi.uzh.ch/site/main.php?text=downloads%2Fdownloads.html)
- [Grammatical Framework](https://www.grammaticalframework.org/)

APE's upstream README describes SWI-Prolog as a prerequisite and documents command-line, socket, and HTTP interfaces. The SWI-Prolog pack index lists version 6.7.180715, while the GitHub release page lists 6.7-131003 as its latest release. This makes deployment and maintenance worth testing before adoption; it does not by itself establish that the parser is unusable.

GF's official site describes it as a programming language and development platform for grammar applications. It reports GF 3.12 in August 2025 and documents multiple compilation/embedding targets. Its maintenance is encouraging, but it is a grammar-authoring project, not a ready-made semantic parser that can be evaluated without first writing a grammar.

## Benchmark corpus

The machine-readable cases are in [translator-benchmark.json](../tests/Lattice.Core.Tests/Intelligence/translator-benchmark.json). They cover unary facts and rules, existential quantification, fixed-object relational rules, ground relations, unresolved pronouns, antecedent and lexical ambiguity, quantifier scope, negation, relational premises, and higher-arity relations.

The expected outcome is relative to Lattice's current semantic contract, not to the full expressive power of ACE or GF. A backend may parse a sentence correctly and still produce a formula that Lattice must reject as unsupported. That is a successful boundary decision, not a parser failure.

### Acceptance rules

1. Never erase negation, quantifier scope, argument roles, or anaphoric uncertainty to force a formula into the current model.
2. Return multiple valid candidates when the backend exposes distinct supported readings; do not select one based on likelihood alone.
3. Preserve unresolved references for conversation-context resolution. A translator must not guess an antecedent.
4. Reject formulas containing unsupported negation, variable-object quantification, relational premises, joins, or higher-arity relations until Lattice implements those structures.
5. Retain original input and raw backend output in adapter diagnostics. Only validated candidates receive `SemanticUtterance.SourceText`.
6. Distinguish parser errors, unknown vocabulary, and unsupported semantic output where the backend exposes those states.

## Recommended experiment

Build an optional APE adapter outside Core, invoked through the same external-provider boundary used for other processes. It should:

1. Run a pinned APE distribution and report its version at startup.
2. Request machine-readable DRS output, not display-oriented HTML.
3. Convert only a deliberately small, documented DRS subset to Lattice formulas.
4. Return no candidates for parse failures, multiple candidates only for genuine supported ambiguity, and an unsupported result for formulas outside Lattice's model.
5. Run every benchmark case and report exact match, ambiguity handling, unsupported-but-correctly-rejected, and incorrect/unsafe conversion separately.
6. Remain optional; absence of SWI-Prolog or APE must not affect Core, native LCE, or symbolic reasoning.

Do not add negation or relational joins merely to make APE integration easier. Semantic expansion needs a separate reasoning feature and tests.

## Scope not evaluated here

No LLM, hosted model, or local neural model was selected or benchmarked. This evaluation is intentionally about deterministic grammar-based options and preserves the project's symbolic-first direction. A neural translator would require a separate decision, explicit opt-in, and the same strict candidate validation and review boundary.