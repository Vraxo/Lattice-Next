# Interactive program acceptance baseline

## Frozen user request

```text
Write a Python program using Pygame that plays Pong. Initialize Pygame, open a window that is 640 wide and 480 tall, and run the game at 60 frames per second.
Set paddle1_y to 210 and set paddle2_y to 210 and set ball_x to 320 and set ball_y to 240 and set ball_dx to 4 and set ball_dy to 3 and set score1 to 0 and set score2 to 0.
The player controls the left paddle with the W and S keys and the right paddle with the up and down arrow keys.
Keep the game running while score1 is less than 11 and score2 is less than 11. Set ball_x to ball_x + ball_dx and set ball_y to ball_y + ball_dy. While the W key is held, set paddle1_y to paddle1_y - 5. While the S key is held, set paddle1_y to paddle1_y + 5. While the up arrow key is held, set paddle2_y to paddle2_y - 5. While the down arrow key is held, set paddle2_y to paddle2_y + 5.
When ball_y is less than 8 and ball_dy is less than 0 then set ball_dy to -ball_dy. When ball_y is greater than 472 and ball_dy is greater than 0 then set ball_dy to -ball_dy.
When ball_x is less than 38 and ball_x is greater than 20 and ball_y is greater than paddle1_y and ball_y is less than paddle1_y + 60 and ball_dx is less than 0 then set ball_dx to -ball_dx. When ball_x is greater than 602 and ball_x is less than 620 and ball_y is greater than paddle2_y and ball_y is less than paddle2_y + 60 and ball_dx is greater than 0 then set ball_dx to -ball_dx.
When ball_x is less than 0 then set score2 to score2 + 1 and set ball_x to 320 and set ball_y to 240. When ball_x is greater than 640 then set score1 to score1 + 1 and set ball_x to 320 and set ball_y to 240.
When score1 is at least 11 then print 'player one wins'. When score2 is at least 11 then print 'player two wins'.
Clear the screen to black. Draw the left paddle as a white rectangle at x 20 and y paddle1_y with width 10 and height 60. Draw the right paddle as a white rectangle at x 610 and y paddle2_y with width 10 and height 60. Draw the ball as a white circle at x ball_x and y ball_y with radius 8. Draw score1 as a white rectangle at x 10 and y 10 with width score1 * 8 and height 10. Draw score2 as a white rectangle at x 10 and y 30 with width score2 * 8 and height 10. Present the frame.
Close the window at any time to quit the game.
```

The score is rendered as two horizontal bars (eight pixels per point), not text.
Movement is specified per frame at 60 FPS; no elapsed-time multiplication is
implied. Section 3 must still independently prove that clock-return values can
participate in arithmetic. Winning prints the specified message and ends the
loop; closing the window must also terminate and release Pygame resources.

## Runtime and test method

Target CPython 3.12 (CI), Pygame 2.6.1; locally verified with CPython 3.11.
Install and run the environment probe from the repository root:

```sh
python3 -m venv .venv-test
.venv-test/bin/python -m pip install -r tests/python/requirements.txt
.venv-test/bin/python -m unittest discover -s tests/python -p 'test_*.py'
```

The probe sets SDL video and audio drivers to `dummy` before importing Pygame.
It verifies a window surface, drawing, frame presentation, clock operation,
keyboard state, injected quit-event retrieval, and resource cleanup. It is an
**environment probe**, not a generated-game test or proof of playability.
A real display is required for the final manual two-player play check.

All future generation acceptance tests must submit this request through
`session.SubmitAsync` → `KnowledgeDrivenGenerationResponder` →
`SequenceInputFactExtractor` → `SemanticProgramPlanner` →
`SequenceGenerationPipeline`, extract the returned Python fence, and execute it
without source edits. Section 5 adds incremental request-derived execution;
Section 6 adds bounded subprocess execution, deterministic input/time, behavior
assertions, and a second interactive request. Do not store a reference game or
patch generated code to make these tests pass.

## Baseline evidence and an important limitation

This is a source trace, not a successful .NET execution: no .NET SDK is available
locally. CI owns the Release build and full .NET suite.

`EnglishLexer` marks sentence-final punctuation. `TryFindMatch` stops at the
first such boundary unless a rule explicitly sets `SearchAllSentences`.
`SequenceKnowledgeBaseLoader.TryGetOptionalBoolean` defaults that flag to false.
The extractor does not otherwise iterate sentences. A source-faithful diagnostic
scan of all 245 Python rules therefore finds only five matching rules for this
request: generation intent (`write`), Python language, program output, and the
two generic JSON-round-trip intent/language cues. It does **not** capture the
24 assignments, eight conditions, or two print clauses later in the request.
An earlier diagnostic claiming 17 firing rules incorrectly crossed sentence
boundaries and is superseded. This diagnostic is not an authoritative runtime
test; Section 4 must add request-path regressions for sentence traversal.

