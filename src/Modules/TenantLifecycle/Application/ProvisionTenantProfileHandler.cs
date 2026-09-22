using Contracts;
using Microsoft.EntityFrameworkCore;
using TenantLifecycle.Domain;
using TenantLifecycle.Outbox;
using TenantLifecycle.Persistence;

namespace TenantLifecycle.Application;

public sealed class ProvisionTenantProfileHandler(TenantLifecycleDbContext context)
{
    public async Task<ProvisionTenantProfileResult> HandleAsync(
        ProvisionTenantProfileCommand command,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(command.TenantId, cancellationToken);

        var existing = await context.TenantProfiles.SingleOrDefaultAsync(
            profile => profile.TenantId == command.TenantId,
            cancellationToken);
        if (existing is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return new ProvisionTenantProfileResult(existing.RowVersion, AlreadyProvisioned: true);
        }

        var profile = TenantProfile.Provision(command.TenantId, new TenantProfileDetails(
            command.DisplayName, null, null, null, null, null, null, "Europe/Istanbul", "TRY"));
        context.TenantProfiles.Add(profile);
        context.OutboxMessages.Add(OutboxMessage.Create(
            command.TenantId, nameof(TenantProfile), profile.RowVersion, TenantProfileOutbox.ProvisionedEventType,
            TenantProfileOutbox.EventSource, TenantProfileOutbox.Subject(command.TenantId.Value), command.CorrelationId,
            null, TenantProfileOutbox.Payload(profile)));
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new ProvisionTenantProfileResult(profile.RowVersion, AlreadyProvisioned: false);
    }
}
