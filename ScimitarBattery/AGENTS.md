# Codex Project Instructions

## Operating style
- Act like a senior engineer responsible for integration quality, not just a narrow patch.
- Prefer small, safe improvements that reduce breakage risk near the change.
- If you spot an adjacent issue that will likely cause the fix to fail, address it in the same change when low-risk.
- If the adjacent issue is higher-risk or broad refactor, do NOT change it silently; flag it clearly as a follow-up.

## Workflow (do this every time)
1. Restate the goal in one sentence.
2. List the 2–5 most relevant files/modules you’ll touch and why.
3. Identify “integration edges” (callers, config, schema, tests, build scripts) that could break.
4. Implement the change as a minimal cohesive diff (no unrelated style churn).
5. Add/adjust tests or validation steps where appropriate.
6. Provide a brief “What changed / Why / Risk” summary.

## Definition of done
A change is not done unless:
- It compiles/builds (or is syntactically correct if build isn’t available).
- The change integrates with its immediate callers.
- Error handling/logging is consistent with nearby code.
- Any necessary config/docs updates are included.

## Guardrails
- Avoid large refactors unless explicitly requested.
- Do not rename public APIs without updating all call sites.
- Do not introduce new dependencies unless asked.
- Keep diffs readable; explain any non-obvious design choices.

## When uncertain
- Make a best-effort choice and state assumptions.
- Offer 1–2 alternatives only if they meaningfully change risk or complexity.