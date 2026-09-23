namespace Access.Application.Authentication;

public interface IInvitationTokenProtector
{
    string Protect(string token);
    string Unprotect(string protectedToken);
}
