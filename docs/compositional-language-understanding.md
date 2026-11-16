# Compositional language understanding

Lattice's language input is processed in layers:

1. **Normalization** canonicalizes Unicode and whitespace without discarding the original source.
2. **Lexical analysis** identifies words, quoted spans, and punctuation while preserving source offsets.
3. **Construction parsing** recognizes reusable grammatical roles such as an action, an artifact, a language, and an output operation.
4. **Semantic mapping** resolves those roles into a `TaskRequestFormula`.
5. **Reasoning and execution** consume the semantic formula; they do not inspect the original wording to decide what the user meant.

The first compositional task construction handles code-generation requests. It recognizes creation verbs, programming-language mentions, code artifacts, and output operations independently of their word order. Quoted output is retained as a value, not treated as source code.

This is an incremental foundation, not a claim of unrestricted English understanding. Existing task constructions remain in place while they are migrated onto shared lexical and semantic primitives. New constructions should add reusable grammatical and semantic knowledge rather than another complete-sentence pattern.
