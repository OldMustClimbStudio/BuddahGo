# Scoring - Output Quality Rubric

Use this before reporting a task complete.

## Dimensions

### 1. Correctness
| Score | Meaning |
|---|---|
| 2 | Change fixes the root cause and behavior matches the intended result |
| 1 | Change improves behavior but root cause is only partially addressed |
| 0 | Change does not fix the issue or introduces new bugs |

### 2. Scope Control
| Score | Meaning |
|---|---|
| 2 | Change stayed inside the declared manifest system and declared file scope |
| 1 | Minor scope creep but still understandable and contained |
| 0 | Scope exceeded the declared boundary or crossed systems without approval |

### 3. Safety
| Score | Meaning |
|---|---|
| 2 | No protected-file misuse and no forbidden patterns introduced |
| 1 | A high-risk file was touched with valid justification and review |
| 0 | A forbidden action or unsafe boundary violation occurred |

### 4. Validation
| Score | Meaning |
|---|---|
| 2 | All required and conditional validation steps passed |
| 1 | Core validation passed but some non-blocking confirmation is missing |
| 0 | Validation was skipped, failed, or bypassed without recovery |

### 5. Clarity
| Score | Meaning |
|---|---|
| 2 | Report clearly states what changed, why, and what was validated |
| 1 | Report exists but is missing important context or is harder to scan |
| 0 | Report is unclear or forces the user to infer what happened |

## Thresholds
| Total | Disposition |
|---|---|
| 9-10 | Ship it |
| 7-8 | Acceptable, but note any remaining gaps |
| 5-6 | Partial, request review before merge |
| <5 | Stop, revert, and restart from the harness workflow |

## Self-Scoring Rule
- Score the task honestly before reporting done.
- If total is below 7, do not claim completion.
- If validation scored 0, go directly to `Docs/recovery.md`.