Even after fixing traversal, the scalar mapper requires exactly one condition;
eight independent `when` clauses need ordered block projection. Existing
multi-sentence greeting tests use specialized captures and do not prove generic
statement traversal. Non-optional expression-capture failures abort extraction;
merely enabling cross-sentence matching indiscriminately is not a safe fix.

## Requirement trace: reuse versus missing

Paths below are under `src/Lattice.Core/Generation/` unless stated otherwise.
Each missing capability has an owner in the active plan. No new production
abstraction is introduced by this baseline.

| Request requirement | Existing path to reuse | Actual missing capability / owner |
| --- | --- | --- |
| Python program intent | Python sequence input rules, language selector, shared responder/planner/pipeline | No new route needed. |
| Initialize library, create 640×480 window, construct clock | `ImportRequirement`, `CallRequirement.ApiContract`, planner import/call paths, pack `emit-import` | General request import/API capture (Section 4); reusable Pygame contracts and call values (Section 3). Imports currently arise in specialized serialization/directory projection, not generic import rules. |
| Eight initial bindings, movement and score/reset assignments | Assignment requirements, set-target/value captures, semantic arithmetic, validator name checks, assignment planner/productions | Capture across sentences and preserve ordered scope (Section 4). Generic assignment capture need not be replaced by augmented assignment; that requirement already exists. |
| Continue while both scores < 11 | Boolean normalizer supports conjunction/comparisons | No continuous-loop requirement/validator/planner/header realization (Section 2); enclosing request body boundaries (Section 4). |
| Held W/S/up/down controls | Conditional requirements and expression model; nested planning effects | No key-state/constants API knowledge (Section 3); held-key interpretation and nesting (Section 4). Held keys must be sampled once per frame, not implemented as blocking inner loops. |
| Ball motion and wall/paddle collisions | Binary/unary arithmetic including negation, boolean comparisons including `is at least`, conditional planner | Ordered independent/nested condition projection (Section 4); scalar mapper currently rejects multiple conditions. Execute collision branches in Section 5. |
| Missed ball, score increment, reset | Ordinary assignments and conditionals | Correct per-clause bodies, sequencing and loop-carried dependencies (Sections 2/4); behavioral verification (Section 5). |
| Winner messages and termination at 11 | Output requirements/productions at supported depths; condition model | Correct output ownership and loop exit order (Sections 2/4); boundary execution (Section 5). |
| Black clear, white rectangles/circle, score bars, present | Semantic call plumbing, collection literals, expression renderer | Surface/draw/display contracts and constants (Section 3); ordinary request argument capture (Section 4); rendering checks (Section 5). Pygame accepts lists for size/color/rectangle arguments, so a tuple abstraction is not required. |
| 60 FPS | Arithmetic and API expression renderer hook | Clock construction/tick return contracts (Section 3), frame placement (Section 4), deterministic timing test (Section 6). |
| Close at any time and clean up | Iteration/conditional requirements for event processing | Event retrieval/quit/shutdown contracts (Section 3), request capture and exit semantics (Sections 2/4), injected-quit execution (Sections 5/6). |

### Cross-cutting boundaries

- `SemanticProgram.cs` already has `SemanticApiExpression`; its renderer hook and
  `CallRequirement.ApiContract` plumbing are reuse points, but there are no
  production constructions supplying this request's contracts. Section 3 should
  complete that mechanism, not create a second API emitter.
- Dotted API names cannot simply be passed as function identifiers: pack
  identifier continuation rules and planner `IsValidName` reject dots. Existing
  dotted goal bindings do not remove those gates. Section 3 owns contract-backed
  realization rather than relaxing identifier validation indiscriminately.
- Member/method/subscript semantic constructors exist, but request expression
  parsing does not yet compose dotted/indexed expressions. Section 4 owns this
  gap. Grouping tokens in the pack alone do not establish parser support.
- Existing nested planning, iteration, imports, assignments, outputs and
  conditional productions must be reused. Check depth-specific productions:
  maximum depth is four, augmented assignments support depths 0–3, call prefixes
  0–2, and conditional headers have a narrower existing set. Sections 2/3 must
  test needed compositions and reject unsupported depths explicitly.
- There is no Pygame or game-domain input knowledge today. Add library signatures
  and generic programming language, never a game-name fact, template or handler.
- Python CI previously lacked Pygame. This increment supplies the pinned runtime
  and headless probe. Full generated-game and anti-shortcut evidence belongs to
  Sections 5/6; this document does not claim those sections are complete.

## Section 2 progress: while planning

