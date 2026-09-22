using Npgsql;

namespace TenantLifecycle.Tests.Integration;

/// <summary>Each documented row CHECK is exercised through raw SQL so domain validation cannot mask database drift.</summary>
[Collection(nameof(PostgresCollection))]
public sealed class TenantProfileConstraintTests(PostgresFixture fixture)
{
    [Theory]
    [InlineData("tenant", "0", "ck_tenant_profiles_tenant_id")]
    [InlineData("display", " leading", "ck_tenant_profiles_display_name")]
    [InlineData("legal", "name\nnext", "ck_tenant_profiles_legal_name")]
    [InlineData("tax_number", " 123", "ck_tenant_profiles_tax_number")]
    [InlineData("tax_office", "office\t", "ck_tenant_profiles_tax_office")]
    [InlineData("email", "INFO@EXAMPLE.TEST", "ck_tenant_profiles_email")]
    [InlineData("phone", "555\n123", "ck_tenant_profiles_phone")]
    [InlineData("address", "line\tbad", "ck_tenant_profiles_address")]
    [InlineData("timezone", "Europe/Istanbul ", "ck_tenant_profiles_timezone")]
    [InlineData("currency", "try", "ck_tenant_profiles_currency_code")]
    public async Task Every_profile_check_rejects_its_invalid_value(string field, string value, string constraint)
    {
        await using var connection = new NpgsqlConnection(fixture.AdminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            INSERT INTO tenant_lifecycle.tenant_profiles
            (tenant_id, display_name, legal_name, tax_number, tax_office, email, phone, address, timezone, currency_code, row_version, created_at, updated_at)
            VALUES (@tenant, @display, @legal, @tax_number, @tax_office, @email, @phone, @address, @timezone, @currency, 1, now(), now())
            """, connection);
        command.Parameters.AddWithValue("tenant", 83000L + Array.IndexOf(Fields, field));
        command.Parameters.AddWithValue("display", "Valid company");
        command.Parameters.AddWithValue("legal", "Valid legal name");
        command.Parameters.AddWithValue("tax_number", "12345");
        command.Parameters.AddWithValue("tax_office", "Valid office");
        command.Parameters.AddWithValue("email", "info@example.test");
        command.Parameters.AddWithValue("phone", "555123");
        command.Parameters.AddWithValue("address", "Valid address");
        command.Parameters.AddWithValue("timezone", "Europe/Istanbul");
        command.Parameters.AddWithValue("currency", "TRY");
        command.Parameters[field == "tenant" ? "tenant" : field].Value = field == "tenant" ? long.Parse(value) : value;

        var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal("23514", error.SqlState);
        Assert.Equal(constraint, error.ConstraintName);
    }

    private static readonly string[] Fields = ["tenant", "display", "legal", "tax_number", "tax_office", "email", "phone", "address", "timezone", "currency"];
}
