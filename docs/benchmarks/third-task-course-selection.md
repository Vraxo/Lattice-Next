# Third-task transfer benchmark: choose a ceramics class

## Purpose

This benchmark checks whether Lattice can transfer the same objective, candidate-evidence, preference, and temporal mechanisms to a materially different domain. It is a synthetic course-selection scenario, not a live class directory. No course names, prices, schedules, or travel details describe real providers.

Use the ordinary session/runtime path and the declarative pack at `data/knowledge-packs/ceramics-course-candidates.json`. Do not add a ceramics-specific parser, command, evaluator, schema, or recommendation rule.

## User request

> Help me choose a Saturday ceramics class. I have a limited budget and no car. I would prefer a small group and beginner-friendly instruction. My budget is $180. I'm starting at Central Station, and I can leave at 9:00 and be back by 13:00.

The objective has a $180 total-cost hard constraint, no-car transport constraint, 09:00–13:00 availability window, and two independent preferences: small group and beginner-friendly instruction.

## Supplied candidates

| Candidate | All-in cost | Access | Group / level | Saturday start | Class duration |
|---|---:|---|---|---:|---:|
| Clay House Studio | $160 | Walking, 15 min each way | 8 places, beginner-friendly | 10:00 | 120 min |
| Community Kiln | $120 | Bus, 25 min each way | 14 places, beginner-friendly | 09:30 | 150 min |
| Northbank Ceramics | Not supplied | Walking, 15 min each way | 6 places, beginner-friendly | 11:00 | 90 min |
| Foundry Workshop | $100 | Car required, 20 min each way | 6 places, beginner-friendly | 10:30 | 90 min |

The pack supplies a same-day temporal plan for each candidate: travel to the class, attend the scheduled class, then travel back. The class start is constrained by the candidate's supplied start-time set and 08:00–13:00 class window. These are synthetic feasibility inputs, not verified live timetables. No bus departure schedule is supplied; the bus duration is only a stated journey duration.

## Expected behavior

1. Parse the request through the same generic objective path used for day-out and laptop comparison.
2. Keep the $180 budget, no-car constraint, 09:00–13:00 window, and both preferences separate.
3. Assess all four candidates using the shared evaluator. Foundry Workshop is excluded by the no-car constraint. The other three fit the time window; Northbank's total cost remains Unknown and should generate one candidate-specific evidence request.
4. Do not infer Northbank's price from its competitors or treat its missing price as a disadvantage beyond the unresolved budget condition.
5. Accept the user's same-session answer `Northbank Ceramics costs $150.` through the existing generic candidate-evidence path. Re-evaluate Northbank without altering unrelated candidate facts or objective conditions.
6. After the user says `Walking only.`, exclude Community Kiln, retain the budget and time window, and keep Clay House Studio and Northbank Ceramics eligible.
7. Do not invent a unique winner: Clay House and Northbank both satisfy the stated preferences. Cost is a hard ceiling, not a preference for the cheapest option.

## Transfer criterion

The benchmark passes only if the ordinary objective parser, typed knowledge pack, candidate evaluator, one-at-a-time evidence request, user-supplied monetary answer, temporal witness, preference assessment, and correction handling compose without adding a ceramics-specific workflow.
