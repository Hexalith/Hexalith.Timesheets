# Acceptance Auditor review prompt

This is a quick-dev fallback review prompt. Review only the supplied change set according to the role below, report concrete findings with severity and file/line evidence, and distinguish real defects from pre-existing state. Do not modify files, stage changes, commit, or run remote operations.

You are the acceptance auditor. You may inspect the repository read-only. Read the approved spec and every context file listed in its frontmatter, then verify each task and Given/When/Then acceptance criterion against the diff and current state. Check the repository safeguards, submodule ownership rules, and validation evidence.

## Diff under review

```diff
diff --git a/.editorconfig b/.editorconfig
index 0ec40f4..2152d6f 100644
--- a/.editorconfig
+++ b/.editorconfig
@@ -51,5 +51,5 @@ indent_size = 2
 # root = false and does not declare a placement rule, so without this override
 # its sources inherit the repository's outside_namespace rule above and fail the
 # build under TreatWarningsAsErrors. Match the submodule's actual style instead.
-[Hexalith.PolymorphicSerializations/**.cs]
+[references/Hexalith.PolymorphicSerializations/**.cs]
 csharp_using_directive_placement = inside_namespace:silent
diff --git a/.gitmodules b/.gitmodules
index 2ce1193..b124123 100644
--- a/.gitmodules
+++ b/.gitmodules
@@ -1,33 +1,33 @@
 [submodule "Hexalith.Commons"]
-	path = Hexalith.Commons
+	path = references/Hexalith.Commons
 	url = https://github.com/Hexalith/Hexalith.Commons.git
 [submodule "Hexalith.AI.Tools"]
-	path = Hexalith.AI.Tools
+	path = references/Hexalith.AI.Tools
 	url = https://github.com/Hexalith/Hexalith.AI.Tools.git
 [submodule "Hexalith.Builds"]
 	path = references/Hexalith.Builds
 	url = https://github.com/Hexalith/Hexalith.Builds.git
 [submodule "Hexalith.Tenants"]
-	path = Hexalith.Tenants
+	path = references/Hexalith.Tenants
 	url = https://github.com/Hexalith/Hexalith.Tenants.git
 [submodule "Hexalith.EventStore"]
-	path = Hexalith.EventStore
+	path = references/Hexalith.EventStore
 	url = https://github.com/Hexalith/Hexalith.EventStore.git
 [submodule "Hexalith.FrontComposer"]
-	path = Hexalith.FrontComposer
+	path = references/Hexalith.FrontComposer
 	url = https://github.com/Hexalith/Hexalith.FrontComposer.git
 [submodule "Hexalith.PolymorphicSerializations"]
-	path = Hexalith.PolymorphicSerializations
+	path = references/Hexalith.PolymorphicSerializations
 	url = https://github.com/Hexalith/Hexalith.PolymorphicSerializations.git
 [submodule "Hexalith.Projects"]
-	path = Hexalith.Projects
+	path = references/Hexalith.Projects
 	url = https://github.com/Hexalith/Hexalith.Projects.git
 [submodule "Hexalith.Conversations"]
-	path = Hexalith.Conversations
+	path = references/Hexalith.Conversations
 	url = https://github.com/Hexalith/Hexalith.Conversations.git
 [submodule "Hexalith.Works"]
-	path = Hexalith.Works
+	path = references/Hexalith.Works
 	url = https://github.com/Hexalith/Hexalith.Works.git
 [submodule "Hexalith.Parties"]
-	path = Hexalith.Parties
+	path = references/Hexalith.Parties
 	url = https://github.com/Hexalith/Hexalith.Parties.git
diff --git a/Directory.Build.props b/Directory.Build.props
index bf36bb0..387b7d0 100644
--- a/Directory.Build.props
+++ b/Directory.Build.props
@@ -1,12 +1,12 @@
 <Project>
   <PropertyGroup>
-    <HexalithEventStoreRoot Condition="'$(HexalithEventStoreRoot)' == '' and Exists('$(MSBuildThisFileDirectory)Hexalith.EventStore\src\Hexalith.EventStore.Contracts')">$(MSBuildThisFileDirectory)Hexalith.EventStore</HexalithEventStoreRoot>
-    <HexalithTenantsRoot Condition="'$(HexalithTenantsRoot)' == '' and Exists('$(MSBuildThisFileDirectory)Hexalith.Tenants\src\Hexalith.Tenants.Contracts')">$(MSBuildThisFileDirectory)Hexalith.Tenants</HexalithTenantsRoot>
-    <HexalithPartiesRoot Condition="'$(HexalithPartiesRoot)' == '' and Exists('$(MSBuildThisFileDirectory)Hexalith.Parties\src\Hexalith.Parties.Contracts')">$(MSBuildThisFileDirectory)Hexalith.Parties</HexalithPartiesRoot>
-    <HexalithProjectsRoot Condition="'$(HexalithProjectsRoot)' == '' and Exists('$(MSBuildThisFileDirectory)Hexalith.Projects\src\Hexalith.Projects.Contracts')">$(MSBuildThisFileDirectory)Hexalith.Projects</HexalithProjectsRoot>
-    <HexalithWorksRoot Condition="'$(HexalithWorksRoot)' == '' and Exists('$(MSBuildThisFileDirectory)Hexalith.Works\src\Hexalith.Works.Contracts')">$(MSBuildThisFileDirectory)Hexalith.Works</HexalithWorksRoot>
-    <HexalithFrontComposerRoot Condition="'$(HexalithFrontComposerRoot)' == '' and Exists('$(MSBuildThisFileDirectory)Hexalith.FrontComposer\src\Hexalith.FrontComposer.Contracts')">$(MSBuildThisFileDirectory)Hexalith.FrontComposer</HexalithFrontComposerRoot>
-    <HexalithCommonsRoot Condition="'$(HexalithCommonsRoot)' == '' and Exists('$(MSBuildThisFileDirectory)Hexalith.Commons\src\libraries\Hexalith.Commons')">$(MSBuildThisFileDirectory)Hexalith.Commons</HexalithCommonsRoot>
+    <HexalithEventStoreRoot Condition="'$(HexalithEventStoreRoot)' == '' and Exists('$(MSBuildThisFileDirectory)references\Hexalith.EventStore\src\Hexalith.EventStore.Contracts')">$(MSBuildThisFileDirectory)references\Hexalith.EventStore</HexalithEventStoreRoot>
+    <HexalithTenantsRoot Condition="'$(HexalithTenantsRoot)' == '' and Exists('$(MSBuildThisFileDirectory)references\Hexalith.Tenants\src\Hexalith.Tenants.Contracts')">$(MSBuildThisFileDirectory)references\Hexalith.Tenants</HexalithTenantsRoot>
+    <HexalithPartiesRoot Condition="'$(HexalithPartiesRoot)' == '' and Exists('$(MSBuildThisFileDirectory)references\Hexalith.Parties\src\Hexalith.Parties.Contracts')">$(MSBuildThisFileDirectory)references\Hexalith.Parties</HexalithPartiesRoot>
+    <HexalithProjectsRoot Condition="'$(HexalithProjectsRoot)' == '' and Exists('$(MSBuildThisFileDirectory)references\Hexalith.Projects\src\Hexalith.Projects.Contracts')">$(MSBuildThisFileDirectory)references\Hexalith.Projects</HexalithProjectsRoot>
+    <HexalithWorksRoot Condition="'$(HexalithWorksRoot)' == '' and Exists('$(MSBuildThisFileDirectory)references\Hexalith.Works\src\Hexalith.Works.Contracts')">$(MSBuildThisFileDirectory)references\Hexalith.Works</HexalithWorksRoot>
+    <HexalithFrontComposerRoot Condition="'$(HexalithFrontComposerRoot)' == '' and Exists('$(MSBuildThisFileDirectory)references\Hexalith.FrontComposer\src\Hexalith.FrontComposer.Contracts')">$(MSBuildThisFileDirectory)references\Hexalith.FrontComposer</HexalithFrontComposerRoot>
+    <HexalithCommonsRoot Condition="'$(HexalithCommonsRoot)' == '' and Exists('$(MSBuildThisFileDirectory)references\Hexalith.Commons\src\libraries\Hexalith.Commons')">$(MSBuildThisFileDirectory)references\Hexalith.Commons</HexalithCommonsRoot>
     <HexalithEventStoreRoot Condition="'$(HexalithEventStoreRoot)' == '' and Exists('$(MSBuildThisFileDirectory)..\Hexalith.EventStore\src\Hexalith.EventStore.Contracts')">$(MSBuildThisFileDirectory)..\Hexalith.EventStore</HexalithEventStoreRoot>
     <HexalithTenantsRoot Condition="'$(HexalithTenantsRoot)' == '' and Exists('$(MSBuildThisFileDirectory)..\Hexalith.Tenants\src\Hexalith.Tenants.Contracts')">$(MSBuildThisFileDirectory)..\Hexalith.Tenants</HexalithTenantsRoot>
     <HexalithPartiesRoot Condition="'$(HexalithPartiesRoot)' == '' and Exists('$(MSBuildThisFileDirectory)..\Hexalith.Parties\src\Hexalith.Parties.Contracts')">$(MSBuildThisFileDirectory)..\Hexalith.Parties</HexalithPartiesRoot>
diff --git a/Hexalith.Builds b/Hexalith.Builds
deleted file mode 160000
index cf04c41..0000000
--- a/Hexalith.Builds
+++ /dev/null
@@ -1 +0,0 @@
-Subproject commit cf04c419378dfe1bd3c41a9244b5e3283092056e
diff --git a/Hexalith.EventStore b/Hexalith.EventStore
deleted file mode 160000
index 61bafea..0000000
--- a/Hexalith.EventStore
+++ /dev/null
@@ -1 +0,0 @@
-Subproject commit 61bafeabc9f209002ee2035046a1d48cdc55cabf
diff --git a/docs/launch-readiness.md b/docs/launch-readiness.md
index 196ac13..9313dd6 100644
--- a/docs/launch-readiness.md
+++ b/docs/launch-readiness.md
@@ -17,7 +17,7 @@ Final package-currency verdict: **CONCERNS / WAIVED**. Direct Timesheets package
 | Vulnerable and deprecated packages | PASS | `dotnet list Hexalith.Timesheets.slnx package --vulnerable --include-transitive` and `dotnet list Hexalith.Timesheets.slnx package --deprecated` reported no vulnerable or deprecated packages from the current sources. | No compatibility, security, or deterministic-build reason was found for adding an explicit transitive pin. |
 | Root npm applicability | not applicable | The Timesheets root has no `package.json`, `package-lock.json`, `npm-shrinkwrap.json`, `pnpm-lock.yaml`, or `yarn.lock`. | Manifests under `Hexalith.*` are sibling-submodule owned and excluded from this Timesheets root audit. |
 | Transitive drift | reviewed, no pin | Solution-level `dotnet list Hexalith.Timesheets.slnx package --outdated --include-transitive` and the AppHost project-level variant still fail with `error: Sequence contains no matching element`. Project-level audits succeeded for the remaining Timesheets projects. Drift sources are `Google.Protobuf` via sibling `Hexalith.EventStore.Client` -> `Dapr.Client` `1.18.4`; `Polly.*` and `System.Threading.RateLimiting` via direct `Microsoft.Extensions.Http.Resilience` `10.7.0`; `DiffEngine`, `EmptyFiles`, `System.CodeDom`, and `System.Management` via `Shouldly`; `Microsoft.Testing.*`, `Microsoft.ApplicationInsights`, and `Microsoft.Bcl.AsyncInterfaces` via `xunit.v3`; `Newtonsoft.Json` via `Microsoft.NET.Test.Sdk`; and `Castle.Core` / `System.Diagnostics.EventLog` via `NSubstitute`. | The drift is patch/test-stack/platform-transitive only, with no vulnerability/deprecation finding. Adding root transitive pins would promote package dependencies without a documented compatibility, security, or deterministic-build need. |
-| Platform and submodule alignment | waived | Root Timesheets pins .NET SDK `10.0.302`, Aspire packages/AppHost SDK `13.4.6`, and `CommunityToolkit.Aspire.Hosting.Dapr` `13.4.0-preview.1.260602-0230`; it does not directly pin Dapr SDK or Fluent UI packages today. `Hexalith.Builds/Props/Directory.Packages.props` remains submodule-owned and pins base `Dapr` `1.17.9`, Dapr ASP.NET Core/Actors/Workflow `1.18.4`, Aspire Hosting `13.4.6`, and `Microsoft.FluentUI.Components` `4.11.6`. | Owner: Platform / Hexalith.Builds. Risk: architecture and launch-readiness claims can overstate actual package pins. Revisit condition: the platform reconciles `Hexalith.Builds` package policy or a Timesheets-owned UI/package story adds direct Fluent UI/Dapr pins. |
+| Platform and submodule alignment | waived | Root Timesheets pins .NET SDK `10.0.302`, Aspire packages/AppHost SDK `13.4.6`, and `CommunityToolkit.Aspire.Hosting.Dapr` `13.4.0-preview.1.260602-0230`; it does not directly pin Dapr SDK or Fluent UI packages today. `references/Hexalith.Builds/Props/Directory.Packages.props` remains submodule-owned and pins base `Dapr` `1.17.9`, Dapr ASP.NET Core/Actors/Workflow `1.18.4`, Aspire Hosting `13.4.6`, and `Microsoft.FluentUI.Components` `4.11.6`. | Owner: Platform / Hexalith.Builds. Risk: architecture and launch-readiness claims can overstate actual package pins. Revisit condition: the platform reconciles `Hexalith.Builds` package policy or a Timesheets-owned UI/package story adds direct Fluent UI/Dapr pins. |
 
 ## Launch-Scope Classification
 
diff --git a/Hexalith.AI.Tools b/references/Hexalith.AI.Tools
similarity index 100%
rename from Hexalith.AI.Tools
rename to references/Hexalith.AI.Tools
diff --git a/references/Hexalith.Builds b/references/Hexalith.Builds
index f0750ca..cf04c41 160000
--- a/references/Hexalith.Builds
+++ b/references/Hexalith.Builds
@@ -1 +1 @@
-Subproject commit f0750ca703cc3ada6eb25050cb6b287e83ce3938
+Subproject commit cf04c419378dfe1bd3c41a9244b5e3283092056e
diff --git a/Hexalith.Commons b/references/Hexalith.Commons
similarity index 100%
rename from Hexalith.Commons
rename to references/Hexalith.Commons
diff --git a/Hexalith.Conversations b/references/Hexalith.Conversations
similarity index 100%
rename from Hexalith.Conversations
rename to references/Hexalith.Conversations
diff --git a/references/Hexalith.EventStore b/references/Hexalith.EventStore
new file mode 160000
index 0000000..4025191
--- /dev/null
+++ b/references/Hexalith.EventStore
@@ -0,0 +1 @@
+Subproject commit 402519170541ff4708b67168af6eaadc256cddb9
diff --git a/Hexalith.FrontComposer b/references/Hexalith.FrontComposer
similarity index 100%
rename from Hexalith.FrontComposer
rename to references/Hexalith.FrontComposer
diff --git a/Hexalith.Parties b/references/Hexalith.Parties
similarity index 100%
rename from Hexalith.Parties
rename to references/Hexalith.Parties
diff --git a/Hexalith.PolymorphicSerializations b/references/Hexalith.PolymorphicSerializations
similarity index 100%
rename from Hexalith.PolymorphicSerializations
rename to references/Hexalith.PolymorphicSerializations
diff --git a/Hexalith.Projects b/references/Hexalith.Projects
similarity index 100%
rename from Hexalith.Projects
rename to references/Hexalith.Projects
diff --git a/Hexalith.Tenants b/references/Hexalith.Tenants
similarity index 100%
rename from Hexalith.Tenants
rename to references/Hexalith.Tenants
diff --git a/Hexalith.Works b/references/Hexalith.Works
similarity index 100%
rename from Hexalith.Works
rename to references/Hexalith.Works

diff --git a/_bmad-output/implementation-artifacts/spec-move-submodules-to-references.md b/_bmad-output/implementation-artifacts/spec-move-submodules-to-references.md
new file mode 100644
index 0000000..3f1b13a
--- /dev/null
+++ b/_bmad-output/implementation-artifacts/spec-move-submodules-to-references.md
@@ -0,0 +1,83 @@
+---
+title: 'Move repository submodules under references'
+type: 'refactor'
+created: '2026-07-20'
+status: 'in-review'
+baseline_commit: 'd4dd622'
+context:
+  - '{project-root}/AGENTS.md'
+  - '{project-root}/CLAUDE.md'
+  - '{project-root}/.gitmodules'
+  - '{project-root}/Directory.Build.props'
+---
+
+<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">
+
+## Intent
+
+**Problem:** The Timesheets repository declares most Hexalith dependencies at
+repository-root paths, while the shared workspace conventions and agent
+baseline expect root-declared dependencies under `references/`. The checkout
+also contains a stale duplicate `Hexalith.Builds` gitlink at the root even
+though its active declaration already points to `references/Hexalith.Builds`.
+
+**Approach:** Relocate every Timesheets-owned root submodule checkout to
+`references/<module>`, remove the stale root `Hexalith.Builds` gitlink, update
+`.gitmodules` and the parent MSBuild dependency probes, and preserve each
+submodule's checked-out commit and repository content.
+
+## Boundaries & Constraints
+
+**Always:** Keep the same remote URLs and submodule commits; use only the
+existing root-declared submodules; keep the operation non-recursive; make
+parent build properties resolve local dependencies from `references/` while
+preserving supported sibling fallbacks; leave submodule content unchanged.
+
+**Ask First:** Any request to change a submodule commit, remote URL, nested
+submodule, dependency version, or product source code is outside this intent.
+
+**Never:** Do not initialize or update submodules from remote; do not edit
+files inside a submodule; do not leave root-level Hexalith gitlinks or stale
+root path declarations; do not commit or push.
+
+## I/O & Edge-Case Matrix
+
+| Scenario | Input / State | Expected Output / Behavior | Error Handling |
+|----------|--------------|---------------------------|----------------|
+| Existing checkout | Initialized root submodules and the existing `references/Hexalith.Builds` checkout | All unique submodules are available at `references/<module>` with their original commits | Stop if a target path is occupied by unrelated content |
+| Duplicate Builds gitlink | Root `Hexalith.Builds` and `references/Hexalith.Builds` both tracked | The stale root gitlink is removed and the existing references checkout remains | Stop if the two pointers cannot be distinguished without changing content |
+| Fresh clone metadata | Updated `.gitmodules` and gitlink paths | `git submodule status` and non-recursive initialization metadata address only `references/` paths | Report the exact failing Git command |
+| Build resolution | Projects evaluate root dependency properties | Local dependency roots resolve under `references/`, and supported sibling fallbacks remain available | Report the exact MSBuild or restore failure |
+
+</frozen-after-approval>
+
+## Code Map
+
+- `.gitmodules` -- declares the parent repository's submodule names, paths, and remotes.
+- `Directory.Build.props` -- resolves local and sibling Hexalith project roots for MSBuild.
+- `Directory.Packages.props` -- imports centralized package versions from the Builds reference and must retain a valid references-first import.
+- `Hexalith.*` and `references/Hexalith.*` -- gitlink worktrees whose tracked paths are being normalized.
+
+## Tasks & Acceptance
+
+**Execution:**
+- [x] `.gitmodules` -- change every declared submodule path to `references/<module>` and remove no remote or URL -- align metadata with workspace ownership.
+- [x] `Hexalith.*` gitlinks -- move the ten root worktrees and remove the stale root `Hexalith.Builds` duplicate while preserving the existing references pointer -- normalize the tracked dependency layout.
+- [x] `Directory.Build.props` -- change local dependency probes and values from root paths to `references/` paths while retaining explicit-property and sibling fallbacks -- keep project evaluation valid after the move.
+- [x] `Directory.Packages.props` -- verify and adjust only if needed so the references-first Builds props import remains valid -- preserve centralized package resolution.
+
+**Acceptance Criteria:**
+- Given the parent repository metadata is inspected, when all declared submodule paths are listed, then every path is under `references/` and all original remotes remain unchanged.
+- Given the parent Git index is inspected, when gitlinks are listed, then there is exactly one gitlink per declared module, every gitlink is under `references/`, and no root `Hexalith.*` gitlink remains.
+- Given the moved worktrees are checked, when each submodule HEAD is compared with its pre-move commit, then every commit is unchanged and each worktree remains a valid Git submodule.
+- Given a local Timesheets project is evaluated, when its dependency root properties are queried, then local dependencies resolve from `references/` and the Builds package props import succeeds.
+- Given repository validation runs, when Git consistency, whitespace checks, restore, and the focused architecture/build checks execute, then they pass without recursive submodule initialization or submodule content changes.
+
+## Verification
+
+**Commands:**
+- `git diff --check` -- expected: no whitespace or conflict-marker errors.
+- `git submodule status` -- expected: one valid entry per `references/` submodule.
+- `git ls-files -s | awk '$1 == "160000"'` -- expected: all gitlinks are under `references/`.
+- `dotnet msbuild src/Hexalith.Timesheets.Works/Hexalith.Timesheets.Works.csproj -nologo -getProperty:HexalithWorksRoot` -- expected: a valid resolved dependency path.
+- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1 /nr:false` -- expected: restore succeeds using the relocated references.

```

## Required response

Return:
1. Findings, ordered by severity, with exact paths and line references where possible.
2. Any acceptance or safety risk that is not a finding.
3. A concise verdict: pass, pass with defer items, or findings requiring a fix.

