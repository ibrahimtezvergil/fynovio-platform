using Contracts;
using CRM.Domain;

namespace CRM.Tests.Domain;

public sealed class CrmSettingsTests
{
    [Fact]
    public void Assignment_modes_requiring_a_target_reject_missing_references()
    {
        var settings = CrmSettings.Create(new TenantId(21));

        Assert.Throws<ArgumentException>(() => settings.Replace(null, OpportunityCreationMode.Form, null, false, true,
            AssignmentMode.DefaultPrincipal, AssignmentPolicy.AnyAssignablePrincipal, null, null, null));
        Assert.Throws<ArgumentException>(() => settings.Replace(null, OpportunityCreationMode.Form, null, false, true,
            AssignmentMode.Team, AssignmentPolicy.AnyAssignablePrincipal, null, null, null));
        Assert.Throws<ArgumentException>(() => settings.Replace(null, OpportunityCreationMode.Form, null, false, true,
            AssignmentMode.Territory, AssignmentPolicy.AnyAssignablePrincipal, null, null, null));
    }

    [Fact]
    public void Replace_advances_the_optimistic_version_and_preserves_typed_defaults()
    {
        var settings = CrmSettings.Create(new TenantId(21));
        var principal = new PrincipalRef("https://identity.example", "seller-1");

        settings.Replace(4, OpportunityCreationMode.Wizard, 8, true, false, AssignmentMode.DefaultPrincipal,
            AssignmentPolicy.ManagerOnly, principal, null, null);

        Assert.Equal(2, settings.RowVersion);
        Assert.Equal(4, settings.DefaultPipelineDefinitionId);
        Assert.Equal(OpportunityCreationMode.Wizard, settings.OpportunityCreationMode);
        Assert.Equal(8, settings.DefaultOpportunityTypeId);
        Assert.True(settings.RequireLostReason);
        Assert.False(settings.RequireWonLine);
        Assert.Equal(principal.Issuer, settings.DefaultPrincipalIssuer);
        Assert.Equal(principal.Subject, settings.DefaultPrincipalSubject);
    }
}
