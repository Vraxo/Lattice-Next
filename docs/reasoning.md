# First symbolic reasoning slice

## Scope

The first reasoning implementation provides deterministic, four-valued reasoning over a deliberately small part of the semantic model, keeping explicit positive and negative evidence separate. It demonstrates that Lattice can use parsed meaning rather than merely display it.

The current engine accepts three kinds of asserted input:

- **Ground fact:** a predicate applied to one or more individual arguments, such as `dog(fido)` or `likes(fido, milk)`. Definite descriptive arguments are grounded to their stable individual identifier; their head noun and modifiers are also stored as unary facts.
- **Existential assertion:** a quantified formula containing one or more unary conditions, such as `exists(x, and(red(x), ball(x), shiny(x)))`. When stored, its conditions are attached to one fresh `AnonymousIndividualTerm`.
- **Universal rule:** one universally quantified implication with one or more unary premises (positive predicates or explicitly negated predicates), optionally one positive or explicitly negative binary relation premise to a fixed concrete object, and either one unary conclusion or one binary relation conclusion to a fixed concrete object. A simple rule is `forall(x, implies(dog(x), animal(x)))`; a descriptive class rule is `forall(x, implies(and(red(x), ball(x)), toy(x)))`; a rule with an explicit negative condition is `forall(x, implies(and(dog(x), not(cat(x))), friendly(x)))`; a relational rule is `forall(x, implies(dog(x), likes(x, milk)))`.

A ground fact has a non-empty predicate identifier and concrete individual arguments. Definite descriptive terms are normalized to `IndividualTerm` values before storage. Existential witnesses use a distinct semantic term type and never become named conversation referents. Questions are queries, not knowledge, and cannot be inserted into the knowledge base.

The knowledge base exposes its stored facts and rules through live read-only collection views. Callers must use `TryAdd` to insert claims, preserving the same validation, deduplication, and provenance behavior for every source.

## Direct lookup and inference

For any ground predicate, the engine first checks for an exact stored fact. Matching includes the predicate identifier, argument count, argument order, and every individual identifier. A relational query such as `Does Fido like milk?` therefore matches `likes(fido, milk)`, but not `likes(fido, water)` or `likes(rex, milk)`.

After exact lookup, the forward-chaining path derives unary predicates:

1. Start with the unary facts stored in the current KnowledgeBase.
2. For each rule, find candidate individuals that satisfy its first premise.
3. Check that every remaining unary premise is established for that same individual, and any fixed-object relational premise is explicitly established for that subject and exact object.
4. Apply the rule only when all premises are supported, then add its conclusion.
5. Repeat until a full pass derives nothing new.

Known unary facts are deduplicated by predicate and individual, but their proof paths are not discarded. The engine retains every distinct acyclic derivation it can construct, including direct support alongside rule-derived support. Premise alternatives are combined only when they refer to the same individual. A derivation that would repeat its own conclusion fact in its proof ancestry is rejected, preventing cyclic rules from manufacturing circular explanations. This lets rules be chained even when they were added in an order that requires more than one pass while retaining independent explanations. Ground relational facts are deduplicated by predicate and their ordered argument list, and relational conclusions likewise retain independent rule paths.

A query result exposes the first proof through the existing `Proof` property and all retained alternatives through `Proofs`. The CLI presents each independent proof separately. Duplicate source records for one stored fact or rule remain grouped as provenance on that proof rather than being represented as separate derivations.

A separate relational inference pass applies the restricted quantified relation rules after unary forward chaining reaches closure. Both unary and relational premises must be supported. A positive relational premise matches only an explicitly stored positive ground relation, while a negative relational premise matches only an explicitly stored negative ground relation; both must have the same subject and exact fixed object:

1. Find candidate subjects that satisfy the first unary premise.
2. Require every remaining unary premise to be established for that exact same subject.
3. Substitute that subject into the conclusion's first argument and retain the rule's fixed concrete object as its second argument.
4. Construct a derived proof containing every supporting unary proof and the rule's source records.

For example:

    Every red dog likes milk.
    The red dog is shiny.
    Does the red dog like milk?

The rule requires both `red(red-dog)` and `dog(red-dog)`. The proof includes the supporting description facts as premises and the universal rule as the derived conclusion. A blue dog does not satisfy the rule. Relational conclusions are not fed back into unary inference or reused as premises; this is not relational joining or traversal.

Every asserted fact and rule retains structured source records. A source records its kind (user assertion, imported claim, or external observation), original source text, and an optional resolved referent. The default ingestion path classifies input as a user assertion; callers that ingest imported claims or observations can explicitly select the corresponding source kind.

