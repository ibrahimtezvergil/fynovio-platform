using System.Text.Json;
using Contracts;
using Microsoft.EntityFrameworkCore;
using SemanticCatalog.Domain;
using SemanticCatalog.Evidence;
using SemanticCatalog.Outbox;
using SemanticCatalog.Persistence;

namespace SemanticCatalog.Application;

public sealed record PublishOutcome(ChangeSet ChangeSet, bool Published, IReadOnlyList<CatalogFieldDefinition> Fields, IReadOnlyList<CatalogViewDefinition> Views);

/// <summary>Publishes a change set (adr-semantic-catalog-changeset.md S-5, OD-4). It runs inside the caller's transaction, with
/// the tenant context already set, and never commits: the caller adds its own idempotency record and saves once, so state,
/// outbox and evidence stay one atomic unit.
///
/// Publishing takes a per-tenant lock on the catalog revision. If the set was authored against an older revision it becomes
/// `Superseded` and applies nothing — a stale bundle never overwrites a newer definition. Otherwise every item is applied,
/// the revision is bumped, and the set is `Published`.
///
/// Activation: a set's *participants* are the modules that must act on it before it is usable. Field changes have none (CRM
/// reads definitions at the moment of use), so a published set is `Active` inside the publish transaction. The first
/// participant will make publishing stop at `Activating` until it acknowledges.</summary>
public sealed class ChangeSetPublisher(SemanticCatalogDbContext context)
{
    public const string PublishedEventType = "enterprise.semantic.change_set.published.v1";
    public const string FieldChangedEventType = "enterprise.crm.custom_field_definition.changed.v1";
    public const string ViewChangedEventType = "enterprise.semantic.view_definition.changed.v1";
    private const int MaxActiveViewsPerObjectType = 50;
    private const int MaxActiveFieldsPerObjectType = 100;

    /// <summary>Modules that must acknowledge a published set before it is `Active`. None for field changes.</summary>
    internal static readonly IReadOnlyList<string> ActivationParticipants = [];

    /// <summary>The revision a new draft should be authored against.</summary>
    public async Task<long> CurrentRevisionAsync(TenantId tenantId, CancellationToken cancellationToken = default) =>
        await context.CatalogRevisions.AsNoTracking().Where(r => r.TenantId == tenantId).Select(r => (long?)r.Revision).SingleOrDefaultAsync(cancellationToken) ?? 0;

    /// <summary>A new draft against the current revision, with an id allocated up front so the outbox and evidence rows of the
    /// publish can name it in the same save. A set that is drafted and published in one transaction (a settings edit) passes
    /// <paramref name="authorUnderLock"/>: the base revision is then read under the tenant lock the publish takes anyway, so a
    /// concurrent publish cannot make it stale. A draft that waits for review reads the revision unlocked — staleness is exactly
    /// what the publish-time check is for.</summary>
    public async Task<ChangeSet> DraftAsync(
        TenantId tenantId, PrincipalRef author, IEnumerable<ChangeSetItem> items, bool authorUnderLock = false, CancellationToken cancellationToken = default)
    {
        var baseRevision = authorUnderLock ? await LockRevisionAsync(tenantId, cancellationToken) : await CurrentRevisionAsync(tenantId, cancellationToken);
        var set = ChangeSet.Draft(tenantId, author, baseRevision, items);
        context.Entry(set).Property(x => x.Id).CurrentValue = await context.AllocateChangeSetIdAsync(cancellationToken);
        return set;
    }

