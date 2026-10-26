"""Compile the unchanged SDK secret-guard source family and preserve its normal repo-root lookup."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile

parser = argparse.ArgumentParser()
parser.add_argument('--sdk-root', type=Path, required=True)
parser.add_argument('--evidence-directory', type=Path, required=True)
args = parser.parse_args()
sdk = args.sdk_root.resolve()
evidence = args.evidence_directory.resolve()
evidence.mkdir(parents=True, exist_ok=True)
sources = sorted((sdk / 'tests/Hexalith.EventStore.Server.Tests/Security').glob('SecretsProtectionTests*.cs'))
if not sources:
    raise SystemExit('The unchanged secret-guard source family is unavailable.')
# Exactly the ordinary test-project depth: five parent hops from bin/Debug/net10.0 reaches sdk.
scratch = Path(tempfile.mkdtemp(prefix='FiveDeferralSecretGuard', dir=sdk / 'tests'))
project = scratch / 'FiveDeferralSecretGuard.csproj'
project.write_text('''<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType></PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Shouldly" />
    <PackageReference Include="xunit.v3" />
    <Using Include="Xunit" />
    <Compile Include="../Hexalith.EventStore.Server.Tests/Security/SecretsProtectionTests*.cs" />
  </ItemGroup>
</Project>
''')
shutil.copy2(project, evidence / 'unchanged-secret-guard.csproj.txt')
receipt = {'source_sha256': {str(p.relative_to(sdk)): hashlib.sha256(p.read_bytes()).hexdigest() for p in sources},
           'commands': [], 'normal_repo_root': str(sdk)}
exit_code = 0
try:
    commands = [('build', ['dotnet', 'build', str(project), '--configuration', 'Debug', '-m:1', '/nr:false']),
                ('test', [str(scratch / 'bin/Debug/net10.0/FiveDeferralSecretGuard'),
                          '-method', '*TrackedReusableContent_DoesNotContainUsableSecrets', '-noColor'])]
    for name, command in commands:
        with (evidence / ('unchanged-secret-guard-' + name + '.log')).open('w') as output:
            result = subprocess.run(command, cwd=sdk, env=dict(os.environ, DOTNET_CLI_HOME='/tmp/dotnet-cli-home'),
                                    stdout=output, stderr=subprocess.STDOUT, check=False)
        receipt['commands'].append({'command': command, 'cwd': str(sdk), 'exit_code': result.returncode})
        exit_code = result.returncode
        if exit_code:
            break
finally:
    shutil.rmtree(scratch)
    receipt['owned_scratch_removed'] = not scratch.exists()
    receipt['source_unchanged'] = all(hashlib.sha256(p.read_bytes()).hexdigest() == receipt['source_sha256'][str(p.relative_to(sdk))] for p in sources)
    (evidence / 'unchanged-secret-guard.json').write_text(json.dumps(receipt, indent=2) + '\n')
raise SystemExit(exit_code)
