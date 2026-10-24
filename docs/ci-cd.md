# Timesheets CI/CD

Timesheets builds and validates five NuGet libraries through the shared
Hexalith.Builds domain workflows at immutable commit
`544fd2e907d62ccdc4b8f24168b8ab5dfcc13eb9`. CI runs on pushes and pull requests to
`main`. Publication is a separately authorized manual action.

## Dependency and test modes

The repository uses SDK `10.0.402`, Central Package Management, NuGet auditing of
direct and transitive dependencies, and warnings as errors. Local source work
selects Debug explicitly:

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false
```

Use the same dependency graph for restore, build, and the built test executables.
Compare `HexalithEventStoreVersion` in the imported catalog with the default
forwarded by the selected Works checkout. This checkout uses
`references/Hexalith.Builds/Props/Directory.Packages.props` and
`references/Hexalith.Works/Directory.Build.props`. Honor an explicit
`HexalithWorksRoot`, the references-first/sibling-second fallback, and any alternate
Builds import when locating the actual inputs. When the selected defaults differ,
pass `-p:HexalithEventStoreVersion=<catalog version>` to **both** local commands
above, then run the newly built Debug executables as documented in
[README](../README.md#build-and-test). On 2026-10-21 both defaults are `3.126.0`, so
no override is needed at this checkout. The Works forwarding-policy follow-up
remains open; do not carry a historical `3.125.1` workaround into a newer catalog.

The Timesheets Works adapter and its test project now preserve Configuration
on external source references during solution traversal. The [integration handoff results](../_bmad-output/implementation-artifacts/tests/five-integration-handoffs.md)
record this bounded correction. It does not close version propagation: NuGet
restore drops per-reference AdditionalProperties metadata, so the
conditional catalog-derived override above still applies to both commands.

Release uses published sibling NuGet packages exclusively. Both
`Directory.Solution.props` and `Directory.Build.props` default an unspecified
configuration to Release so the shared workflow's bare restore agrees with its
Release build. Invalid or contradictory modes fail with HXT0001/HXT0002.

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:UseHexalithProjectReferences=false -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Release --no-restore -warnaserror -m:1 /nr:false
```

The existing AppHost remains in the solution. Its EventStore Aspire compile
dependency follows the same source/package switch; its gateway executable source
reference is Debug-only. Its existing executable resource paths and topology are
preserved. This pipeline publishes no container or AppHost artifact.

