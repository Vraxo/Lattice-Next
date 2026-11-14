# Conversational context and reference resolution

## Purpose

The conversation-context slice connects independently parsed utterances without making the parser responsible for deciding what a pronoun means.

LCE 0.2 introduced the singular pronoun "it" as an UnresolvedReferenceTerm. LCE 0.3 added multiple sentences in one input. LCE 0.4 adds binary relational assertions and questions. LCE 0.6 adds structured definite descriptions whose head nouns and modifiers enter the knowledge base as facts. LCE 0.8 adds indefinite existential assertions, which do not create conversational antecedents. LCE 0.9 adds one relational "it" reference in either argument position. The Core context layer still attempts pronoun resolution separately from parsing.

## Current resolution policy

This initial policy is intentionally conservative:

- If no individual has been explicitly mentioned in a supported unary assertion or yes/no question (including a supported explicitly negated predicate), or as an argument in a relational assertion or question, "it" has no known referent and Lattice asks the user to name the individual.
- If exactly one distinct individual has been explicitly mentioned, "it" resolves to that individual.
- If more than one distinct individual has been mentioned, the reference is considered ambiguous. Lattice asks the user to name the intended individual rather than guessing based on recency.

Repeated mentions of the same individual do not create ambiguity. Universal rules do not introduce an individual. Concrete named individual arguments explicitly present in an utterance enter this context, including the individual named by a supported explicit-negation assertion; inferred conclusions do not automatically become new conversational antecedents. LCE 0.8 existential assertions introduce anonymous witnesses inside the knowledge base, but those witnesses are deliberately not observed as conversational individuals and cannot resolve "it".

The policy deliberately favors correctness over convenience. It does not yet implement recency, grammatical gender, number agreement, salience, sentence-level co-reference, or clarification follow-up memory. Users can always repeat the individual's name explicitly.

## Multi-sentence turns

The parser first validates the complete input text. If any sentence is unsupported or incomplete, it returns a failure with no partial utterance list. The CLI therefore does not update conversation context or the knowledge base for a text that failed parsing.

After a successful parse, the CLI processes each utterance in source order:

1. Resolve any supported reference using the context accumulated so far.
2. If it is an assertion, add the grounded fact or rule to the knowledge base.
3. Observe the accepted assertion's individual arguments so later sentences can refer to an explicitly mentioned individual.
4. If it is a question, resolve it first, then query the reasoning engine and present the answer before moving to the next sentence.

This means an input such as "Fido is a dog. It is a mammal. Is it a mammal?" works as one turn: the first sentence establishes Fido, the second sentence resolves "it" to Fido and adds a fact, and the final question can query that fact.

A relational sentence such as "Fido likes milk." explicitly mentions both Fido and milk. Both are tracked as individual identifiers, so a later singular "it" in that session is ambiguous unless the session has only one distinct individual.

Definite descriptive noun phrases such as "the red ball" are represented with a head noun and modifiers, and retain a stable identifier (red-ball). The same phrase in a later sentence therefore addresses the same individual for storage and exact lookup. The description contributes one known individual to conversational context, not one individual per adjective. "The red ball" and "the crimson ball" remain different identifiers, and pronoun resolution still uses the conservative exactly-one-known-individual policy.

A clarification request for a sentence does not abort the rest of an already parsed turn. The unresolved sentence is not stored or queried; later sentences are still processed using the context that was successfully established by earlier accepted utterances.

## Separation of responsibilities

- The parser creates semantic meaning and leaves unresolved references explicit.
- ConversationContext observes concrete individual arguments and grounds supported unary or relational pronoun references.
- KnowledgeBase stores grounded facts, anonymous existential witnesses, and supported universal rules.
- ReasoningEngine receives grounded predicates and does not know about pronouns or conversation turns.
- The CLI composes these services and presents clarification requests.

When a pronoun assertion is resolved, the utterance keeps its original SourceText and separately records its ResolvedReferent. The knowledge base and proof trace preserve both. An explanation can therefore show the wording the user actually supplied and the individual to which the pronoun was resolved.

## Tool observations

The context also retains the latest successful `ToolObservation` separately from mentioned individuals and accepted knowledge. The observation keeps its producing tool ID and content. Deterministic intent recognizers may use a numeric observation as the antecedent for a follow-up request such as `Multiply that by 5.` A supported chain of at most eight operations, such as `Multiply that by 5, then subtract 10.`, is executed sequentially: each next invocation is constructed from the preceding successful numeric observation, and execution stops if a step fails, does not produce a numeric result, or reaches the execution limit. This is a narrow data reference, not a semantic assertion: observations do not increase the knowledge base's facts or rules and are not supplied to the reasoning engine as premises. A later successful tool invocation replaces the latest observation. Failed invocations do not replace it.

The optional `web-search` capability returns search results as one such sourced observation. Its content records the DDGS metasearch provider, the retrieval time, and each returned title, URL, domain, and snippet. These are search-provider observations, not verified facts or fetched page contents. They remain separate from knowledge and are not premises for symbolic inference. As with other tools, a later successful invocation replaces the latest observation.

## Lifetime and limitations

The CLI owns one ConversationContext for the duration of the interactive process. It is separate from the knowledge base but has the same session lifetime. It is not persisted, shared between sessions, or loaded from a knowledge pack.

The implementation supports the unary subject-position forms and one relational pronoun in either argument position listed in [language.md](language.md). A relational assertion uses `It` as its subject pronoun or lowercase `it` as its object pronoun; a relational question uses lowercase `it` in either position. A relation cannot contain two unresolved pronouns. Resolution still requires exactly one distinct known individual, regardless of argument position. This is not a general co-reference resolver; recency, salience, and richer antecedent selection remain future work.


## Family-set completion

Family-set completion is a separate structured inference path, not pronoun resolution. It combines an explicit family relation (“X's parents”), a typed anchor individual, a stated set cardinality, and the named members. The responder infers the anchor as the missing member only when its person type matches the requested family role and exactly one set position remains. This does not imply general kinship inference or unrestricted discourse resolution.
