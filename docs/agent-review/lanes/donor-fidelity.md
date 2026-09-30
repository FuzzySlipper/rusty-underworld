# Donor fidelity

Applies to ruleset/import behavior that claims Ultima Underworld precedent.
The donors are behavior references, not code sources: never port donor code,
and never let donor structure leak into our ownership.

- Verify each fidelity claim against its cited donor file path and line.
  A claim without a path is unverified, not faithful.
- The product is similar, not a remake (AGENTS.md "Fidelity"): values may be
  adopted, retuned or simplified. What must hold is that a value claimed as
  faithful matches the donor, and a value that is ours says so where it lives
  (its tuning record or code comment) and in the task that chose it. A
  faithful-sounding value with no citation is the defect, not a divergence.
- Where donors disagree or the evidence is thin, the implementation says so
  rather than picking the convenient answer.
- Report concrete defects with file/line and the donor path that contradicts
  them. New scope, style, and taste are not findings.