Identical facts and rules remain deduplicated for reasoning, but each distinct source is retained. Repeating the same source does not create duplicate provenance. A fact's source collection therefore represents independent support without multiplying the semantic fact itself.

A ProofNode distinguishes asserted premises from derived conclusions. Asserted nodes carry the sources attached to the stored fact; derived nodes carry the sources attached to the rule that produced them and recursively retain their supporting premise proofs. A rule is never applied by matching its conditions against different individuals. When a source sentence contained a resolved pronoun, the proof also retains the referent selected by the conversation-context layer.

## Foundational semantic world model

The runtime's foundational knowledge is loaded by composing the modules under `knowledge/domains/`, not as LCE claims in the legacy KnowledgeBase. Concept identity, type definitions, relation signatures, graph assertions, and class expressions are validated by `SemanticWorldModelLoader` and reasoned over by `SemanticWorldModelReasoner`. All asserted facts share one relation graph: `is-a` connects types, `instance-of` connects individuals to types, and ordinary relations connect their declared argument kinds. The reasoner derives transitive subtype membership and classifications from connected structures; the default model is not counted as LCE facts or rules in session status.

Version 3 foundational modules store no assertion quotations, source URLs, provenance fields, or URI-valued data properties. Evidence identifiers are generated from the asserted graph edges and definition identifiers. Older version 1 and 2 models remain loadable; their parent lists and type assertions are converted into `is-a` and `instance-of` graph edges. The version 1 `KnowledgePackLoader` remains for backward-compatible task-specific candidate data and user-provided assertions, not for authoring foundational definitions.

## Versioned knowledge packs

Core can load version 1 JSON knowledge packs through `KnowledgePackLoader`. A pack contains a format version, stable pack ID, display name, and an ordered array of LCE assertion strings. Reusing the controlled language keeps the pack format aligned with the semantic model instead of introducing a parallel AST serialization.

The loader validates the complete document and every claim before adding anything to the destination knowledge base. It rejects unsupported format versions, unknown or duplicate fields, malformed claims, questions, and assertions that the current reasoning engine cannot store. A failed validation leaves the destination knowledge base unchanged.

Accepted claims are classified as imported claims and retain the pack ID in their structured source records. If a claim already exists as a user assertion or another imported claim, the semantic fact or rule remains deduplicated while both sources remain available to proof explanations. Packs contain declarative data only; they cannot execute code.

The CLI loads a pack with `:load path/to/pack.json`. Loaded claims are available to ordinary queries for the rest of the current process. The current implementation does not yet save the active knowledge base back to disk, unload packs, or replace an already-loaded pack by version.

## Universal questions

The native-English grammar accepts direct class-wide yes/no questions such as:

    Every dog is an animal.
    Are all dogs animals?
    Is every dog an animal?

The question is represented with the same universal implication semantics as an assertion, for example
`forall(x, implies(dog(x), animal(x)))`. It is evaluated as a universal claim, not as a request to
check only the individuals currently known to the knowledge base.

A matching positive universal rule supports the answer. A rule with fewer positive class conditions
can also support a narrower question: `Every dog is an animal.` supports `Are all red dogs animals?`,
because every red dog is a dog. The reverse does not follow: `Every red dog is an animal.` does not
establish that every dog is an animal. This rule-level check is deliberately conservative; it does
not compose chains of universal rules or treat rules with additional negative or relational premises
as proof of an unrestricted class claim.

A concrete counterexample refutes the question only when the knowledge base supports every condition
of the queried class for the same individual and also supports explicit negative evidence for the
conclusion. That negative evidence may be asserted directly or derived by the existing negative
inference pass. For example:

    Fido is a dog.
    Fido is not an animal.
    Are all dogs animals?

The result is **False**, and its explanation retains both the dog membership and the negative
animal evidence. If a positive universal rule also exists, the result is **Both**; neither side is
discarded.

Known positive examples do not establish a universal rule. Even when every currently known dog is
known to be an animal, the answer remains **Unknown** unless a universal rule supports the claim.
Likewise, an explicit negative conclusion for one individual does not refute a universal question
unless that individual is also known to satisfy every condition in the queried class. Missing facts
are never treated as negative evidence.

This first question slice covers unary class conclusions. Universal questions about binary
relationships, rule-to-rule chaining, and a general quantified query language remain outside the
current boundary.

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

## Explicit negative evidence and four-valued answers

