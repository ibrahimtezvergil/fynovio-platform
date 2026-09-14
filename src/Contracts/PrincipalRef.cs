namespace Contracts;

/// <summary>Typed issuer + subject. Tenant membership is bound separately, not embedded here.
/// Never use a mutable value like email as identity — issuer/subject pairs come from the IdP.</summary>
public readonly record struct PrincipalRef
{
    public string Issuer { get; }
    public string Subject { get; }

    public PrincipalRef(string issuer, string subject)
    {
        if (string.IsNullOrWhiteSpace(issuer))
            throw new ArgumentException("Issuer is required.", nameof(issuer));
        if (string.IsNullOrWhiteSpace(subject))
            throw new ArgumentException("Subject is required.", nameof(subject));

        Issuer = issuer;
        Subject = subject;
    }

    public override string ToString() => $"{Issuer}:{Subject}";
}
