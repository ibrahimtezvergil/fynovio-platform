using Access.Application.Authentication;
using Microsoft.Extensions.Options;

namespace Host.Email;

public sealed class DevInvitationPreviewRecorder(
    DevMailbox mailbox,
    Authentication.AuthenticationHostOptions authenticationOptions,
    IOptions<EmailOptions> emailOptions) : IInvitationPreviewRecorder
{
    public Task RecordAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        mailbox.Record(EmailRenderer.Render(message,
            authenticationOptions.PublicAppBaseUrl ?? "http://localhost:5173", emailOptions.Value.Smtp.FromName));
        return Task.CompletedTask;
    }
}
