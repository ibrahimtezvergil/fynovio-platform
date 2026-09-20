using System.Net;
using System.Net.Mail;
using System.Threading.Channels;
using Microsoft.Extensions.Options;

namespace Host.Email;

/// <summary>Bounded in-memory hand-off between the request path and the delivery worker.
/// Not durable: a crash loses queued mail (a transactional outbox is the follow-up if that matters).</summary>
public sealed class EmailOutbox
{
    private readonly Channel<RenderedEmail> _channel = Channel.CreateBounded<RenderedEmail>(
        new BoundedChannelOptions(256) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });

    /// <summary>Never blocks and never throws; false when the queue is full.</summary>
    public bool TryEnqueue(RenderedEmail email) => _channel.Writer.TryWrite(email);

    public IAsyncEnumerable<RenderedEmail> ReadAllAsync(CancellationToken cancellationToken) => _channel.Reader.ReadAllAsync(cancellationToken);
}

public interface IEmailTransport
{
    Task SendAsync(RenderedEmail email, CancellationToken cancellationToken);
}

/// <summary>Used when no SMTP provider is configured: says that a message was NOT delivered.
/// Logs the recipient (masked) and the template only — never the link or token.</summary>
public sealed class UndeliveredEmailTransport(ILogger<UndeliveredEmailTransport> logger) : IEmailTransport
{
    public Task SendAsync(RenderedEmail email, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "E-mail '{Template}' for {Recipient} was not delivered: no SMTP transport is configured (Email:Smtp:Enabled).",
            email.TemplateId, EmailLogging.Mask(email.To));
        return Task.CompletedTask;
    }
}

public sealed class SmtpEmailTransport(IOptions<EmailOptions> options) : IEmailTransport
{
    private readonly SmtpOptions _options = options.Value.Smtp;

    public async Task SendAsync(RenderedEmail email, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.Host))
            throw new InvalidOperationException("Email:Smtp:Host is required when Email:Smtp:Enabled is true.");

        // SmtpClient is flagged "not recommended for new development"; it is the BCL transport and adds no dependency.
        // A short-lived client per message keeps it free of shared mutable state.
#pragma warning disable SYSLIB0014
        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.EnableSsl,
            Timeout = _options.TimeoutSeconds * 1000,
            Credentials = string.IsNullOrEmpty(_options.Username) ? null : new NetworkCredential(_options.Username, _options.Password)
        };
#pragma warning restore SYSLIB0014

        using var message = new MailMessage
        {
            From = new MailAddress(_options.FromAddress, _options.FromName),
            Subject = email.Subject,
            Body = email.TextBody,
            IsBodyHtml = false
        };
        message.To.Add(new MailAddress(email.To));
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(email.HtmlBody, null, "text/html"));

        await client.SendMailAsync(message, cancellationToken);
    }
}

/// <summary>Drains the outbox off the request path, so the time an e-mail takes to send can never show
/// through an endpoint's response time (that would tell a caller whether an address exists).
/// Failures are logged with the template, the masked recipient and the error type — never the message.</summary>
public sealed class EmailDeliveryService(EmailOutbox outbox, IEmailTransport transport, ILogger<EmailDeliveryService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var email in outbox.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await transport.SendAsync(email, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    logger.LogWarning(
                        "E-mail '{Template}' for {Recipient} could not be delivered ({ErrorType}: {Reason}).",
                        email.TemplateId, EmailLogging.Mask(email.To), exception.GetType().Name, exception.Message);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Host shutdown.
        }
    }
}

internal static class EmailLogging
{
    /// <summary>`jane.doe@example.com` → `j***@example.com`.</summary>
    public static string Mask(string address)
    {
        var at = address.IndexOf('@');
        return at <= 0 ? "***" : $"{address[0]}***{address[at..]}";
    }
}
