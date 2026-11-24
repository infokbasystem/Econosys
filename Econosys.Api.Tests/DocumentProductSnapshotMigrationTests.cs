using Econosys.Api.Migrations;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace Econosys.Api.Tests;

public class DocumentProductSnapshotMigrationTests
{
    [SqlServerFact]
    public async Task OriginalAlterFailsWhenIndexedColumnHasDifferentCollation()
    {
        await using var connection = await OpenTempDb();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        await Execute(connection, transaction, """
            CREATE TABLE #SnapshotColorTest (Id int NOT NULL, strColor nvarchar(50) COLLATE Finnish_Swedish_CI_AS NULL);
            CREATE NONCLUSTERED INDEX [included color] ON #SnapshotColorTest (Id) INCLUDE (strColor);
            """);
        var error = await Assert.ThrowsAsync<SqlException>(() =>
            Execute(connection, transaction, "ALTER TABLE #SnapshotColorTest ALTER COLUMN strColor nvarchar(255) NULL"));
        Assert.Contains(error.Errors.Cast<SqlError>(), item => item.Number == 5074);
    }

    [SqlServerFact]
    public async Task ColorMigrationPreservesIndexesAndRejectsTruncatingDowngrade()
    {
        await using var connection = await OpenTempDb();
        Assert.Equal("tempdb", connection.Database);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        await Execute(connection, transaction, """
            CREATE TABLE #SnapshotColorTest (Id int NOT NULL, Other int NULL, strColor nvarchar(50) COLLATE Finnish_Swedish_CI_AS NULL);
            CREATE NONCLUSTERED INDEX [included color] ON #SnapshotColorTest (Id DESC) INCLUDE (Other, strColor);
            CREATE UNIQUE NONCLUSTERED INDEX [key color] ON #SnapshotColorTest (strColor ASC, Id DESC);
            INSERT INTO #SnapshotColorTest VALUES (1, 2, N'original');
            """);

        var originalIndexes = await IndexDefinitions(connection, transaction);
        await Execute(connection, transaction, ColorSql(up: true));
        Assert.Equal(originalIndexes, await IndexDefinitions(connection, transaction));
        Assert.Equal("Finnish_Swedish_CI_AS", await Scalar<string>(connection, transaction,
            "SELECT collation_name FROM tempdb.sys.columns WHERE object_id = OBJECT_ID(N'tempdb..#SnapshotColorTest') AND name = N'strColor'"));
        await Execute(connection, transaction, "UPDATE #SnapshotColorTest SET strColor = REPLICATE(N'x', 255)");
        Assert.Equal(255, await Scalar<int>(connection, transaction, "SELECT LEN(strColor) FROM #SnapshotColorTest"));

        await transaction.SaveAsync("before_downgrade");
        await Assert.ThrowsAsync<SqlException>(() => Execute(connection, transaction, ColorSql(up: false)));
        await transaction.RollbackAsync("before_downgrade");
        Assert.Equal(originalIndexes, await IndexDefinitions(connection, transaction));
        Assert.Equal(255, await Scalar<int>(connection, transaction, "SELECT LEN(strColor) FROM #SnapshotColorTest"));

        await Execute(connection, transaction, "UPDATE #SnapshotColorTest SET strColor = N'original'");
        await Execute(connection, transaction, ColorSql(up: false));
        Assert.Equal(originalIndexes, await IndexDefinitions(connection, transaction));
        Assert.Equal(100, await Scalar<int>(connection, transaction,
            "SELECT max_length FROM tempdb.sys.columns WHERE object_id = OBJECT_ID(N'tempdb..#SnapshotColorTest') AND name = N'strColor'"));
        await transaction.RollbackAsync();
    }

    [SqlServerFact]
    public async Task ColorMigrationRejectsUnsupportedIndexesWithoutDroppingThem()
    {
        await using var connection = await OpenTempDb();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        await Execute(connection, transaction, """
            CREATE TABLE #SnapshotColorTest (Id int NOT NULL, Other int NULL, strColor nvarchar(255) NULL);
            CREATE NONCLUSTERED INDEX [filtered] ON #SnapshotColorTest (Id) INCLUDE (strColor) WHERE Other IS NOT NULL;
            """);
        var originalIndexes = await IndexDefinitions(connection, transaction);
        var error = await Assert.ThrowsAsync<SqlException>(() => Execute(connection, transaction, ColorSql(up: false)));
        Assert.Contains("filtered indexes", error.Message);
        Assert.Equal(originalIndexes, await IndexDefinitions(connection, transaction));
        await transaction.RollbackAsync();
    }

