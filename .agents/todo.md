# Lattice — Generate a playable Pong through the generic programming system

## Goal

A user gives Lattice an ordinary natural-language request for a Pong game. Lattice interprets it into language-neutral program requirements, plans those requirements with reusable programming and API knowledge, and generates a complete, executable **Python + Pygame** game through the existing shared generation pipeline.

The result must be genuinely playable: a game window, two controllable paddles, a moving ball, wall and paddle collisions, scoring, and a working close/quit action.

**The point is not to teach Lattice what Pong is as a special case.** Every capability added here must be reusable for other programs. There must be no Pong-specific parser, fact ID, semantic requirement, planner branch, generation route, or stored program/template.

## Working rules

- Work through the sections in order. Each checkbox is one small, independently verifiable change.
- Keep each change to one behavior or one narrowly related set of declarative facts. Split a task before implementing it if it turns out to contain multiple independent changes.
- Reuse the current semantic model, planner, Python language pack, domain knowledge, and shared pipeline wherever they already support the requirement.
- Add missing programming concepts as language-neutral semantics. Add library names and signatures as reusable API knowledge, not as task-specific handlers.
- Never store a complete or partial Pong solution as a template, prompt-specific rule, or code fragment.
- Do not add a second planner, emitter, responder, or special route for games.
- Tests must enter through the same request path used by normal program-generation requests. Directly constructing a finished semantic tree is useful for unit tests, but does not prove request-to-program generation.
- Mark a task complete only after its focused tests pass and its change is merged.
- After all tasks in a section are complete, replace that section's **Result** line with one brief statement of what now works and the verification that proves it.
- Do not expand the scope to unrelated programming constructs, extra languages, or other games unless a concrete blocker requires it.

---

## 1. Freeze the target and establish the baseline

- [x] Write one canonical natural-language acceptance request for Pong, including the gameplay and controls the generated program must implement. Keep it as a user request, not pseudocode or a list of Python statements.
- [x] Verify that the chosen Python/Pygame runtime can be installed and executed in the automated test environment, including a headless display mode. Make the test setup reproducible.
- [x] Trace each requirement in the acceptance request through the existing request extractor, semantic model, validator, planner, Python pack, and API knowledge. Record only the capabilities that are genuinely missing; explicitly mark existing capabilities for reuse.

**Section exit:** The exact game request, target runtime, test method, and actual implementation gaps are known. No speculative framework or duplicate abstraction has been added.

**Result:** PR #597 merged: frozen request and reuse/gap trace recorded; pinned headless Pygame probe, Release build, and full .NET/Python checks passed CI. Generic sentence traversal remains a Section 4 gap.

## 2. Add reusable continuous-loop semantics

- [x] Add a language-neutral `WhileRequirement` containing a boolean condition and an ordered body.
- [x] Validate while conditions, bodies, referenced names, and nesting with focused positive and negative tests.
- [x] Plan a while requirement through the existing planner and shared sequence pipeline, preserving values updated by its body.
- [x] Add the Python language-pack realization for while headers at the nesting depths the existing planner supports.
- [x] Project a generically worded, request-derived while-loop into `WhileRequirement`; do not recognize game names or game-specific phrases.
- [x] Add end-to-end execution tests for loops with changed conditions, initial values, updates, and body statements.

**Section exit:** Lattice can interpret, plan, generate, and execute a generic while-loop with request-derived values and nested statements.

**Result:** PRs #598–#600 merged reusable while semantics, validation, planning, Python headers, and generic scalar request projection. Full CI passed, including five normal-session generated-Python execution cases with changed values and zero iterations. Multi-sentence and nested request-block capture remain Section 4 work.

## 3. Make external API calls composable values

