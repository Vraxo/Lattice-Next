# First symbolic reasoning slice

## Scope

The first reasoning implementation provides deterministic, positive reasoning over a deliberately small part of the semantic model. It demonstrates that Lattice can use parsed meaning rather than merely display it.

The current engine accepts three kinds of asserted input:

- **Ground fact:** a predicate applied to one or more individual arguments, such as `dog(fido)` or `likes(fido, milk)`. Definite descriptive arguments are grounded to their stable individual identifier; their head noun and modifiers are also stored as unary facts.
- **Existential assertion:** a quantified formula containing one or more unary conditions, such as `exists(x, and(red(x), ball(x), shiny(x)))`. When stored, its conditions are attached to one fresh `AnonymousIndividualTerm`.
- **Universal rule:** one universally quantified implication with one or more unary premises and one unary conclusion. A simple rule is `forall(x, implies(dog(x), animal(x)))`; a descriptive class rule is `forall(x, implies(and(red(x), ball(x)), toy(x)))`.

A ground fact has a non-empty predicate identifier and concrete individual arguments. Definite descriptive terms are normalized to `IndividualTerm` values before storage. Existential witnesses use a distinct semantic term type and never become named conversation referents. Questions are queries, not knowledge, and cannot be inserted into the knowledge base.

## Direct lookup and inference

For any ground predicate, the engine first checks for an exact stored fact. Matching includes the predicate identifier, argument count, argument order, and every individual identifier. A relational query such as `Does Fido like milk?` therefore matches `likes(fido, milk)`, but not `likes(fido, water)` or `likes(rex, milk)`.

After exact lookup, the existing forward-chaining path applies to unary predicates:

1. Start with the unary facts stored in the current KnowledgeBase.
2. For each rule, find candidate individuals that satisfy its first premise.
3. Check that every remaining premise is also established for that same individual.
4. Apply the rule only when all premises are supported, then add its conclusion.
5. Repeat until a full pass derives nothing new.

Known unary facts are deduplicated by predicate and individual. This lets rules be chained even when they were added in an order that requires more than one pass, while preventing cyclic rules from causing an endless loop. Ground relational facts are deduplicated by predicate and their ordered argument list.

Relational facts are currently **lookup-only**. They are not joined with other facts, traversed, or used by universal rules. The current language parses binary relations, while the semantic and knowledge representations can retain more than two grounded arguments.

Every asserted fact and rule retains structured source records. A source records its kind (user assertion, imported claim, or external observation), original source text, and an optional resolved referent. The default ingestion path classifies input as a user assertion; callers that ingest imported claims or observations can explicitly select the corresponding source kind.

Identical facts and rules remain deduplicated for reasoning, but each distinct source is retained. Repeating the same source does not create duplicate provenance. A fact's source collection therefore represents independent support without multiplying the semantic fact itself.

A ProofNode distinguishes asserted premises from derived conclusions. Asserted nodes carry the sources attached to the stored fact; derived nodes carry the sources attached to the rule that produced them and recursively retain their supporting premise proofs. A rule is never applied by matching its conditions against different individuals. When a source sentence contained a resolved pronoun, the proof also retains the referent selected by the conversation-context layer.

## Versioned knowledge packs

Core can load version 1 JSON knowledge packs through `KnowledgePackLoader`. A pack contains a format version, stable pack ID, display name, and an ordered array of LCE assertion strings. Reusing the controlled language keeps the pack format aligned with the semantic model instead of introducing a parallel AST serialization.

The loader validates the complete document and every claim before adding anything to the destination knowledge base. It rejects unsupported format versions, unknown or duplicate fields, malformed claims, questions, and assertions that the current reasoning engine cannot store. A failed validation leaves the destination knowledge base unchanged.

Accepted claims are classified as imported claims and retain the pack ID in their structured source records. If a claim already exists as a user assertion or another imported claim, the semantic fact or rule remains deduplicated while both sources remain available to proof explanations. Packs contain declarative data only; they cannot execute code.

The CLI loads a pack with `:load path/to/pack.json`. Loaded claims are available to ordinary queries for the rest of the current process. The current implementation does not yet save the active knowledge base back to disk, unload packs, or replace an already-loaded pack by version.

## Descriptive facts

