using Contracts;
using TenantLifecycle.Domain;

namespace TenantLifecycle.Tests.Domain;

public sealed class TenantProfileTests
{
    private static readonly TenantProfileDetails Details = new(
        "Fynovio", "Fynovio Teknoloji A.Ş.", "1234567890", "Kadıköy", "INFO@FYNOVIO.TEST", "+90 555 000 00 00",
        "Acıbadem Mah.\nİstanbul", "Europe/Istanbul", "TRY");

    [Fact]
    public void Provision_normalizes_email_and_sets_the_natural_tenant_identity()
    {
        var profile = TenantProfile.Provision(new TenantId(42), Details);

        Assert.Equal(new TenantId(42), profile.TenantId);
        Assert.Equal("info@fynovio.test", profile.Email);
        Assert.Equal(1, profile.RowVersion);
    }

    [Theory]
    [InlineData(" Fynovio")]
    [InlineData("Fynovio ")]
    [InlineData("Fynovio\nA.Ş.")]
    public void Provision_rejects_untrimmed_or_multiline_display_names(string displayName) =>
        Assert.Throws<ArgumentException>(() => TenantProfile.Provision(new TenantId(1), Details with { DisplayName = displayName }));

    [Fact]
    public void Replace_increments_the_version_after_validating_every_value()
    {
        var profile = TenantProfile.Provision(new TenantId(1), Details);

        profile.Replace(Details with { DisplayName = "Fynovio Platform", CurrencyCode = "EUR" });

        Assert.Equal("Fynovio Platform", profile.DisplayName);
        Assert.Equal("EUR", profile.CurrencyCode);
        Assert.Equal(2, profile.RowVersion);
    }
}
