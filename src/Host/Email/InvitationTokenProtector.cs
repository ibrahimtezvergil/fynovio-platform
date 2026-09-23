using Access.Application.Authentication;
using Microsoft.AspNetCore.DataProtection;

namespace Host.Email;

public sealed class InvitationTokenProtector(IDataProtectionProvider provider) : IInvitationTokenProtector
{
    private readonly IDataProtector _protector = provider.CreateProtector("fynovio.invitation-delivery.v1");

    public string Protect(string token) => _protector.Protect(token);

    public string Unprotect(string protectedToken) => _protector.Unprotect(protectedToken);
}