The native English grammar supports a deliberately narrow explicit-negation slice for unary predicates and exact binary relations:

    Fido is a dog.
    Fido is not a dog.
    Is Fido a dog?
    Is Fido not a dog?

    Fido likes milk.
    Fido does not like milk.
    Does Fido like milk?
    Does Fido not like milk?

These sentences map directly to a predicate or its semantic negation; the grammar does not infer negation from missing information. Negative relational evidence is stored and queried by exact predicate and ordered arguments, just like positive ground relations. It does not participate in positive rule inference. LCE 0.9 itself remains unchanged and does not gain negation as part of its controlled grammar.

Positive and negative ground assertions are stored independently with their own provenance. A negative assertion never deletes, overwrites, or weakens positive evidence. A query reports one of four evidence states:

| Truth value | Meaning |
|---|---|
| **True** | Positive evidence exists; no negative evidence is known. |
| **False** | Explicit negative evidence exists; no positive evidence is known. |
| **Both** | Positive and negative evidence both exist. |
| **Unknown** | Neither kind of evidence exists. |

A negated question reverses which side counts as supporting the question, but preserves the same underlying evidence. The CLI displays supporting and opposing proof paths separately. A contradiction is not treated as a reason to discard either side.

This is a paraconsistent evidence policy for the current slice: negative facts do not satisfy positive rule premises, and a contradiction does not cause unrelated conclusions to follow. A universal rule may require an explicit negative unary premise, such as `and(dog(x), not(cat(x))) => friendly(x)`. Positive premises are matched only against positive ground unary evidence and positive conclusions derived by rules. Negative premises are matched only against explicit negative ground unary evidence and negative conclusions derived by rules. The engine computes both polarities as separate, iterative closures, so a conclusion of either polarity can support later rules through a premise of the same polarity. Proof ancestry prevents a derivation from repeating the same predicate, individual, and polarity, so cycles cannot manufacture circular support. If both positive and negative evidence exist for a premise, the negative evidence still satisfies its negated condition; the contradiction is not erased. If only positive evidence exists, or neither polarity is known, the negated condition does not match. In particular, **Unknown is not False**, and the absence of a positive fact never creates negative evidence.

## Deliberate limits

This is a first reasoning slice, not a general theorem prover. It does not currently support:

- relational joins, relation traversal, variable-bound relation objects, or multi-variable quantified rules;
- rules with multiple conclusions, disjunction, existential relational conditions, or nested formulas beyond a flat conjunction of unary premises;
- negated passive relations, existential relational conditions, nested formulas beyond the supported existential slice, or equality;
- persistent storage or unloading/replacing already-loaded knowledge packs;
- automatic extraction of knowledge from observations or external tools.

The foundational semantic model has a separate, deliberately small class-expression language: positive existential restrictions combined with conjunction and disjunction. Primitive types are explicitly marked as primitive and may carry necessary restrictions; defined types require a sufficient expression. The class-expression reasoner evaluates these expressions over asserted relations and derived type memberships. It does not currently support universal restrictions, negation, cardinality, equality/inequality, inverse roles, or unrestricted first-order logic.

Existential restrictions may also constrain the related individual with a nested class expression. A restriction such as:

```json
{
  "kind": "some",
  "relation": "actuates",
  "targetType": "physical-component",
  "filler": {
    "kind": "some",
    "relation": "strikes",
    "targetType": "string-component"
  }
}
```

means that the subject actuates a physical component, and that same component strikes a string component. The filler is evaluated against the related individual, not against the original subject. Nested restrictions compose with the existing conjunction and disjunction operators, and their complete evidence paths are retained.

The instrument model uses this to define a struck-string mechanism from its structure rather than asserting that a particular mechanism belongs to that type. A string component is composed from five independently represented requirements: an elongated geometric form, a bending response, a tensioned mechanical state, vibration that produces sound, and transmission of vibration to a resonator. Its geometry is represented through longitudinal and cross-sectional extents, plus a longitudinal direction. A transverse direction is derived from its perpendicular relationship to a longitudinal direction. In the shared mechanics module, a tensile force is a pulling-mode force directed along a longitudinal direction; a tensile load connects that force to the component it acts on. A tensioned state is derived from a tensile load and its association with a physical component. A transverse force is derived from its direction being transverse, and a transverse load connects it to the affected component. A bending deformation has a bending mode and deforms a physical component; a bending response occurs under a transverse load, is associated with a component, and produces that deformation. The piano-string example supplies the explicit force → load → state and deformation → response paths. The model remains qualitative: it does not calculate force magnitudes, stiffness/compliance, yield limits, elastic recovery, or aspect-ratio thresholds. Its existential restrictions also cannot yet enforce that multiple role fillers refer to the same component in every possible classification.

