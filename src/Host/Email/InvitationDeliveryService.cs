using Access.Application.Authentication;
using Access.Domain.Authentication;
using Access.Persistence;
using Host.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Host.Email;

public sealed class InvitationDeliveryService(
    IServiceScopeFactory scopeFactory,
    IEmailTransport transport,
    IInvitationTokenProtector protector,
    AuthenticationHostOptions authenticationOptions,
    IOptions<EmailOptions> emailOptions,
    TimeProvider timeProvider,
    ILogger<InvitationDeliveryService> logger,
    DevMailbox? devMailbox = null) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DeliverPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogWarning("Invitation delivery polling failed ({ErrorType}).", exception.GetType().Name);
            }
            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                    return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    public async Task DeliverPendingAsync(CancellationToken cancellationToken)
    {
        if (!emailOptions.Value.Smtp.Enabled && devMailbox is null)
            return;

        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AccessDbContext>();
        var now = timeProvider.GetUtcNow();
        var tenantIds = await context.AccountTokens.AsNoTracking()
            .Where(token => token.Purpose == AccountTokenPurpose.Invite
                && token.ConsumedAt == null && token.RevokedAt == null)
            .Select(token => token.TenantId).Distinct().ToListAsync(cancellationToken);

        foreach (var tenantId in tenantIds.Where(id => id.HasValue).Select(id => id!.Value))
        {
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            await context.SetTenantContextAsync(new Contracts.TenantId(tenantId), cancellationToken);
            var due = await context.InvitationDeliveries
                .Where(delivery => delivery.TenantId == new Contracts.TenantId(tenantId)
                    && delivery.DeliveredAt == null && delivery.NextAttemptAt <= now)
                .OrderBy(delivery => delivery.NextAttemptAt).Take(20).ToListAsync(cancellationToken);

            foreach (var delivery in due)
            {
                var claimed = await context.InvitationDeliveries
                    .Where(row => row.TenantId == delivery.TenantId && row.InvitationId == delivery.InvitationId
                        && row.DeliveredAt == null && row.NextAttemptAt <= now)
                    .ExecuteUpdateAsync(update => update.SetProperty(row => row.NextAttemptAt, now.AddMinutes(5)), cancellationToken);
                if (claimed != 1)
                    continue;

                var token = await context.AccountTokens.AsNoTracking()
                    .SingleOrDefaultAsync(row => row.Id == delivery.InvitationId && row.TenantId == tenantId, cancellationToken);
                if (token is null || !token.IsOutstanding(now))
                {
                    delivery.MarkDelivered(now);
                    continue;
                }

                try
                {
                    var rawToken = protector.Unprotect(delivery.ProtectedToken);
                    var email = EmailRenderer.Render(new EmailMessage(token.EmailNormalized!, EmailMessage.InviteTemplate,
                        token.Locale ?? "tr", new Dictionary<string, string>
                        {
                            ["token"] = rawToken,
                            ["tenantId"] = tenantId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            ["expiresAt"] = token.ExpiresAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture)
                        }), authenticationOptions.PublicAppBaseUrl ?? "http://localhost:5173", emailOptions.Value.Smtp.FromName);
                    if (emailOptions.Value.Smtp.Enabled)
                        await transport.SendAsync(email, cancellationToken);
                    delivery.MarkDelivered(now);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    delivery.Retry(now);
                    logger.LogWarning("Invitation delivery failed ({ErrorType}); retry scheduled.", exception.GetType().Name);
                }
            }

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            context.ChangeTracker.Clear();
        }
    }
}
