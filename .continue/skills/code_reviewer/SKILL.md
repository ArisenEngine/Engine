---
name: code-reviewer
description: Reviews code changes for bugs, style, and DOD performance. Use when reviewing PRs or checking code quality in the Arisen Engine.
---

# Code Reviewer SOP

When conducting code reviews, follow this procedure to verify correctness, package integrity, and hot-path performance.

## 1. Preparation and context
- Identify the review target: local changes or a remote PR.
- If remote: `gh pr checkout <PR_NUMBER>`.
- Read the repo workflow in `CLAUDE.md`.
- Read the architecture docs most relevant to the changed area. Start with `Arisen/Docs/Architecture/ProjectManagement.md`, then load rendering, lifecycle, service-registry, or package docs as needed.

## 2. Automated verification
Run the closest valid build or test path before deep manual review.

Main workspace examples:
```powershell
Arisen\Scripts\Windows\build_workspace.bat --config Debug --profile Development
Arisen\Scripts\Windows\build_workspace.bat --config Release --profile Production
```

Isolated package-test example:
```powershell
Arisen\Scripts\Windows\build_workspace.bat --package com.arisen.rhi.vulkan.native --config Debug
```

> [!NOTE]
> If verification fails, report the failing build or test results before spending time on deeper manual analysis.

## 3. Technical review checklist

### A. Correctness and quality
- [ ] Does the logic match the intended feature or fix?
- [ ] Are edge cases, null handling, sequencing, and concurrency expectations correct?
- [ ] Is the change internally consistent with nearby code and package contracts?

### B. Arisen performance rules
- [ ] **Zero-overhead**: no managed allocations in hot loops, simulation ticks, or render-pass execution.
- [ ] **DOD**: avoid interface dispatch and object-heavy patterns in hot paths.
- [ ] **Memory locality**: bulk data should stay contiguous and batch-friendly.

### C. Multi-threading and concurrency
- [ ] Are shared-state mutations safe and intentional?
- [ ] Are ECS commands, task-graph boundaries, or explicit synchronization used where required?
- [ ] Avoid `lock` in hot paths.

### D. Package integrity
- [ ] Does the change respect package boundaries and manifest-driven composition?
- [ ] Are service dependencies expressed through interfaces and `IServiceRegistry` instead of concrete cross-package coupling?

## 4. Providing feedback
- Be specific about file paths, line numbers, and rationale.
- Propose concrete alternatives when something should change.
- Structure findings as `Critical`, `Improvements`, and `Nits`.
- End with a clear recommendation: approve or request changes.

## 5. Cleanup
- If you checked out a remote PR branch, confirm whether the user wants to switch back afterward.