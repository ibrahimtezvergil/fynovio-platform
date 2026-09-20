namespace Host.Email;

/// <summary>Bound from the "Email" configuration section. SMTP is opt-in: without
/// `Email:Smtp:Enabled=true` nothing leaves the process (Development still records every message in
/// the in-memory dev mailbox). The username/password are secrets — supply them through user-secrets
/// or environment variables (`Email__Smtp__Password`), never through a tracked file.</summary>
public sealed class EmailOptions
{
    public SmtpOptions Smtp { get; init; } = new();
}

public sealed class SmtpOptions
{
    public bool Enabled { get; init; }
    public string? Host { get; init; }
    public int Port { get; init; } = 587;
    public string? Username { get; init; }
    public string? Password { get; init; }

    /// <summary>STARTTLS/implicit TLS as negotiated by <c>System.Net.Mail.SmtpClient</c>. Keep on for any real provider.</summary>
    public bool EnableSsl { get; init; } = true;

    public string FromAddress { get; init; } = "no-reply@fynovio.local";
    public string FromName { get; init; } = "Fynovio";
    public int TimeoutSeconds { get; init; } = 15;
}
