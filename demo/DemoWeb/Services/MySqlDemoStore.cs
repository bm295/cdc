using DemoWeb.Models;
using DemoWeb.Options;
using Microsoft.Extensions.Options;
using MySqlConnector;

namespace DemoWeb.Services;

public sealed class MySqlDemoStore(IOptions<CdcDemoOptions> options)
{
    private readonly CdcDemoOptions _options = options.Value;

    public Task<IReadOnlyList<CustomerRow>> GetSourceCustomersAsync(CancellationToken cancellationToken)
    {
        return QueryCustomersAsync(_options.SourceConnectionString, "customers", cancellationToken);
    }

    public Task<IReadOnlyList<CustomerRow>> GetReplicaCustomersAsync(CancellationToken cancellationToken)
    {
        return QueryCustomersAsync(_options.ReplicaConnectionString, "customers_replica", cancellationToken);
    }

    public async Task<DemoActionResponse> InsertCustomerAsync(
        DemoActionRequest request,
        CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var suffix = CreateSuffix();
        var firstName = OrDefault(request.FirstName, "Demo");
        var lastName = OrDefault(request.LastName, $"Customer{suffix}");
        var email = OrDefault(request.Email, $"demo.{suffix}@example.local");

        const string sql = """
            INSERT INTO customers(first_name, last_name, email)
            VALUES (@firstName, @lastName, @email);
            SELECT LAST_INSERT_ID();
            """;

        await using var connection = new MySqlConnection(_options.SourceConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@firstName", firstName);
        command.Parameters.AddWithValue("@lastName", lastName);
        command.Parameters.AddWithValue("@email", email);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        var id = Convert.ToInt32(result);

        return new DemoActionResponse(
            "Insert customer",
            "submitted",
            "INSERT INTO customers(first_name, last_name, email) VALUES (...)",
            $"Inserted source customer {id}. Debezium should publish a create event.",
            id,
            startedAt);
    }

    public async Task<DemoActionResponse> UpdateCustomerAsync(
        DemoActionRequest request,
        CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var customerId = await ResolveTargetCustomerIdAsync(request.CustomerId, cancellationToken);
        var email = OrDefault(request.Email, $"updated.{CreateSuffix()}@example.local");

        const string sql = """
            UPDATE customers
            SET email = @email
            WHERE id = @id;
            """;

        await using var connection = new MySqlConnection(_options.SourceConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", customerId);
        command.Parameters.AddWithValue("@email", email);
        await command.ExecuteNonQueryAsync(cancellationToken);

        return new DemoActionResponse(
            "Update customer",
            "submitted",
            "UPDATE customers SET email = ... WHERE id = ...",
            $"Updated source customer {customerId}. The replica should catch up after the worker handles the update event.",
            customerId,
            startedAt);
    }

    public async Task<DemoActionResponse> DeleteCustomerAsync(
        DemoActionRequest request,
        CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var customerId = await FindTargetCustomerIdAsync(request.CustomerId, cancellationToken);

        if (customerId is null)
        {
            return new DemoActionResponse(
                "Delete customer",
                "skipped",
                "DELETE FROM customers WHERE id = ...",
                "No source customer exists to delete.",
                null,
                startedAt);
        }

        const string sql = "DELETE FROM customers WHERE id = @id;";

        await using var connection = new MySqlConnection(_options.SourceConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", customerId.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);

        return new DemoActionResponse(
            "Delete customer",
            "submitted",
            sql,
            $"Deleted source customer {customerId}. The replica should remove that row after the delete event.",
            customerId,
            startedAt);
    }

    public async Task<DemoActionResponse> TruncateCustomersAsync(CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.UtcNow;

        const string sql = "TRUNCATE TABLE customers;";

        await using var connection = new MySqlConnection(_options.SourceConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new MySqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);

        return new DemoActionResponse(
            "Truncate source",
            "submitted",
            sql,
            "Truncated the source table. The consumer should receive a truncate event and clear the replica.",
            null,
            startedAt);
    }

    public async Task<DemoActionResponse> SeedCustomersAsync(CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var suffix = CreateSuffix();

        const string sql = """
            INSERT INTO customers(first_name, last_name, email)
            VALUES
                (@firstNameA, @lastNameA, @emailA),
                (@firstNameB, @lastNameB, @emailB);
            """;

        await using var connection = new MySqlConnection(_options.SourceConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@firstNameA", "Anne");
        command.Parameters.AddWithValue("@lastNameA", $"Kretchmar{suffix}");
        command.Parameters.AddWithValue("@emailA", $"anne.{suffix}@example.local");
        command.Parameters.AddWithValue("@firstNameB", "Maggie");
        command.Parameters.AddWithValue("@lastNameB", $"Smith{suffix}");
        command.Parameters.AddWithValue("@emailB", $"maggie.{suffix}@example.local");
        await command.ExecuteNonQueryAsync(cancellationToken);

        return new DemoActionResponse(
            "Seed sample rows",
            "submitted",
            "INSERT INTO customers(first_name, last_name, email) VALUES (...), (...)",
            "Inserted two sample source rows.",
            null,
            startedAt);
    }

    private async Task<IReadOnlyList<CustomerRow>> QueryCustomersAsync(
        string connectionString,
        string tableName,
        CancellationToken cancellationToken)
    {
        var customers = new List<CustomerRow>();

        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var sql = $"""
            SELECT id, first_name, last_name, email
            FROM {tableName}
            ORDER BY id;
            """;

        await using var command = new MySqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var idOrdinal = reader.GetOrdinal("id");
        var firstNameOrdinal = reader.GetOrdinal("first_name");
        var lastNameOrdinal = reader.GetOrdinal("last_name");
        var emailOrdinal = reader.GetOrdinal("email");

        while (await reader.ReadAsync(cancellationToken))
        {
            customers.Add(new CustomerRow(
                reader.GetInt32(idOrdinal),
                reader.GetString(firstNameOrdinal),
                reader.GetString(lastNameOrdinal),
                reader.GetString(emailOrdinal)));
        }

        return customers;
    }

    private async Task<int> ResolveTargetCustomerIdAsync(
        int? requestedCustomerId,
        CancellationToken cancellationToken)
    {
        var customerId = await FindTargetCustomerIdAsync(requestedCustomerId, cancellationToken);

        if (customerId is not null)
        {
            return customerId.Value;
        }

        var insertResponse = await InsertCustomerAsync(new DemoActionRequest(null, "Demo", "Autocreated", null), cancellationToken);
        return insertResponse.CustomerId ?? throw new InvalidOperationException("Inserted demo customer did not return an id.");
    }

    private async Task<int?> FindTargetCustomerIdAsync(
        int? requestedCustomerId,
        CancellationToken cancellationToken)
    {
        if (requestedCustomerId is not null)
        {
            return requestedCustomerId.Value;
        }

        await using var connection = new MySqlConnection(_options.SourceConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new MySqlCommand("SELECT id FROM customers ORDER BY id DESC LIMIT 1;", connection);
        var result = await command.ExecuteScalarAsync(cancellationToken);

        if (result is null || result == DBNull.Value)
        {
            return null;
        }

        return Convert.ToInt32(result);
    }

    private static string OrDefault(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static string CreateSuffix()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
    }
}
