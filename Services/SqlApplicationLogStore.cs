using Microsoft.Data.SqlClient;

namespace AiTicketTriage.Api.Services
{
    public sealed class SqlApplicationLogStore : IApplicationLogStore
    {
        private readonly string _connectionString;
        private static readonly SemaphoreSlim TableLock = new(1, 1);
        private static bool _tableReady;

        public SqlApplicationLogStore(IConfiguration configuration)
        {
            _connectionString =
                configuration.GetConnectionString("LogsDatabase")
                ?? throw new InvalidOperationException(
                    "Logs database connection string is not configured.");
        }

        public async Task WriteAsync(
            string level,
            string message,
            CancellationToken cancellationToken = default)
        {
            await EnsureTableAsync(cancellationToken);

            const string sql = """
                INSERT INTO dbo.ApplicationLogs (TimeStamp, [Level], Message)
                VALUES (@TimeStamp, @Level, @Message);
                """;

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@TimeStamp", DateTime.UtcNow);
            command.Parameters.AddWithValue("@Level", level);
            command.Parameters.AddWithValue("@Message", message);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        private async Task EnsureTableAsync(CancellationToken cancellationToken)
        {
            if (_tableReady)
            {
                return;
            }

            await TableLock.WaitAsync(cancellationToken);

            try
            {
                if (_tableReady)
                {
                    return;
                }

                const string sql = """
                    IF OBJECT_ID(N'dbo.ApplicationLogs', N'U') IS NULL
                    BEGIN
                        CREATE TABLE dbo.ApplicationLogs
                        (
                            Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                            TimeStamp DATETIME2 NOT NULL,
                            [Level] NVARCHAR(32) NOT NULL,
                            Message NVARCHAR(MAX) NOT NULL
                        );
                    END
                    """;

                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);
                await using var command = new SqlCommand(sql, connection);
                await command.ExecuteNonQueryAsync(cancellationToken);

                _tableReady = true;
            }
            finally
            {
                TableLock.Release();
            }
        }
    }
}
