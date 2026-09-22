using System.Text.Json;
using Contracts;
using Microsoft.EntityFrameworkCore;
using TenantLifecycle.Domain;
using TenantLifecycle.Idempotency;
using TenantLifecycle.Outbox;
using TenantLifecycle.Persistence;

namespace TenantLifecycle.Application;

public sealed class UpdateCompanySettingsHandler(TenantLifecycleDbContext context, IAuthorizer authorizer)
{
    private const string Operation = "UpdateCompanySettings";

    public async Task<UpdateCompanySettingsResult> HandleAsync(
        UpdateCompanySettingsCommand command,
        CancellationToken cancellationToken = default)
    {
        IdempotencySupport.ValidateKey(command.IdempotencyKey);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(command.TenantId, cancellationToken);

        var actor = new ActorContext(command.TenantId, command.Principal, command.CorrelationId);
        var decision = await authorizer.AuthorizeAsync(
            new AuthorizationRequest(actor, new ActionKey(TenantProfileActionKeys.SettingsUpdate), new ResourceDescriptor(nameof(TenantProfile), command.TenantId.Value, null)),
            cancellationToken);
        if (!decision.IsAllowed)
            throw new CompanySettingsAuthorizationDeniedException(TenantProfileActionKeys.SettingsUpdate, decision.ReasonCode);

        var requestHash = HashRequest(command);
        var existing = await IdempotencySupport.FindAsync(
            context, command.TenantId, command.Principal, Operation, command.IdempotencyKey, cancellationToken);
        if (existing is not null)
        {
            if (existing.RequestHash != requestHash)
                throw new IdempotencyKeyReusedException(Operation, command.IdempotencyKey);

            await transaction.CommitAsync(cancellationToken);
            return new UpdateCompanySettingsResult(Deserialize(existing), Replayed: true);
        }

        var profile = await context.TenantProfiles.SingleOrDefaultAsync(
            candidate => candidate.TenantId == command.TenantId,
            cancellationToken) ?? throw new CompanySettingsNotFoundException();
        if (profile.RowVersion != command.ExpectedVersion)
            throw new CompanySettingsConcurrencyConflictException(command.ExpectedVersion, profile.RowVersion);

        profile.Replace(command.Details);
        var settings = GetCompanySettingsHandler.Map(profile);
        context.OutboxMessages.Add(OutboxMessage.Create(
            command.TenantId, nameof(TenantProfile), profile.RowVersion, TenantProfileOutbox.UpdatedEventType,
            TenantProfileOutbox.EventSource, TenantProfileOutbox.Subject(command.TenantId.Value), command.CorrelationId,
            null, TenantProfileOutbox.Payload(profile)));
        context.IdempotencyRecords.Add(IdempotencyRecord.Create(
            command.TenantId, command.Principal, Operation, command.IdempotencyKey, requestHash, 200,
            JsonSerializer.Serialize(settings), IdempotencySupport.Retention));

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new CompanySettingsConcurrencyConflictException(command.ExpectedVersion);
        }

        await transaction.CommitAsync(cancellationToken);
        return new UpdateCompanySettingsResult(settings, Replayed: false);
    }

    private static CompanySettings Deserialize(IdempotencyRecord record) =>
        JsonSerializer.Deserialize<CompanySettings>(record.ResponsePayload)
        ?? throw new InvalidOperationException("Stored idempotency response is empty.");

    private static string HashRequest(UpdateCompanySettingsCommand command) => IdempotencySupport.Hash(new
    {
        operation = Operation,
        tenantId = command.TenantId.Value,
        principalIssuer = command.Principal.Issuer,
        principalSubject = command.Principal.Subject,
        expectedVersion = command.ExpectedVersion,
        displayName = command.Details.DisplayName,
        legalName = command.Details.LegalName,
        taxNumber = command.Details.TaxNumber,
        taxOffice = command.Details.TaxOffice,
        email = command.Details.Email?.ToLowerInvariant(),
        phone = command.Details.Phone,
        address = command.Details.Address,
        timezone = command.Details.Timezone,
        currencyCode = command.Details.CurrencyCode
    });
}
