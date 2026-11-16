# Compositional language understanding

Lattice's input pipeline separates lexical recognition, shallow syntax, semantic interpretation, and execution:

1. **Normalization** canonicalizes Unicode and whitespace without discarding the original source.
2. **Lexical analysis** identifies words, quoted spans, and punctuation while preserving source offsets.
3. **Syntactic analysis** groups tokens into clauses, noun phrases, and prepositional phrases. Clauses retain their predicate, explicit subject and object, modifiers, clause type, and how an omitted subject is bound.
4. **Semantic interpretation** maps grammatical roles and domain knowledge into a semantic formula such as `TaskRequestFormula`.
5. **Reasoning and execution** consume that semantic formula; they do not inspect the original wording to decide what the user meant.

The syntax representation is intentionally shallow and deterministic. It is not a full English parser and does not claim to resolve every ambiguity. It provides reusable structure that task-specific semantic interpreters can consume instead of each interpreter independently scanning raw text for a complete prompt shape.

## First consumer: code-generation requests

The Python code-task interpreter now consumes the syntax tree. It identifies a creation clause, its code-artifact noun phrase, a Python-language mention attached to that clause, and a later output clause. These grammatical roles can appear in different orders:

- `Write a Hello World program in Python.`
- `Create a Python script that prints 'Hello, world!'.`
- `I want to create a Python program that prints Hello World.`
- `Generate a program in Python to display Hello, world.`

The output value is taken from the output clause or from the explicit “Hello World” phrase in the artifact description. A Python mention elsewhere, such as inside the text to print, does not count as the programming language. An unspecified output goal is not invented.

## Current boundaries

- Verb recognition uses the curated English relation lexicon plus a small set of common task verbs.
- Clause boundaries are recognized from common relative, infinitive, coordinating, and subordinating markers.
- Noun phrases currently use a simple determiner/modifier/head representation; nested phrases and ambiguous attachment are not fully resolved.
- Subject binding is represented as explicit, implicit, antecedent, or previous-clause, but full discourse/coreference resolution remains a separate step.
- Existing native-English semantic parsers and other task parsers are not all migrated to this shared representation yet.

The next migrations should reuse these grammatical constituents and add lexical or semantic knowledge where evidence from evaluation requires it, rather than adding more complete-sentence regex patterns.
