using CdcConsumer.Options;
using Microsoft.Extensions.Options;
using MySqlConnector;

namespace CdcConsumer.Infrastructure.ReplicaDb;

public sealed class MySqlReplicaCustomerStore : IReplicaCustomerStore
{
    private readonly string _connectionString;

    public MySqlReplicaCustomerStore(IOptions<ReplicaDbOptions> options)
    {
        _connectionString = options.Value.ConnectionString;
    }

    public async Task UpsertAsync(CustomerRecord customer, CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            INSERT INTO customers_replica (id, first_name, last_name, email)
            VALUES (@id, @firstName, @lastName, @email)
            ON DUPLICATE KEY UPDATE
                first_name = VALUES(first_name),
                last_name = VALUES(last_name),
                email = VALUES(email);
            """;

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", customer.Id);
        command.Parameters.AddWithValue("@firstName", customer.FirstName);
        command.Parameters.AddWithValue("@lastName", customer.LastName);
        command.Parameters.AddWithValue("@email", customer.Email);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new MySqlCommand("DELETE FROM customers_replica WHERE id = @id;", connection);
        command.Parameters.AddWithValue("@id", id);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task TruncateAsync(CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new MySqlCommand("TRUNCATE TABLE customers_replica;", connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
