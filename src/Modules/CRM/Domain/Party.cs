using Contracts;

namespace CRM.Domain;

public enum PartyCreationSource
{
    Manual,
    AiVoiceCapture
}

/// <summary>Kept in the CRM schema as a named pilot exception to doc 08's Master Data
/// ownership — see docs/schema/crm-sales-schema.md, "Open architectural note".</summary>
public sealed class Party
{
    public long Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Surname { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public PartyCreationSource CreationSource { get; private set; }
    public long? MergedIntoPartyId { get; private set; }
    public string? CustomFields { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Party() { }

    public static Party Create(
        TenantId tenantId,
        string name,
        PartyCreationSource creationSource,
        string? surname = null,
        string? phone = null,
        string? email = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));

        var now = DateTimeOffset.UtcNow;
        return new Party
        {
            TenantId = tenantId,
            Name = name,
            Surname = surname,
            Phone = phone,
            Email = email,
            CreationSource = creationSource,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>Merges an AI-derived draft record into the canonical record it duplicates
    /// (see docs/schema/crm-sales-schema.md §"merged_into_party_id" rename rationale).</summary>
    public void MergeInto(long canonicalPartyId)
    {
        if (canonicalPartyId == Id)
            throw new InvalidOperationException("A party cannot merge into itself.");

        MergedIntoPartyId = canonicalPartyId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetCustomFields(string? customFieldsJson)
    {
        CustomFields = customFieldsJson;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
