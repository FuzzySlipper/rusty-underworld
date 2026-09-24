# Donor fidelity

Applies to ruleset/import behavior that claims Ultima Underworld precedent.
The donors are behavior references, not code sources: never port donor code,
and never let donor structure leak into our ownership.

- Verify each fidelity claim against its cited donor file path and line.
  A claim without a path is unverified, not faithful.
- Numbers, thresholds, orderings, and formulas match the donor unless the
  task explicitly records an approximation (then the task record, not the
  code comment, owns the divergence).
- Where donors disagree or the evidence is thin, the implementation says so
  rather than picking the convenient answer.
- Report concrete defects with file/line and the donor path that contradicts
  them. New scope, style, and taste are not findings.
