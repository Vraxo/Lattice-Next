# Conversational context and reference resolution

## Purpose

The conversation-context slice connects independently parsed utterances without making the parser responsible for deciding what a pronoun means.

LCE 0.2 introduced the singular pronoun "it" as an UnresolvedReferenceTerm. LCE 0.3 allows multiple sentences in one input. The Core context layer attempts resolution for each sentence in source order, so an explicit individual mention in an earlier sentence can ground a pronoun in a later sentence in the same turn.

## Current resolution policy

This initial policy is intentionally conservative:

- If no individual has been explicitly mentioned in a supported unary assertion or yes/no question, "it" has no known referent and Lattice asks the user to name the individual.
- If exactly one distinct individual has been explicitly mentioned, "it" resolves to that individual.
- If more than one distinct individual has been mentioned, the reference is considered ambiguous. Lattice asks the user to name the intended individual rather than guessing based on recency.

Repeated mentions of the same individual do not create ambiguity. Universal rules do not introduce an individual. Only explicit individual mentions enter this context; inferred conclusions do not automatically become new conversational antecedents.

The policy deliberately favors correctness over convenience. It does not yet implement recency, grammatical gender, number agreement, salience, sentence-level co-reference, or clarification follow-up memory. Users can always repeat the individual's name explicitly.

## Multi-sentence turns

The parser first validates the complete input text. If any sentence is unsupported or incomplete, it returns a failure with no partial utterance list. The CLI therefore does not update conversation context or the knowledge base for a text that failed parsing.

After a successful parse, the CLI processes each utterance in source order:

1. Resolve any reference using the context accumulated so far.
2. If it is an assertion, add the grounded fact or rule to the knowledge base.
3. Observe the accepted assertion so later sentences can refer to its explicitly named individual.
4. If it is a question, resolve it first, then query the reasoning engine and present the answer before moving to the next sentence.

This means an input such as "Fido is a dog. It is a mammal. Is it a mammal?" works as one turn: the first sentence establishes Fido, the second sentence resolves "it" to Fido and adds a fact, and the final question can query that fact.

A clarification request for a sentence does not abort the rest of an already parsed turn. The unresolved sentence is not stored or queried; later sentences are still processed using the context that was successfully established by earlier accepted utterances.

## Separation of responsibilities

- The parser creates semantic meaning and leaves unresolved references explicit.
- ConversationContext observes explicit individual mentions and grounds resolvable references.
- KnowledgeBase stores only grounded individual facts and supported universal rules.
- ReasoningEngine receives grounded predicates and does not know about pronouns or conversation turns.
- The CLI composes these services and presents clarification requests.

When a pronoun assertion is resolved, the utterance keeps its original SourceText and separately records its ResolvedReferent. The knowledge base and proof trace preserve both. An explanation can therefore show the wording the user actually supplied and the individual to which the pronoun was resolved.

## Lifetime and limitations

The CLI owns one ConversationContext for the duration of the interactive process. It is separate from the knowledge base but has the same session lifetime. It is not persisted, shared between sessions, or loaded from a knowledge pack.

The implementation currently supports only the unary subject-position pronoun forms listed in [language.md](language.md). It is not a general co-reference resolver. Richer antecedent selection should wait until the language layer can represent more than one entity and more complex discourse structures.
