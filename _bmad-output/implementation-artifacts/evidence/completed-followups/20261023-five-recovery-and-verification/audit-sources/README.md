# Repeatable rename and source audit

Run from the Timesheets repository root with the pinned .NET SDK. These tools use
Roslyn from that SDK and read Git/source/discovery data; they do not rename files
or modify the working tree. `Naming.csproj` deliberately avoids importing the
repository's package graph. No third-party package restore is needed.

```bash
export DOTNET_CLI_HOME=/tmp/dotnet-cli-home
AUDIT=_bmad-output/implementation-artifacts/evidence/completed-followups/20261023-five-recovery-and-verification/audit-sources
EVIDENCE=_bmad-output/implementation-artifacts/evidence/completed-followups/20261023-five-recovery-and-verification
BASELINE=357db9d8c2df951a60c8ffb59c0d61c6a7bb3c8c

dotnet run --project "$AUDIT/Naming.csproj" --configuration Debug -p:AuditMode=Inventory -- "$PWD" "$BASELINE" > /tmp/five-review-inventory-baseline.json
dotnet run --project "$AUDIT/Naming.csproj" --configuration Debug -p:AuditMode=Inventory -- "$PWD" > /tmp/five-review-inventory-current.json
dotnet run --project "$AUDIT/Naming.csproj" --configuration Debug -p:AuditMode=Verify -- "$PWD" /tmp/five-review-inventory-baseline.json "$BASELINE"
python3 "$AUDIT/discover.py" /tmp/five-review-discovery-final.json
python3 "$AUDIT/compare_discovery.py" "$EVIDENCE/discovery-before.json.gz" "$EVIDENCE/discovery-after.json.gz" "$EVIDENCE/test-renames.json.gz"
python3 "$AUDIT/compare_discovery.py" "$EVIDENCE/discovery-before.json.gz" /tmp/five-review-discovery-final.json "$EVIDENCE/test-renames.json.gz"
python3 "$AUDIT/dependency_identities.py" /tmp/five-review-dependency-identities.json
```

The baseline inventory contains 786 attributed underscore methods. Current
inventory is `[]`. `Inventory.cs` refuses collisions with an existing sibling
method. `Verify.cs` compares each renamed method against the baseline after only
Roslyn identifier-token substitution, preserving exact attributes, signatures,
data, assertions and trivia apart from the four exact active references listed below (two source-fitness
literals and two complete comment trivia values). No regular expression rewrites method text. The retained original rename manifest
was collected after inserting some new tests; its source offsets are historical,
while `(File, Old, New)` identities equal the independently regenerated baseline.
The compiler supplies the final language-level collision gate.

The historical before/after discovery comparison preserves 2,546 cases with no
additional cases. Full-gate discovery preserves those same cases and adds 16
explicit regressions (2,562 total). Final supplementary discovery adds eight more
test-only cases (2,570 total, 24 additions after the rename snapshot). IDs and assembly paths are excluded because
renaming changes deterministic IDs and paths depend on the checkout; class,
method, argument display names, explicit flags, traits and skips are compared.

`dependency_identities.py` inventories every root-declared checkout, including
checkouts not used by every project. It never initializes or updates a submodule.
Canonical gitlinks, actual HEADs, dirty flags and porcelain status are retained;
all 13 dependency checkouts matched clean gitlinks for this run. The root itself
was dirty with the approved implementation batch.

The `original-tool/` text files retain the original temporary audit sources.
The portable project above removes their machine-specific SDK and manifest paths.

Two non-identifier exceptions are the source-fitness string references in
`tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/PerformanceEvidenceTests.cs`,
inside `IntegrationTestsReserveRuntimeAndPerformanceLanesWithoutEnteringFastBaseline`:

| Original literal | Approved current literal |
| --- | --- |
| `Runtime_integration_lane_is_reserved_for_eventstore_state_store_evidence` | `RuntimeIntegrationLaneIsReservedForEventstoreStateStoreEvidence` |
| `Performance_lane_is_reserved_for_launch_latency_evidence` | `PerformanceLaneIsReservedForLaunchLatencyEvidence` |

The remaining two exceptions preserve current method references inside comments:

- `tests/Hexalith.Timesheets.Projections.Tests/MagicLinkStateProjectionHandlerTests.cs`, method `IndexRebuildDropsAHashAnotherTenantAlreadyOwns`: the complete old trivia `// rather than letting either side win. Index_rebuild_replaces_only_target_tenant_slice never` becomes `// rather than letting either side win. IndexRebuildReplacesOnlyTargetTenantSlice never`.
- `tests/Hexalith.Timesheets.Server.Tests/AuthorityBoundaryTests.cs`, method `ServerAuthorityContextIsDefinedOnlyInTheServerKernel`: the complete old trivia `// type by Public_contracts_and_client_do_not_expose_server_authority_context. The host's` becomes `// type by PublicContractsAndClientDoNotExposeServerAuthorityContext. The host's`.

The validator scopes each exception to the exact file, containing method and
complete old/new literal or comment trivia text, requiring each exactly once.
All other literals, comments, tokens and trivia remain byte-exact. Output reports
all four exceptions.
