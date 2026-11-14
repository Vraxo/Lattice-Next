# Primitive concept audit

## Purpose

This audit establishes a default policy for the foundational semantic world model:

> **Compositional semantics by default; primitives by exception.**

A primitive is not a concept that is inherently impossible to decompose. It is a concept that the current ontology deliberately treats as foundational because no adequate, non-circular definition is presently available at the model's level of detail.

A concept must not be made defined merely by expanding its label into synonyms, by listing its known instances, or by encoding a taxonomy as a definition. A sufficient expression must identify reusable structural, functional, relational, or formal conditions that distinguish the concept.

## Baseline inventory

Audited at commit `3413245d3165a1d260fc78e9e7c558f86ee2391c`.

| Domain module | Primitive types | Defined types | Audit observation |
|---|---:|---:|---|
| Core | 1 | 0 | `entity` is the root primitive. |
| Instruments | 16 | 15 | Several mechanisms and components need richer structural vocabulary. |
| Biology | 2 | 0 | Biological structure and life-process relations are not represented yet. |
| Geometry | 12 | 4 | Existing definitions demonstrate useful compositional geometry; several primitives are mathematical foundations or lack formal relations. |
| Software | 12 | 0 | Current model mostly classifies software by asserted taxonomy and attached capabilities/domains. |
| Economics | 28 | 3 | Many concepts are roles, market structures, or economic phenomena whose defining relations/conditions are not fully modeled. |
| Language | 8 | 1 | Lexical and semantic categories are mostly represented as taxonomy, not compositional linguistic structures. |
| **Total** | **79** | **23** | **102 type definitions in the composed inventory.** |

This is an inventory of authored type definitions, not a claim that all 79 primitives are equally fundamental or equally well justified.

## Primitive-by-primitive disposition

The following is a first-pass semantic disposition, not a claim that every listed concept has a complete philosophical definition.

### Core

| Primitive | Disposition | What would be needed |
|---|---|---|
| `entity` | Keep foundational | Root of the ontology; decomposing it would require a more fundamental meta-ontology and would not currently improve domain reasoning. |

### Instruments

| Primitive | Disposition | What would be needed |
|---|---|---|
| `physical-object` | Keep for now | Spatial extent, material constitution, persistence, and physical-boundary concepts. |
| `physical-process` | Keep for now | Participants, temporal intervals, change, and process phases. |
| `sound-production-mechanism` | Candidate for later decomposition | Reusable mechanism, component, energy-transfer, and causal-operation structures. |
| `playing-interface` | Candidate for later decomposition | Input/control roles and the relation between performer action and mechanism activation. |
| `keyboard-interface` | Candidate for later decomposition | Key components, their arrangement, and key-to-action activation. |
| `string-sound-mechanism` | Keep as an abstract grouping until its children are structurally defined | Shared string excitation and vibration concepts; do not define it as an arbitrary enumeration of its current subtypes. |
| `plucked-string-mechanism` | Candidate; missing relation | A reusable plucking interaction between an actuator and a string. |
| `bowed-string-mechanism` | Candidate; missing relation | Bow/string contact, sustained friction, and excitation. |
| `percussion-mechanism` | Candidate; underspecified | Impact/excitation structure that distinguishes percussion from other striking mechanisms. |
| `woodwind-mechanism` | Candidate; missing structure | Air-column, airflow, and excitation structures (edge, reed, or other embouchure mechanisms). |
| `physical-component` | Keep for now | A general part/whole and physical-constitution model. The current `contains` relation is not enough to define componenthood without circularity. |
| `sound-event` | Keep for now | Acoustic properties and propagation: frequency, amplitude, waveform, duration, and medium. |
| `mechanical-state` | Keep as an abstract state category | State-bearing entities and state/value semantics. |
| `tensioned-state` | Candidate; missing property representation | A tension/force property or measurable mechanical condition. |
| `hammer-component` | Candidate; missing internal structure | Hammer head, shank/stem, attachment, and the assembly relation. The current model only identifies it as the component actuated by a sound-production mechanism; its head/shank structure is not represented yet. |
| `piano-hammer-action` | **Decomposed in this change** | Defined by actuating a hammer component that strikes a string component. The example graph supplies both links. |

The piano decomposition uses the general `actuates` and `strikes` relations. No special-purpose `actuates-hammer` relation is introduced. `string-component` already has a compositional definition; `hammer-component` remains a clearly marked primitive until head, shank, and assembly structure are represented.

### Biology

| Primitive | Disposition | What would be needed |
|---|---|---|
| `animal` | Keep for now | Organism, metabolism, growth, reproduction, and biological organization. |
| `fish` | Candidate; currently taxonomy-only | Aquatic adaptation and anatomical/physiological features such as gills and fins, represented as reusable structures rather than a list of fish instances. |

### Geometry

