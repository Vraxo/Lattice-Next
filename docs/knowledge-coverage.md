# Benchmark-driven semantic knowledge coverage

## Purpose

The foundational world model is being expanded around the concepts and relationships needed by the current 34-prompt assistant benchmark. The target is one connected ontology whose concepts can be reused across subjects, not a collection of prompt-specific answer templates.

The machine-readable mapping is maintained in [assistant-benchmark-v1.json](../knowledge/coverage/assistant-benchmark-v1.json). Each entry lists the prompt, relevant concept IDs, relation IDs, coverage status, and remaining limitations.

## Coverage snapshot

The ontology now contains:

- 17 composable domain modules
- 560 concepts: 321 types and 239 individuals
- 139 relation signatures
- 861 graph assertions
- 573 lexical forms

The 34 benchmark prompts are currently mapped as:

- 18 represented at the ontology level
- 15 partially represented
- 1 not represented as static ontology knowledge

These are **knowledge-graph coverage counts**, not benchmark answer scores. A concept being present does not guarantee that the current language interpreter or responder can retrieve and explain it correctly.

## Runtime benchmark result

The latest fully executed integrated run is PR #228's head `4b98eab09adf0157685dae661cc519a2f4b13f56` (CI run 874):

- .NET build: passed
- .NET tests: 925 passed, 0 failed
- External Python tool tests: 22 passed (8 web-search and 14 web-page-fetch)
- Assistant benchmark: 34 prompts, 18 substantive answers, 20 answered, 21 handled, 13 unhandled, 0 runner errors
- Average latency: 381 ms (single run; stability not measured)

The runtime benchmark score is **18/34 (52.9%)**, up from 16/34 on the preceding integrated run. A run counts as substantive only when its benchmark record has been manually reviewed, the runtime marks it answered, and it returns a non-empty answer.

Dolly-029's output was inspected: Steve Urkel is returned with both the `neighbor-of` and `character-in` assertions, plus candidate, family, and show type-membership evidence. Dolly-023's five film examples each have both a comedy-genre assertion and a 1980s release-period assertion; the response explicitly avoids claiming an unsupported ranking. Dolly-025's ingredient list is supported by its eight ingredient assertions.

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


## Mobile-phone impact coverage

The mobile-phone graph distinguishes the device type from the possible effects of a smartphone example. Smartphones are classified as mobile phones; the example links through `may-contribute-to` to typed effects, and each effect is explicitly related to people through `affects`. The runtime query requires both the device-to-effect path and the target-type relation before composing an answer.

Evidence and interpretation notes:

