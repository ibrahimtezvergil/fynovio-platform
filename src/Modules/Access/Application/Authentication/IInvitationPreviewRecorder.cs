namespace Access.Application.Authentication;

public interface IInvitationPreviewRecorder
{
    Task RecordAsync(EmailMessage message, CancellationToken cancellationToken);
}
