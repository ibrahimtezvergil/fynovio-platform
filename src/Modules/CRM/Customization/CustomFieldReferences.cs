using System.Text.Json;
using Contracts;

namespace CRM.Customization;

/// <summary>What a reference field's value looks like to a reader: the stored id, and either the target's label or the fact that
/// it is not available to *this* reader. The label is resolved at read time at the reader's own authorization and is never
/// stored (adr-semantic-catalog-changeset.md S-6).</summary>
public sealed record CustomFieldReferenceDto(long Id, bool Accessible, string? Label);

/// <summary>Reference fields (A-1): verification on write and hydration on read, both through `ILinkTargetDirectory` — the same
/// mechanism and the same unavailable-looks-like-missing rule the calendar's links use.</summary>
public static class CustomFieldReferences
{
    /// <summary>The ids of reference fields whose value is new or different in <paramref name="afterJson"/>. An unchanged value is
    /// not re-verified: a custom-field update replaces the whole object, so a stored reference to a record the writer can no
    /// longer see must not block an unrelated edit.</summary>
    public static IReadOnlyDictionary<string, long> Changed(IReadOnlyCollection<FieldDefinition> definitions, string? beforeJson, string? afterJson)
    {
        var before = CustomFieldValues.Parse(beforeJson);
        var after = CustomFieldValues.Parse(afterJson);
        var changed = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var definition in definitions.Where(d => d.Type == FieldType.Reference && d.IsActive))
        {
            if (!after.TryGetValue(definition.Key, out var value) || !value.TryGetInt64(out var id))
                continue;
            if (before.TryGetValue(definition.Key, out var previous) && previous.TryGetInt64(out var previousId) && previousId == id)
                continue;
            changed[definition.Key] = id;
        }

        return changed;
    }

    /// <summary>Every changed reference must resolve to an accessible record for the writer, or the write is refused with
    /// `invalid_reference`. Unknown, other-tenant and denied are deliberately indistinguishable (the directory returns
    /// `Unavailable` for all three), so the error cannot be used to probe for records.</summary>
    public static async Task VerifyAsync(
        ILinkTargetDirectory directory, ActorContext actor, IReadOnlyCollection<FieldDefinition> definitions,
        string? beforeJson, string? afterJson, CancellationToken cancellationToken)
    {
        var changed = Changed(definitions, beforeJson, afterJson);
        if (changed.Count == 0)
            return;

        var byKey = definitions.ToDictionary(d => d.Key, StringComparer.Ordinal);
        var references = changed.ToDictionary(pair => pair.Key, pair => ToEntityRef(actor.TenantId, byKey[pair.Key], pair.Value));
        var resolved = await directory.ResolveAsync(actor, references.Values.ToList(), cancellationToken);

        var errors = references
            .Where(pair => resolved.GetValueOrDefault(pair.Value) is not LinkTargetResolution.Accessible)
            .Select(pair => new CustomFieldError(pair.Key, "invalid_reference", $"'{byKey[pair.Key].Label}' must refer to an existing record you can see."))
            .ToList();
        if (errors.Count > 0)
            throw new CustomFieldValidationException(errors);
    }

    /// <summary>Hydrates the reference fields of many records with ONE directory call. Deprecated fields are included: their
    /// stored values stay readable. A record of another shape or a field without a stored id contributes nothing.</summary>
    public static async Task<IReadOnlyDictionary<long, IReadOnlyDictionary<string, CustomFieldReferenceDto>>> HydrateAsync(
        ILinkTargetDirectory directory, ActorContext actor, IReadOnlyCollection<FieldDefinition> definitions,
        IEnumerable<(long RecordId, string? CustomFieldsJson)> records, CancellationToken cancellationToken)
    {
        var referenceFields = definitions.Where(d => d.Type == FieldType.Reference).ToList();
        var wanted = new List<(long RecordId, string Key, EntityRef Reference)>();
        if (referenceFields.Count > 0)
        {
            foreach (var (recordId, json) in records)
            {
                var values = CustomFieldValues.Parse(json);
                foreach (var field in referenceFields)
                {
                    if (values.TryGetValue(field.Key, out var value) && value.TryGetInt64(out var id) && id > 0)
                        wanted.Add((recordId, field.Key, ToEntityRef(actor.TenantId, field, id)));
                }
            }
        }

        var result = new Dictionary<long, IReadOnlyDictionary<string, CustomFieldReferenceDto>>();
        if (wanted.Count == 0)
            return result;

        var resolved = await directory.ResolveAsync(actor, wanted.Select(w => w.Reference).Distinct().ToList(), cancellationToken);
        foreach (var group in wanted.GroupBy(w => w.RecordId))
        {
            result[group.Key] = group.ToDictionary(
                w => w.Key,
                w => resolved.GetValueOrDefault(w.Reference) is LinkTargetResolution.Accessible accessible
                    ? new CustomFieldReferenceDto(w.Reference.Id, true, accessible.Label)
                    : new CustomFieldReferenceDto(w.Reference.Id, false, null),
                StringComparer.Ordinal);
        }

        return result;
    }

    private static EntityRef ToEntityRef(TenantId tenantId, FieldDefinition definition, long id)
    {
        var target = definition.Config.Target ?? throw new InvalidOperationException($"Reference field '{definition.Key}' has no target.");
        return new EntityRef(tenantId, target.BoundedContext, target.EntityType, id);
    }
}