- [JAMA Network Open (2025)](https://jamanetwork.com/journals/jamanetworkopen/fullarticle/2831993) reports an association between daily pre-bed screen use and poorer sleep outcomes in a large adult sample. This is cross-sectional evidence, not proof that every phone use causes sleep problems.
- [National Sleep Foundation consensus statement (2024)](https://pmc.ncbi.nlm.nih.gov/articles/PMC13181348/) describes how engaging screen content and pre-bedtime exposure may delay or disrupt sleep.
- [NHTSA distracted-driving guidance](https://www.nhtsa.gov/risky-driving/distracted-driving) documents the visual, manual, and cognitive distraction involved in phone use while driving.
- [Preventive Medicine Reports systematic review (2025)](https://pmc.ncbi.nlm.nih.gov/articles/PMC12304691/) reports an association between problematic smartphone use and lower physical activity among university students, while noting the predominance of correlational studies.

The answer therefore corrects the absolute premise: mobile phones are not inherently harmful. The graph represents potential risks whose likelihood and importance depend on use patterns; it does not model risk magnitude or prove causation.


## Dutch artist painting query

The benchmark's Dutch-artist question is represented with a typed artist hierarchy and a specific `painted-by` relation. Johannes Vermeer is classified as a Dutch artist; the artwork is classified as a painting; and the relation connects the painting to Vermeer.

Historical grounding: the [Mauritshuis collection record](https://www.mauritshuis.nl/en/our-collection/artworks/670-girl-with-a-pearl-earring) identifies the painting as a work by Johannes Vermeer and describes him as a Dutch painter. The runtime query does not retrieve a stored answer sentence; it resolves the typed relation and composes the answer from the graph.


## Sunset and nighttime outcome

The event graph links sunset to the start of nighttime, and connects that event to Earth's rotation, apparent solar motion, and the day-night cycle. The answer is derived from those relations rather than stored as a sentence.

- [U.S. Naval Observatory: Rise, Set, and Twilight Definitions](https://aa.usno.navy.mil/faq/RST_defs) explains that Earth's rotation causes the phenomena of rising and setting.
- [Lunar and Planetary Institute: Day and Night](https://www.lpi.usra.edu/education/skytellers/day_night.shtml) explains that the side of Earth facing the Sun experiences day, while the side facing away experiences night.

The model represents the general sunset-to-nighttime transition; it does not calculate local sunset times or account for latitude, season, atmospheric refraction, or polar-day conditions.


## Continental versus contiguous United States

The geographic graph distinguishes the continental United States (49 states, including Alaska) from the contiguous United States (the lower 48). The highest-point relations identify Mount McKinley (also known as Denali) for the 49-state continental region and Mount Whitney for the contiguous region.

The query responder follows those relations instead of comparing unmodeled elevation numbers. It explicitly notes the contiguous-state answer when a question uses the ambiguous phrase "continental US".

Grounding:
- [USGS, Elevations and Distances](https://pubs.usgs.gov/gip/Elevations-Distances/elvadist.html) lists Alaska's Mount McKinley and California's Mount Whitney as their respective state high points.
- [USGS, Mount Whitney Quadrangle](https://pubs.usgs.gov/bul/1760/report.pdf) identifies Mount Whitney as the highest point in the conterminous United States.
- [USGS, Extreme and Mean Altitudes](https://pubs.usgs.gov/bul/0817/report.pdf) distinguishes the continental United States (49 states) from the conterminous United States (48 states).
- [Federal Register, Executive Order 14172](https://www.govinfo.gov/content/pkg/FR-2025-01-31/pdf/2025-02096.pdf) reinstates Mount McKinley as the federal name; the mountain is also widely known as Denali.

The knowledge graph records the current federal name and retains Denali as an alternate lexical form.


## Regional golf terminology

The golf graph represents an albatross as a score of three strokes under par. It also represents double eagle as a regional equivalent used in the United States. Both terms point to the same score meaning, and the regional term has explicit golf and United States usage assertions. The responder requires all of those graph paths before answering; it does not rely on a stored answer sentence.

Grounding:
- [USGA, Golf History Questions](https://www.usga.org/history/faq--golf-history-questions-232994f0.html) describes albatross as a term for a double eagle.
- [The Double Eagle Club, FAQ](https://www.doubleeagleclub.org/frequently-asked-questions) identifies double eagle as the term used most often by American golfers.
- [International Golf Federation, Golf Glossary](https://www.igfgolf.org/pdf/media-centre/golf-glossary-for-website.pdf) cross-references albatross and double eagle.

The answer is a terminology distinction, not a claim that the terms denote different scores.


## Common ingredient queries

The full English breakfast is represented as a dish with eight asserted food-ingredient relations: egg, bacon, sausage, baked beans, mushrooms, tomatoes, toast, and black pudding. The generic `What are common <type> of <subject>?` query form resolves the requested ingredient type, returns only graph-backed targets, and includes both the ingredient relations and type memberships as evidence. It abstains for unknown dishes and unsupported target types.


Grounding for the full English breakfast ingredient list:
- [BBC Good Food — What is a full English breakfast?](https://www.bbcgoodfood.com/howto/guide/what-is-a-full-english-breakfast) identifies eggs, bacon, toast, tomatoes, baked beans, and sausages as essentials, with mushrooms and black pudding among common additions. It also notes that modern plates vary and do not always contain every component.
- [English Breakfast Society — Reference Guide](https://englishbreakfastsociety.com/reference/) describes the core components and recognises regional variation.


## 1980s comedy-film examples

The media graph already contains five film individuals with both a comedy-genre assertion and a 1980s release-period assertion. The runtime now intersects those two relations and checks that each candidate is a film. It returns examples, not a ranking: the graph has no objective quality or popularity scores.

Grounding:
- [AFI Catalog — Airplane! (1980)](https://catalog.afi.com/Film/53895-AIRPLANE) identifies the film as a comedy released in 1980.
- [Filmsite — Comedy Films](https://www.filmsite.org/comedyfilms7.html) lists 1980s examples including Ghostbusters, Ferris Bueller's Day Off, Coming to America, and A Fish Called Wanda.
- [MyShows — Comedy Movies 1980–1989](https://en.myshows.me/movies/catalog/y-1980-1989/g-comedy/) independently lists all five modeled titles in its 1980s comedy catalog.
