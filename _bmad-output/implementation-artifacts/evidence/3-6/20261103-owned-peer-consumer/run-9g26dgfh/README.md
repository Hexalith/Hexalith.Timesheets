# Initial owned-transport consumer attempt — retained failure

Command: `DOTNET_CLI_HOME=/tmp/dotnet-cli-home PYTHONPATH=scripts python3 scripts/run_isolated_magic_link_dapr.py --route-catalog --public-port 18080 --log-directory _bmad-output/implementation-artifacts/evidence/3-6/20261103-owned-peer-consumer`

Exit **1**, before catalog commitment, writer activation or metadata. Both existing resources reported running, but actual native sidecars were absent. A separate focused actual CLI probe reproduced the cause: pinned Dapr CLI 1.18.2 resolves `--runtime-path` as an installation root containing `.dapr/bin/daprd`; the initial adapter layout placed the executable one level too high. The owner layout was repaired and the public Aspire/actual-CLI witness passed before the [fresh retry](../run-tvhk9h3o/README.md).

[Source before](source-before.json) records the original attempted graph. No post-journey source-equality or runtime qualification is claimed. [Owner transport ready](owner-transport-ready.json) and [cleanup](owner-transport-cleanup.json) retain sanitized control-process/trust receipts: the three exact control owners exited and temporary trust was removed. The runner removed its one Redis container and components. [Original fixture cleanup](cleanup.json) remains **incomplete** because no committed catalog custody exists; its private profile remains protected and retained. No credential or private configuration is copied here.

This failed attempt remains historical evidence. Story 3.6 stays **in-progress** and launch stays **FAIL**. [Manifest](manifest.json) hashes the retained artifacts. Ignored original `.log` files have identical retained `.txt` copies documented in `log-copies.json`.
