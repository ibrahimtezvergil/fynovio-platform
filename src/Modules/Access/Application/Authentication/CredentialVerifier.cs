using Access.Domain.Authentication;
using Access.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Access.Application.Authentication;

internal enum CredentialCheck
{
    Verified,
    VerifiedRehashNeeded,
    Failed,
    Locked
}

/// <summary>The password-verification rules of login, reusable by every flow that asks for an existing
/// password (accepting an invitation for an existing account, changing a password): lockout is
/// honoured, an unusable check still costs a hash, and a wrong password is counted — with a bounded
/// retry when a concurrent attempt updates the same credential. It persists ONLY the failure counter;
/// on success the caller resets the counters inside its own transaction. Call it before opening the
/// caller's transaction so a recorded failure is never rolled back with it.</summary>
internal sealed class CredentialVerifier(AccessDbContext context, PasswordService passwords, LockoutOptions lockout)
{
    private const int MaxRetries = 3;

    public async Task<CredentialCheck> CheckAsync(
        AccountCredential credential,
        string password,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (credential.IsLocked(now))
        {
            passwords.VerifyDummy(password);
            return CredentialCheck.Locked;
        }

        var result = passwords.Verify(credential.PasswordHash, password);
        if (result == PasswordVerificationResult.Failed)
        {
            await RecordFailureAsync(credential, now, cancellationToken);
            return CredentialCheck.Failed;
        }

        return result == PasswordVerificationResult.SuccessRehashNeeded
            ? CredentialCheck.VerifiedRehashNeeded
            : CredentialCheck.Verified;
    }

    private async Task RecordFailureAsync(AccountCredential credential, DateTimeOffset now, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                credential.RecordFailedAttempt(lockout.MaxFailedAttempts, lockout.LockoutMinutes, now);
                await context.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxRetries - 1)
            {
                // Another attempt updated the same credential: reload it and count this failure on top.
                await context.Entry(credential).ReloadAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return; // still contended after the retries — never let it escape as a 500
            }
        }
    }
}
