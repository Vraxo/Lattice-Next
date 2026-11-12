# Product vision: compounding capability

## The north star

> Lattice should start out doing a few simple things reliably, but its capabilities should compound. Eventually, we should be able to give it a task it wasn't specifically programmed to perform, and have it solve it by combining things it already knows how to do.

This is the central product goal. Lattice is not meant to become an ever-growing list of commands, canned workflows, supported phrasings, or individually programmed task handlers. It should become a system whose capabilities reinforce one another, so that new tasks become solvable through new combinations of existing general mechanisms.

A small system that can reliably combine what it knows is a better foundation than a large system that recognizes many inputs but can only respond along paths explicitly anticipated by its developers.

## What capability compounding means

A task should become possible because Lattice can combine reusable abilities such as:

- interpreting language and representing its meaning without losing distinctions;
- maintaining context, entities, claims, observations, and provenance;
- retrieving and combining relevant knowledge;
- applying rules and deriving conclusions across multiple steps;
- distinguishing what is supported, contradicted, unknown, or merely plausible;
- identifying missing information and asking useful questions;
- representing goals, constraints, dependencies, and possible plans;
- invoking optional tools, interpreting their sourced results, and reassessing what to do next.

These abilities should work across subjects. Domain knowledge may be supplied declaratively through knowledge packs, and external actions may be supplied through tools, but the reasoning process should not need a special branch for every domain or task.

This does not mean that every task must be solved by one universal algorithm, nor that domain-specific integrations are forbidden. It means the reusable intelligence should do the work that is genuinely general, while domain-specific content stays in data, knowledge, or replaceable capabilities wherever practical.

## How development should work

**Develop from meaningful tasks down to general capabilities, then verify by building back up to the task.**

For each proposed increment:

1. **Name the task.** Describe a useful, multi-step outcome that a person could actually ask Lattice to achieve. It should be more substantial than a trivial one-line lookup or arithmetic example.
2. **Expose the gap.** Determine what prevents the current system from completing it: semantic expressiveness, relation handling, inference, evidence management, uncertainty, planning, tool use, or something else.
3. **Build the reusable mechanism.** Implement the smallest general capability that closes a real part of the gap. Do not encode the target task as a special command, intent, or bespoke reasoning routine.
4. **Prove composition.** Demonstrate that the new mechanism works with existing capabilities in the end-to-end task. Add nearby cases that were not the motivating example.
5. **Preserve reliability.** Test exact meaning, unsupported and ambiguous cases, unknown information, contradictions, and provenance wherever relevant.

An increment should ideally satisfy both conditions:

- It makes visible progress on a meaningful task.
- It adds a capability that can be reused in other tasks or domains.

A narrow correctness fix is still worthwhile even when it does not unlock a new task. Foundational work is also acceptable when a concrete task explains why it is needed. What should be avoided is feature accumulation without a clear connection to capability.

### Things to avoid

- Expanding the grammar merely because another sentence pattern can be added.
- Adding a dedicated handler, command, or reasoning branch for every task in a growing catalog.
- Treating toy examples as the final measure of progress.
- Building speculative general-purpose frameworks before a concrete task demonstrates the need.
- Mistaking fluent responses or successful tool calls for actual reasoning.

Language remains essential, but it is an interface to meaning and reasoning—not the product goal by itself. Continue improving language when doing so enables a task or faithfully exposes a useful capability.

## A first-use proving ground: plan a day out

A first-time user should be able to try Lattice with a request that feels natural and useful, rather than a tiny factual query. One candidate is:

> I have a free Saturday, a limited budget, and no car. I would prefer somewhere quiet and outdoors. Help me put together a good day.

A small initial benchmark can provide a declarative set of possible destinations, activities, costs, travel options, and relevant constraints. Lattice should identify the goal and constraints, notice what important information is missing, ask a focused follow-up when necessary, compare feasible options, explain trade-offs, and produce a plan whose recommendations are supported by the supplied information.

