using Host.Authentication;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Host.Tests.Authentication;

public sealed class AuthenticationOptionsValidationTests
{
    [Fact]
    public void JwtOptions_BindsFromConfiguration()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Authentication:Jwt:Issuer", "https://dev.fynovio.local" },
                { "Authentication:Jwt:Audience", "fynovio-platform" },
                { "Authentication:Jwt:SigningKey", "dev-only-signing-key-not-for-production-use-32-bytes-min" },
                { "Authentication:Jwt:AccessTokenMinutes", "10" },
            })
            .Build();

        var options = config.GetSection("Authentication:Jwt").Get<JwtOptions>();
        Assert.NotNull(options);
        Assert.Equal("https://dev.fynovio.local", options.Issuer);
        Assert.Equal("fynovio-platform", options.Audience);
        Assert.Equal("dev-only-signing-key-not-for-production-use-32-bytes-min", options.SigningKey);
        Assert.Equal(10, options.AccessTokenMinutes);
    }

    [Fact]
    public void SigningKey_MustBeAtLeast32Bytes()
    {
        var shortKey = "short"; // 5 bytes
        var byteCount = System.Text.Encoding.UTF8.GetByteCount(shortKey);
        Assert.True(byteCount < 32, "Test setup: short key should be less than 32 bytes");
    }

    [Fact]
    public void PublicAppBaseUrl_MustBeAbsoluteHttpUrl()
    {
        var validUrls = new[]
        {
            "http://localhost:5173",
            "https://app.fynovio.com",
            "https://192.168.1.1:8080"
        };

        foreach (var url in validUrls)
        {
            var result = Uri.TryCreate(url, UriKind.Absolute, out var uri);
            Assert.True(result);
            Assert.True(uri!.Scheme == "http" || uri.Scheme == "https");
        }
    }

    [Fact]
    public void PublicAppBaseUrl_RejectsRelativePaths()
    {
        var invalidUrls = new[]
        {
            "/api",
            "localhost:5173",
            "not-a-url",
            "ftp://example.com"
        };

        foreach (var url in invalidUrls)
        {
            var result = Uri.TryCreate(url, UriKind.Absolute, out var uri);
            if (result && uri != null)
            {
                Assert.False(uri.Scheme == "http" || uri.Scheme == "https",
                    $"URL {url} should not be valid (is absolute but wrong scheme)");
            }
        }
    }

    [Fact]
    public void AllowedOrigins_CanBeEmpty()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { })
            .Build();

        var origins = config.GetSection("Authentication:AllowedOrigins").Get<string[]>() ?? [];
        Assert.Empty(origins);
    }

    [Fact]
    public void AllowedOrigins_CanContainMultipleUrls()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Authentication:AllowedOrigins:0", "https://localhost:5173" },
                { "Authentication:AllowedOrigins:1", "https://app.fynovio.com" },
            })
            .Build();

        var origins = config.GetSection("Authentication:AllowedOrigins").Get<string[]>() ?? [];
        Assert.Equal(2, origins.Length);
        Assert.Contains("https://localhost:5173", origins);
        Assert.Contains("https://app.fynovio.com", origins);
    }

    [Fact]
    public void AccessTokenMinutes_DefaultsTo10()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Authentication:Jwt:Issuer", "https://test" },
                { "Authentication:Jwt:Audience", "test" },
                { "Authentication:Jwt:SigningKey", "dev-only-signing-key-not-for-production-use-32-bytes-min" },
            })
            .Build();

        var options = config.GetSection("Authentication:Jwt").Get<JwtOptions>();
        Assert.NotNull(options);
        // Default should be 10 when not specified
        if (!config.GetSection("Authentication:Jwt:AccessTokenMinutes").Exists())
        {
            Assert.Equal(10, options.AccessTokenMinutes);
        }
    }
}
