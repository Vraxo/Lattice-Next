# Conversational context and reference resolution

## Purpose

The first conversation-context slice connects independently parsed utterances without making the parser responsible for deciding what a pronoun means.

LCE 0.2 parses the singular pronoun "it" into an UnresolvedReferenceTerm. It does not replace that reference with an individual during parsing. The Core context layer attempts resolution before an assertion is stored or a question is sent to the reasoning engine.

## Current resolution policy

This initial policy is intentionally conservative:

- If no individual has been explicitly mentioned in a supported unary assertion or yes/no question, "it" has no known referent and Lattice asks the user to name the individual.
- If exactly one distinct individual has been explicitly mentioned, "it" resolves to that individual.
- If more than one distinct individual has been mentioned, the reference is considered ambiguous. Lattice asks the user to name the intended individual rather than guessing based on recency.

Repeated mentions of the same individual do not create ambiguity. Universal rules do not introduce an individual. Only explicit individual mentions enter this context; inferred conclusions do not automatically become new conversational antecedents.

The policy deliberately favors correctness over convenience. It does not yet implement recency, grammatical gender, number agreement, salience, sentence-level co-reference, or clarification follow-up memory. Users can always repeat the individual's name explicitly.

## Separation of responsibilities

- The parser creates semantic meaning and leaves unresolved references explicit.
- ConversationContext observes explicit individual mentions and grounds resolvable references.
- KnowledgeBase stores only grounded individual facts and supported universal rules.
- ReasoningEngine receives grounded predicates and does not know about pronouns or conversation turns.
- The CLI composes these services and presents clarification requests.

When a pronoun assertion is resolved, the utterance keeps its original SourceText and separately records its ResolvedReferent. The knowledge base and proof trace preserve both. An explanation can therefore show the wording the user actually supplied and the individual to which the pronoun was resolved.

## Lifetime and limitations

The CLI owns one ConversationContext for the duration of the interactive process. It is separate from the knowledge base but has the same session lifetime. It is not persisted, shared between sessions, or loaded from a knowledge pack.

The implementation currently supports only the unary subject-position pronoun forms listed in language.md. It is not a general co-reference resolver. Richer antecedent selection should wait until the language layer can represent more than one entity and more complex discourse structures.
