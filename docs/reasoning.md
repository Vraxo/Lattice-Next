# First symbolic reasoning slice

## Scope

The first reasoning implementation provides deterministic, positive reasoning over a deliberately small part of the semantic model. It demonstrates that Lattice can use parsed meaning rather than merely display it.

The current engine accepts two kinds of asserted input:

- **Ground fact:** a predicate applied to one or more individual arguments, such as `dog(fido)` or `likes(fido, milk)`. Descriptive arguments are grounded to their stable individual identifier; their head noun and modifiers are also stored as unary facts.
- **Universal rule:** one universally quantified implication with one or more unary premises and one unary conclusion. A simple rule is `forall(x, implies(dog(x), animal(x)))`; a descriptive class rule is `forall(x, implies(and(red(x), ball(x)), toy(x)))`.

A fact is ground only when its predicate identifier is non-empty and every argument is a concrete individual or a valid descriptive individual term. Descriptive terms are normalized to `IndividualTerm` values before storage. Questions are queries, not knowledge, and cannot be inserted into the knowledge base.

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

The engine preserves the source sentence behind every asserted fact and rule. A ProofNode records the conclusion, the sentence that supplied or derived it, and every supporting premise proof. A rule is never applied by matching its conditions against different individuals. When a source sentence contained a resolved pronoun, the proof also retains the referent selected by the conversation-context layer.

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

## Answer semantics

- **Yes:** the queried predicate has a direct fact or a derivation from the current facts and rules.
- **Unknown:** the engine could not find an exact fact or derive the queried unary predicate from the current knowledge base.

Unknown is not false. This implementation uses positive facts only; it has no negation, negative assertions, or closed-world assumption. It therefore does not answer No merely because it lacks a proof. It also does not yet resolve contradictions or represent a both-supported state.

## Deliberate limits

This is a first reasoning slice, not a general theorem prover. It does not currently support:

- relational rule inference, joins, or relation traversal;
- rules with multiple conclusions, disjunction, or nested formulas beyond a flat conjunction of unary predicates;
- negation, disjunction, existential quantification, or equality;
- persistent storage or loading declarative knowledge packs;
- automatic extraction of knowledge from observations or external tools.

The semantic model can represent some structures beyond this engine's current reasoning boundary. Such structures are not silently accepted as reasoning rules.

## Lifetime and boundaries

The CLI owns one in-memory KnowledgeBase for the duration of the interactive process. Accepted facts and rules remain available across turns in that process and are discarded when it ends. There is no disk persistence yet.

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
