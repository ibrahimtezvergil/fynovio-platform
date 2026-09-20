using Access.Domain.Authentication;
using Xunit;

namespace Access.Tests.Domain.Authentication;

public sealed class RefreshTokenTests
{
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;
    private readonly Guid _sessionId = Guid.NewGuid();

    [Fact]
    public void Create_ValidInputs_CreatesToken()
    {
        var issuedAt = _now;
        var expiresAt = _now.AddDays(7);

        var token = RefreshToken.Create(_sessionId, "hash123", issuedAt, expiresAt);

        Assert.NotEqual(Guid.Empty, token.Id);
        Assert.Equal(_sessionId, token.SessionId);
        Assert.Equal("hash123", token.TokenHash);
        Assert.False(token.IsRotated);
    }

    [Fact]
    public void IsExpired_BeforeExpiry_ReturnsFalse()
    {
        var issuedAt = _now;
        var expiresAt = _now.AddDays(7);
        var token = RefreshToken.Create(_sessionId, "hash123", issuedAt, expiresAt);

        var checkTime = _now.AddDays(5);
        Assert.False(token.IsExpired(checkTime));
    }

    [Fact]
    public void IsExpired_AfterExpiry_ReturnsTrue()
    {
        var issuedAt = _now;
        var expiresAt = _now.AddDays(7);
        var token = RefreshToken.Create(_sessionId, "hash123", issuedAt, expiresAt);

        var checkTime = _now.AddDays(10);
        Assert.True(token.IsExpired(checkTime));
    }

    [Fact]
    public void MarkRotated_MarksTokenAsRotated()
    {
        var token = RefreshToken.Create(_sessionId, "hash123", _now, _now.AddDays(7));
        var newTokenId = Guid.NewGuid();

        token.MarkRotated(newTokenId, _now);

        Assert.True(token.IsRotated);
        Assert.NotNull(token.RotatedAt);
        Assert.Equal(newTokenId, token.ReplacedById);
    }

    [Fact]
    public void MarkRotated_AlreadyRotated_Throws()
    {
        var token = RefreshToken.Create(_sessionId, "hash123", _now, _now.AddDays(7));
        token.MarkRotated(Guid.NewGuid(), _now);

        Assert.Throws<InvalidOperationException>(() => token.MarkRotated(Guid.NewGuid(), _now));
    }
}
