## Run: 2026-06-19T20:05:16Z

**Epic:** timesheets - Epic Breakdown
**Stories:** 1.1-1.9, 2.1-2.8, 3.1-3.5, 4.1-4.7

### Patterns Observed
- Direct verifier checks were essential. Several Codex and Claude sessions completed work but left the monitor sleeping at a TUI prompt; source-of-truth checks against story artifacts and sprint-status recovered safely.
- Claude review was strong at finding higher-order defects, but multiple review sessions had long idle thinking phases. Codex fallback completed one stalled review and was useful when a review needed a more direct finalize path.
- The repository's `dotnet test` path repeatedly hit local VSTest socket permission failures. The README direct xUnit v3 executable fallback remained the reliable validation path.
- Persistent modified submodule gitlinks for `Hexalith.FrontComposer` and `Hexalith.Tenants` required explicit staging exclusions throughout the run.

### Code Review Insights
- Common issues: missing pagination handling, order-dependent aggregation, incomplete metadata surfacing, brittle string coupling, stale or incomplete story File Lists, and tests that covered behavior only indirectly.
- Reviews often added the decisive regression tests after implementation was already green, especially for report/export/dashboard boundary behavior.
- Average cycles to clean: most stories completed in one review cycle; Story 4.3 required fallback review after two stalled Claude cycles.

### Timing Estimates
- create-story: usually a few minutes, longer when prior-story context was large.
- dev-story: moderate to long for Epic 4 because reports, exports, and dashboard stories touched contracts, server services, projections, metadata, and tests.
- code-review: highly variable; quick when no fixes were needed, longer when review added red/green regressions and reran direct xUnit lanes.

### Recommendations for Future Runs
- Keep the direct xUnit fallback documented and prefer it immediately when VSTest socket setup fails.
- Continue excluding known submodule pointer changes during story commits unless a story explicitly owns those submodules.
- Prefer review prompts that ask Claude to finalize promptly after validation, because completed review TUIs often remain open at a prompt.
- For reporting/export stories, require pagination, deterministic ordering, freshness, metadata privacy, and fail-closed authorization tests in the story checklist up front.

## Run: 2026-06-22T22:27:03Z

**Epic:** timesheets - Epic Breakdown
**Stories:** 1.10, 1.11, 3.6, 3.7, 4.8, 4.9, 4.10, 5.1

### Patterns Observed
- Source-of-truth checks against `sprint-status.yaml`, story status, and generated artifacts were required after several child sessions completed but remained idle at a prompt.
- Claude created strong review and retrospective output, but two dev attempts stalled without source progress; Codex fallback completed 4.10 and 5.1 dev work effectively.
- Exact-count drift remained the most common review finding until Story 5.1, where QA and review both remeasured the built executable suites.

### Code Review Insights
- Common issues: stale suite counts, incomplete File Lists, stale architecture/doc status notes, and incorrect evidence paths.
- Average cycles to clean: most resumed stories completed with one review cycle; Story 3.7 needed a second review cycle after stop-hook recovery.

### Timing Estimates
- create-story: quick for scoped repair stories, longer for 5.1 because it needed artifact synthesis across the project.
- dev-story: variable; fallback is worthwhile after several minutes of no source-of-truth progress.
- code-review: review cost is justified for documentation/evidence stories because it catches overstatement and traceability defects.

### Recommendations for Future Runs
- Keep Codex fallback available for stalled Claude dev sessions, especially documentation-heavy synthesis work.
- Continue running direct xUnit v3 executables and recording exact counts from those executables.
- Keep final launch readiness centralized in `docs/launch-readiness.md`, and update it whenever a waiver is resolved or formally accepted.
