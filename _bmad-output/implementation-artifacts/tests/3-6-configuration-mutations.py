"""Verify the built Story 3.6 source fitness test against unsafe configuration mutations.

Run after the README Debug source-reference build:
    python3 _bmad-output/implementation-artifacts/tests/3-6-configuration-mutations.py

This script temporarily edits the two production Program.cs files, invokes only the
built source fitness test, and restores the original bytes in finally blocks.
Run it with no other build, source editor, or test process using those files.
"""

import os
from pathlib import Path
import subprocess


def main():
    root = Path(__file__).resolve().parents[3]
    apphost = root / 'src/Hexalith.Timesheets.AppHost/Program.cs'
    host = root / 'src/Hexalith.Timesheets/Program.cs'
    originals = {apphost: apphost.read_bytes(), host: host.read_bytes()}
    app_source = originals[apphost].decode()
    host_source = originals[host].decode()
    export = '''    .WithEnvironment(
        "Timesheets__InternalSurface__Port",
        InternalPort.ToString(System.Globalization.CultureInfo.InvariantCulture));'''
    binding = '''builder.Services.Configure<InternalSurfaceOptions>(
    builder.Configuration.GetSection(InternalSurfaceOptions.SectionName));'''
    internal_endpoint = '    .WithHttpEndpoint(name: "internal", port: InternalPort, isProxied: false)'
    if app_source.count(internal_endpoint + '\n' + export) != 1 or host_source.count(binding) != 1:
        raise RuntimeError('Production wiring changed; update the mutation anchors before running.')
    executable = root / 'tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests'
    command = [str(executable), '-method', 'Hexalith.Timesheets.IntegrationTests.MagicLinkConfirmationCapabilityEndpointTests.AppHostExportsTheInternalListenerPortToTheHostOptionsSection']
    env = dict(os.environ, DOTNET_CLI_HOME='/tmp/dotnet-cli-home')

    def check(label, expected_failure):
        result = subprocess.run(command, cwd=root, env=env, capture_output=True, text=True, timeout=30)
        expected = 'Total: 1, Errors: 0, Failed: ' + ('1' if expected_failure else '0')
        if (result.returncode != 0) != expected_failure or expected not in result.stdout:
            raise RuntimeError(label + '\n' + result.stdout + result.stderr)
        print(label + ': ' + ('rejected as expected' if expected_failure else 'passed'), flush=True)

    def append_settings(settings):
        return app_source.replace(export, export[:-1] + '\n' + settings + ';')

    mutations = [
        ('AllowOnAnyPort export', apphost, append_settings('    .WithEnvironment("Timesheets__InternalSurface__AllowOnAnyPort", "true")')),
        ('later public Port override', apphost, append_settings('    .WithEnvironment("Timesheets__InternalSurface__Port", "8080")')),
        ('colon-key public Port override', apphost, append_settings('    .WithEnvironment("Timesheets:InternalSurface:Port", "8080")')),
        ('command-line public Port override', apphost, append_settings('    .WithArgs("--Timesheets:InternalSurface:Port=8080")')),
        ('inline-comment export substitute', apphost, app_source.replace(internal_endpoint + '\n' + export, internal_endpoint + ' // .WithEnvironment("Timesheets__InternalSurface__Port", InternalPort.ToString(System.Globalization.CultureInfo.InvariantCulture))\n    ;')),
        ('block-comment export substitute', apphost, app_source.replace(export, '/*\n' + export[:-1] + '\n*/\n    ;')),
        ('spaced AppHost conditional', apphost, app_source.replace(export, '# if false\n' + export[:-1] + '\n# endif\n    ;')),
        ('AppHost conditional', apphost, app_source.replace(export, '#if false\n' + export[:-1] + '\n#endif\n    ;')),
        ('spaced host conditional', host, host_source.replace(binding, '# if false\n' + binding + '\n# endif')),
        ('inline-comment host binding substitute', host, host_source.replace(binding, 'builder.Services.AddSingleton(TimeProvider.System); // .Configure<InternalSurfaceOptions>(builder.Configuration.GetSection(InternalSurfaceOptions.SectionName));')),
        ('host Configure bypass', host, host_source.replace(binding, binding + '\nbuilder.Services.Configure<InternalSurfaceOptions>(o => o.AllowOnAnyPort = true);')),
        ('host in-memory configuration bypass', host, host_source.replace(binding, binding + '\nbuilder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Timesheets:InternalSurface:AllowOnAnyPort"] = "true" });')),
        ('lowercase bypass export', apphost, append_settings('    .WithEnvironment("timesheets__internalsurface__allowonanyport", "true")')),
        ('mixed-case Port override', apphost, append_settings('    .WithEnvironment("timesheets__InternalSurface__port", "8080")')),
    ]
    literals = [
        ('ordinary', '"https://example.invalid"'),
        ('verbatim', '@"https://example.invalid"'),
        ('raw', '"""https://example.invalid"""'),
        ('interpolated verbatim @$', r'@$"C:\x\" + "http://a"'),
        ('interpolated verbatim $@', r'$@"C:\x\" + "http://a"'),
        ('interpolated verbatim @$ with hole and doubled quotes', '@$"https://{PublicPort}/""segment""/*literal*/"'),
        ('interpolated verbatim $@ with hole and doubled quotes', '$@"https://{PublicPort}/""segment""/*literal*/"'),
    ]
    for label, literal in literals:
        settings = '    .WithEnvironment("Endpoint", ' + literal + ').WithEnvironment("Timesheets__InternalSurface__Port", "8080")'
        mutations.append((label + ' URL before Port override', apphost, append_settings(settings)))

    try:
        check('baseline', False)
        try:
            apphost.write_bytes(append_settings('    .WithEnvironment("Endpoint", "https://internalsurface.example.invalid")').encode())
            check('harmless InternalSurface URL', False)
        finally:
            apphost.write_bytes(originals[apphost])
        for label, literal in literals:
            try:
                apphost.write_bytes(append_settings('    .WithEnvironment("Endpoint", ' + literal + ')').encode())
                check(label + ' harmless URL literal', False)
            finally:
                apphost.write_bytes(originals[apphost])
        for label, path, source in mutations:
            try:
                path.write_bytes(source.encode())
                check(label, True)
            finally:
                path.write_bytes(originals[path])
        check('restored production sources', False)
    finally:
        for path, content in originals.items():
            path.write_bytes(content)
    if any(path.read_bytes() != content for path, content in originals.items()):
        raise RuntimeError('Production source bytes were not restored exactly.')
    print(f'Production source bytes restored exactly; {len(mutations)} mutations rejected.')


if __name__ == '__main__':
    main()
