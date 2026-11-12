# Laptop candidate comparison benchmark

## Request

> I'm looking for a laptop that will last a long time. I don't need gaming performance, but I care about screen quality, reliability, and repairability. Help me compare these three options. My budget is $1,000.

Candidates:
- Aster 14
- Birch 15
- Cedar 14

## Supplied candidate evidence

The deliberately sparse pack is `data/knowledge-packs/laptop-candidates.json`.

- All three names are candidate records; the pack does not provide prices.
- Cedar 14 has an upgrade path.
- No candidate has supplied screen-quality or reliability evidence.
- No warranty evidence is supplied. Warranty coverage must not be treated as proof of reliability.
- No evidence is supplied for Aster 14's or Birch 15's repairability.

The pack intentionally omits unsupported product claims. The goal is to test whether the same objective and typed-attribute mechanisms used by the day-out benchmark can carry over without a laptop-specific workflow or invented comparisons.

## Expected behavior

- Parse the request as an ordinary objective with a typed `$1,000` spending limit and preferences for screen quality, reliability, and repairability.
- Retain the goal and original source wording.
- Evaluate all three candidates through the same candidate-attribute evaluator.
- Keep the budget Unknown for all candidates because no prices are supplied, and explicitly identify `total-cost` as the missing hard-constraint evidence for each candidate.
- Report each preference independently: screen quality, reliability, and repairability must not collapse into one opaque status.
- Keep all three preferences Unknown for Aster 14 and Birch 15 because no matching evidence is supplied.
- Record Cedar 14's upgrade path as evidence for a repairability-related match, while leaving screen quality and reliability Unknown. The absence of those tags is not evidence that the laptop has a poor screen or is unreliable.
- Do not infer a reliability ranking, infer that an option is unreliable from missing evidence, or use warranty as a reliability proxy.
- Do not rank the candidates or invent a winner.
- Ask for the missing candidate values needed to verify the budget instead of leaving the user with a generic unresolved status.

This benchmark is intentionally a test of evidence discipline and cross-domain reuse, not a product recommendation.


## Runtime transfer checkpoint

The same native runtime path now loads this pack, retains the objective, evaluates the candidates, and exposes the evidence without a laptop-specific evaluator. Since no prices are supplied, the $1,000 hard budget remains Unknown for all three candidates and the runtime produces no eligible shortlist. Cedar 14's upgrade-path evidence is surfaced as a known detail, while screen quality, reliability, and warranty remain unsupported. This is an intentional no-recommendation result, not a failed ranking.


The runtime now reports the exact missing hard-constraint attribute: `total-cost` for Aster 14, Birch 15, and Cedar 14. It asks for those candidate values before attempting a budget comparison. This is a general evidence-gap report, not an assumption about any candidate's price.
