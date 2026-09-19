using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Contracts;
using CRM.Domain;
using CRM.Evidence;
using CRM.Idempotency;
using CRM.Outbox;
using CRM.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CRM.Application;

public sealed class OpenOpportunityHandler(CrmDbContext context, IAuthorizer authorizer)
{
    private const string Operation = "OpenOpportunity";
    private const string ActionKeyValue = "crm.opportunity.open";
    private const string EventType = "enterprise.crmsales.opportunity.opened.v1";
    private const string EventSource = "/enterprise/crm-sales";
    private const int SucceededStatus = 200;
    private static readonly TimeSpan IdempotencyRetention = TimeSpan.FromDays(7);

    public async Task<OpenOpportunityResult> HandleAsync(OpenOpportunityCommand command, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(command.TenantId, cancellationToken);

        var opportunity = await context.Opportunities.SingleOrDefaultAsync(o => o.Id == command.OpportunityId, cancellationToken)
            ?? throw new OpportunityNotFoundException(command.OpportunityId);

        var actor = new ActorContext(command.TenantId, command.Principal, command.CorrelationId);
        var resource = new ResourceDescriptor(nameof(Opportunity), opportunity.Id, opportunity.AssignedPrincipal);
        var decision = await authorizer.AuthorizeAsync(
            new AuthorizationRequest(actor, new ActionKey(ActionKeyValue), resource), cancellationToken);
        if (!decision.IsAllowed)
            throw new OpportunityAuthorizationDeniedException(ActionKeyValue, decision.ReasonCode, decision.DenialStage, opportunity.Id);

        var requestHash = HashRequest(command);
        var existing = await context.IdempotencyRecords
            .AsNoTracking()
            .SingleOrDefaultAsync(
                r => r.TenantId == command.TenantId && r.PrincipalIssuer == command.Principal.Issuer
                    && r.PrincipalSubject == command.Principal.Subject && r.Operation == Operation
                    && r.IdempotencyKey == command.IdempotencyKey,
                cancellationToken);
        if (existing is not null)
        {
            if (existing.RequestHash != requestHash)
                throw new IdempotencyKeyReusedException(Operation, command.IdempotencyKey);

            await transaction.CommitAsync(cancellationToken);
            var stored = JsonSerializer.Deserialize<OpenedPayload>(existing.ResponsePayload)
                ?? throw new InvalidOperationException("Stored idempotency response is empty.");
            return new OpenOpportunityResult(stored.OpportunityId, stored.PipelineStageId, Replayed: true);
        }

        if (opportunity.RowVersion != command.ExpectedVersion)
            throw new OpportunityConcurrencyConflictException(opportunity.Id, command.ExpectedVersion);

        var (pipelineDefinitionVersionId, pipelineStageId) = await ResolveEntryStageAsync(command.TenantId, cancellationToken);
        opportunity.Open(command.ExpiryDate, pipelineDefinitionVersionId, pipelineStageId);

        var payload = new OpenedPayload(opportunity.Id, pipelineDefinitionVersionId, pipelineStageId);
        var payloadJson = JsonSerializer.Serialize(payload);

        context.OutboxMessages.Add(OutboxMessage.Create(
            command.TenantId, nameof(Opportunity), opportunity.Id, opportunity.RowVersion,
            EventType, EventSource, $"opportunities/{opportunity.Id}", command.CorrelationId, null, payloadJson));
        context.EvidenceRecords.Add(EvidenceRecord.Create(
            command.TenantId, nameof(Opportunity), opportunity.Id, opportunity.RowVersion,
            command.Principal, "Opportunity.Open", payloadJson, command.CorrelationId));
        context.IdempotencyRecords.Add(IdempotencyRecord.Create(
            command.TenantId, command.Principal, Operation, command.IdempotencyKey, requestHash, SucceededStatus, payloadJson, IdempotencyRetention));

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new OpportunityConcurrencyConflictException(opportunity.Id, command.ExpectedVersion);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Another request with the same idempotency key committed first between our
            // lookup and our SaveChanges — replay its result instead of inventing an
            // ad-hoc lock (architecture plan §13/§7A).
            await transaction.RollbackAsync(cancellationToken);
            var winner = await context.IdempotencyRecords.AsNoTracking().SingleAsync(
                record => record.TenantId == command.TenantId
                    && record.PrincipalIssuer == command.Principal.Issuer
                    && record.PrincipalSubject == command.Principal.Subject
                    && record.Operation == Operation
                    && record.IdempotencyKey == command.IdempotencyKey,
                cancellationToken);
            if (winner.RequestHash != requestHash)
                throw new IdempotencyKeyReusedException(Operation, command.IdempotencyKey);
            var stored = JsonSerializer.Deserialize<OpenedPayload>(winner.ResponsePayload)
                ?? throw new InvalidOperationException("Stored idempotency response is empty.");
            return new OpenOpportunityResult(stored.OpportunityId, stored.PipelineStageId, Replayed: true);
        }

        await transaction.CommitAsync(cancellationToken);
        return new OpenOpportunityResult(opportunity.Id, pipelineStageId, Replayed: false);
    }

    /// <summary>Resolves the tenant's single Opportunity pipeline, per this plan's own
    /// scope note: oldest PipelineDefinition, its highest-numbered version, that
    /// version's IsEntry stage. Returns (null, null) if the tenant has no
    /// PipelineDefinition/PipelineDefinitionVersion configured at all — Open() already
    /// treats that as valid. Once a version is resolved, it must have exactly one usable
    /// (IsEntry, IsActive) stage or opening is a hard error — a configured-but-invalid
    /// pipeline is a different case from "nothing configured yet" and must never silently
    /// open with a null stage (2026-09-19 entry-stage resolution's binding invariant).</summary>
    private async Task<(long? PipelineDefinitionVersionId, long? PipelineStageId)> ResolveEntryStageAsync(
        TenantId tenantId, CancellationToken cancellationToken)
    {
        var definitionId = await context.PipelineDefinitions
            .Where(p => p.TenantId == tenantId)
            .OrderBy(p => p.Id)
            .Select(p => (long?)p.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (definitionId is not { } resolvedDefinitionId)
            return (null, null);

        var versionId = await context.PipelineDefinitionVersions
            .Where(v => v.TenantId == tenantId && v.PipelineDefinitionId == resolvedDefinitionId)
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => (long?)v.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (versionId is not { } resolvedVersionId)
            return (null, null);

        var stageId = await context.PipelineStages
            .Where(s => s.TenantId == tenantId && s.PipelineDefinitionVersionId == resolvedVersionId && s.IsEntry && s.IsActive)
            .Select(s => (long?)s.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (stageId is null)
            throw new PipelineConfigurationInvalidException(resolvedVersionId, "no active stage is flagged as the entry stage");

        return (resolvedVersionId, stageId);
    }

    // PostgreSQL error code 23505 = unique_violation (Npgsql exposes SqlState as this raw
    // string, not a named constant — verify against the installed Npgsql version's
    // PostgresException.SqlState docs before relying on this if it's ever unclear).
    private const string UniqueViolationSqlState = "23505";

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: UniqueViolationSqlState };

    private static string HashRequest(OpenOpportunityCommand command)
    {
        var canonical = string.Create(
            CultureInfo.InvariantCulture,
            $"{Operation}|{command.TenantId.Value}|{command.Principal}|{command.OpportunityId}|{command.ExpectedVersion}|{command.ExpiryDate:O}");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
