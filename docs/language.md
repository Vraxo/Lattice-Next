# Language strategy and LCE 0.9

## Decision

The language began as **Lattice Controlled English (LCE) 0.5**, a small, explicitly specified controlled-language subset inspired by the design principles of Attempto Controlled English (ACE). The current supported grammar is LCE 0.9.

LCE 0.9 retains unary assertions, universal rules, relational assertions, definite descriptions, and multi-sentence turns. It adds explicit existential assertions and existence questions for indefinite descriptions. An indefinite assertion introduces an unnamed witness whose descriptive conditions are kept together; it does not automatically identify that witness with another statement's subject. It is not ACE, is not ACE-compatible as a whole, and must never be advertised as full ACE support. The grammar remains deliberately limited so that every supported construction can be implemented, inspected, and tested without pretending to understand unrestricted English. The same parser entry point also accepts a growing native-English grammar; those constructions are mapped directly into the semantic model rather than translated into LCE text.

## Why not integrate full ACE immediately?

ACE is a mature controlled natural language with documented construction and interpretation rules. Its grammar covers substantially more than the first examples needed by Lattice, including richer noun phrases, complements, coordination, quantification, negation, and anaphoric relationships between sentences. See the [ACE 6.7 construction rules](https://attempto.ifi.uzh.ch/site/docs/ace_constructionrules.html) and [ACE in a Nutshell](https://attempto.ifi.uzh.ch/site/docs/ace_nutshell.html).

The official Attempto Parsing Engine (APE) converts ACE text into a Discourse Representation Structure (DRS), among other outputs. The official resources describe APE as a SWI-Prolog application and document its external/runtime interfaces. See the [Attempto tools and resources](https://attempto.ifi.uzh.ch/site/main.php?text=resources%2Fresources.html) and [APE web-client help](https://attempto.ifi.uzh.ch/site/docs/ape_webclient_help.html).

Using APE could be a valid later option if Lattice needs broad ACE coverage. For the first language slice, however, an external parser/runtime would add deployment and integration complexity before Lattice has established its own semantic contract. We therefore start with a small native parser and keep the language layer replaceable.

## LCE 0.9 grammar

An input text contains one or more supported sentences. Each sentence is terminated by its required punctuation and is processed in source order.

    text                 = sentence, { whitespace, sentence } ;

    sentence             = universal-rule | universal-relational-rule | indefinite-existential | indefinite-assertion
                         | existential-question | unary-assertion | pronoun-assertion
                         | property-assertion | relation-assertion | unary-question
                         | pronoun-question | property-question | relation-question ;

    universal-rule       = "Every", class-description, "is", article, concept, "." ;
    universal-relational-rule = "Every", class-description, third-person-verb, object-term, "." ;
    class-description    = { adjective, whitespace }, concept ;
    indefinite-existential = indefinite-description, "exists", "." ;
    indefinite-assertion = indefinite-description, "is", [ article ], concept, "." ;
    existential-question = "Does", indefinite-question-description, "exist", "?" ;
    indefinite-description = ("A" | "An"), { adjective, whitespace }, noun ;
    indefinite-question-description = article, { adjective, whitespace }, noun ;
    unary-assertion      = noun-phrase, "is", article, concept, "." ;
    pronoun-assertion    = "It", "is", article, concept, "." ;
    property-assertion   = definite-description, "is", adjective, "." ;
    relation-assertion   = relation-subject, third-person-verb, relation-object, "." ;
    unary-question       = "Is", noun-phrase, article, concept, "?" ;
    pronoun-question     = "Is", "it", article, concept, "?" ;
    property-question    = "Is", definite-description, adjective, "?" ;
    relation-question    = "Does", relation-question-subject, base-verb, relation-object, "?" ;

    noun-phrase          = proper-name | definite-description ;
    relation-subject     = noun-phrase | "It" ;
    relation-question-subject = noun-phrase | "it" ;
    relation-object      = object-term | "it" ;
    definite-description = "the", { adjective }, noun ;
    adjective            = lowercase-word ;
    noun                 = lowercase-word ;

    article              = "a" | "an" ;
    concept              = lowercase-word ;
    proper-name          = capitalized-word ;
    object-term          = proper-name | definite-description | lowercase-word ;
    base-verb            = lowercase-word ;
    third-person-verb    = lowercase-word-ending-in-s ;

    lowercase-word       = letter-lower, { letter-lower | digit | "-" } ;
    capitalized-word     = letter-upper, { letter | digit | "-" } ;

Additional constraints:

- The function words and sentence-initial words use the exact capitalization shown.
- Concepts, adjectives, and verbs are single lowercase tokens. Plurals, adjective ordering rules, nested noun phrases, and domain vocabulary rules are not supported yet.
- Proper names are one capitalized token. Their semantic identifiers are normalized to lowercase.
- A definite descriptive noun phrase has the form "the" followed by one or more lowercase descriptive tokens. The final token is the head noun; preceding tokens are modifiers. Examples include "the red ball" and "the old blue car".
- A universal class description uses the same trailing-head convention without the article "the". In "Every red ball is a toy.", the head noun is "ball" and the modifier is "red". Multiple modifiers are supported, as in "Every old blue car is a vehicle." The modifier predicates and head-noun predicate form a conjunction: every condition must apply to the same individual before the conclusion follows.
- A universal relational rule uses that same class description followed by a third-person singular relation verb and one concrete object term. For example, "Every red dog likes milk." means that every individual satisfying both red(x) and dog(x) stands in the likes relation to the fixed individual milk. The semantic conclusion is binary, with the bound subject variable in the first argument and the concrete object in the second.
- Plural universal relational rules use a finite list of explicit base verbs, including `admire`, `build`, `buy`, `carry`, `catch`, `chase`, `close`, `create`, `do`, `eat`, `find`, `follow`, `give`, `go`, `have`, `hear`, `help`, `hold`, `keep`, `know`, `leave`, `like`, `love`, `make`, `need`, `open`, `read`, `see`, `sell`, `send`, `take`, `teach`, `tell`, `trust`, `use`, `visit`, `want`, `watch`, and `write`. The parser maps each base verb to its third-person singular predicate for the canonical relation.
- Present passive relational assertions and questions use an explicit past-participle lexicon, as in "The letter is written by Fido." and "Is the book made by Fido?". The patient is retained as the relation object and the agent as the subject. Passive wording is mapped to the same atemporal binary relation; tense and aspect are not represented. Unlisted participles are rejected rather than guessed.
- A definite descriptive noun phrase becomes a structured semantic term containing its head noun and ordered modifiers. For example, "the old blue car" has head noun car and modifiers old, blue.
- The term also exposes a stable canonical individual identifier by joining modifiers and head noun with hyphens: "the red ball" becomes red-ball. Repeating the same phrase therefore addresses the same identifier.
- When a grounded assertion contains a descriptive term, the knowledge base stores the sentence's main predicate and adds one unary fact for the head noun and one for each modifier. Thus "The red ball is a toy." contributes toy(red-ball), ball(red-ball), and red(red-ball).
- A definite-description property assertion such as "The red ball is shiny." contributes shiny(red-ball), ball(red-ball), and red(red-ball). The matching property question is "Is the red ball shiny?".
- Definite descriptions remain identity keys rather than full natural-language entity resolution. "The red ball" and "the crimson ball" are different identifiers; adjective synonyms, ordering variants, and comparative adjectives are not interpreted.
- An indefinite description begins with "A" or "An", followed by zero or more modifiers and a final head noun. For example, "A red ball" contributes the conditions red(x) and ball(x).
- "A red ball exists." asserts that at least one individual satisfies the description. "A red ball is shiny." asserts that one individual satisfies red(x), ball(x), and shiny(x). The same form accepts an article after "is", as in "A red ball is a toy."
- Each indefinite assertion introduces a fresh anonymous witness. Its descriptive conditions and any following predicate are attached to that one witness. Separate indefinite assertions are not assumed to refer to the same object, even when their wording overlaps.
- Anonymous witnesses are internal knowledge-base identities, not named conversation individuals. They do not become antecedents for "it" and are not silently matched to a definite description such as "the red ball".
- An existence question uses "Does a/an ... exist?". It succeeds when all requested unary conditions are supported by one and the same known individual or anonymous witness. A named definite individual can therefore support an existence query, but an unnamed witness does not become that named individual.
- Indefinite descriptions are supported only in unary existential forms. They are not yet supported as relational arguments, pronoun antecedents, or general noun phrases in other sentence patterns.
- Relational assertions and universal relational rules use a third-person singular verb form ending in `s`, such as `likes`, `watches`, or `carries`. The copula `is` is reserved for unary assertions and is not a relation predicate.
- A relational assertion may use the unresolved pronoun `It` as its subject or lowercase `it` as its object. A relational question may use lowercase `it` as either its subject or object. The parser leaves the pronoun unresolved; ConversationContext resolves it only when exactly one individual is known.
- A relational sentence may contain only one `it` reference. Forms such as `It likes it.` and `Does it like it?` are rejected rather than treating two pronouns as the same individual.
- A universal relational rule may have one or more unary class-description premises, all bound to the same subject variable, and exactly one binary relation conclusion whose object is a concrete proper name, definite description, or lowercase word. Relational premises, multiple quantified variables, variable objects, and pronouns inside quantified rules are not supported.
- Relational questions use `Does`, followed by the verb's base form. Lattice maps regular verbs to third-person singular using standard spelling rules: consonant + `y` becomes `ies`; endings `s`, `x`, `z`, `ch`, `sh`, and `o` take `es`; other verbs take `s`.
- The supported irregular base forms `have`, `do`, and `go` map to `has`, `does`, and `goes`. Other irregular forms are not normalized by this grammar.
- The relation predicate is represented using its third-person singular form. Thus, `Fido likes milk.` and `Does Fido like milk?` both produce `likes(fido, milk)`.
- The verb `be` is not supported in the `Does ...` relation-question form.
- The token `It` is reserved for the pronoun in unary and relational assertion subjects, and cannot be used as a proper name in those subject forms. The pronoun is exactly `It` as an assertion subject and `it` in unary questions, relational-question subjects, and relational object positions.
- Both `a` and `an` are accepted as articles. Article agreement is not checked in this initial grammar.
- A period terminates an assertion or universal rule; a question mark terminates a yes/no question.
- Sentences may be separated by spaces, tabs, or other whitespace. Each sentence must have its own terminator.
- A text is parsed completely before any of its sentences are processed. If any sentence is unsupported or incomplete, the entire text is rejected and the session state is left unchanged.
- Once parsing succeeds, sentences are processed in their original order. Context established by an earlier sentence is therefore available to later sentences in the same input.
- The console currently accepts a multi-sentence turn on one input line.

Examples:

| Input | Result |
|---|---|
| `Every dog is an animal.` | One universal rule with one unary premise |
| `Every red ball is a toy.` | `forall(x, implies(and(red(x), ball(x)), toy(x)))`; both unary premises are required |
| `Every old blue car is a vehicle.` | `forall(x, implies(and(old(x), blue(x), car(x)), vehicle(x)))` |
| `Every dog likes milk.` | `forall(x, implies(dog(x), likes(x, milk)))`; a unary premise and one fixed relation object |
| `Every red dog likes milk.` | `forall(x, implies(and(red(x), dog(x)), likes(x, milk)))`; both conditions must hold for the same subject |
| `A red ball exists.` | `exists(x, and(red(x), ball(x)))`; introduces one anonymous witness |
| `A red ball is shiny.` | `exists(x, and(red(x), ball(x), shiny(x)))`; all conditions describe the same witness |
| `A red ball is a toy.` | `exists(x, and(red(x), ball(x), toy(x)))` |
| `Does a red ball exist?` | `ask(exists(x, and(red(x), ball(x))))` |
| `Fido is a dog.` | One unary fact |
| `It is a mammal.` | One fact with an unresolved reference |
| `Is Fido an animal?` | One unary yes/no question |
| `Fido likes milk.` | Binary fact: `likes(fido, milk)` |
| `It likes milk.` | Binary fact with an unresolved subject reference: `likes(it, milk)` |
| `Fido likes it.` | Binary fact with an unresolved object reference: `likes(fido, it)` |
| `Fido likes the red ball.` | Binary fact: `likes(fido, red-ball)` |
| `The red ball is a toy.` | Main fact: `toy(red-ball)`; description facts: `ball(red-ball)`, `red(red-ball)` |
| `The red ball is shiny.` | Property fact: `shiny(red-ball)`; description facts: `ball(red-ball)`, `red(red-ball)` |
| `Is the red ball red?` | Property query: `ask(red(red-ball))` |
| `Does it like milk?` | Query with an unresolved subject reference: `ask(likes(it, milk))` |
| `Does Fido like it?` | Query with an unresolved object reference: `ask(likes(fido, it))` |
| `Does Fido like the red ball?` | Query: `ask(likes(fido, red-ball))` |
| `Does Fido like milk?` | Query: `ask(likes(fido, milk))` |
| `Fido has milk. Does Fido have milk?` | A relation fact followed by its matching query |
| `Every dog is an animal. Fido is a dog. Is Fido an animal?` | A rule, a fact, then a question |
| `Fido is a dog. It is a mammal. Is it a mammal?` | A fact, a context-resolved fact, then a question |

Inputs outside this grammar are rejected with an explicit sentence number where possible. The parser does not guess, silently repair wording, or translate arbitrary English. The parser deliberately does not resolve "it"; reference resolution requires conversation context and happens after the complete text has been parsed.

## Native English grammar

The public `LceParser` entry point accepts both LCE 0.9 and a growing, deterministic subset of ordinary English. This is part of the native language parser, not a translator or preprocessing stage: recognized sentences are mapped directly into the existing semantic formulas. No intermediate LCE sentence is generated.

The native-English entry point is a small dispatcher. Separate internal components own quantifier constructions, noun descriptions, relational constructions, and definite-description properties. Shared helpers own token validation, morphology, and semantic formula construction. The sentence parsers depend on these shared helpers and on the noun-description parser where needed; shared helpers do not depend back on sentence parsers. This keeps each grammar responsibility independently navigable without relying on partial classes. Lexical exceptions and finite vocabularies are stored separately in a versioned JSON lexicon embedded in the Core assembly. Core validates the complete resource once at first use and exposes immutable, case-insensitive collections to the parser. The resource is shipped with the assembly; it is not runtime configuration. The algorithms that interpret the entries remain ordinary C# code.

The grammar is built from reusable phrase structures. Named individuals support direct unary property assertions and questions: `Fido is happy.` maps to `happy(fido)`, and `Is Fido happy?` maps to `ask(happy(fido))`. A narrow explicit-negation form is also supported for named individuals: `Fido is not a dog.` and `Fido is not happy.` map to `not(dog(fido))` and `not(happy(fido))`; questions use the corresponding `Is Fido not ...?` form. Binary relations also support explicit negation in the active form: `Fido does not like milk.` and `Does Fido not like milk?` map to `not(likes(fido, milk))` and `ask(not(likes(fido, milk)))`. The negative relational forms use a finite supported base verb and retain the ordinary relation argument structure. Class predicates require an article, while property predicates are limited to one uncoordinated adjective from the finite `named-individual-property-adjectives` lexicon. This remains distinct from definite-description properties (`The red ball is shiny.`). Negated universal conclusions are supported only in the narrow forms `Every dog is not a cat.` and `Every dog does not like milk.`. Universal-rule descriptions also support one explicit negative unary condition in the restrictive form `that is/are not ...`, such as `Every dog that is not a cat is friendly.`; this is a rule premise, not a general-purpose negated noun-description feature. Only explicitly stored negative ground unary evidence satisfies it. Negated premises in existential or definite descriptions, negated passive relations, coordinated properties, and compound negation remain unsupported. One description parser recognizes a head noun, preceding modifiers (including `and`-coordinated adjectives), positive restrictive `that is/are + adjective` clauses, the narrow negated unary clause used by universal rules, and (only for universal rules) one fixed-object positive or explicitly negative relational relative clause. A relative clause may coordinate adjectives with `and`; it does not accept an unmarked sequence of adjectives. The same parsed description is then used in different grammatical positions:

- As the subject conditions of universal rules.
- As the descriptive conditions of existential claims.
- As a definite individual in property assertions and questions.
- As the subject or object of a binary relation.

For example, the same description structure is recognized in all of these sentences:

| Input | Semantic reading |
|---|---|
| `All red dogs are animals.` | `forall(x, implies(and(red(x), dog(x)), animal(x)))` |
| `All red and shiny dogs are animals.` | `forall(x, implies(and(red(x), shiny(x), dog(x)), animal(x)))` |
| `Every dog that is red and shiny is an animal.` | Same meaning |
| `Every dog that is red is an animal.` | Same meaning |
| `Every dog that is not a cat is friendly.` | `forall(x, implies(and(dog(x), not(cat(x))), friendly(x)))` |
| `Every dog that does not like milk is unfriendly.` | `forall(x, implies(and(dog(x), not(likes(x, milk))), unfriendly(x)))` |
| `All dogs that are not cats are friendly.` | Same meaning |
| `All dogs that are red are animals.` | Same meaning |
| `All red dogs that are old like milk.` | `forall(x, implies(and(red(x), old(x), dog(x)), likes(x, milk)))` |
| `Some red dogs are animals.` | `exists(x, and(red(x), dog(x), animal(x)))` |
| `There is a ball that is red that is shiny.` | `exists(x, and(red(x), shiny(x), ball(x)))` |
| `The ball that is red that is shiny is a toy.` | `toy(red-shiny-ball)` |
| `The ball that is red likes milk.` | `likes(red-ball, milk)` |
| `Fido likes the ball that is red that is shiny.` | `likes(fido, red-shiny-ball)` |
| `Does the ball that is red like milk?` | `ask(likes(red-ball, milk))` |
| `Is the ball that is red shiny?` | `ask(shiny(red-ball))` |

This means a relative-clause description does not have a separate implementation for every sentence type. Its head and modifiers are parsed once and then composed with the surrounding sentence structure.

The native-English grammar supports singular universal quantification with `Each` (for example, `Each dog is an animal.`), plural universal quantification (`All`), existential `Some` statements over unary conditions, and the reverse implication expressed by simple `Only` statements. `Each` takes a singular head and singular agreement, but has the same universal semantics as `Every`. `Only` with a multi-condition subject description is deliberately not accepted yet because the current universal-rule representation does not support a conjunctive conclusion. Regular plural nouns use the supported `-s`, `-es`, and consonant-plus-`y` to `-ies` spelling rules. A finite explicit mapping also covers `algae`, `analyses`, `appendices`, `antennae`, `bacteria`, `bases`, `cacti`, `cases`, `children`, `courses`, `crises`, `criteria`, `curricula`, `diagnoses`, `diseases`, `emphases`, `feet`, `foci`, `formulae`, `fungi`, `geese`, `horses`, `houses`, `indices`, `knives`, `larvae`, `leaves`, `lice`, `lives`, `loci`, `matrices`, `men`, `mice`, `nebulae`, `noises`, `noses`, `nuclei`, `nurses`, `oases`, `oxen`, `parentheses`, `pauses`, `people`, `phenomena`, `phases`, `phrases`, `promises`, `radii`, `spouses`, `stimuli`, `syllabi`, `teeth`, `theses`, `thieves`, `universes`, `vertebrae`, `vertices`, `wives`, `women`, and `wolves`. Common singular nouns that end in `s`, such as `analysis`, `bus`, `lens`, and `virus`, are explicitly distinguished from plural forms. The invariant noun forms `aircraft`, `barracks`, `bison`, `crossroads`, `deer`, `fish`, `headquarters`, `means`, `moose`, `offspring`, `reindeer`, `salmon`, `series`, `sheep`, `spacecraft`, `species`, `swine`, and `trout` are accepted in both singular and plural quantifier patterns without changing their canonical form. `News` is explicitly classified as singular despite its final `s`, so it is not mistaken for a plural noun. The explicit mapping remains intentionally finite: ambiguous or context-sensitive forms such as `data` are not assigned a single singular form, and reliable general lexical-category detection remains future work.

Positive restrictive relative clauses use `that is + adjective` for singular heads and `that are + adjective` for plural heads. Universal-rule descriptions additionally allow one explicit negative unary predicate after `that is/are not`, such as `that is not a cat` or `that is not happy`, and one explicit negative fixed-object relation using `that does not + base verb` for singular heads or `that do not + base verb` for plural heads. For example, `Every dog that does not like milk is unfriendly.` maps to `forall(x, implies(and(dog(x), not(likes(x, milk))), unfriendly(x)))`. Relational clauses remain restricted to universal rules and one fixed concrete object. These negative forms are not enabled in existential claims or definite descriptions. Verb-containing clauses beyond the narrow fixed-object positive and explicitly negative relational relative clauses described above, unmarked adjective sequences in relative clauses, negated quantified constructions, negated passive relations, modality, variable-bound relational objects, relational premises beyond one fixed-object relation in a universal rule, higher-arity events, and ambiguous lexical constructions remain unsupported. The parser rejects them instead of weakening or guessing their meaning.

Both controlled LCE and native-English constructions preserve the original sentence in `SemanticUtterance.SourceText`, and both can appear together in one `ParseText` turn. This language expansion does not automatically expand what the reasoning engine can represent or prove.
  
## Parsing API and semantic representation

- `Parse` parses exactly one sentence and retains its single-utterance result contract.
- `ParseText` parses one or more sentences and returns an ordered collection of semantic utterances.
- `TextParseResult` is all-or-nothing: on failure it contains no partial utterance collection.
- Each `SemanticUtterance.SourceText` contains that sentence's original trimmed text, not the whole input turn.

The parser does not return grammar-specific syntax nodes. It produces a small language-independent semantic expression model:

- **SemanticIdentifier:** a normalized symbolic identifier.
- **IndividualTerm** and **VariableTerm:** the two established term kinds.
- **UnresolvedReferenceTerm:** a reference that needs conversational context before it can be grounded to an individual.
- **PredicateApplication:** a predicate applied to an immutable ordered list of terms. Unary predicates and n-ary relations use the same representation.
- **Conjunction:** a formula whose conjuncts must all hold.
- **Implication:** a formula whose conclusion follows when its premise holds.
- **UniversalQuantification** and **ExistentialQuantification:** bind a variable over a formula.
- **AnonymousIndividualTerm:** an internal witness introduced when an existential assertion is stored; it is deliberately not a named conversation referent.
- **SemanticUtterance:** preserves whether the input was an assertion or a question, the meaning, and the original source text. A resolved utterance also retains the grounded referent separately from the original wording.

For example, `Every dog is an animal.` is represented as:

    UniversalQuantification(
      VariableTerm("x"),
      Implication(
        PredicateApplication("dog", [VariableTerm("x")]),
        PredicateApplication("animal", [VariableTerm("x")])
      )
    )

The quantified relational example `Every dog likes milk.` is represented as:
    
    UniversalQuantification(
      VariableTerm("x"),
      Implication(
        PredicateApplication("dog", [VariableTerm("x")]),
        PredicateApplication("likes", [VariableTerm("x"), IndividualTerm("milk")])
      )
    )

The ground relational example `Fido likes milk.` is represented as:

    PredicateApplication(
      "likes",
      [IndividualTerm("fido"), IndividualTerm("milk")]
    )

A definite description is represented as a structured `DescriptiveIndividualTerm`. For example, `the red ball` has head `ball`, modifier `red`, and stable identifier `red-ball`. When an assertion is added to the knowledge base, the descriptor is grounded to that identifier and contributes the separate unary facts `ball(red-ball)` and `red(red-ball)`. The adjective is therefore available as a predicate; it is not just embedded in an opaque name.

An indefinite description instead produces an `ExistentialQuantification`. For example, `A red ball is shiny.` becomes:

    exists(x, and(red(x), ball(x), shiny(x)))

When stored, the knowledge base gives this assertion one fresh `AnonymousIndividualTerm` and records each condition against that same witness. An existence question checks the full conjunction against one known individual or witness; it never joins conditions from separate witnesses.

## Optional semantics translator boundary

The Core contract `ISemanticsTranslator` allows an optional adapter to propose semantic candidates from ordinary input without making a translator implementation a dependency of Core. The separate `Lattice.Translators.LmStudio` project provides one optional adapter. It asks a local LM Studio model to produce candidate LCE text, parses each candidate with the native `LceParser`, and returns only parsed semantic structures to the provider-neutral boundary. The adapter is not enabled by default and no model is bundled or selected; LCE remains the native input language.

A translator returns candidate `UtteranceKind` and `SemanticFormula` values. It does not choose provenance or provide an authoritative `SourceText`: `SemanticTranslationService` attaches the exact original input to every validated `SemanticUtterance`. `SemanticUtteranceValidator` restricts candidates to the currently supported forms: ground unary or binary predicates, the supported single `it` reference, unary existential claims and queries, and universal rules with unary premises and either a unary conclusion or a fixed-object binary conclusion. Unsupported arities, free variables, nested or unsupported formulas, malformed identifiers, and unsupported utterance kinds are rejected.

The service validates the entire response before returning any candidates. One valid candidate is marked `ReadyForReview`; multiple valid candidates are marked `Ambiguous`; an empty response is `NoInterpretation`; malformed or unsupported output is `Rejected`; provider errors are `Failed`. Cancellation is propagated. The service does not commit facts, resolve references against session context, invoke capabilities, or select an interpretation on the user's behalf.

These checks establish only that a candidate has a structure Lattice currently knows how to process. They cannot establish that the translator preserved the meaning of the original wording. Even a single structurally valid candidate remains a proposal that requires review before the caller sends it through ordinary context resolution and knowledge ingestion.

## Input normalization

Normalization is a small formatting cleanup step before parsing. It does not expand LCE's grammar or attempt to repair a sentence.

The current normalization rules are deliberately narrow:

- Normalize Unicode text to canonical composition (NFC).
- Convert Unicode whitespace characters to ordinary spaces for consistent token separation.
- Treat a zero-width space as a space, preserving a possible word boundary rather than joining words.
- Remove a leading byte-order mark; an embedded byte-order mark becomes a space.
- Remove soft hyphens, which are formatting hints rather than visible word characters.

The original trimmed sentence is retained in SemanticUtterance.SourceText. The normalized form is used only for recognition and semantic identifier extraction. Thus, explanations and provenance continue to show the user's supplied wording.

Normalization does not change ordinary letters, spelling, word order, articles, capitalization, or sentence punctuation. It does not auto-correct typos, add missing words, or infer missing punctuation. Unsupported sentences remain unsupported after cleanup.

This boundary is intentionally separate from the controlled grammar. More aggressive cleanup or natural-language correction would need its own explicit design and validation rather than being hidden inside the parser.

## Design constraints

- Keep the semantic model independent of English word order and grammar.
- Keep parsing separate from reference resolution, inference, and the CLI.
- Parse a complete text successfully before allowing the CLI to mutate session state.
- Do not interpret a missing predicate as false.
- Keep original wording and resolved referents separately so explanations do not silently rewrite what the user said.
- Optional translator adapters must implement `ISemanticsTranslator` and pass through `SemanticTranslationService` and `SemanticUtteranceValidator`; they must not bypass context resolution, knowledge validation, provenance, or user review.
