using Access.Application.Authentication;
using Xunit;

namespace Access.Tests.Application.Authentication;

public sealed class TokenSecretsTests
{
    [Fact]
    public void GenerateAndHash_CreatesDifferentTokensEachTime()
    {
        var (token1, hash1) = TokenSecrets.GenerateAndHash();
        var (token2, hash2) = TokenSecrets.GenerateAndHash();

        Assert.NotEqual(token1, token2);
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void GenerateAndHash_TokenIsNotEmpty()
    {
        var (token, hash) = TokenSecrets.GenerateAndHash();
        Assert.NotEmpty(token);
        Assert.NotEmpty(hash);
    }

    [Fact]
    public void HashToken_ConsistentResult()
    {
        var token = "test-token-12345";
        var hash1 = TokenSecrets.HashToken(token);
        var hash2 = TokenSecrets.HashToken(token);

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void HashesEqual_SameHashReturnsTrue()
    {
        var (_, hash1) = TokenSecrets.GenerateAndHash();
        var (_, hash2) = TokenSecrets.GenerateAndHash();

        Assert.False(TokenSecrets.HashesEqual(hash1, hash2));
    }

    [Fact]
    public void HashesEqual_DifferentHashReturnsFalse()
    {
        var (_, hash) = TokenSecrets.GenerateAndHash();
        var differentHash = TokenSecrets.HashToken("different-token");

        Assert.False(TokenSecrets.HashesEqual(hash, differentHash));
    }

    [Fact]
    public void Base64UrlDecode_ValidToken_Decodes()
    {
        var (token, _) = TokenSecrets.GenerateAndHash();
        var decoded = TokenSecrets.Base64UrlDecode(token);

        Assert.NotEmpty(decoded);
        Assert.Equal(32, decoded.Length); // 256 bits = 32 bytes
    }
}