The initial scenario does not need live maps, booking systems, or a real destination database. A small synthetic knowledge pack is enough to test whether Lattice can combine language, knowledge, relations, rules, constraint evaluation, context, and explanation. Live retrieval and tools can be added later as general capabilities, not as hidden requirements of a bespoke travel feature.

**The target is not a trip planner.** The target is a system that can work with goals, constraints, alternatives, and evidence in a domain-neutral way. The same mechanisms should transfer to choosing equipment, organizing an event, comparing project approaches, or making other everyday plans. The travel scenario is simply an approachable first demonstration.

This is a candidate proving ground, not a fixed commitment. Its bounded scenario, expected behavior, transfer check, and baseline protocol are specified in [the first-use benchmark](benchmarks/first-use-day-out.md). Validate its scope and the smallest useful end-to-end behavior before selecting implementation work.

## A candidate proving ground: investigate a service outage

One promising task to explore—not a fixed product commitment—is:

> A small service is failing. Given a set of component relationships, operational rules, and sourced observations, determine which components are affected, identify the best-supported upstream explanation, show the evidence and reasoning, distinguish unresolved possibilities, and say what information would be useful to check next.

For example, a declarative scenario might say that a customer-facing application depends on an API, which depends on a database. A sourced observation reports that the database became unavailable, while other observations describe the symptoms seen by the application. Lattice should be able to connect the relationships, trace the impact, explain the conclusion, and avoid claiming more than the evidence supports.

The point is **not** to implement an outage-investigation command. The scenario should be represented as ordinary entities, relations, rules, and observations. The task is interesting because it requires several capabilities to work together: multi-step relational reasoning, evidence and provenance, explanations, uncertainty, and eventually the ability to choose a useful next question or diagnostic action.

The same mechanisms could later support dependency analysis in a project, supply-chain investigation, equipment troubleshooting, or other problems where effects propagate through relationships. A small synthetic scenario is sufficient for an initial benchmark; no live infrastructure or domain-specific integration is required.

This is a candidate because it is concrete, understandable, and has a meaningful outcome. It also gives a reason to revisit currently deferred work such as relation joins and traversal. We should validate the exact scenario and scope before committing to it as the next development target.

## Measuring first-use language understanding

Lattice's natural-language development should be evaluated against a small, curated set of source prompts rather than relying primarily on developer-authored examples. The initial target is approximately 30–40 prompts, with real conversational prompts (OASST1 and WildChat), a separate 2022-era task-category proxy (Dolly 15k, informed by InstructGPT), and a separate synthetic diagnostic suite. Task usefulness must be evaluated separately from semantic interpretation. Preserve original wording and provenance, disclose source imbalance, and expand only when traceable examples improve coverage. This does not change the model-independent architecture goal: the benchmark is intended to drive reusable language and semantic capabilities, not prompt-specific handlers.

See [Real-world assistant benchmark](benchmarks/real-world-assistant-benchmark.md) for source selection, curation rules, evaluation dimensions, and the initial data-exploration workflow.

## Architectural consequences

- Keep language, semantics, context, knowledge, reasoning, planning, and tool execution distinct but composable.
- Prefer declarative facts and rules over domain-specific reasoning branches.
- Treat knowledge packs as reusable domain knowledge, not executable task implementations.
- Treat tool results as sourced observations; do not silently promote them into trusted knowledge.
- Preserve provenance so Lattice can explain why it believes a conclusion.
- Preserve open-world uncertainty: not knowing a fact is not the same as knowing its negation.
- Keep Core deterministic and independently testable where its responsibilities permit it. Optional models and tools must not become hidden sources of reasoning authority.
- Keep increments small and runnable, but evaluate progress by the tasks that their combined capabilities make possible.

The roadmap should evolve as Lattice demonstrates new abilities. This document defines the direction, not a rigid feature sequence.