A description such as `the red ball` has a head noun (`ball`) and a modifier (`red`), while retaining the stable individual identifier `red-ball`. When an assertion uses that description, the knowledge base records the main assertion plus unary facts for the head and each modifier. This lets ordinary unary rules operate on descriptive knowledge without adding adjective-specific inference logic.

For example:

    Every ball is a toy.
    The red ball is shiny.
    Is the red ball a toy?

The head noun fact `ball(red-ball)` and modifier fact `red(red-ball)` jointly support the rule, which derives `toy(red-ball)`. The modifier is independently queryable with `Is the red ball red?`. Each supporting fact retains the original sentence as its provenance.

A conjunctive rule can be stated directly:

    Every red ball is a toy.
    The red ball is shiny.
    Is the red ball a toy?

The rule is represented as `and(red(x), ball(x)) => toy(x)`. It fires only when both predicates are established for the same individual. A blue ball does not satisfy the rule, and a red cube does not satisfy it either.

## Existential claims and unnamed witnesses

An indefinite assertion introduces an individual without giving it a name:

    A red ball is shiny.

Its semantic form is `exists(x, and(red(x), ball(x), shiny(x)))`. When the knowledge base accepts it, the three conditions become unary facts attached to one fresh anonymous witness. That single witness can satisfy a universal rule:

    Every red ball is a toy.
    A red ball is shiny.
    Does a toy exist?

The engine derives `toy(witness)` from the red and ball conditions, so the existence question is supported. The proof retains the original existential assertion and the rule that derives the queried condition.

Existence questions require all requested conditions to be supported by one individual. They can match either an anonymous witness or a previously named individual. For example, `The red ball is shiny.` also supports `Does a red ball exist?`, because the knowledge base already knows one named individual with both properties.

Each indefinite assertion receives a fresh witness. Thus:

    A red ball is shiny.
    A blue ball is round.
    Does a red round ball exist?

is **Unknown**. The red ball witness has no round fact, and the blue ball witness has no red fact. Lattice does not combine conditions from separate existential witnesses merely because their descriptions overlap.

Anonymous witnesses are internal knowledge-base identities. They are not added to conversational context, do not resolve the pronoun `it`, and are not treated as the same individual as a definite description such as `the red ball`.

## Answer semantics

- **Yes:** the queried predicate has a direct fact or a derivation from the current facts and rules.
- **Unknown:** the engine could not find an exact fact or derive the queried unary predicate from the current knowledge base.

Unknown is not false. This implementation uses positive facts only; it has no negation, negative assertions, or closed-world assumption. It therefore does not answer No merely because it lacks a proof. It also does not yet resolve contradictions or represent a both-supported state.

## Deliberate limits

This is a first reasoning slice, not a general theorem prover. It does not currently support:

- relational rule inference, joins, or relation traversal;
- rules with multiple conclusions, disjunction, or nested formulas beyond a flat conjunction of unary predicates;
- negation, existential relational conditions, nested existential formulas, or equality;
- persistent storage or unloading/replacing already-loaded knowledge packs;
- automatic extraction of knowledge from observations or external tools.

The semantic model can represent some structures beyond this engine's current reasoning boundary. Such structures are not silently accepted as reasoning rules.

## Lifetime and boundaries

The CLI owns one in-memory KnowledgeBase for the duration of the interactive process. User assertions and claims loaded from declarative knowledge packs remain available across turns in that process and are discarded when it ends. There is no durable session persistence or pack unloading yet.

The reasoning engine lives in Lattice.Core. It consumes semantic structures and does not depend on LCE parsing, conversation context, Spectre.Console, a neural model, a knowledge-pack format, or external tools. The CLI remains responsible for parsing user input, invoking context resolution, storing accepted assertions in the session knowledge base, and presenting answers.

## Worked example

    you › Every dog is an animal.
    Lattice › Stored rule.

    you › Fido is a dog.
    Lattice › Stored fact.

    you › Is Fido an animal?
    Lattice › Yes.
    Because:
      • Fido is a dog.
      • Every dog is an animal.

    you › Fido likes milk.
    Lattice › Stored fact.

    you › Does Fido like milk?
    Lattice › Yes.
    Because:
      • Fido likes milk.

    you › Does Fido like water?
    Lattice › Unknown.
    No supporting fact or rule is known in this session.
