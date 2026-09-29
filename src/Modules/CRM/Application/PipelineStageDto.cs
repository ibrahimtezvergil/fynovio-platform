using CRM.Domain;

namespace CRM.Application;

public sealed record PipelineStageDto(long Id, string Name, int SortOrder, bool IsActive, bool IsEntry, bool IsArchived = false, PipelineStageKind Kind = PipelineStageKind.Open);
