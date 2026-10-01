namespace CRM.Customization;

/// <summary>A select choice. `Key` is stable and is what a record stores; removing a choice deprecates it
/// instead, so values already stored keep resolving to a label.</summary>
public sealed record TenantFieldOption(string Key, string Label, bool IsDeprecated = false);
