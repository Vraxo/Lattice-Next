# First symbolic reasoning slice

## Scope

The first reasoning implementation provides deterministic, positive inference over a deliberately small part of the semantic model. It demonstrates that Lattice can use parsed meaning rather than merely display it.

The current engine accepts two kinds of asserted input:

- **Ground fact:** a unary predicate applied to one individual, such as `dog(fido)`.
- **Universal rule:** one universally quantified implication with one unary premise and one unary conclusion, such as `forall(x, implies(dog(x), animal(x)))`.

A rule's premise and conclusion must use the same bound variable. Questions are queries, not knowledge, and cannot be inserted into the knowledge base.

## Inference

`ReasoningEngine` uses forward chaining:

1. Start with the facts stored in the current `KnowledgeBase`.
2. Find facts whose predicate matches a rule's premise.
3. Apply that rule to the same individual and add its conclusion.
4. Repeat until a full pass derives nothing new.

Known facts are deduplicated by predicate and individual. This lets rules be chained even when they were added in an order that requires more than one pass, while preventing cyclic rules from causing an endless loop.

The engine preserves the source sentence behind every asserted fact and rule. A `ProofNode` records the conclusion, the sentence that supplied or derived it, and (for an inference) the proof of its premise. The CLI presents this chain as the explanation for a supported answer.

## Answer semantics

- **Yes:** the queried predicate has a derivation from the current facts and rules.
- **Unknown:** the engine could not derive the queried predicate from the current knowledge base.

Unknown is not false. This implementation uses positive facts only; it has no negation, negative assertions, or closed-world assumption. It therefore does not answer `No` merely because it lacks a proof. It also does not yet resolve contradictions or represent a both-supported state.

## Deliberate limits

This is a first inference slice, not a general theorem prover. It does not currently support:

- predicates with more than one argument;
- rules with multiple premises, multiple conclusions, or nested formulas;
- negation, disjunction, existential quantification, or equality;
- variable-to-variable joins or relation traversal;
- persistent storage or loading declarative knowledge packs;
- automatic extraction of knowledge from observations or external tools.

The semantic model can represent some structures beyond this engine's current reasoning boundary. Such structures are not silently accepted as reasoning facts or rules.

## Lifetime and boundaries

The CLI owns one in-memory `KnowledgeBase` for the duration of the interactive process. Accepted facts and rules remain available across turns in that process and are discarded when it ends. There is no disk persistence yet.

The reasoning engine lives in `Lattice.Core`. It consumes semantic structures and does not depend on LCE parsing, Spectre.Console, a neural model, a knowledge-pack format, or external tools. The CLI remains responsible for parsing user input, storing accepted assertions in the session knowledge base, and presenting answers.

## Worked example

```text
you › Every dog is an animal.
Lattice › Stored rule.

you › Fido is a dog.
Lattice › Stored fact.

you › Is Fido an animal?
Lattice › Yes.
Because:
  • Fido is a dog.
  • Every dog is an animal.

you › Is Fido a reptile?
Lattice › Unknown.
No supporting fact or rule is known in this session.
```
