// Fixture-only pure validation against the pinned, captured SDK. Never writes authority/domain state.
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Validates only the isolated fixture's actual SDK activation and technical witnesses.</summary>
internal static class CatalogBindingVerifier
{
    private static readonly JsonSerializerOptions JsonOptions = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };

    /// <summary>Emits constant validity/stage data and digests while withholding private parser diagnostics.</summary>
    public static int Main(string[] args)
    {
        string stage = "input";
        try
        {
            var sdk = new[] { typeof(RouteCatalogActivation).Assembly, typeof(RouteCatalogActivationBinding).Assembly }
                .ToDictionary(assembly => assembly.GetName().Name!, assembly => Hash(File.ReadAllBytes(assembly.Location)));
            if (args.Length == 0)
            {
                Console.WriteLine(JsonSerializer.Serialize(new { sdk_assemblies = sdk }));
                return 0;
            }
            Require(args.Length == 2);
            string directory = args[0];
            var activation = Read<RouteCatalogActivation>(directory, "activation.json");
            var gateway = Read<RouteCatalogHostObservation>(directory, "eventstore.observation.json");
            var timesheets = Read<RouteCatalogHostObservation>(directory, "timesheets.observation.json");
            stage = "catalog";
            var envelope = RouteCatalogCodec.Decode(activation.CanonicalCatalog, activation.RootDigest);
            Require(envelope.SchemaVersion == 3 && activation.CanonicalCatalog.SequenceEqual(File.ReadAllBytes(Path.Combine(directory, "catalog.json"))));
            stage = "membership";
            Require(activation.TechnicalHttpReceivers is { Count: 1 });
            var technical = activation.TechnicalHttpReceivers![0];
            Require(technical.AppId == "eventstore" && technical.FenceVersion == RouteCatalogTechnicalHttpPolicy.FenceVersion);
            Require(gateway.ReceiverSurface?.FenceVersion == RouteCatalogTechnicalHttpPolicy.FenceVersion
                && gateway.ReceiverSurface.TechnicalHttp is { Count: > 0 }
                && timesheets.ReceiverSurface?.FenceVersion == FiniteRouteCatalogReceiver.Version
                && timesheets.ReceiverSurface.TechnicalHttp is null);
            stage = "inventory";
            var actions = gateway.ReceiverSurface!.TechnicalHttp!;
            Require(Same(technical.Endpoints.OrderBy(Key), actions.OrderBy(Key)));
            Require(Same((technical.Invocations ?? []).OrderBy(InvocationKey), (gateway.DeclaredInvocations ?? []).OrderBy(InvocationKey)));
            stage = "invocations";
            var grants = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["api/v1/commands/bound/workload"] = "POST", ["api/v1/commands/workload"] = "POST",
                ["api/v1/commands/status/workload/*"] = "GET", ["api/v1/streams/read/workload"] = "POST",
            };
            Require(gateway.DeclaredInvocations is { Count: 4 }
                && gateway.DeclaredInvocations.Select(InvocationKey).Distinct(StringComparer.Ordinal).Count() == 4
                && gateway.DeclaredInvocations.All(invocation => invocation.CallerAppId == "timesheets"
                    && grants.TryGetValue(invocation.Method, out string? verb) && invocation.HttpMethods.SequenceEqual([verb])));
            stage = "code";
            var actionAssemblies = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var action in actions)
            {
                string name = action.ActionIdentity.Split(',')[0];
                Require(name is "Hexalith.EventStore.Gateway" or "Hexalith.EventStore.Admin.Server");
                string path = Path.Combine(args[1], name + ".dll");
                byte[] image = File.ReadAllBytes(path);
                using var pe = new PEReader(new MemoryStream(image, writable: false));
                var metadata = pe.GetMetadataReader();
                string codeDigest = Hash(image);
                string code = $"{AssemblyName.GetAssemblyName(path).FullName}:{metadata.GetGuid(metadata.GetModuleDefinition().Mvid)}:{codeDigest}:";
                Require(action.ActionIdentity.StartsWith(code, StringComparison.Ordinal));
                actionAssemblies[name] = codeDigest;
            }
            stage = "status";
            Require(actions.Count(action => action.Pattern == "/api/v1/commands/status/workload/{tenant}/{messageId}"
                && action.Methods.SequenceEqual(["GET"]) && action.CredentialKind == "workload") == 1);
            stage = "surface";
            var observed = gateway.ReceiverSurface.Endpoints.Where(endpoint => endpoint.Operation == RouteCatalogTechnicalHttpPolicy.Operation).ToArray();
            Require(actions.Select(Key).Distinct(StringComparer.OrdinalIgnoreCase).Count() == actions.Count
                && observed.Select(endpoint => endpoint.Pattern + "|" + string.Join(',', endpoint.Methods)).Distinct(StringComparer.OrdinalIgnoreCase).Count() == observed.Length
                && observed.Length == actions.Count && actions.All(action => observed.Count(endpoint => endpoint.Pattern == action.Pattern
                && endpoint.Methods.SequenceEqual(action.Methods) && endpoint.Policies.SequenceEqual(action.Policies)
                && endpoint.Admitted && !endpoint.Anonymous) == 1));
            stage = "topology";
            foreach (var observation in new[] { gateway, timesheets })
            {
                Require(observation.TopologyDocuments.Count >= 2 && observation.HostId == observation.AppId
                    && observation.RequiredHostIds.Order().SequenceEqual(new[] { "eventstore", "timesheets" })
                    && (observation.SelectedAppIds ?? []).Order().SequenceEqual(new[] { "eventstore", "timesheets" }));
                RouteCatalogTopologyValidator.ValidateDevelopment(observation.TopologyDocuments[0], observation.TopologyDocuments[1],
                    envelope, activation.RequiredHosts, observation.AppId, activation.AuxiliaryOperations,
                    observation.ReceiverSurface, observation.SelectedAppIds, observation.DeclaredInvocations);
            }
            stage = "proof";
            string digest = RouteCatalogActivationBinding.ComputeDigest(activation);
            Require(digest == activation.TrustProof);
            Console.WriteLine(JsonSerializer.Serialize(new { valid = true, binding_digest = digest,
                technical_action_count = actions.Count, sdk_assemblies = sdk, action_assemblies = actionAssemblies }));
            return 0;
        }
        catch (Exception)
        {
            // Parser/SDK diagnostics may contain private inputs. Emit no arbitrary diagnostic text.
            Console.WriteLine(JsonSerializer.Serialize(new { valid = false, stage }));
            return 1;
        }
    }

    private static T Read<T>(string directory, string name) => JsonSerializer.Deserialize<T>(File.ReadAllBytes(Path.Combine(directory, name)), JsonOptions)!;
    private static bool Same<T>(IEnumerable<T> left, IEnumerable<T> right) => JsonSerializer.Serialize(left) == JsonSerializer.Serialize(right);
    private static string Key(RouteCatalogTechnicalHttpEndpoint action) => action.Pattern + "|" + string.Join(',', action.Methods);
    private static string InvocationKey(RouteCatalogDeclaredInvocation invocation) => invocation.CallerAppId + "|" + invocation.Method;
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Require(bool valid) { if (!valid) throw new InvalidOperationException(); }
}
