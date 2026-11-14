# Benchmark-driven semantic knowledge coverage

## Purpose

The foundational world model is being expanded around the concepts and relationships needed by the current 34-prompt assistant benchmark. The target is one connected ontology whose concepts can be reused across subjects, not a collection of prompt-specific answer templates.

The machine-readable mapping is maintained in [assistant-benchmark-v1.json](../knowledge/coverage/assistant-benchmark-v1.json). Each entry lists the prompt, relevant concept IDs, relation IDs, coverage status, and remaining limitations.

## Coverage snapshot

The ontology now contains:

- 17 composable domain modules
- 540 concepts: 310 types and 230 individuals
- 131 relation signatures
- 811 graph assertions
- 534 lexical forms

The 34 benchmark prompts are currently mapped as:

- 15 represented at the ontology level
- 17 partially represented
- 2 not represented as static ontology knowledge

These are **knowledge-graph coverage counts**, not benchmark answer scores. A concept being present does not guarantee that the current language interpreter or responder can retrieve and explain it correctly.

## Runtime benchmark result

The latest CI run completed successfully:

- .NET test suite: 876 passed, 0 failed
- External Python tool tests: passed
- Assistant benchmark: 34 prompts, 7 substantive answers, 7 answered, 8 handled, 26 unhandled, 0 runner errors
- Average latency: 259 ms (single run; stability not measured)

The benchmark result did not improve relative to the previous run. This is expected from an ontology-only expansion: the new subgraphs are now present and validated, but the current language interpreter and responder still do not reliably navigate many of them. Improving end-to-end coverage requires connecting language understanding and answer composition to these graph structures; it should not be disguised as a knowledge-count improvement.

## Cross-domain structure

The graph deliberately reuses foundational types and relations:

- Agents, organizations, places, events, processes, activities, artifacts, materials, information objects, and mathematical objects provide shared semantic anchors.
- Instrument components connect to physical objects, geometry, mechanical states, sound events, and music.
- Geography connects mountains, regions, organizations, headquarters, and economic incentives.
- Biology connects organisms, body parts, substances, adaptations, environments, and physiological processes.
- Media connects creative works, directors, episodes, seasons, characters, genres, and time periods.
- Sports and food connect records, athletes, scoring concepts, dishes, ingredients, and annual lists.
- Transportation and everyday activities connect vehicles, functions, components, processes, plans, materials, and reuse cases.
- Software connects products to capabilities and reusable comparison criteria.

For example, the Earth/day-night model links a planet to rotation, solar illumination, apparent motion, sunset, and the day/night cycle. The camel model links an animal to body parts, stored fat, water-conservation adaptations, physiological processes, and desert environment. These are graph paths, not stored explanatory paragraphs.

## What remains outside the ontology

The graph intentionally does not invent missing information or encode unstable rankings as timeless facts:

- Research citations, current software versions, current product comparisons, annual lists, and current rankings need external evidence.
- Subjective requests such as “best films” or “top companies” need criteria and should not be represented as objective rankings.
- Personal plans need user constraints and current geographic data.
- Brainstorming and creative generation require composition over the graph, not a fixed answer stored in it.
- Riddles and underspecified prompts may require discourse context or clarification.
- Several domains still have broad primitive concepts whose internal structure needs future refinement.

## Development priority

Future ontology work should be selected by benchmark coverage gaps and cross-domain reuse. New concepts should connect to existing semantic structures wherever appropriate. Avoid expanding one narrow domain deeply unless a concrete prompt or reusable inference requires it.

The next milestone is not simply adding more nodes. It is making the language and reasoning layers reliably navigate these connected subgraphs, compose answers, preserve uncertainty, and request evidence or clarification when the graph is insufficient.
