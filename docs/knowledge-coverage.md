# Benchmark-driven semantic knowledge coverage

## Purpose

The foundational world model is being expanded around the concepts and relationships needed by the current 34-prompt assistant benchmark. The target is one connected ontology whose concepts can be reused across subjects, not a collection of prompt-specific answer templates.

The machine-readable mapping is maintained in [assistant-benchmark-v1.json](../knowledge/coverage/assistant-benchmark-v1.json). Each entry lists the prompt, relevant concept IDs, relation IDs, coverage status, and remaining limitations.

## Coverage snapshot

The ontology now contains:

- 17 composable domain modules
- 548 concepts: 315 types and 233 individuals
- 133 relation signatures
- 826 graph assertions
- 545 lexical forms

The 34 benchmark prompts are currently mapped as:

- 16 represented at the ontology level
- 17 partially represented
- 1 not represented as static ontology knowledge

These are **knowledge-graph coverage counts**, not benchmark answer scores. A concept being present does not guarantee that the current language interpreter or responder can retrieve and explain it correctly.

## Runtime benchmark result

The latest fully executed integrated run is the PR #208 head, before the later stacked changes in PRs #209–#211:

- .NET test suite: 887 passed, 0 failed
- External Python tool tests: passed
- Assistant benchmark: 34 prompts, 7 substantive answers, 7 answered, 9 handled, 25 unhandled, 0 runner errors
- Average latency: 334 ms (single run; stability not measured)

The stacked PRs #209–#212 have not yet had an integrated .NET test run, external Python test run, or full benchmark run. PR #212 adds end-to-end tests for contextual family-set completion, but they remain unverified until CI completes. The benchmark's substantive answer count therefore remains **7/34** until the integrated stack is executed and the new response is reviewed. Ontology coverage counts above are not runtime capability scores.

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


## United Nations headquarters site

The headquarters-site graph distinguishes two contributions:

- John D. Rockefeller Jr. provided the financial gift that made the United Nations' acquisition of the Manhattan site possible.
- The City of New York contributed additional property, including parcels and property rights needed to complete the site.

Primary and institutional references:

- [United Nations, History of the UN Headquarters](https://www.un.org/sites/un2.un.org/files/1.history_of_unhq_english_2023.pdf) — records Rockefeller Jr.'s $8.5 million offer for the purchase and the City's additional property gifts.
- [United Nations document A/55/117](https://documents.un.org/api/symbol/access?l=en&s=A%2F55%2F117&t=pdf) — states that Rockefeller's $8.5 million gift enabled the UN to purchase land from private owners, while New York City contributed property rights, access, and land.
- [UN General Assembly resolution 100 (I)](https://mandates.un.org/mandate/A/RES/100%28I%29?end_year=1947&start_year=1947) — records the $8.5 million gift for acquisition of the New York tract.

The benchmark wording “Who gave the UN the land” is therefore answered with the contribution roles separated, rather than treating the cash gift and municipal land contributions as the same act.
