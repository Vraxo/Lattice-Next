# Language strategy and LCE 0.6

## Decision

The initial parser uses **Lattice Controlled English (LCE) 0.5**: a small, explicitly specified controlled-language subset inspired by the design principles of Attempto Controlled English (ACE).

LCE 0.6 retains unary assertions, universal rules, questions, and multi-sentence turns. It adds binary relational assertions and direct yes/no relational questions. It is not ACE, is not ACE-compatible as a whole, and must never be advertised as full ACE support. The grammar remains deliberately limited so that every supported construction can be implemented, inspected, and tested without pretending to understand unrestricted English.

## Why not integrate full ACE immediately?

ACE is a mature controlled natural language with documented construction and interpretation rules. Its grammar covers substantially more than the first examples needed by Lattice, including richer noun phrases, complements, coordination, quantification, negation, and anaphoric relationships between sentences. See the [ACE 6.7 construction rules](https://attempto.ifi.uzh.ch/site/docs/ace_constructionrules.html) and [ACE in a Nutshell](https://attempto.ifi.uzh.ch/site/docs/ace_nutshell.html).

The official Attempto Parsing Engine (APE) converts ACE text into a Discourse Representation Structure (DRS), among other outputs. The official resources describe APE as a SWI-Prolog application and document its external/runtime interfaces. See the [Attempto tools and resources](https://attempto.ifi.uzh.ch/site/main.php?text=resources%2Fresources.html) and [APE web-client help](https://attempto.ifi.uzh.ch/site/docs/ape_webclient_help.html).

Using APE could be a valid later option if Lattice needs broad ACE coverage. For the first language slice, however, an external parser/runtime would add deployment and integration complexity before Lattice has established its own semantic contract. We therefore start with a small native parser and keep the language layer replaceable.

## LCE 0.6 grammar

An input text contains one or more supported sentences. Each sentence is terminated by its required punctuation and is processed in source order.

    text                 = sentence, { whitespace, sentence } ;

    sentence             = universal-rule | unary-assertion | pronoun-assertion
                         | property-assertion | relation-assertion | unary-question
                         | pronoun-question | property-question | relation-question ;

    universal-rule       = "Every", concept, "is", article, concept, "." ;
    unary-assertion      = noun-phrase, "is", article, concept, "." ;
    pronoun-assertion    = "It", "is", article, concept, "." ;
    property-assertion   = definite-description, "is", adjective, "." ;
    relation-assertion   = noun-phrase, third-person-verb, object-term, "." ;
    unary-question       = "Is", noun-phrase, article, concept, "?" ;
    pronoun-question     = "Is", "it", article, concept, "?" ;
    property-question    = "Is", definite-description, adjective, "?" ;
    relation-question    = "Does", noun-phrase, base-verb, object-term, "?" ;

    noun-phrase          = proper-name | definite-description ;
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
- A definite descriptive noun phrase becomes a structured semantic term containing its head noun and ordered modifiers. For example, "the old blue car" has head noun car and modifiers old, blue.
- The term also exposes a stable canonical individual identifier by joining modifiers and head noun with hyphens: "the red ball" becomes red-ball. Repeating the same phrase therefore addresses the same identifier.
- When a grounded assertion contains a descriptive term, the knowledge base stores the sentence's main predicate and adds one unary fact for the head noun and one for each modifier. Thus "The red ball is a toy." contributes toy(red-ball), ball(red-ball), and red(red-ball).
- A definite-description property assertion such as "The red ball is shiny." contributes shiny(red-ball), ball(red-ball), and red(red-ball). The matching property question is "Is the red ball shiny?".
- Descriptions remain identity keys rather than full natural-language entity resolution. "The red ball" and "the crimson ball" are different identifiers; adjective synonyms, ordering variants, comparative adjectives, and indefinite phrases such as "a red ball" are not interpreted.
- Relational assertions use a third-person singular verb form ending in `s`, such as `likes`, `watches`, or `carries`. The copula `is` is reserved for unary assertions and is not a relation predicate.
- Relational questions use `Does`, followed by the verb's base form. Lattice maps regular verbs to third-person singular using standard spelling rules: consonant + `y` becomes `ies`; endings `s`, `x`, `z`, `ch`, `sh`, and `o` take `es`; other verbs take `s`.
- The supported irregular base forms `have`, `do`, and `go` map to `has`, `does`, and `goes`. Other irregular forms are not normalized by this grammar.
- The relation predicate is represented using its third-person singular form. Thus, `Fido likes milk.` and `Does Fido like milk?` both produce `likes(fido, milk)`.
- The verb `be` is not supported in the `Does ...` relation-question form.
- The token `It` is reserved for the pronoun in unary assertions and cannot be used as a proper name in those subject forms. The pronoun is exactly `It` as an assertion subject and `it` in a unary yes/no question. Relational forms do not yet support pronoun arguments.
- Both `a` and `an` are accepted as articles. Article agreement is not checked in this initial grammar.
- A period terminates an assertion or universal rule; a question mark terminates a yes/no question.
- Sentences may be separated by spaces, tabs, or other whitespace. Each sentence must have its own terminator.
- A text is parsed completely before any of its sentences are processed. If any sentence is unsupported or incomplete, the entire text is rejected and the session state is left unchanged.
- Once parsing succeeds, sentences are processed in their original order. Context established by an earlier sentence is therefore available to later sentences in the same input.
- The console currently accepts a multi-sentence turn on one input line.

Examples:

| Input | Result |
|---|---|
| `Every dog is an animal.` | One universal rule |
| `Fido is a dog.` | One unary fact |
| `It is a mammal.` | One fact with an unresolved reference |
| `Is Fido an animal?` | One unary yes/no question |
| `Fido likes milk.` | Binary fact: `likes(fido, milk)` |
| `Fido likes the red ball.` | Binary fact: `likes(fido, red-ball)` |
| `The red ball is a toy.` | Main fact: `toy(red-ball)`; description facts: `ball(red-ball)`, `red(red-ball)` |
| `The red ball is shiny.` | Property fact: `shiny(red-ball)`; description facts: `ball(red-ball)`, `red(red-ball)` |
| `Is the red ball red?` | Property query: `ask(red(red-ball))` |
| `Does Fido like the red ball?` | Query: `ask(likes(fido, red-ball))` |
| `Does Fido like milk?` | Query: `ask(likes(fido, milk))` |
| `Fido has milk. Does Fido have milk?` | A relation fact followed by its matching query |
| `Every dog is an animal. Fido is a dog. Is Fido an animal?` | A rule, a fact, then a question |
| `Fido is a dog. It is a mammal. Is it a mammal?` | A fact, a context-resolved fact, then a question |

Inputs outside this grammar are rejected with an explicit sentence number where possible. The parser does not guess, silently repair wording, or translate arbitrary English. The parser deliberately does not resolve "it"; reference resolution requires conversation context and happens after the complete text has been parsed.

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
- **Implication:** a formula whose conclusion follows when its premise holds.
- **UniversalQuantification:** binds a variable over a formula.
- **SemanticUtterance:** preserves whether the input was an assertion or a question, the meaning, and the original source text. A resolved utterance also retains the grounded referent separately from the original wording.

For example, `Every dog is an animal.` is represented as:

    UniversalQuantification(
      VariableTerm("x"),
      Implication(
        PredicateApplication("dog", [VariableTerm("x")]),
        PredicateApplication("animal", [VariableTerm("x")])
      )
    )

The relational example `Fido likes milk.` is represented as:

    PredicateApplication(
      "likes",
      [IndividualTerm("fido"), IndividualTerm("milk")]
    )

A definite description is represented as a structured `DescriptiveIndividualTerm`. For example, `the red ball` has head `ball`, modifier `red`, and stable identifier `red-ball`. When an assertion is added to the knowledge base, the descriptor is grounded to that identifier and contributes the separate unary facts `ball(red-ball)` and `red(red-ball)`. The adjective is therefore available as a predicate; it is not just embedded in an opaque name.

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
- Future language front ends, including an optional neural translator, must produce the same validated semantic representation rather than bypass it.