| Primitive | Disposition | What would be needed |
|---|---|---|
| `geometric-object` | Keep foundational within geometry | A formal distinction between mathematical objects and physical entities. |
| `geometric-shape` | Candidate for review | A general boundary/region model; avoid assuming every shape is a closed plane figure. |
| `plane-figure` | Keep until planar embedding is modeled | A plane/planarity relation and dimensional constraints. |
| `geometric-structure` | Keep as an abstract structural category | General composition and incidence semantics beyond the existing boundary slice. |
| `geometric-element` | Keep as an abstract category | A principled account of geometric constituents, not merely a disjunction of currently known point and segment types. |
| `geometric-point` | Keep foundational | A point is an idealized primitive in the current geometric formalization. |
| `geometric-property` | Keep as an abstract property category | Property/value semantics and applicable measurement domains. |
| `straightness` | Candidate for formalization, not a prose definition | A geometric predicate or invariant grounded in the formal geometry model. |
| `topological-configuration` | Keep as an abstract category | Incidence, adjacency, and connectivity relations. |
| `closed-cycle` | Candidate; missing relations | Ordered/connected path structure and an explicit closure condition. |
| `linguistic-category` | Revisit module ownership | This belongs in the language ontology; its current presence in geometry is organizational, not a semantic reason to keep it primitive. |
| `part-of-speech` | Candidate for formalization | Explicit POS subtypes and lexical-item-to-POS classification, rather than an untyped string field alone. |

Existing defined geometry types (`boundary`, `closed-boundary`, `line-segment`, and `polygon`) are useful examples of the intended direction: their definitions use typed boundary elements, endpoints, properties, and topology.

### Software

| Primitive | Disposition | What would be needed |
|---|---|---|
| `software` | Keep for now | Executable artifact, code/module composition, and runtime behavior. |
| `open-source-software` | Candidate; missing legal structure | License, permissions, conditions, and distribution rights. |
| `python-package` | Candidate; missing packaging structure | Package metadata, modules, dependencies, and distribution identity. |
| `process-simulation-framework` | Candidate; current taxonomy is insufficient | Simulation models, process units, flowsheets, and simulation capabilities. |
| `process-modeling-framework` | Candidate; current taxonomy is insufficient | Model representation and supported modeling operations. |
| `optimization-framework` | Candidate; current taxonomy is insufficient | Decision variables, objective functions, constraints, and solver interaction. |
| `process-optimizer` | Candidate; current taxonomy is insufficient | Optimization problem representation and solving behavior. |
| `process-systems-engineering-framework` | Candidate; current taxonomy is insufficient | Composition of process modeling and optimization capabilities, plus their integration. |
| `biorefinery-modeling-framework` | Candidate; current taxonomy is insufficient | Biomass conversion/process domain structures and simulation/modeling capabilities. |
| `optimization-modeling-framework` | Candidate; current taxonomy is insufficient | Optimization model construction and mathematical-program representation. |
| `capability` | Keep as an abstract role/concept | A capability taxonomy with explicit operational criteria. |
| `application-domain` | Keep as an abstract classification concept | Domain entities and a formal account of what makes software applicable to a domain. |

### Economics

| Primitive | Disposition | What would be needed |
|---|---|---|
| `market-participant` | Candidate; missing market-role relations | Participation in exchange, supply, demand, and market membership. |
| `buyer` | Candidate; missing transaction semantics | Acquiring goods/services in exchange, with transaction roles. |
| `seller` | Candidate; missing transaction semantics | Providing goods/services in exchange, with transaction roles. |
| `dominant-buyer` | Candidate; insufficient current evidence | A market structure plus a defensible dominance criterion; do not equate it with being named as a dominant buyer. |
| `buyer-market-structure` | Candidate; missing market model | Buyer-side market participants and structural conditions. |
| `market-power` | Candidate; missing formal criteria | Ability to influence price/terms, with market and counterfactual conditions. |
| `buyer-market-power` | Candidate; missing formal criteria | Buyer-side market power conditions and mechanisms. |
| `market-power-effect` | Candidate; missing causal/economic structure | A causal outcome linked to a market-power mechanism and baseline. |
| `wage-markdown` | Candidate; missing quantitative semantics | Wage relative to marginal revenue product or another explicitly selected benchmark. |
| `labor-market` | Candidate; partial relations already exist | Employers, workers, opportunities, and market boundary; the current graph has participants/features but lacks a complete membership model. |
| `employer` | Candidate; missing employment relation semantics | Employing workers and offering employment opportunities. |
| `worker` | Candidate; missing employment relation semantics | Supplying labor under an employment relationship. |
| `worker-group` | Candidate; missing group composition | Group membership and shared labor-market properties. |
| `labor-market-feature` | Keep as an abstract feature category for now | Feature-specific criteria and measurable/relational representations. |
| `employer-concentration` | Candidate; missing quantitative structure | Employer counts/shares and a concentration measure; current reasoner lacks cardinality and numeric comparison. |
| `job-differentiation` | Candidate; missing job attributes | Job characteristics and a relation for meaningful differences. |
| `search-frictions` | Candidate; missing process model | Search processes, costs, information, and matching delays. |
| `limited-outside-options` | Candidate; partial relation exists | A worker's opportunity set and a criterion for when it is limited; this needs more than the current existence of one outside option. |
| `switching-friction` | Candidate; partial relation exists | A change-of-employer process and the costs/constraints that impede it. |
| `relocation-cost` | Candidate; missing cost structure | A move between geographic areas and its monetary/non-monetary costs. |
| `non-compete-restriction` | Candidate; missing legal/contract structure | Contract parties, scope, duration, and restriction on post-employment activity. |
| `geographic-area` | Keep as a foundational spatial concept | Spatial boundaries, containment, and adjacency. |
| `employment-opportunity` | Candidate; missing offer/vacancy structure | Employer, role, terms, availability, and eligibility. |
| `market-power-mechanism` | Candidate; needs mechanism taxonomy | A causal process that produces or sustains market power. |
| `research-publication` | Candidate; partially modeled | A document/work with authorship, publication venue, date, and bibliographic identity; the current relations already cover several of these. |
| `person` | Keep foundational in this domain for now | Identity and personhood are not represented through a reusable biological/social model. |
| `journal` | Candidate; partially modeled | A serial publication with publisher, issues, and publication identity. |
| `calendar-year` | Keep as a formal temporal unit until temporal relations are modeled | A year interval with ordering and calendar-system semantics. |

