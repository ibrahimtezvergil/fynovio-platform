using Host.Authentication;
using Xunit;

namespace Host.Tests.Authentication;

public sealed class ClientFingerprintTests
{
    private static JwtOptions Jwt(string key) => new()
    {
        Issuer = "https://dev.fynovio.local",
        Audience = "fynovio-platform",
        SigningKey = key
    };

    private static readonly string Key = "dev-only-signing-key-not-for-production-use-32-bytes-min";

    [Fact]
    public void Same_input_gives_the_same_fingerprint_and_different_inputs_differ()
    {
        var fingerprint = new ClientFingerprint(Jwt(Key));

        Assert.Equal(fingerprint.Hash("203.0.113.7"), fingerprint.Hash("203.0.113.7"));
        Assert.NotEqual(fingerprint.Hash("203.0.113.7"), fingerprint.Hash("203.0.113.8"));
    }

    [Fact]
    public void Output_is_32_lowercase_hex_characters_and_never_contains_the_raw_value()
    {
        var fingerprint = new ClientFingerprint(Jwt(Key));

        var hash = fingerprint.Hash("203.0.113.7")!;

        Assert.Equal(32, hash.Length);
        Assert.Matches("^[0-9a-f]{32}$", hash);
        Assert.DoesNotContain("203.0.113.7", hash);
    }

    [Fact]
    public void Fingerprints_depend_on_the_signing_key_so_they_cannot_be_precomputed()
    {
        var first = new ClientFingerprint(Jwt(Key)).Hash("Mozilla/5.0");
        var second = new ClientFingerprint(Jwt(new string('k', 40))).Hash("Mozilla/5.0");

        Assert.NotEqual(first, second);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Nothing_to_hash_yields_null(string? value) =>
        Assert.Null(new ClientFingerprint(Jwt(Key)).Hash(value));
}
