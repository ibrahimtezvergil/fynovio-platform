namespace Access.Domain.Authorization;

internal static class TemplateProvenance
{
    /// <summary>Provenance is all-or-nothing and only meaningful on a template copy.</summary>
    public static void Validate(string origin, string? moduleKey, int? version, string templateOrigin)
    {
        if (moduleKey is null && version is null)
            return;
        if (moduleKey is null || version is null)
            throw new ArgumentException("Origin module key and origin version are set together.");
        if (origin != templateOrigin)
            throw new ArgumentException("Only a system_template row carries template provenance.");
        if (string.IsNullOrWhiteSpace(moduleKey) || version < 1)
            throw new ArgumentException("Origin module key is required and origin version must be at least 1.");
    }
}