### Language

| Primitive | Disposition | What would be needed |
|---|---|---|
| `lexical-item` | Candidate for decomposition | Form, language, lexical sense, grammatical category, and expression relation. |
| `semantic-content` | Keep as an abstract semantic category | A compositional representation of referents, predicates, roles, and propositions. |
| `action-or-state` | Candidate for review | A principled event/state distinction; avoid using it as a permanent bucket if the underlying ontology can distinguish them. |
| `action` | Candidate for event decomposition | Participants, roles, temporal extent, and change. |
| `state` | Candidate for state decomposition | Bearer, property, value, and temporal validity. |
| `economic-concept` | Revisit module ownership | Move to economics as an organizing concept; its primitive status is not justified by its current hierarchy. |
| `market-structure` | Revisit module ownership | Move to economics and define through market participants and structural relations when those exist. |
| `economic-market-structure` | Revisit module ownership | Move to economics; requires formal market composition and structural criteria. |

## What can be decomposed immediately

The audit found one high-confidence additional decomposition in the existing graph: `piano-hammer-action`.

Its definition is:

```json
{
  "concept": "piano-hammer-action",
  "definitionKind": "defined",
  "necessaryConditions": [],
  "sufficientExpression": {
    "kind": "some",
    "relation": "actuates",
    "targetType": "hammer-component",
    "filler": {
      "kind": "some",
      "relation": "strikes",
      "targetType": "string-component"
    }
  }
}
```

This means there exists a physical component classified as a hammer, actuated by the mechanism, and that same component strikes a string. The current example graph asserts the generic actuation and striking relations. The string type is defined from its mechanical state and vibration-transfer role; the hammer type is still a primitive classification. Therefore the reasoner can derive:

1. `piano-action` is a `piano-hammer-action`;
2. `piano-keyboard` activates that action, so it is a `piano-keyboard-interface`;
3. `piano-example` has that interface and uses that mechanism, so it is a `piano`.

The previous explicit `instance-of piano-hammer-action` and `instance-of piano` assertions are removed. The special-purpose `actuates-hammer` relation is also removed; the definition uses the general `actuates` relation with the hammer type as its filler.

## What this audit does not authorize

- Do not define a type by enumerating its current instances or children.
- Do not turn every primitive into a defined type just to reduce a count.
- Do not invent relations whose meaning is only a renamed class label.
- Do not use existential restrictions to imply universal properties.
- Do not infer numerical thresholds, cardinalities, or necessary conditions that the current reasoner cannot express.
- Do not treat an example graph as the universal definition of its class.

## Recommended implementation order

1. **Instrument mechanics:** define the hammer-action from its actuation path (done here); then model hammer head/shank/attachment and define the hammer assembly. Add reusable plucking, bowing, airflow, and percussion interaction relations before defining those mechanisms.
2. **Geometry:** extend the existing boundary/topology model with incidence, connectivity, and planar embedding. Then revisit geometric shape, plane figure, and closed cycle.
3. **Language:** represent lexical items, senses, grammatical categories, and compositional semantic content as graph structures rather than POS strings and broad taxonomic buckets.
4. **Economics:** model transactions, employment relationships, opportunities, market membership, and quantitative market properties before defining buyer/seller, employer/worker, concentration, and market power.
5. **Software:** represent packages, modules, licenses, models, capabilities, and solver/simulation operations before deriving framework categories.
6. **Biology:** introduce organism, anatomical-part, physiological-process, and habitat relations before defining animal and fish.

This order prioritizes domains where the current graph already contains useful structure and where the next layer of reusable primitives will unlock multiple definitions.

## Reasoner capability boundary

The current semantic class-expression engine supports positive existential restrictions, nested fillers, conjunction, and disjunction. It does not currently support universal restrictions, negation in class expressions, cardinality, equality/inequality, inverse roles, or unrestricted first-order logic. Definitions must stay within that boundary, and any stronger ontology requirement should be treated as a reasoner feature request rather than approximated with misleading assertions.
