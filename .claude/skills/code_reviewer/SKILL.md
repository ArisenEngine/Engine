---
name: code-reviewer
description: Reviews code changes for bugs, style, and DOD performance. Use when reviewing PRs or checking code quality in the Arisen Engine.
---

# Code Reviewer SOP

When conducting code reviews, follow this step-by-step Standard Operating Procedure to ensure the Arisen Engine's performance and stability are maintained.

## 1. Preparation & Context
- Identify the review target (local changes or remote PR).
- If remote: `gh pr checkout <PR_NUMBER>`.
- Read the documentation: `Docs/GEMINI.MD` and `Docs/Architecture/ProjectManagement.md`.

## 2. Automated Verification
Run the standard verification suite before manual review:
```powershell
./Engine/Arisen/Scripts/Windows/build_workspace.bat Testing
```
> [!NOTE]
> If testing fails, prioritize reporting these failures before deep manual analysis.

## 3. Technical Review Checklist

### A. Correctness & Quality
- [ ] Does the logic match the intended feature/fix?
- [ ] Are edge cases (null, empty, timeout, concurrency) handled?
- [ ] Is error handling robust and informative?

### B. Arisen Performance Rules (CRITICAL)
- [ ] **Zero-Overhead**: Are there any managed allocations (`new`) in entity loops or simulation ticks?
- [ ] **Data-Oriented Design (DOD)**: Are interfaces being called in hot paths? (Virtual dispatch should be avoided).
- [ ] **Memory Locality**: Are components processed in contiguous flat arrays?

### C. Multi-Threading & Concurrency
- [ ] Are there any unsafe mutations of shared state outside of ECS commands?
- [ ] Is atomic synchronization used correctly where necessary?
- [ ] Avoid `lock` statements entirely in hot paths.

### D. Package Integrity
- [ ] Does the PR respect package boundaries?
- [ ] No static domain references between disconnected packages.

## 4. Providing Feedback
- Be specific about line numbers and rationale.
- Propose concrete code alternatives.
- Structure findings as: **Critical**, **Improvements**, and **Nits**.
- Conclusion: Recommendation to Approve or Request Changes.

## 5. Cleanup
- If remote: ask the user if they want to switch back to the default branch.
