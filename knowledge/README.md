# Knowledge

The runtime starts every session with the versioned core world model in
`core-world-model.json`. It is embedded in the runtime assembly, validated
through the same knowledge-pack loader as external packs, and available without
a user import or a working-directory dependency.

The core model is intentionally small and foundational. It is not a catalogue
of prewritten answers: it stores structured assertions, general rules, and typed
reference metadata that the reasoning and response layers can inspect and
compose. Reference attributes are marked as non-candidates so they do not leak
into unrelated objective comparisons.

Additional domain packs remain useful for specialized or changing information.
They extend the same session knowledge base and retain their own provenance.
They are not required for concepts that belong in the default world model.


## Foundational instrument knowledge

The core world model includes verified classifications for the Georgian panduri (string instrument) and the zurna (woodwind instrument). The International Research Center for Traditional Polyphony lists panduri among Georgian string instruments: https://polyphony.ge/en/georgia/georgian-traditional-music/musical-instruments/. The Metropolitan Museum of Art classifies zurna examples as double-reed aerophones: https://www.metmuseum.org/art/collection/search/501941. The Smithsonian Institution's Encyclopaedia of Islam describes the zurna as a characteristic woodwind instrument: https://isamveri.org/pdfdkm/23/DKM230353.pdf.
