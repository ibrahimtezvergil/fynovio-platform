using Access.Application.Authentication;

namespace Host.Email;

/// <summary>The app's <see cref="IEmailSender"/>: renders the message, records it in the dev mailbox
/// (Development only) and hands it to the outbox. It returns immediately for every recipient, so a
/// "known" and an "unknown" address cost the caller the same time, and a mail failure can never
/// change an endpoint's response.</summary>
public sealed class EmailDispatcher(
    EmailOutbox outbox,
    Authentication.AuthenticationHostOptions authenticationOptions,
    Microsoft.Extensions.Options.IOptions<EmailOptions> emailOptions,
    ILogger<EmailDispatcher> logger,
    DevMailbox? devMailbox = null) : IEmailSender
{
    private const string DevelopmentFallbackBaseUrl = "http://localhost:5173";

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        RenderedEmail rendered;
        try
        {
            rendered = EmailRenderer.Render(
                message,
                authenticationOptions.PublicAppBaseUrl ?? DevelopmentFallbackBaseUrl,
                emailOptions.Value.Smtp.FromName);
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError("E-mail '{Template}' could not be rendered: {Reason}", message.TemplateId, exception.Message);
            return Task.CompletedTask;
        }

        devMailbox?.Record(rendered);

        if (!outbox.TryEnqueue(rendered))
            logger.LogWarning("E-mail '{Template}' for {Recipient} was dropped: the outbox is full.", rendered.TemplateId, EmailLogging.Mask(rendered.To));

        return Task.CompletedTask;
    }
}
