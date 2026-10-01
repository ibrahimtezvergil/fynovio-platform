using System.Text.Json;
using Contracts;
using CRM.Activity;
using CRM.Customization;
using CRM.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CRM.Application;

public sealed record GetOpportunityActivityQuery(TenantId TenantId, long OpportunityId, PrincipalRef Principal, Guid CorrelationId);

/// <summary>One timeline row. The facts are labelled at read time — a renamed stage or field shows its current name —
/// and only the fields that apply to <see cref="Kind"/> are set.</summary>
public sealed record OpportunityActivityDto(
    Guid EventId,
    string Kind,
    DateTimeOffset OccurredAt,
    long Version,
    string? StageName = null,
    string? FromStageName = null,
    string? PrincipalName = null,
    decimal? Amount = null,
    string? Currency = null,
    string? LostReason = null,
    IReadOnlyList<string>? ChangedFields = null);

/// <summary>The opportunity's activity timeline, newest first (adr-event-consumption.md, E-2 (a)). Authorized exactly like
/// <see cref="GetOpportunityHandler"/> — same action key and resource descriptor — and a denial looks like not-found.</summary>
public sealed class GetOpportunityActivityHandler(CrmDbContext context, IAuthorizer authorizer, ISemanticDefinitionReader definitionReader, IAuthorizedPrincipalDirectory? principalDirectory = null)
{
    public const int MaxEntries = 100;

    public async Task<IReadOnlyList<OpportunityActivityDto>?> HandleAsync(GetOpportunityActivityQuery query, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(query.TenantId, cancellationToken);

        var opportunity = await context.Opportunities.AsNoTracking().SingleOrDefaultAsync(o => o.TenantId == query.TenantId && o.Id == query.OpportunityId, cancellationToken);
        var actor = new ActorContext(query.TenantId, query.Principal, query.CorrelationId);
        if (opportunity is null || !await OpportunityReadAuthorization.IsAllowedAsync(authorizer, actor, opportunity, cancellationToken))
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        var entries = await context.OpportunityActivity.AsNoTracking()
            .Where(e => e.TenantId == query.TenantId && e.OpportunityId == query.OpportunityId)
            .OrderByDescending(e => e.OccurredAt).ThenByDescending(e => e.AggregateVersion)
            .Take(MaxEntries)
            .ToListAsync(cancellationToken);
        var facts = entries.Select(e => (Entry: e, Fact: Fact.Parse(e.Payload))).ToList();

        var stageIds = facts.SelectMany(f => new[] { f.Fact.StageId, f.Fact.FromStageId }).OfType<long>().Distinct().ToList();
        var stages = await context.PipelineStages.AsNoTracking().Where(s => s.TenantId == query.TenantId && stageIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Name, cancellationToken);

        var fieldKeys = facts.SelectMany(f => f.Fact.ChangedKeys ?? []).Distinct().ToList();
        var fieldLabels = fieldKeys.Count == 0
            ? new Dictionary<string, string>()
            : (await definitionReader.ListFieldsAsync(query.TenantId, OpportunityFields.OwnerContext, OpportunityFields.ObjectType, cancellationToken))
                .Where(d => fieldKeys.Contains(d.Key))
                .ToDictionary(d => d.Key, d => d.Label);

        var principals = facts.Select(f => f.Fact.NewPrincipal).OfType<PrincipalRef>().Distinct().ToList();
        var names = principals.Count > 0 && principalDirectory is not null
            ? await principalDirectory.ResolveDisplayNamesAsync(query.TenantId, principals, cancellationToken)
            : new Dictionary<PrincipalRef, string>();

        await transaction.CommitAsync(cancellationToken);

        return facts.Select(f => new OpportunityActivityDto(
            f.Entry.EventId,
            f.Entry.Kind,
            f.Entry.OccurredAt,
            f.Entry.AggregateVersion,
            StageName: Lookup(stages, f.Fact.StageId),
            FromStageName: Lookup(stages, f.Fact.FromStageId),
            PrincipalName: f.Fact.NewPrincipal is { } principal && names.TryGetValue(principal, out var name) ? name : null,
            Amount: f.Fact.Amount,
            Currency: f.Fact.Currency,
            LostReason: f.Fact.LostReason,
            ChangedFields: f.Fact.ChangedKeys?.Select(key => fieldLabels.GetValueOrDefault(key, key)).ToList())).ToList();
    }

    private static string? Lookup(IReadOnlyDictionary<long, string> names, long? id) =>
        id is { } value && names.TryGetValue(value, out var name) ? name : null;

    /// <summary>The fields of the producers' payloads (CreatedPayload, OpenedPayload, StageChangedPayload, …) the
    /// timeline shows. Read leniently: a field a payload lacks is simply absent.</summary>
    private sealed record Fact(long? StageId, long? FromStageId, PrincipalRef? NewPrincipal, decimal? Amount, string? Currency, string? LostReason, IReadOnlyList<string>? ChangedKeys)
    {
        public static Fact Parse(string payload)
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            return new Fact(
                Long(root, "ToStageId") ?? Long(root, "PipelineStageId") ?? Long(root, "TargetStageId") ?? Long(root, "StageId"),
                Long(root, "FromStageId"),
                Principal(String(root, "NewPrincipal")),
                Decimal(root, "TotalAmount") ?? Decimal(root, "EstimatedAmount"),
                String(root, "Currency"),
                String(root, "LostReason"),
                root.TryGetProperty("ChangedKeys", out var keys) && keys.ValueKind == JsonValueKind.Array
                    ? keys.EnumerateArray().Where(k => k.ValueKind == JsonValueKind.String).Select(k => k.GetString()!).ToList()
                    : null);
        }

        private static long? Long(JsonElement root, string name) =>
            root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number) ? number : null;

        private static decimal? Decimal(JsonElement root, string name) =>
            root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number) ? number : null;

        private static string? String(JsonElement root, string name) =>
            root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

        /// <summary><see cref="PrincipalRef.ToString"/> is <c>issuer:subject</c>; the issuer may itself contain colons.</summary>
        private static PrincipalRef? Principal(string? value)
        {
            var separator = value?.LastIndexOf(':') ?? -1;
            return separator > 0 && separator < value!.Length - 1 ? new PrincipalRef(value[..separator], value[(separator + 1)..]) : null;
        }
    }
}
