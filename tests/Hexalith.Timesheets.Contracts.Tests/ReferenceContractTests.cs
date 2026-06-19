using Hexalith.Timesheets.Contracts;
using Hexalith.Timesheets.Contracts.References;

using Shouldly;

namespace Hexalith.Timesheets.Contracts.Tests;

public sealed class ReferenceContractTests
{
    [Fact]
    public void Stable_references_reject_blank_identifiers()
    {
        Should.Throw<ArgumentException>(static () => new TenantReference(" "));
        Should.Throw<ArgumentException>(static () => new PartyReference(" "));
        Should.Throw<ArgumentException>(static () => new ProjectReference(" "));
        Should.Throw<ArgumentException>(static () => new WorkReference(" "));
    }

    [Fact]
    public void Stable_references_expose_only_stable_identifier_values()
    {
        Type[] referenceTypes =
        [
            typeof(TenantReference),
            typeof(PartyReference),
            typeof(ProjectReference),
            typeof(WorkReference)
        ];

        string[] siblingOwnedProperties =
        [
            "Name",
            "DisplayName",
            "Email",
            "Contact",
            "Profile",
            "LifecycleState",
            "MembershipState",
            "Hierarchy",
            "PlanningState"
        ];

        foreach (Type referenceType in referenceTypes)
        {
            System.Reflection.PropertyInfo[] publicProperties = referenceType
                .GetProperties()
                .ToArray();

            publicProperties.Length.ShouldBe(1, referenceType.Name);
            publicProperties[0].PropertyType.ShouldBe(typeof(string), referenceType.Name);
            publicProperties[0].Name.EndsWith("Id", StringComparison.Ordinal)
                .ShouldBeTrue(referenceType.Name);

            foreach (string siblingOwnedProperty in siblingOwnedProperties)
            {
                publicProperties.Select(static property => property.Name)
                    .ShouldNotContain(siblingOwnedProperty, referenceType.Name);
            }
        }
    }

    [Fact]
    public void Metadata_catalog_exposes_capture_and_evidence_surface_descriptors()
    {
        TimesheetsMetadataCatalog.Descriptors.Count.ShouldBe(3);
        TimesheetsMetadataCatalog.Descriptors.Select(static descriptor => descriptor.Name)
            .ShouldContain("timesheets.command.record-time");
        TimesheetsMetadataCatalog.Descriptors.Select(static descriptor => descriptor.Name)
            .ShouldContain("timesheets.projection.time-entry-evidence");
    }
}
