namespace Contracts;

/// <summary>SaaS isolation boundary. Never a legal company; never reused after offboarding.</summary>
public readonly record struct TenantId
{
    public long Value { get; }

    public TenantId(long value)
    {
        if (value <= 0)
            throw new ArgumentOutOfRangeException(nameof(value), "TenantId must be a positive identifier.");

        Value = value;
    }

    public override string ToString() => Value.ToString();
}
