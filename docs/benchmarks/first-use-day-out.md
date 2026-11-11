# First-use benchmark: plan a day out

## Purpose

This is a bounded proving ground for Lattice's ability to combine general capabilities. It is not a travel-planning feature specification. The scenario is deliberately synthetic and self-contained: every destination, fare, timetable, and observation below belongs to the benchmark world.

The benchmark should be attempted with the ordinary user-facing language and existing session/knowledge mechanisms. Do not add a task-specific command, destination schema, travel intent, or bespoke recommendation routine to make it pass. If the current system cannot represent or complete a step, record the gap rather than silently changing the task or expected result.

## Conversation

Initial request:

> I have a free Saturday, a limited budget, and no car. I would prefer somewhere quiet and outdoors. Help me put together a good day.

The first response should identify the goal and distinguish the constraints:

- Goal: plan a worthwhile Saturday outing.
- Hard constraint: no car; options must be reachable without driving.
- Unspecified hard constraint: the actual spending limit.
- Preferences: quiet and outdoors.
- The available options and their evidence are supplied below.

A useful first clarification is:

> What is your maximum total budget, including transport?

User's answer:

> $25 total. I'll bring lunch. I'm starting at Central Station, and I can leave at 9:00 and be back by 18:00.

The system should retain the original goal and preferences, add the budget/origin/time constraints, then compare the available options. It should produce a feasible day outline, not merely list facts.

## Synthetic world

All prices are in fictional benchmark dollars. All travel times are one-way from Central Station. The published Saturday timetable below is authoritative for this exercise; do not imply that it has been checked live.

| Option | Access from Central Station | Transport cost | Entry | Saturday character | Main activity | Facilities / schedule |
|---|---|---:|---:|---|---|---|
| Fernbank Nature Reserve | Bus, 25 min each way | $6 return | Free | Usually quiet | Marked 2-hour loop | Picnic tables and toilets. Outbound buses 09:00, 10:30, 12:00; return buses 14:00, 15:30, 17:00. Open 08:00–18:00. |
| Quarry Lake Trail | Bus, 45 min each way | $12 return | Free | Very quiet | Marked 3-hour trail | Picnic area. Facilities beyond the picnic area are unspecified. Outbound buses 09:15 and 11:15; the only listed return bus is 15:45. Open 07:00–17:00. |
| Riverside Gardens | Walk, 15 min each way | Free | Free | Usually busy on Saturdays | Paved riverside paths, about 90 minutes | Picnic tables and toilets. Open 06:00–20:00. |
| Hilltop Farm Grounds | Bus, 35 min each way | $8 return | $10 | Usually quiet | Grounds walk, about 2 hours | Picnic tables. Food service is unspecified. Outbound buses 09:00, 11:00, 13:00; return buses 15:00 and 16:30. Open 09:00–17:00. |

Additional scenario facts:

- The user is bringing lunch, so do not add a food purchase to the cost.
- The stated transport prices are return fares, not one-way fares.
- No weather forecast, live disruption report, accessibility assessment, or real-world location data is available.
- “Usually quiet” and “usually busy on Saturdays” are qualitative observations, not guarantees for a particular date.
- Unspecified facilities or conditions are **unknown**, not absent.

## Expected outcome

There is no mandatory numerical score and no single magic ranking. A good answer should make its reasoning inspectable and should recognize that more than one option is feasible.

A defensible recommendation is **Fernbank Nature Reserve**:

- It is reachable without a car and fits the $25 total budget.
- Its known outing cost is $6 because entry is free and the user brings lunch.
- It directly matches both preferences: outdoors and usually quiet.
- The 09:00 outbound and 14:00 return buses allow a substantial visit within the user's time window.
- The marked loop, picnic tables, and toilets support a practical day without inventing facilities.

One supported outline is: take the 09:00 bus, complete the marked loop, have the packed lunch at the picnic tables, spend the remaining time at the reserve, and take the 14:00 return bus. The timetable gives a 25-minute journey each way; do not invent intermediate departure times or claim a precise arrival time beyond what those facts support.

The comparison should acknowledge meaningful alternatives:

- **Quarry Lake** is the quietest stated option and remains within budget at $12, but it has the longest journey and a constrained return timetable. The dataset does not establish other facilities.
- **Hilltop Farm** is quiet and feasible at $18 total, but costs more than Fernbank and has a shorter stated activity.
- **Riverside Gardens** is free and easiest to reach, but its Saturday crowd level conflicts with the quiet preference.