All six projects run individually through xUnit v3/Microsoft.Testing.Platform:
ArchitectureTests, Contracts.Tests, Server.Tests, Projections.Tests,
IntegrationTests, and Works.Tests. `global.json` selects the platform runner. After
the matching Debug/source build, run each project with
`dotnet test --project <project.csproj> --configuration Debug --no-build -p:UseHexalithProjectReferences=true`,
or use the built executables in [README](../README.md#build-and-test).
The shared workflow aggregates failures; a
supplemental job validates its downloaded TRX artifact for every project and rejects
missing, empty, all-skipped, inconsistent or failing results. CI also executes the
Python release-tooling failure-path suite. The same job requires Python 3.11+,
runs the installed BMAD party-mode and forge-idea regression suites plus both
Claude mirrors in separate processes, rejects empty or entirely skipped suites, and checks mirror
contents and Copilot wrappers against the installed persona metadata. Workflow
skills do not require persona wrappers. The resource-free selected magic-link
index diagnostic suite also runs in this job. Reproduce these developer-tooling
checks locally with:

```bash
python3 -m unittest discover -s scripts/tests -v
python3 scripts/test_magic_link_index_selection.py -v
python3 -m unittest discover -s scripts -p test_isolated_magic_link_dapr.py -v
```

The blocking `apphost-configuration` job builds the AppHost in Release/package
mode and runs `check_magic_link_apphost_guards.py --configuration-only
--configuration Release`. The 35 compiled probes verify ordinary defaults,
incomplete/unsafe fixture refusal and the package build's refusal of private
compiled-gateway settings. Sanitized per-probe receipts are uploaded even on
failure. These probes exit before resources and require no Docker, Dapr or Aspire
CLI. Local developers run the same driver after the Debug/source build, using
`--configuration-only` with its default Debug selection. The Python fixture-driver
and credential-redaction regressions also run routinely in `release-tooling`.
Neither lane supplies running-topology or deployed acceptance evidence.

CodeQL, dependency review, Dependabot,
and push/PR Conventional Commit checks use pinned shared callers. Commit types
match the pinned Builds policy; dependency commits use `build(deps)`/`ci(deps)`.
Dependabot monitors NuGet, npm, Actions and only the root `.gitmodules` inventory,
so shared catalog/workflow gitlinks can receive reviewed updates without nested
submodule traversal.

Five existing IntegrationTests skips remain visible: two static infrastructure and
performance reservations, two `TIMESHEETS_PERF=1` opt-ins, and one
`TIMESHEETS_LIVE_DAPR=1` opt-in. Ordinary CI supplies neither opt-in. The CI/CD spec's
older reference to four skips predates the live-Dapr fact; no test was removed or
weakened to change that count.

## Package contract

`tools/release-packages.json` is the release inventory:

| Package ID | Project |
|---|---|
| Hexalith.Timesheets.Contracts | src/Hexalith.Timesheets.Contracts |
| Hexalith.Timesheets.Client | src/Hexalith.Timesheets.Client |
| Hexalith.Timesheets.Server | src/Hexalith.Timesheets.Server |
| Hexalith.Timesheets.Projections | src/Hexalith.Timesheets.Projections |
| Hexalith.Timesheets.Testing | src/Hexalith.Timesheets.Testing |

The packer rejects inventory drift, builds at the final version, requires an empty
output directory, and emits five primary archives, five symbol archives, and a
checksum ledger. Validation checks framework assemblies, identity/version,
README, MIT expression, dependency XML, restore-backed dependency IDs/versions,
unpublished/source leaks and unchanged archive bytes. Payloads are restricted to
the named package's net10.0 DLL/XML/PDB shape and ordinary NuGet metadata; extra
assemblies, reference/runtime payloads and other frameworks fail validation.
Dependency asset restrictions fail closed; the generated Build,Analyzers exclusion
is allowed. Legitimate platform
ServiceDefaults dependencies are allowed. The Timesheets host, AppHost,
ServiceDefaults, Works adapter, and tests are excluded.

```bash
python3 scripts/pack-release-packages.py /tmp/timesheets-candidate 0.0.0-ci-test
python3 scripts/validate-nuget-packages.py /tmp/timesheets-candidate
python3 scripts/validate-consumer-package-references.py /tmp/timesheets-candidate
python3 -m unittest discover -s scripts/tests -v
```

Consumers live outside the repository, use their own Central Package Management
and private NuGet cache, and clear inherited feeds. Each restores one exact local
candidate through PackageReference. Exact package source mappings reserve all five
candidate IDs, including transitive edges, for the local feed; nuget.org supplies
external dependencies. Validation compares each restored candidate archive byte for
byte with its local archive and requires local-feed cache provenance. Each consumer
executes a small, side-effect-free public API example and checks the assembly
informational version. Consumers add no framework references that could conceal
missing package metadata, and none uses a source ProjectReference.

## Manual protected release

An operator dispatches `release.yml` from the current green `main`. The source
gate requires successful, completed push runs of `ci.yml`, `commitlint.yml`, and
`codeql.yml` for that exact SHA on main. The protected shared
release rebuilds Release, installs the locked Node toolchain, verifies dependency
signatures, and invokes semantic-release. It does not rerun suites already proven
by exact-source CI.

Publication requires all of the following repository configuration:

- A `production` environment with required reviewers and a deployment branch
  policy admitting `main`; protect `main` and require CI, test-evidence,
  apphost-configuration, release-tooling, and commit policy checks before merging.
- Repository variable `NUGET_USER`, the nuget.org profile that created the
  Hexalith.Timesheets trusted publishing policy. The publication job in
  `release.yml` exchanges a GitHub OIDC token in the `production` environment
  for a short-lived publish key and uses that key in the same job. No stored
  NuGet API key is used. The policy must name workflow `release.yml` and
  environment `production`. Environment approval gates that job.
- Repository variable `HEXALITH_RELEASE_PUBLISH_ENABLED` exactly equal to the
  lowercase string `true`. Set it explicitly at repository scope to avoid inheriting
  an organization-wide value unintentionally. Unset/false values freeze publication.
- Actions permissions allowing the pinned reusable workflows and their actions.

The source caller reads publication enablement through GitHub's `vars` context;
the pinned shared workflow performs its own exact-string freeze immediately before
Semantic Release. No repository-variable REST read is attempted with GITHUB_TOKEN:
that endpoint requires a separate Variables permission. Source/environment
inspection uses `contents: read` and `actions: read`; API failures fail closed and
are reported as unavailable evidence. See GitHub's
[variable endpoint permissions](https://docs.github.com/en/rest/actions/variables#get-a-repository-variable)
and [environment endpoint permissions](https://docs.github.com/en/rest/deployments/environments#get-an-environment).

Semantic-release derives one version from Conventional Commits. Before creating a
tag, its verify/prepare hooks check secret availability, approved Builds inputs,
protected environment, exact green source, inventory and unoccupied NuGet versions,
then pack and validate all consumers. A frozen source/manifest/version identity and
artifact digest bind prepare to publish. Publication rechecks source/protection and
unchanged bytes at each NuGet write. Occupied versions fail; duplicate suppression
is never used. No additional issue-based release authority or container governance
is introduced.

Before the first NuGet write, logs retain the safe frozen identity (repository,
source SHA, version, Builds pin, manifest digest and artifact digest) and the full
archive SHA256 ledger. Successful GitHub releases also attach `identity.json` and
`release-artifacts.sha256` alongside the archives. A NuGet-only or failed attempt
therefore retains identity/checksum evidence in its job log even when release
assets cannot be uploaded. No credential or push command is included in that
evidence.

After a release attempt, a separate job resolves the version from the tag pointing
to the dispatched source and polls all five official NuGet indexes with bounded
retries. Missing versions, partial publication and registry failures are retained
per package and fail verification. Truncated HTTP bodies and protocol failures use
the same bounded retry path and retain package-specific failure evidence. A
successful semantic-release run that creates
no version is explicitly skipped. Partial publication requires operator diagnosis;
this implementation supplies no recovery automation. To verify a known version:

```bash
python3 scripts/verify-publication.py 1.0.0
```

`npm run release:dry-run` is read-only release analysis and still requires suitable
GitHub/release credentials and configured prerequisites. Commit, push, release
dispatch, settings changes and actual publication require separate authorization.

## Official registry and repository evidence

On 2026-10-18, the official NuGet v3 flat-container indexes returned HTTP 200 with
exact version `1.0.1` for the Works manifest's Contracts, Server, Projections,
Reactor and Testing packages. At that date, Timesheets centrally pinned Works
Contracts `1.0.1`.
The imported Builds catalog then selected EventStore `3.124.0` (Builds `544fd2e`);
package-only Release restore/build and candidate consumers validate that published
dependency graph. Correction (2026-10-19): the `references/Hexalith.Builds` catalog pin
at that date, `332b2c144defba546fe0a09626a4a72f2340e22f`, selected
EventStore `3.125.1`, while Works still forwarded `3.124.0` into source-reference
builds; `docs/launch-readiness.md` records that divergence (the workflows
still call Builds `544fd2e907d62ccdc4b8f24168b8ab5dfcc13eb9`).
Update (2026-10-20, Story 5.2 round 7): the approved root CPM pin is now Works
Contracts `1.0.2`, with unchanged nuspec dependencies from `1.0.1`. Package-mode
restore and warnings-as-errors Release build pass. The direct audit reports only
the catalog-owned EventStore `3.125.1 -> 3.126.0` update, published after the
2026-10-18 audit; changing that line belongs to the Builds catalog owner.
Update (2026-10-20, Story 5.2 round 8): at `929ea22c977385590d77d95f73af86e8b051c4fc`,
after the workspace sync moved the gitlinks, the `references/Hexalith.Builds` catalog pin
`6a38cb0f2e74977d7457fc59e6af79457ed8196a` selected EventStore `3.126.0` and Works
`64602575b902c0f8d3bfaa41a27565eee5682639` forwarded `3.126.0`. The package-mode
direct audit then reported no update; these catalog-owned values are dated
observations that later workspace syncs can move.
Timesheets' five package IDs remain unpublished until a separately authorized
release; a 404 is absence and never publication evidence. Official index URLs use
`https://api.nuget.org/v3-flatcontainer/<lowercase-package-id>/index.json`.

Read-only GitHub checks at **2026-10-18T12:00:28Z** found:

| Check | Result |
|---|---|
| `GET /repos/Hexalith/Hexalith.Timesheets/environments/production` | HTTP 404 |
| `GET /repos/Hexalith/Hexalith.Timesheets/actions/secrets` | No repository secret names |
| `GET /repos/Hexalith/Hexalith.Timesheets/actions/variables/HEXALITH_RELEASE_PUBLISH_ENABLED` | HTTP 404 |
| `GET /repos/Hexalith/Hexalith.Timesheets/branches/main/protection` | HTTP 404, branch not protected |
| `GET /repos/Hexalith/Hexalith.Timesheets/rules/branches/main` | Empty rule list |
| `GET /repos/Hexalith/Hexalith.Timesheets/actions/workflows` | Empty workflow list |

These are external activation prerequisites. Organization-secret availability was
not proven by the empty repository-secret listing. No setting, secret, branch,
submodule pointer, hosted workflow, or registry state was changed during local
implementation. This CI/CD work does not close the separate launch-readiness
failures in `docs/launch-readiness.md`.

The 2026-10-24 advisory refresh closes the original braces/micromatch finding.
[GitHub withdrew GHSA-vfj7-8cjw-p6xm on 2026-10-23](https://github.com/advisories/GHSA-vfj7-8cjw-p6xm)
because it is not practically exploitable. Today's `npm audit --json` exits 1
with three unrelated vulnerable packages: `fast-uri` and `ip-address` (moderate),
and `undici` (high). It reports no braces/micromatch finding. The original eight
high findings and the 2026-10-20 gate correction below remain historical evidence.
The [dated audit and withdrawal receipts](../_bmad-output/implementation-artifacts/tests/five-build-and-hosting-deferrals.md)
retain exact inputs and outcomes. No manifest, lockfile, or dependency version
changed for this withdrawal. The three current packages remain a separate CI/CD
release-tooling follow-up and keep package currency at **CONCERNS**; launch remains
**FAIL**.

**Historical 2026-10-18 finding and 2026-10-20 gate correction:**
The locked Node release-tooling graph passes signature/attestation verification.
The 2026-10-18 `npm audit` reported **eight high findings**, all from the upstream
unpatched braces/micromatch chain and affected semantic-release dependents.
Compatible refreshes, including bundled npm **11.21.1**, removed the critical
Handlebars finding and all bundled npm findings. The official registry still
identifies braces **3.0.3** and micromatch **4.0.8** as latest; braces is affected by
[GHSA-vfj7-8cjw-p6xm](https://github.com/advisories/GHSA-vfj7-8cjw-p6xm).
No forced downgrade or broad override was used. It was recorded as an **activation and merge
blocker**: the unchanged shared dependency-review gate rejects high-severity
introductions. Do not merge or activate this configuration as green until an
upstream fix or separately reviewed resolution exists. Correction (2026-10-20):
the configuration is already active. The workflows have run on direct pushes to
`main` since `c4695a5`, and `dependency-review.yml` triggers only on pull
requests and [evaluates only dependency changes introduced by that pull request](https://github.com/actions/dependency-review-action#overview).
The existing braces lockfile findings stay outside this gate unless a pull request
changes the affected packages; no workflow runs an npm vulnerability audit; the pinned commitlint workflow and the shared release workflow (`domain-release.yml@544fd2e907d62ccdc4b8f24168b8ab5dfcc13eb9`, called by `release.yml`) run `npm audit signatures` for provenance verification. The gate has not
evaluated these findings; the `Root npm applicability` row in
`docs/launch-readiness.md` keeps them open. The NuGet audit gate
remains enabled and is separate from Node tooling concerns.
