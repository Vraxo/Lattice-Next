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
- Keep the budget Unknown for all candidates because no prices are supplied.
- Keep preference fit Unknown for Aster 14 and Birch 15 because no matching evidence is supplied.
- Record Cedar 14's upgrade path as evidence of a repairability-related preference match, while leaving screen quality and reliability unsupported.
- Do not infer a reliability ranking, infer that an option is unreliable from missing evidence, or use warranty as a reliability proxy.
- Do not rank the candidates or invent a winner.

This benchmark is intentionally a test of evidence discipline and cross-domain reuse, not a product recommendation.
