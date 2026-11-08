# Language strategy and LCE 0.1

## Decision

The initial parser uses **Lattice Controlled English (LCE) 0.1**: a small, explicitly specified controlled-language subset inspired by the design principles of Attempto Controlled English (ACE).

LCE 0.1 is not ACE, is not ACE-compatible as a whole, and must never be advertised as full ACE support. It is deliberately limited to three sentence patterns so that the first semantic layer can be implemented, inspected, and tested without adding a Prolog runtime or pretending to understand unrestricted English.

## Why not integrate full ACE immediately?

ACE is a mature controlled natural language with documented construction and interpretation rules. Its grammar covers substantially more than the first examples needed by Lattice, including richer noun phrases, complements, coordination, quantification, negation, and anaphoric relationships between sentences. See the [ACE 6.7 construction rules](https://attempto.ifi.uzh.ch/site/docs/ace_constructionrules.html) and [ACE in a Nutshell](https://attempto.ifi.uzh.ch/site/docs/ace_nutshell.html).

The official Attempto Parsing Engine (APE) converts ACE text into a Discourse Representation Structure (DRS), among other outputs. The official resources describe APE as a SWI-Prolog application and document its external/runtime interfaces. See the [Attempto tools and resources](https://attempto.ifi.uzh.ch/site/main.php?text=resources%2Fresources.html) and [APE web-client help](https://attempto.ifi.uzh.ch/site/docs/ape_webclient_help.html).

Using APE could be a valid later option if Lattice needs broad ACE coverage. For the first language slice, however, an external parser/runtime would add deployment and integration complexity before Lattice has established its own semantic contract. We therefore start with a small native parser and keep the language layer replaceable.

## LCE 0.1 grammar

The parser accepts exactly one input sentence matching one of these forms:

```ebnf
utterance       = universal-rule | assertion | yes-no-question ;

universal-rule  = "Every", concept, "is", article, concept, "." ;
assertion       = proper-name, "is", article, concept, "." ;
yes-no-question = "Is", proper-name, article, concept, "?" ;

article          = "a" | "an" ;
concept          = lowercase-word ;
proper-name      = capitalized-word ;

lowercase-word   = letter-lower, { letter-lower | digit | "-" } ;
capitalized-word = letter-upper, { letter | digit | "-" } ;
```

Additional constraints:

- The function words and sentence-initial words use the exact capitalization shown.
- Concepts are single lowercase tokens. Multiword noun phrases, plurals, adjectives, and domain vocabulary rules are not supported yet.
- Proper names are one capitalized token. Their semantic identifiers are normalized to lowercase.
- Both `a` and `an` are accepted as articles. Article agreement is not checked in this initial grammar.
- A period terminates an assertion or universal rule; a question mark terminates a yes/no question.
- Multiple sentences in one input are not accepted yet.

Examples:

| Input | Resulting meaning |
|---|---|
| `Every dog is an animal.` | `forall(x, implies(dog(x), animal(x)))` |
| `Fido is a dog.` | `dog(fido)` |
| `Is Fido an animal?` | `ask(animal(fido))` |

Inputs outside this grammar are rejected with an explicit message. The parser does not guess, silently repair wording, or translate arbitrary English.

## Semantic representation

The parser does not return grammar-specific syntax nodes. It produces a small language-independent semantic expression model:

- `SemanticIdentifier`: a normalized symbolic identifier.
- `IndividualTerm` and `VariableTerm`: the two term kinds required by this slice.
- `PredicateApplication`: a predicate applied to an immutable ordered list of terms. This supports unary predicates now and provides a representation for n-ary relations.
- `Implication`: a formula whose conclusion follows when its premise holds.
- `UniversalQuantification`: binds a variable over a formula.
- `SemanticUtterance`: preserves whether the input was an assertion or a question, the meaning, and the original source text.

For example, `Every dog is an animal.` is represented as:

```text
UniversalQuantification(
  VariableTerm("x"),
  Implication(
    PredicateApplication("dog", [VariableTerm("x")]),
    PredicateApplication("animal", [VariableTerm("x")])
  )
)
```

The current grammar only creates unary predicate applications. The semantic model can represent an n-ary relation such as `likes(fido, milk)`, but LCE 0.1 does not yet parse relational sentences.

This is a representation and parser, not a reasoning engine. No conclusion is derived from these expressions in this slice.

## Design constraints

- Keep the semantic model independent of English word order and grammar.
- Keep parsing separate from inference and from the CLI.
- Do not interpret a missing predicate as false.
- Keep the original input alongside its parsed meaning.
- Future language front ends, including an optional neural translator, must produce the same validated semantic representation rather than bypass it.
