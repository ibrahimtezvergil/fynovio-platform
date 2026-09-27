namespace Host.Bootstrap;

/// <summary>The minimal pipeline every CRM-enabled tenant gets automatically
/// (docs/architecture-analysis/2026-09-27-crm-opportunity-pipeline-won-lost-stage-integration.md
/// §5 item 7.a/§6.1) — one open-kind entry stage, nothing else. A tenant can rename or
/// extend it immediately after through the ordinary pipeline-draft endpoints; this exists
/// only so "CRM enabled" never means "cannot open an opportunity."</summary>
public static class CrmDefaultPipelineSeed
{
    public const string PipelineName = "Sales pipeline";
    public static readonly IReadOnlyList<string> ActiveStageNames = ["Open"];
}