    [SqlServerFact]
    public async Task ColorOperationsRunTogetherInOneScriptBatch()
    {
        await using var connection = await OpenTempDb();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        var tables = new[] { "Inquiry", "Quotation", "CustomerOrder", "SupplierOrder" };
        foreach (var table in tables)
            await Execute(connection, transaction, $"CREATE TABLE [#Snapshot{table}] (strColor nvarchar(50) NULL)");

        var migration = new AddDocumentProductSnapshots();
        foreach (var operations in new[] { migration.UpOperations, migration.DownOperations })
        {
            var statements = operations.OfType<SqlOperation>().Select(operation =>
            {
                var sql = operation.Sql;
                var table = tables.Single(name => sql.Contains($"N''[dbo].[{name}]''"));
                return sql.Replace($"N''[dbo].[{table}]''", $"N''[#Snapshot{table}]''")
                    .Replace("OBJECT_ID(@table, ''U'')",
                        $"OBJECT_ID(N''tempdb..#Snapshot{table}'', ''U'')");
            });
            await Execute(connection, transaction, string.Join("\n", statements));
        }
        await transaction.RollbackAsync();
    }

    private static async Task<SqlConnection> OpenTempDb()
    {
        var builder = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ECONOSYS_TEST_SQLSERVER"))
        {
            InitialCatalog = "tempdb"
        };
        var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    private static string ColorSql(bool up)
    {
        var migration = new AddDocumentProductSnapshots();
        var operations = up ? migration.UpOperations : migration.DownOperations;
        var sql = operations.OfType<SqlOperation>().Single(operation =>
            operation.Sql.Contains("N''[dbo].[Inquiry]''")).Sql;
        return sql.Replace("N''[dbo].[Inquiry]''", "N''[#SnapshotColorTest]''")
            .Replace("OBJECT_ID(@table, ''U'')", "OBJECT_ID(N''tempdb..#SnapshotColorTest'', ''U'')");
    }

    private static async Task Execute(SqlConnection connection, SqlTransaction transaction, string sql)
    {
        await using var command = new SqlCommand(sql, connection, transaction);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<T> Scalar<T>(SqlConnection connection, SqlTransaction transaction, string sql)
    {
        await using var command = new SqlCommand(sql, connection, transaction);
        return (T)Convert.ChangeType((await command.ExecuteScalarAsync())!, typeof(T));
    }

    private static async Task<string> IndexDefinitions(SqlConnection connection, SqlTransaction transaction)
    {
        return await Scalar<string>(connection, transaction, """
            SELECT CONVERT(nvarchar(max), (
                SELECT i.name AS [@name], i.is_unique AS [@unique], i.is_padded AS [@padded],
                    i.fill_factor AS [@fillFactor], i.ignore_dup_key AS [@ignoreDuplicates],
                    i.allow_row_locks AS [@rowLocks], i.allow_page_locks AS [@pageLocks],
                    i.data_space_id AS [@dataSpace], p.data_compression AS [@compression],
                    (SELECT c.name AS [@name], ic.key_ordinal AS [@ordinal],
                        ic.is_descending_key AS [@descending], ic.is_included_column AS [@included]
                     FROM tempdb.sys.index_columns ic
                     JOIN tempdb.sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                     WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id
                     ORDER BY ic.index_column_id FOR XML PATH('column'), TYPE)
                FROM tempdb.sys.indexes i
                JOIN tempdb.sys.partitions p ON p.object_id = i.object_id AND p.index_id = i.index_id
                WHERE i.object_id = OBJECT_ID(N'tempdb..#SnapshotColorTest') AND i.index_id > 0
                ORDER BY i.name FOR XML PATH('index'), TYPE
            ))
            """);
    }

    private sealed class SqlServerFactAttribute : FactAttribute
    {
        public SqlServerFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ECONOSYS_TEST_SQLSERVER")))
                Skip = "Set ECONOSYS_TEST_SQLSERVER to run the SQL Server migration tests in tempdb.";
        }
    }
}