    /// <summary>Locks the tenant's revision row (creating it on first use) and returns the revision.</summary>
    private async Task<long> LockRevisionAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO semantic.catalog_revisions (tenant_id, revision) VALUES ({tenantId.Value}, 0) ON CONFLICT (tenant_id) DO NOTHING", cancellationToken);
        return await context.Database.SqlQuery<long>(
            $"SELECT revision AS \"Value\" FROM semantic.catalog_revisions WHERE tenant_id = {tenantId.Value} FOR UPDATE").SingleAsync(cancellationToken);
    }

    public async Task<PublishOutcome> PublishAsync(ChangeSet set, PrincipalRef principal, Guid correlationId, CancellationToken cancellationToken = default)
    {
        if (context.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Publishing must run inside the caller's transaction.");
        if (set.Status != ChangeSetStatus.Approved)
            throw new InvalidOperationException($"Only an approved change set can be published (it is {set.Status}).");

        if (context.Entry(set).State == EntityState.Detached)
            context.ChangeSets.Add(set);

        var current = await LockRevisionAsync(set.TenantId, cancellationToken);
        if (set.BaseRevision != current)
        {
            set.Supersede();
            context.EvidenceRecords.Add(SetEvidence(set, principal, correlationId, current, "ChangeSet.Superseded"));
            return new PublishOutcome(set, false, [], []);
        }

        var fields = new List<CatalogFieldDefinition>();
        var views = new List<CatalogViewDefinition>();
        foreach (var item in set.Items.OrderBy(i => i.Ordinal))
        {
            // Items are applied in order, and a view's field columns are resolved against the fields as they stand at that point.
            if (item.TargetKind == ChangeSetItem.FieldKind)
            {
                var definition = await ApplyFieldAsync(set.TenantId, item, cancellationToken);
                fields.Add(definition);
                AddFieldFacts(set, item, definition, principal, correlationId);
            }
            else if (item.TargetKind == ChangeSetItem.ViewKind)
            {
                var view = await ApplyViewAsync(set.TenantId, item, cancellationToken);
                views.Add(view);
                AddViewFacts(set, item, view, principal, correlationId);
            }
            else
                throw new InvalidOperationException($"Unknown change set item kind '{item.TargetKind}'.");
        }

        var revision = current + 1;
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE semantic.catalog_revisions SET revision = {revision} WHERE tenant_id = {set.TenantId.Value}", cancellationToken);

        set.Publish(revision);
        if (ActivationParticipants.Count == 0)
            set.CompleteActivation();
        else
            set.BeginActivation();

        context.OutboxMessages.Add(OutboxMessage.Create(
            set.TenantId, nameof(ChangeSet), set.Id, set.RowVersion, PublishedEventType, "semantic-catalog", $"semantic/change-sets/{set.Id}",
            correlationId, null,
            JsonSerializer.Serialize(new
            {
                changeSetId = set.Id,
                revision,
                source = set.Source.ToString(),
                contentHash = set.ContentHash,
                itemCount = set.Items.Count,
                items = set.Items.Select(i => new { kind = i.TargetKind, operation = i.Operation.ToString(), key = i.Key })
            })));
        context.EvidenceRecords.Add(SetEvidence(set, principal, correlationId, revision, "ChangeSet.Published"));

        return new PublishOutcome(set, true, fields, views);
    }

    private void AddFieldFacts(ChangeSet set, ChangeSetItem item, CatalogFieldDefinition definition, PrincipalRef principal, Guid correlationId)
    {
        // The pre-changeset event and evidence of a field edit are kept as they were, so no existing consumer or audit reader sees a change.
        context.OutboxMessages.Add(OutboxMessage.Create(
            set.TenantId, "TenantFieldDefinition", definition.Id, definition.RowVersion, FieldChangedEventType, "semantic-catalog",
            $"crm-customization/field-definition/{definition.Id}", correlationId, null,
            JsonSerializer.Serialize(new
            {
                id = definition.Id,
                aggregateType = "Opportunity",
                fieldName = definition.Key,
                operation = item.Operation.ToString(),
                rowVersion = definition.RowVersion
            })));

        context.EvidenceRecords.Add(EvidenceRecord.Create(
            set.TenantId, "TenantFieldDefinition", definition.Id, definition.RowVersion, principal, $"TenantFieldDefinition.{item.Operation}",
            JsonSerializer.Serialize(new
            {
                fieldName = definition.Key,
                aggregateType = "Opportunity",
                operation = item.Operation.ToString(),
                rowVersion = definition.RowVersion,
                changeSetId = set.Id
            }),
            correlationId));
    }

    private static EvidenceRecord SetEvidence(ChangeSet set, PrincipalRef principal, Guid correlationId, long revision, string action) =>
        EvidenceRecord.Create(
            set.TenantId, nameof(ChangeSet), set.Id, set.RowVersion, principal, action,
            JsonSerializer.Serialize(new
            {
                source = set.Source.ToString(),
                // A human holding the write action approves their own one-item set; the approval is recorded, not skipped.
                approval = "self",
                contentHash = set.ContentHash,
                baseRevision = set.BaseRevision,
                revision,
                itemCount = set.Items.Count,
                transitions = set.Transitions.Select(t => $"{t.From}->{t.To}")
            }),
            correlationId);

    private async Task<CatalogFieldDefinition> ApplyFieldAsync(TenantId tenantId, ChangeSetItem item, CancellationToken ct)
    {
        var content = item.Field;
        switch (item.Operation)
        {
            case ChangeOperation.Create:
                {
                    await EnsureRoomForActiveFieldAsync(tenantId, content.OwnerContext, content.ObjectType, ct);
                    var definition = CatalogFieldDefinition.Create(
                        tenantId, content.OwnerContext, content.ObjectType, content.Key, content.Label,
                        content.Type ?? throw new ArgumentException("A create item needs a field type."),
                        content.IsRequired, content.Config, content.SortOrder);
                    context.Entry(definition).Property(x => x.Id).CurrentValue = await context.AllocateFieldDefinitionIdAsync(ct);
                    context.FieldDefinitions.Add(definition);
                    return definition;
                }
            case ChangeOperation.Update:
                {
                    var definition = await LoadFieldAsync(tenantId, content, item.TargetId, ct);
                    CheckVersion(definition.RowVersion, content.ExpectedRowVersion);
                    definition.Update(content.Label, content.IsRequired, content.Config ?? FieldConfig.Empty, content.SortOrder);
                    return definition;
                }
            case ChangeOperation.Deprecate:
                {
                    var definition = await LoadFieldAsync(tenantId, content, item.TargetId, ct);
                    CheckVersion(definition.RowVersion, content.ExpectedRowVersion);
                    definition.Deprecate();
                    return definition;
                }
            case ChangeOperation.Reactivate:
                {
                    var definition = await LoadFieldAsync(tenantId, content, item.TargetId, ct);
                    CheckVersion(definition.RowVersion, content.ExpectedRowVersion);
                    await EnsureRoomForActiveFieldAsync(tenantId, definition.OwnerContext, definition.ObjectType, ct);
                    definition.Reactivate();
                    return definition;
                }
            default:
                throw new ArgumentOutOfRangeException(nameof(item));
        }
    }

    private async Task<CatalogFieldDefinition> LoadFieldAsync(TenantId tenantId, FieldChangeContent content, long? targetId, CancellationToken ct) =>
        await context.FieldDefinitions.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.OwnerContext == content.OwnerContext && x.Id == targetId, ct)
            ?? throw new KeyNotFoundException($"Field definition {targetId} was not found.");

    private async Task EnsureRoomForActiveFieldAsync(TenantId tenantId, string ownerContext, string objectType, CancellationToken ct)
    {
        var activeCount = await context.FieldDefinitions
            .CountAsync(x => x.TenantId == tenantId && x.OwnerContext == ownerContext && x.ObjectType == objectType && x.Status == FieldStatus.Active, ct);
        if (activeCount >= MaxActiveFieldsPerObjectType)
            throw new FieldLimitExceededException(MaxActiveFieldsPerObjectType);
    }

    private static void CheckVersion(long actual, long expected)
    {
        if (actual != expected) throw new CatalogConcurrencyConflictException();
    }

    private void AddViewFacts(ChangeSet set, ChangeSetItem item, CatalogViewDefinition view, PrincipalRef principal, Guid correlationId)
    {
        // Keys and the operation only — never the columns' content (OD-7: metadata, and only as much of it as an event needs).
        context.OutboxMessages.Add(OutboxMessage.Create(
            set.TenantId, nameof(CatalogViewDefinition), view.Id, view.RowVersion, ViewChangedEventType, "semantic-catalog",
            $"semantic/view-definitions/{view.Id}", correlationId, null,
            JsonSerializer.Serialize(new { id = view.Id, ownerContext = view.OwnerContext, objectType = view.ObjectType, key = view.Key, operation = item.Operation.ToString(), rowVersion = view.RowVersion })));

        context.EvidenceRecords.Add(EvidenceRecord.Create(
            set.TenantId, nameof(CatalogViewDefinition), view.Id, view.RowVersion, principal, $"ViewDefinition.{item.Operation}",
            JsonSerializer.Serialize(new { key = view.Key, operation = item.Operation.ToString(), rowVersion = view.RowVersion, columnCount = view.Columns.Count, changeSetId = set.Id }),
            correlationId));
    }

    private async Task<CatalogViewDefinition> ApplyViewAsync(TenantId tenantId, ChangeSetItem item, CancellationToken ct)
    {
        var content = item.View;
        switch (item.Operation)
        {
            case ChangeOperation.Create:
                {
                    await EnsureRoomForActiveViewAsync(tenantId, content.OwnerContext, content.ObjectType, ct);
                    var view = CatalogViewDefinition.Create(tenantId, content.OwnerContext, content.ObjectType, content.Key, content.Name, content.Columns, content.SortOrder);
                    context.Entry(view).Property(x => x.Id).CurrentValue = await context.AllocateViewDefinitionIdAsync(ct);
                    var fieldIds = await ResolveFieldColumnsAsync(tenantId, view, ct);
                    context.ViewDefinitions.Add(view);
                    AddEdges(tenantId, view.Id, fieldIds);
                    return view;
                }
            case ChangeOperation.Update:
                {
                    var view = await LoadViewAsync(tenantId, content, item.TargetId, ct);
                    CheckVersion(view.RowVersion, content.ExpectedRowVersion);
                    view.Update(content.Name, content.Columns, content.SortOrder);
                    var fieldIds = await ResolveFieldColumnsAsync(tenantId, view, ct);
                    await ReplaceEdgesAsync(tenantId, view.Id, fieldIds, ct);
                    return view;
                }
            case ChangeOperation.Deprecate:
                {
                    var view = await LoadViewAsync(tenantId, content, item.TargetId, ct);
                    CheckVersion(view.RowVersion, content.ExpectedRowVersion);
                    view.Deprecate();
                    return view;
                }
            case ChangeOperation.Reactivate:
                {
                    var view = await LoadViewAsync(tenantId, content, item.TargetId, ct);
                    CheckVersion(view.RowVersion, content.ExpectedRowVersion);
                    await EnsureRoomForActiveViewAsync(tenantId, view.OwnerContext, view.ObjectType, ct);
                    view.Reactivate();
                    return view;
                }
            default:
                throw new ArgumentOutOfRangeException(nameof(item));
        }
    }

    /// <summary>Resolves a view's field columns to definition ids: each key must exist for this owner and object type, and a new or
    /// edited view may not use a deprecated one. Unknown keys are refused rather than stored, so an edge can never dangle.</summary>
    private async Task<IReadOnlyList<long>> ResolveFieldColumnsAsync(TenantId tenantId, CatalogViewDefinition view, CancellationToken ct)
    {
        var keys = view.Columns.Where(c => c.Kind == ViewColumn.Field).Select(c => c.Key).ToList();
        if (keys.Count == 0)
            return [];

        // Fields already created or changed by an earlier item of this very set are tracked but not yet saved; they win over the database.
        var tracked = context.FieldDefinitions.Local
            .Where(f => f.TenantId == tenantId && f.OwnerContext == view.OwnerContext && f.ObjectType == view.ObjectType && keys.Contains(f.Key))
            .Select(f => new { f.Id, f.Key, f.Status })
            .ToList();
        var stored = await context.FieldDefinitions.AsNoTracking()
            .Where(f => f.TenantId == tenantId && f.OwnerContext == view.OwnerContext && f.ObjectType == view.ObjectType && keys.Contains(f.Key))
            .Select(f => new { f.Id, f.Key, f.Status })
            .ToListAsync(ct);
        var found = tracked.Concat(stored.Where(row => tracked.All(t => t.Key != row.Key))).ToList();

        foreach (var key in keys)
        {
            var field = found.SingleOrDefault(f => f.Key == key) ?? throw new ViewColumnUnknownException(key);
            if (field.Status != FieldStatus.Active)
                throw new ViewColumnDeprecatedException(key);
        }

        return found.Select(f => f.Id).ToList();
    }

    private void AddEdges(TenantId tenantId, long viewId, IReadOnlyList<long> fieldIds)
    {
        foreach (var fieldId in fieldIds.Distinct())
            context.DependencyEdges.Add(DependencyEdge.ViewUsesField(tenantId, viewId, fieldId));
    }

    /// <summary>The view's edges are recomputed from its new content: delete and insert inside the publish transaction. The delete is
    /// issued now so the same edge can be inserted again by this save.</summary>
    private async Task ReplaceEdgesAsync(TenantId tenantId, long viewId, IReadOnlyList<long> fieldIds, CancellationToken ct)
    {
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM semantic.dependency_edges WHERE tenant_id = {tenantId.Value} AND from_kind = 'view' AND from_id = {viewId}", ct);
        AddEdges(tenantId, viewId, fieldIds);
    }

    private async Task<CatalogViewDefinition> LoadViewAsync(TenantId tenantId, ViewChangeContent content, long? targetId, CancellationToken ct) =>
        await context.ViewDefinitions.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.OwnerContext == content.OwnerContext && x.Id == targetId, ct)
            ?? throw new KeyNotFoundException($"View {targetId} was not found.");

    private async Task EnsureRoomForActiveViewAsync(TenantId tenantId, string ownerContext, string objectType, CancellationToken ct)
    {
        var activeCount = await context.ViewDefinitions
            .CountAsync(x => x.TenantId == tenantId && x.OwnerContext == ownerContext && x.ObjectType == objectType && x.Status == FieldStatus.Active, ct);
        if (activeCount >= MaxActiveViewsPerObjectType)
            throw new ViewLimitExceededException(MaxActiveViewsPerObjectType);
    }
}