- [x] Extend the semantic expression model so a declared API contract can be called with arguments and its returned value used as an expression.
- [x] Render contract-backed API-call expressions through the selected language pack. Reject undeclared contracts instead of emitting guessed call names.
- [x] Test API-call results as assignment values, loop sources, and arguments to other calls, using different arguments and variable names.
- [x] Add reusable Pygame contracts for initialization, window creation, and clock construction.
- [x] Add reusable Pygame contracts for event retrieval, quit events, keyboard state, and the supported key constants.
- [x] Add reusable Pygame contracts for clearing a surface, drawing rectangles/circles/lines, and presenting a frame.
- [x] Add the Pygame frame-timing contract and verify that the returned elapsed time can feed ordinary arithmetic and movement expressions.

**Section exit:** The generator can compose declared Pygame calls and consume their return values using the same API-contract mechanism that other libraries can use. Pygame knowledge contains reusable API facts, not game logic or source templates.

**Result:** PRs #601/#602 merged composable API values and reusable Pygame setup/input/drawing/timing knowledge. Full CI passed, including changed-name/dimension headless generated probes, injected quit events, pixel checks, and tick-result arithmetic. This does not yet prove request-to-game generation.

## 4. Interpret a complete interactive program request

- [x] Capture imports and API-contract references from the request and project them onto the existing import/call requirements.
- [x] Capture ordered initial state declarations and assignments with request-derived names and values.
- [ ] Capture nested while, collection-iteration, and conditional bodies as ordered requirement trees.
- [ ] Capture arithmetic, comparisons, member access, and indexed values as semantic expression trees for state updates and collision conditions.
- [ ] Preserve dependencies and scope across nested blocks, including values updated in one frame and read in the next.
- [ ] Add paraphrase and changed-value tests for the new request patterns. Changing dimensions, speeds, identifiers, controls, or API arguments must change the generated program accordingly.

**Section exit:** The canonical request becomes a complete, ordered, language-neutral requirement tree through normal request interpretation. No fact, rule, or mapper branch is specific to Pong.

**Result:** PRs #603–#605 merged import/API references, ordered state, and sentence-bounded conditional bodies with full CI and normal-session execution tests. Request-side member/method/index parsing is prepared using existing semantic nodes/renderers. General loop/iteration block capture, API argument/result capture, and complete interactive interpretation remain pending.

## 5. Compose and execute the game incrementally

- [ ] Generate and execute the setup slice: initialize Pygame, create the requested window, and bind the request-derived game state.
- [ ] Generate and execute the event/input slice: process quit events and read the requested controls.
- [ ] Generate and execute the movement slice: update paddle and ball positions using frame timing and request-derived speeds.
- [ ] Generate and execute the collision/scoring slice: handle screen edges, paddle hits, missed balls, score changes, and ball resets.
- [ ] Generate and execute the rendering slice: clear the screen, draw the game objects and score, and present each frame.
- [ ] Compose all slices from the original natural-language request and verify that the complete generated program runs without hand-editing or post-generation patching.

**Section exit:** The normal generation path produces a complete playable Pong game. Each gameplay behavior is composed from ordinary requirements, reusable operations, and declared Pygame APIs.

**Result:** Pending.

## 6. Prove generalization and declare the goal achieved

- [ ] Generate a second, smaller interactive program (a keyboard-controlled moving shape) using the same loop, input, timing, and drawing knowledge. Do not add new task-specific handling for it.
- [ ] Add an automated headless execution test with deterministic time and input so paddle movement, ball movement, collisions, scoring, and quit handling can be verified reliably.
- [ ] Add an anti-shortcut regression guard: production code and generation knowledge must contain no Pong-specific route, requirement, prompt rule, or stored program.
- [ ] Run the Release build, full .NET test suite, request-to-program tests, and generated-program execution tests; fix any regressions.
- [ ] Update the generation architecture/acceptance documentation to describe the actual working path and the evidence from the tests.
- [ ] **FINAL GOAL — Verify and record completion:** an ordinary Pong request reaches the normal request-response entry point and produces an executable, playable game through the one shared pipeline; the second interactive request proves the new knowledge is reusable; all anti-shortcut and behavioral checks pass.

**Section exit:** Every criterion above is satisfied. There are no remaining prerequisites hidden behind another roadmap or deferred migration. This is the completed goal.

**Result:** Pending.