The language-neutral while model and structural checks landed in PR #598.
PR #599 adds planning through the shared sequence pipeline and Python headers
at depths 0–2, with focused tests for body updates, nesting, unavailable names,
missing pack operations, and zero-iteration scope. The loop can update existing
bindings; names introduced only inside its body are not definite bindings after
it. Its latest implementation CI passed the Release build and full suite.
Request-derived loop capture and end-to-end execution remain unimplemented;
these unit tests do not establish natural-language loop generation.

## Section 3 progress: reusable API values

PR #601 extends the existing contract expression with ordered semantic arguments
and exact pack-declared arity. Dependencies are traversed recursively, unknown
contracts are refused, and assignment/iteration/nested-call composition is
covered by focused tests. Its full CI passed and it merged.

The Pygame contract increment declares library setup, window/clock construction,
events and quit/key constants, surface drawing/presentation, and frame timing.
Contract tests generate and run a small headless library probe through the shared
planner with changed surface names and dimensions. This semantic-tree probe is
only API validation; it does not replace Section 4 request interpretation or
Sections 5/6 game and generalization evidence. Pygame must be installed before
running the .NET tests (`python -m pip install -r tests/python/requirements.txt`);
CI now does that before its full test step.

## Natural window setup increment

Individually captured API parameters use the generic `argument0` through
`argument7` slots, associated with a call by request position and sentence span.
Slots must be contiguous and cannot be mixed with an explicit argument list for
the same call. A declarative default result binding applies only when the request
supplies no explicit result name. Pygame's window-dimensions contract uses this
mechanism for the requested width and height; there is no window or library
branch in the mapper. Tests execute changed dimensions and computed dimensions
through normal sessions. This slice does not establish the remaining timing,
held-input, drawing, and close-action interpretation of the frozen request.

## Explicit timing request increment

Natural `Create a clock` maps to the declared clock API with a default `clock`
binding (an explicit result name overrides it). `Limit frames to RATE frames per
second using NAME` and `Cap frames to RATE frames per second using NAME` map
to the existing tick contract through generic argument slots. The normal result
clause can bind elapsed milliseconds for ordinary arithmetic. Tests exercise
changed rates, computed values, changed names, repeated calls and bounded loops.
This does not yet interpret the frozen request's global frame-rate sentence as
a per-iteration scheduling constraint; that placement remains unfinished.

PR #611 verified natural surface clearing/presentation, including actual changed
color pixels and receiver names through normal-session generated Python.

## Rectangle description increment

`Draw the LABEL as a COLOR rectangle at x X and y Y with width W and height H`
maps to a reusable scalar-dimensions rectangle API contract. The descriptive
label is not interpreted as a game object; coordinates and dimensions are
ordinary expressions with normal dependency checks. The destination is `screen`
for this supported wording. Generic API invocation remains available for other
receivers. Tests check changed colors/state/dimensions and two independently
positioned calls in one sentence using actual response execution and pixels.
No paddle, score, collision, or game-specific generation knowledge is added.
PR #612 verified explicit timing requests; global frame-rate placement remains
pending alongside held-input, circle drawing, close handling and full composition.

## Circle and mixed-shape interpretation increment

`Draw LABEL as a COLOR circle at x X and y Y with radius R` uses a scalar
circle API contract and the same generic argument slots as rectangle drawing.
Coordinates/radius can reference ordinary state or arithmetic. Tests submit
mixed-shape requests through the normal session and inspect circle center,
outside, and rectangle pixels with changed colors, positions and radii.

Input rules can now require ordered `followingMatchTokens` within the current
clause (before unquoted `then` or sentence punctuation). Rectangle and circle
rules use this instead of request-global shape keywords. The generic extractor
does not know about shapes or libraries. PR #613 verified rectangle drawing.
Canonical scheduling, held-input and close handling remain unfinished.

## Keyboard acquisition increment

`Read keyboard state` / `Read pressed keys` invokes the existing declared key
state contract, defaulting to `keys` unless an explicit result name is supplied.
`Read the W key code`, `Read the S key code`, and up/down arrow equivalents
invoke declared constant contracts, defaulting to `key_code`. Ordinary indexing
and conditional semantics then inspect those values. Changed controls and
binding names are tested at interpretation and normal-session execution.
The SDL dummy driver supplies the released-state baseline; deterministic held
input and the canonical held-key action wording still require later work.
PR #614 verified mixed circle/rectangle requests and pixel execution checks.

## Early repetition exit increment

`Leave repetition` and `Stop repetition` project an ordered language-neutral
`BreakRequirement`, leaving the innermost repetition and resuming after it.
The shared validator requires an enclosing iteration and resets that context
inside declarations. Planning now applies structural validation at entry, so
direct semantic trees cannot bypass this context check. Python operations
realize early exit through the shared pipeline at the supported block depths.
Normal-session tests execute changed thresholds, nested loops and collection
iteration exits. This is a reusable prerequisite, not yet natural close-event
interpretation. PR #615 verified keyboard acquisition and result-bound branches.