The free-reed instrument model applies the same principle without nested restrictions: a free-reed component is defined by receiving air from a physical component and vibrating to produce a sound event; a free-reed mechanism is defined by directing air through such a component. Harmonium and accordion actions and reeds are therefore classified from their asserted airflow and vibration relationships. The instruments inherit their free-reed classification through the mechanism they use. The individual reed and mechanism memberships are not manually asserted.

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

    you › Every dog likes milk.
    Lattice › Stored rule.

    you › Fido is a dog.
    Lattice › Stored fact.

    you › Does Fido like milk?
    Lattice › Yes.
    Because:
      • Fido is a dog.
      • Every dog likes milk.

    you › Does Fido like water?
    Lattice › Unknown.
    No supporting fact or rule is known in this session.


## Explicit negative fixed-object relational premises

A universal rule may require one explicitly negative relation to a fixed object:

    Every dog that does not like milk is unfriendly.
    Fido is a dog.
    Fido does not like milk.
    Is Fido unfriendly?

The rule matches the negative relation only for the same subject, predicate, and object. A missing positive relation is not negative evidence, and a positive `Fido likes milk.` assertion does not satisfy the negative premise. Derived relational conclusions remain terminal and are not reused as relational premises.

## Explicit negative rule conclusions

A universal rule may state that its positive unary conditions support an explicit negative conclusion:

The native-English `No` quantifier expresses the same negative-universal semantics directly:

    No dogs are cats.
    Fido is a dog.
    Is Fido a cat?

This is represented as `forall(x, implies(dog(x), not(cat(x)))` and derives explicit negative evidence for Fido's cat membership. The relational form works the same way:

    No dogs like milk.
    Fido is a dog.
    Does Fido like milk?

It derives negative `likes(fido, milk)` evidence. Singular and plural noun agreement are both supported, and the negative conclusion still requires a supported universal rule with the same proof and provenance rules as other negative conclusions.

    Every dog is not a cat.
    Fido is a dog.
    Is Fido a cat?

The query is **False**, with a proof containing Fido's dog fact and the negative rule. A fixed-object relation conclusion is also supported:

    Every dog does not like milk.
    Fido is a dog.
    Does Fido like milk?

The negative relation is derived for that exact subject and object. A positive assertion can coexist with it, producing **Both** while retaining both explanations. Universal rules may use explicit negative unary premises and one explicit negative fixed-object relational premise. For example, `Every dog that does not like milk is unfriendly.` requires both a dog fact and an explicitly stored negative `likes` fact for that same individual and exact object `milk`; absence of a positive `likes` fact is insufficient. Negative evidence never satisfies a positive premise, nor does positive evidence satisfy a negative premise. Derived relational conclusions remain terminal and are not reused as relational premises. This narrow chaining does not add contraposition, closed-world reasoning, or explosion.


## Piano as a compositional instrument type

The piano is a defined **type**, separate from the `example-piano` individual used as structural evidence. Its sufficient expression requires both a piano-specific keyboard interface and a piano hammer-action mechanism.

The piano keyboard interface is defined by activating a piano hammer-action mechanism. The piano hammer-action is defined by a nested structural restriction using the general `actuates` relation: it actuates a hammer component, and that same hammer component strikes a string component. The piano action is therefore classified from its generic actuation and hammer-to-string relations, rather than from a special-purpose `actuates-hammer` relation or a direct type assertion. The piano hammer-action is a subtype of struck-string mechanism, and the piano keyboard interface is a subtype of keyboard interface.

The piano type combines the piano-specific keyboard interface and piano hammer-action mechanism. For the represented example, the reasoner derives the piano hammer-action from the action → hammer → string structure, derives the piano keyboard interface from the keyboard → action link, and derives the example's membership in the piano type from its playing-interface and sound-production-mechanism links. The example's `instance-of piano` assertion is no longer needed.

The hammer component remains primitive for now: the model does not yet represent the hammer head, shank, and their assembly as reusable structural concepts. This is an acknowledged gap, not a claim that hammers are irreducible. `string-component` is defined from its geometric form, load-induced bending response, tensioned state, sound-producing vibration, and vibration transfer to a resonator. The mechanical model distinguishes axial pulling loads from transverse loads through force modes and geometry-relative directions, but it does not yet calculate numerical forces, compliance, or elastic recovery.
