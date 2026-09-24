using Contracts;
using CRM.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CRM.Application;

public sealed class GetCrmSettingsHandler(CrmDbContext context, IAuthorizer authorizer)
{
    public async Task<CrmSettingsDto> HandleAsync(GetCrmSettingsQuery query, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(query.TenantId, cancellationToken);
        await AuthorizeAsync(query.TenantId, query.Principal, query.CorrelationId, CrmActionKeys.SettingsRead, cancellationToken);
        var settings = await context.CrmSettings.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == query.TenantId, cancellationToken);
        var definitions = await context.PipelineDefinitions.AsNoTracking().Where(x => x.TenantId == query.TenantId).OrderBy(x => x.Name).ToListAsync(cancellationToken);
        var versions = await context.PipelineDefinitionVersions.AsNoTracking().Where(x => x.TenantId == query.TenantId).ToListAsync(cancellationToken);
        var stageRows = await context.PipelineStages.AsNoTracking().Where(x => x.TenantId == query.TenantId).OrderBy(x => x.SortOrder).ToListAsync(cancellationToken);
        var transitionRows = await context.PipelineStageTransitions.AsNoTracking().Where(x => x.TenantId == query.TenantId).ToListAsync(cancellationToken);
        var pipelines = definitions.Select(definition => new PipelineDefinitionDto(definition.Id, definition.Name, definition.RowVersion, definition.IsActive, definition.IsArchived,
            versions.Where(version => version.PipelineDefinitionId == definition.Id).OrderByDescending(version => version.VersionNumber).Select(version => new PipelineVersionDto(
                version.Id, version.VersionNumber, version.Status.ToString(), version.EnforceAllowedTransitions, version.PublishedAt,
                stageRows.Where(stage => stage.PipelineDefinitionVersionId == version.Id).Select(stage => new PipelineStageDto(stage.Id, stage.Name, stage.SortOrder, stage.IsActive, stage.IsEntry, stage.IsArchived)).ToList(),
                transitionRows.Where(edge => edge.PipelineDefinitionVersionId == version.Id).Select(edge => new PipelineTransitionDto(edge.FromStageId, edge.ToStageId)).ToList())).ToList())).ToList();
        var types = await context.OpportunityTypes.AsNoTracking().Where(x => x.TenantId == query.TenantId).OrderBy(x => x.Name).ToListAsync(cancellationToken);
        var reasons = await context.LostReasons.AsNoTracking().Where(x => x.TenantId == query.TenantId).OrderBy(x => x.Name).ToListAsync(cancellationToken);
        var needs = await context.CustomerNeeds.AsNoTracking().Where(x => x.TenantId == query.TenantId).OrderBy(x => x.Name).ToListAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return CrmSettingsMapper.Map(settings, pipelines, types, reasons, needs);
    }

    internal static async Task AuthorizeAsync(TenantId tenantId, PrincipalRef principal, Guid correlationId, string actionKey, IAuthorizer authorizer, CancellationToken cancellationToken)
    {
        var decision = await authorizer.AuthorizeAsync(new AuthorizationRequest(
            new ActorContext(tenantId, principal, correlationId), new ActionKey(actionKey), new ResourceDescriptor("CrmSettings", null, null)), cancellationToken);
        if (!decision.IsAllowed) throw new OpportunityAuthorizationDeniedException(actionKey, decision.ReasonCode, decision.DenialStage);
    }

    private Task AuthorizeAsync(TenantId tenantId, PrincipalRef principal, Guid correlationId, string actionKey, CancellationToken cancellationToken) => AuthorizeAsync(tenantId, principal, correlationId, actionKey, authorizer, cancellationToken);
}

internal static class CrmSettingsMapper
{
    public static CrmSettingsDto Map(CRM.Domain.CrmSettings? settings, IReadOnlyList<PipelineDefinitionDto> pipelines, IReadOnlyList<CRM.Domain.OpportunityType> types, IReadOnlyList<CRM.Domain.LostReason> reasons, IReadOnlyList<CRM.Domain.CustomerNeed> needs) =>
        new(settings is null ? pipelines.Where(pipeline => pipeline.IsActive && !pipeline.IsArchived).OrderBy(pipeline => pipeline.Id).Select(pipeline => (long?)pipeline.Id).FirstOrDefault() : settings.DefaultPipelineDefinitionId,
            (settings?.OpportunityCreationMode ?? CRM.Domain.OpportunityCreationMode.Form).ToString(),
            settings?.DefaultOpportunityTypeId, settings?.RequireLostReason ?? false, settings?.RequireWonLine ?? true,
            (settings?.DefaultAssignmentMode ?? CRM.Domain.AssignmentMode.Manual).ToString(),
            (settings?.AssignmentPolicy ?? CRM.Domain.AssignmentPolicy.AnyAssignablePrincipal).ToString(),
            settings?.DefaultPrincipalIssuer is { } issuer && settings.DefaultPrincipalSubject is { } subject ? new PrincipalRef(issuer, subject) : null,
            settings?.DefaultTeamId, settings?.DefaultTerritoryId, settings?.RowVersion ?? 0,
            pipelines, types.Select(x => new CrmConfigurationItemDto(x.Id, x.Key, x.Name, x.Status.ToString(), x.RowVersion)).ToList(),
            reasons.Select(x => new CrmConfigurationItemDto(x.Id, x.Key, x.Name, x.Status.ToString(), x.RowVersion)).ToList(),
            needs.Select(x => new CustomerNeedDto(x.Id, x.Name, x.Category, x.AveragePrice, x.Status.ToString(), x.RowVersion)).ToList());
}
