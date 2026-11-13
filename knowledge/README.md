# Knowledge

The runtime starts every session with two complementary default knowledge resources.

- `core-world-model.json` is the legacy LCE pack. Its remaining sentence claims are retained as source-backed predicates and rules while their domains are migrated.
- `core-semantic-world-model.json` is the typed semantic model. It declares concept types and individuals, lexical entries, relation signatures, structured type definitions, and typed assertions. The instrument domain has been migrated here; its classification is derived from component and mechanism relationships rather than loaded from sentence claims.

The semantic model keeps **identity, meaning, assertions, and evidence separate**:
- A concept's identifier and label name it; they do not define it.
- A type definition links the type to parent types and equivalent structural conditions.
- A relation has declared domain and range types.
- An individual is classified by the structures it participates in. One individual may satisfy several definitions.
- Each assertion retains source provenance. Source text is evidence metadata, not the value or meaning of the assertion.

For example, a piano satisfies the keyboard-instrument definition because it has a keyboard playing interface. It also satisfies the struck-string-instrument definition because its sound-production mechanism is a struck-string mechanism. Those classifications are independent and can both be true.

## Instrument knowledge

The model represents component and mechanism relations for the piano, harmonium, accordion, and the other core instrument examples. The piano mechanism is represented as a connected sequence: its keyboard activates the action; the action strikes strings; string vibration is transmitted to the soundboard; and the soundboard radiates sound. Harmonium and accordion models connect their playing interfaces and bellows-driven free-reed mechanisms separately.

References used by the current seed include:

- [Yamaha: piano and its sound-producing mechanism](https://www.yamaha.com/en/musical_instrument_guide/piano/)
- [Yamaha: piano action](https://www.yamaha.com/en/musical_instrument_guide/piano/mechanism/mechanism003.html)
- [Yamaha: how strings, vibrations, and the soundboard produce sound](https://usa.yamaha.com/musical-instruments/pianos/explore/musical-instrument-guide/piano/mechanism/mechanism002.html)
- [Metropolitan Museum: harmonium](https://www.metmuseum.org/art/collection/search/505026)
- [Staatliches Institut für Musikforschung: accordion](https://www.simpk.de/en/museum/collection/special-exhibitions/das-akkordeon.html)
- [Yale Library: instrument families](https://yalelibrary.atlassian.net/wiki/spaces/YMD/pages/202036603)
- [European Schools: music syllabus](https://www.eursc.eu/Syllabuses/2016-10-D-14-en-5.pdf)
- [International Research Center for Traditional Polyphony: Georgian instruments](https://polyphony.ge/en/georgia/georgian-traditional-music/musical-instruments/)

The semantic format currently supports type inheritance and conjunctive existential restrictions. It is a deliberately bounded description-logic slice, not a claim that arbitrary ontology expressions or unrestricted first-order logic are implemented.

Additional domain packs remain available through the existing version 1 knowledge-pack loader. Migrating the remaining legacy core claims and prose-valued reference attributes is still outstanding; the instrument domain is the first end-to-end migration.
