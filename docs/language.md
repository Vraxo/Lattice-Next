# Language strategy and LCE 0.3

## Decision

The initial parser uses **Lattice Controlled English (LCE) 0.3**: a small, explicitly specified controlled-language subset inspired by the design principles of Attempto Controlled English (ACE).

LCE 0.3 retains the individual sentence forms introduced in 0.1 and 0.2 and adds support for multiple complete sentences in one input. It is not ACE, is not ACE-compatible as a whole, and must never be advertised as full ACE support. The grammar remains deliberately limited so that every supported construction can be implemented, inspected, and tested without pretending to understand unrestricted English.

## Why not integrate full ACE immediately?

ACE is a mature controlled natural language with documented construction and interpretation rules. Its grammar covers substantially more than the first examples needed by Lattice, including richer noun phrases, complements, coordination, quantification, negation, and anaphoric relationships between sentences. See the [ACE 6.7 construction rules](https://attempto.ifi.uzh.ch/site/docs/ace_constructionrules.html) and [ACE in a Nutshell](https://attempto.ifi.uzh.ch/site/docs/ace_nutshell.html).

The official Attempto Parsing Engine (APE) converts ACE text into a Discourse Representation Structure (DRS), among other outputs. The official resources describe APE as a SWI-Prolog application and document its external/runtime interfaces. See the [Attempto tools and resources](https://attempto.ifi.uzh.ch/site/main.php?text=resources%2Fresources.html) and [APE web-client help](https://attempto.ifi.uzh.ch/site/docs/ape_webclient_help.html).

Using APE could be a valid later option if Lattice needs broad ACE coverage. For the first language slice, however, an external parser/runtime would add deployment and integration complexity before Lattice has established its own semantic contract. We therefore start with a small native parser and keep the language layer replaceable.

## LCE 0.3 grammar

An input text contains one or more supported sentences. Each sentence is terminated by its required punctuation and is processed in source order.

    text                 = sentence, { whitespace, sentence } ;

    sentence             = universal-rule | assertion | pronoun-assertion
                         | yes-no-question | pronoun-question ;

    universal-rule       = "Every", concept, "is", article, concept, "." ;
    assertion            = proper-name, "is", article, concept, "." ;
    pronoun-assertion    = "It", "is", article, concept, "." ;
    yes-no-question      = "Is", proper-name, article, concept, "?" ;
    pronoun-question     = "Is", "it", article, concept, "?" ;

    article              = "a" | "an" ;
    concept              = lowercase-word ;
    proper-name          = capitalized-word ;

    lowercase-word       = letter-lower, { letter-lower | digit | "-" } ;
    capitalized-word     = letter-upper, { letter | digit | "-" } ;

Additional constraints:

- The function words and sentence-initial words use the exact capitalization shown.
- Concepts are single lowercase tokens. Multiword noun phrases, plurals, adjectives, and domain vocabulary rules are not supported yet.
- Proper names are one capitalized token. Their semantic identifiers are normalized to lowercase.
- The token "It" is reserved for the pronoun in assertions and cannot be used as a proper name in these subject forms.
- The pronoun is exactly "It" as an assertion subject and "it" in a yes/no question. Other pronouns and grammatical forms are not supported.
- Both "a" and "an" are accepted as articles. Article agreement is not checked in this initial grammar.
- A period terminates an assertion or universal rule; a question mark terminates a yes/no question.
- Sentences may be separated by spaces, tabs, or other whitespace. Each sentence must have its own terminator.
- A text is parsed completely before any of its sentences are processed. If any sentence is unsupported or incomplete, the entire text is rejected and the session state is left unchanged.
- Once parsing succeeds, sentences are processed in their original order. Context established by an earlier sentence is therefore available to later sentences in the same input.
- The console currently accepts a multi-sentence turn on one input line.

Examples:

| Input | Result |
|---|---|
| `Every dog is an animal.` | One universal rule |
| `Fido is a dog.` | One individual fact |
| `It is a mammal.` | One fact with an unresolved reference |
| `Is Fido an animal?` | One yes/no question |
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
- **PredicateApplication:** a predicate applied to an immutable ordered list of terms. This supports unary predicates now and provides a representation for n-ary relations.
- **Implication:** a formula whose conclusion follows when its premise holds.
- **UniversalQuantification:** binds a variable over a formula.
- **SemanticUtterance:** preserves whether the input was an assertion or a question, the meaning, and the original source text. A resolved utterance also retains the grounded referent separately from the original wording.

For example, Every dog is an animal is represented as:

    UniversalQuantification(
      VariableTerm("x"),
      Implication(
        PredicateApplication("dog", [VariableTerm("x")]),
        PredicateApplication("animal", [VariableTerm("x")])
      )
    )

The current grammar only creates unary predicate applications. The semantic model can represent an n-ary relation such as likes(fido, milk), but LCE 0.3 does not yet parse relational sentences.

## Design constraints

- Keep the semantic model independent of English word order and grammar.
- Keep parsing separate from reference resolution, inference, and the CLI.
- Parse a complete text successfully before allowing the CLI to mutate session state.
- Do not interpret a missing predicate as false.
- Keep original wording and resolved referents separately so explanations do not silently rewrite what the user said.
- Future language front ends, including an optional neural translator, must produce the same validated semantic representation rather than bypass it.
