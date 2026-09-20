using Access.Application.Authentication;
using Xunit;

namespace Access.Tests.Application.Authentication;

public sealed class PasswordPolicyTests
{
    private readonly PasswordPolicy _policy;

    public PasswordPolicyTests()
    {
        var options = new PasswordPolicyOptions { MinLength = 12, MaxLength = 128 };
        _policy = new PasswordPolicy(options);
    }

    [Fact]
    public void Validate_ValidPassword_ReturnsEmpty()
    {
        var violations = _policy.Validate("ValidPassword123", "user@example.com");
        Assert.Empty(violations);
    }

    [Theory]
    [InlineData("long.enough.address@example.com")]
    [InlineData("LONG.ENOUGH.ADDRESS@EXAMPLE.COM")]
    public void Validate_FullEmailAddressAsPassword_IsRejected(string password)
    {
        var violations = _policy.Validate(password, "long.enough.address@example.com");
        Assert.Contains("equals_email", violations);
    }

    [Fact]
    public void Validate_TooShort_ReturnsTooShort()
    {
        var violations = _policy.Validate("short", "user@example.com");
        Assert.Contains("too_short", violations);
    }

    [Fact]
    public void Validate_TooLong_ReturnsTooLong()
    {
        var violations = _policy.Validate(new string('a', 200), "user@example.com");
        Assert.Contains("too_long", violations);
    }

    [Fact]
    public void Validate_EqualsEmailLocalPart_ReturnsViolation()
    {
        var violations = _policy.Validate("user", "user@example.com");
        Assert.Contains("equals_email_local_part", violations);
    }

    [Fact]
    public void Validate_CaseInsensitiveEmailComparison()
    {
        var violations = _policy.Validate("USER", "user@example.com");
        Assert.Contains("equals_email_local_part", violations);
    }

    [Fact]
    public void Validate_Empty_ReturnsRequired()
    {
        var violations = _policy.Validate("", "user@example.com");
        Assert.Contains("required", violations);
    }
}
