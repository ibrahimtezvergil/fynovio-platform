using System.Net;
using System.Text;
using Access.Application.Authentication;

namespace Host.Email;

/// <summary>A message ready for a transport. <see cref="ToString"/> is deliberately redacted: the body
/// and <see cref="Link"/> carry the single-use token, so nothing that formats this record may print them.</summary>
public sealed record RenderedEmail(string To, string TemplateId, string Subject, string TextBody, string HtmlBody, string Link, string Token)
{
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("TemplateId = ").Append(TemplateId);
        return true;
    }
}

/// <summary>Turns an <see cref="EmailMessage"/> into subject/text/html. Links are built from the configured
/// public base URL — never from request headers — and carry the token in the URL *fragment*
/// (<c>/accept-invite#token=…</c>), which browsers do not send to servers, proxies or `Referer`.</summary>
public static class EmailRenderer
{
    public static string InvitePath => "/accept-invite";
    public static string PasswordResetPath => "/reset-password";

    public static RenderedEmail Render(EmailMessage message, string publicBaseUrl, string fromName)
    {
        ArgumentNullException.ThrowIfNull(message);

        var token = message.Values.TryGetValue("token", out var value) && !string.IsNullOrEmpty(value)
            ? value
            : throw new InvalidOperationException($"E-mail template '{message.TemplateId}' requires a token value.");

        var path = message.TemplateId switch
        {
            EmailMessage.InviteTemplate => InvitePath,
            EmailMessage.PasswordResetTemplate => PasswordResetPath,
            _ => throw new InvalidOperationException($"Unknown e-mail template '{message.TemplateId}'.")
        };

        var link = $"{publicBaseUrl.TrimEnd('/')}{path}#token={Uri.EscapeDataString(token)}";
        var english = !string.Equals(message.Locale, "tr", StringComparison.OrdinalIgnoreCase);

        var (subject, intro, action, outro) = (message.TemplateId, english) switch
        {
            (EmailMessage.InviteTemplate, true) => (
                $"You have been invited to {fromName}",
                $"You have been invited to join a workspace on {fromName}.",
                "Accept the invitation",
                "The link works once and expires. If you did not expect this, ignore this message."),
            (EmailMessage.InviteTemplate, false) => (
                $"{fromName} çalışma alanına davet edildiniz",
                $"{fromName} üzerinde bir çalışma alanına katılmak için davet edildiniz.",
                "Daveti kabul et",
                "Bağlantı tek kullanımlıktır ve süresi dolar. Bu daveti beklemiyorsanız bu iletiyi yok sayın."),
            (_, true) => (
                $"Reset your {fromName} password",
                "We received a request to set or reset the password of your account.",
                "Choose a new password",
                "The link works once and expires shortly. If you did not ask for this, ignore this message — your password is unchanged."),
            _ => (
                $"{fromName} şifrenizi sıfırlayın",
                "Hesabınızın şifresini belirlemek veya sıfırlamak için bir istek aldık.",
                "Yeni şifre belirle",
                "Bağlantı tek kullanımlıktır ve kısa sürede sona erer. Bu isteği siz yapmadıysanız bu iletiyi yok sayın; şifreniz değişmedi.")
        };

        var text = $"{intro}\n\n{action}: {link}\n\n{outro}\n";
        var html = $"<p>{WebUtility.HtmlEncode(intro)}</p><p><a href=\"{WebUtility.HtmlEncode(link)}\">{WebUtility.HtmlEncode(action)}</a></p><p>{WebUtility.HtmlEncode(outro)}</p>";

        return new RenderedEmail(message.To, message.TemplateId, subject, text, html, link, token);
    }
}