A recommendation may differ if it explains a coherent trade-off using only supplied evidence. Do not claim Fernbank is objectively best in every respect.

## Multi-turn revision

After receiving the recommendation, the user says:

> Actually, I don't want to take a bus. Walking only.

The system should preserve the Saturday outing goal, $25 budget, time window, and outdoor preference; replace the transport constraint with walking only; and reassess the same options.

Riverside Gardens is now the only listed walking option. A sound response may recommend it while explicitly noting that it is usually busy on Saturdays, which conflicts with the user's quiet preference. It must not keep recommending Fernbank as feasible, forget the user's other constraints, or pretend Riverside is quiet.

## Required behavior and failure cases

| Capability | Passing behavior | Failure to watch for |
|---|---|---|
| Goal and constraint interpretation | Retains the outing goal, no-car constraint, budget, origin, and time window as distinct pieces of meaning | Treats every preference as a hard constraint, or loses a hard constraint |
| Clarification | Asks for the missing budget before asserting affordability | Invents a budget or asks for information already supplied |
| Candidate comparison | Evaluates all four options against the same constraints and preferences | Selects the first matching option or relies on an opaque score |
| Arithmetic | Computes Hilltop's total as $18 and uses return fares as stated | Doubles return fares or adds lunch despite the user bringing it |
| Timetable reasoning | Uses only listed departures and return services | Invents a convenient bus or overlooks the only return service |
| Open-world uncertainty | Keeps unspecified facilities, accessibility, weather, and live service status unknown | Converts missing data into a negative or positive claim |
| Explanation and provenance | Connects each material recommendation and trade-off to supplied facts | Gives unsupported claims or cannot show why it recommended an option |
| Multi-turn context | Revises only the transport constraint and reevaluates the same options | Forgets the budget/preferences or ignores the new walking-only constraint |

A fluent answer is not sufficient. The benchmark is about faithful interpretation, supported evaluation, uncertainty, and context-sensitive revision.

## Transfer check: compare laptops

This is a later, small cross-domain sanity check—not a second implementation target. It asks whether the same general approach can handle a different kind of choice without destination-specific logic.

User:

> I'm looking for a laptop that will last a long time. I don't need gaming performance, but I care about screen quality, reliability, and repairability. Help me compare these three options. My budget is $1,000.

| Option | Price | Display | Memory | Storage | Warranty | Reliability evidence |
|---|---:|---|---|---|---|---|
| Aster 14 | $900 | 14-inch matte IPS, 350 nits | 16 GB soldered | Replaceable SSD | 2 years | No independent long-term reliability data supplied |
| Birch 15 | $950 | 15.6-inch glossy OLED, 400 nits | 16 GB soldered | Replaceable SSD | 1 year | No independent long-term reliability data supplied |
| Cedar 14 | $850 | 14-inch matte IPS, 300 nits | 8 GB soldered plus one SO-DIMM slot | Replaceable SSD | 2 years | No independent long-term reliability data supplied |

A passing transfer response should compare the actual trade-offs, respect the $1,000 budget, distinguish display characteristics from subjective preference, recognize Cedar's memory upgrade path, and state that the supplied information does **not** establish which laptop is more reliable. Warranty length is evidence about warranty coverage, not proof of product reliability. It may ask which display characteristics matter most if a single recommendation depends on that preference.

The transfer check fails if the system requires a new hardcoded laptop-comparison workflow or invents reliability rankings.

## Baseline protocol

Before implementing new reasoning capabilities:

1. Run the initial request against the current native REPL and/or CLI, with no translator or external tools enabled.
2. Record the exact input, output, session setup, and any knowledge loaded. Do not paraphrase failures into successes.
3. Load or express the synthetic facts using only mechanisms that exist today. If the current knowledge-pack format or language cannot faithfully encode a required fact, record that as a baseline limitation; do not extend the format as part of this benchmark-definition step.
4. Run the clarification answer and the walking-only follow-up in the same session if the current interface permits it.
5. Run the laptop transfer prompt against the same baseline.
6. Map each observed failure to the smallest missing general capability. Only then select an implementation increment.

The original pre-objective baseline is recorded in [first-use-day-out-baseline.md](first-use-day-out-baseline.md). Current checkpoints in that file are incremental implementation observations, not claims that the complete benchmark passes.
