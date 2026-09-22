using Contracts;
using Microsoft.EntityFrameworkCore;
using TenantLifecycle.Domain;
using TenantLifecycle.Persistence;

namespace TenantLifecycle.Application;

public sealed class GetCompanySettingsHandler(TenantLifecycleDbContext context, IAuthorizer authorizer)
{
    public async Task<CompanySettings> HandleAsync(GetCompanySettingsQuery query, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(query.TenantId, cancellationToken);

        var actor = new ActorContext(query.TenantId, query.Principal, query.CorrelationId);
        var decision = await authorizer.AuthorizeAsync(
            new AuthorizationRequest(actor, new ActionKey(TenantProfileActionKeys.SettingsView), new ResourceDescriptor(nameof(TenantProfile), query.TenantId.Value, null)),
            cancellationToken);
        if (!decision.IsAllowed)
            throw new CompanySettingsAuthorizationDeniedException(TenantProfileActionKeys.SettingsView, decision.ReasonCode);

        var profile = await context.TenantProfiles.AsNoTracking().SingleOrDefaultAsync(
            candidate => candidate.TenantId == query.TenantId,
            cancellationToken) ?? throw new CompanySettingsNotFoundException();
        await transaction.CommitAsync(cancellationToken);
        return Map(profile);
    }

    internal static CompanySettings Map(TenantProfile profile) => new(
        profile.DisplayName, profile.LegalName, profile.TaxNumber, profile.TaxOffice, profile.Email, profile.Phone,
        profile.Address, profile.Timezone, profile.CurrencyCode, profile.RowVersion);
}
