using System.Text;

namespace Access.Application.Authentication;

/// <summary>What the Access layer hands to the mail transport. It never renders HTML and never
/// logs <see cref="Values"/>: the raw single-use token (and anything else secret) exists only
/// inside this object until the sender turns it into a link. The Host implements the sender and
/// builds links from its configured public base URL — never from request headers.</summary>
public sealed record EmailMessage(
    string To,
    string TemplateId,
    string Locale,
    IReadOnlyDictionary<string, string> Values)
{
    public const string InviteTemplate = "invite";
    public const string PasswordResetTemplate = "password_reset";

    // The synthesized ToString would print Values (i.e. the token) into any log line that formats the message.
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("TemplateId = ").Append(TemplateId);
        return true;
    }
}

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
