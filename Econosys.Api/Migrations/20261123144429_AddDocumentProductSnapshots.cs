using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Econosys.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentProductSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in new[] { "Inquiry", "Quotation", "CustomerOrder", "SupplierOrder" })
            {
                AlterColorColumn(migrationBuilder, table, 255);
                migrationBuilder.AddColumn<string>(name: "Varnish", table: table, type: "nvarchar(255)", maxLength: 255, nullable: true);
                migrationBuilder.AddColumn<string>(name: "PrintMethod", table: table, type: "nvarchar(50)", maxLength: 50, nullable: true);
                migrationBuilder.AddColumn<string>(name: "ProductNoteInternal", table: table, type: "nvarchar(255)", maxLength: 255, nullable: true);
            }
            foreach (var table in new[] { "Inquiry", "SupplierOrder" })
            {
                migrationBuilder.AddColumn<string>(name: "ProductNoteExternal", table: table, type: "nvarchar(255)", maxLength: 255, nullable: true);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in new[] { "Inquiry", "SupplierOrder" })
            {
                migrationBuilder.DropColumn(name: "ProductNoteExternal", table: table);
            }
            foreach (var table in new[] { "Inquiry", "Quotation", "CustomerOrder", "SupplierOrder" })
            {
                migrationBuilder.DropColumn(name: "Varnish", table: table);
                migrationBuilder.DropColumn(name: "PrintMethod", table: table);
                migrationBuilder.DropColumn(name: "ProductNoteInternal", table: table);
                AlterColorColumn(migrationBuilder, table, 50);
            }
        }

        private static void AlterColorColumn(MigrationBuilder migrationBuilder, string table, int length)
        {
            if (length == 255)
            {
                RunIsolatedSql(migrationBuilder, $$"""
                    DECLARE @table nvarchar(300) = N'[dbo].[{{table}}]';
                    DECLARE @collation sysname = (
                        SELECT collation_name FROM sys.columns
                        WHERE object_id = OBJECT_ID(@table, 'U') AND name = N'strColor'
                            AND system_type_id = TYPE_ID(N'nvarchar') AND is_nullable = 1
                    );
                    IF @collation IS NULL
                        THROW 50000, 'Expected nullable nvarchar strColor column was not found.', 1;
                    DECLARE @sql nvarchar(max) = N'ALTER TABLE ' + @table
                        + N' ALTER COLUMN [strColor] nvarchar(255) COLLATE ' + @collation + N' NULL';
                    EXEC sp_executesql @sql;
                    """);
                return;
            }

            RunIsolatedSql(migrationBuilder, $$"""
                DECLARE @table nvarchar(300) = N'[dbo].[{{table}}]';
                DECLARE @objectId int = OBJECT_ID(@table, 'U');
                DECLARE @collation sysname = (
                    SELECT collation_name FROM sys.columns
                    WHERE object_id = @objectId AND name = N'strColor'
                        AND system_type_id = TYPE_ID(N'nvarchar') AND is_nullable = 1
                );
                IF @objectId IS NULL OR @collation IS NULL
                    THROW 50000, 'Expected nullable nvarchar strColor column was not found.', 1;

                DECLARE @sql nvarchar(max) = N'IF EXISTS (SELECT 1 FROM ' + @table
                    + N' WHERE DATALENGTH([strColor]) > {{length}} * 2) '
                    + N'THROW 50000, ''Cannot downgrade: strColor contains values longer than {{length}} characters.'', 1;';
                EXEC sp_executesql @sql;

                -- Filter predicates can reference strColor without listing it as an index column.
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = @objectId AND has_filter = 1)
                    THROW 50000, 'Cannot alter strColor on a table with filtered indexes; review the indexes first.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM sys.indexes i
                    JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
                    JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                    LEFT JOIN sys.data_spaces ds ON ds.data_space_id = i.data_space_id
                    LEFT JOIN sys.stats s ON s.object_id = i.object_id AND s.stats_id = i.index_id
                    WHERE i.object_id = @objectId AND c.name = N'strColor'
                        AND (i.type <> 2 OR i.is_primary_key = 1 OR i.is_unique_constraint = 1
                            OR i.is_disabled = 1 OR i.is_hypothetical = 1 OR i.auto_created = 1
                            OR i.is_padded = 1 OR i.fill_factor <> 0 OR i.ignore_dup_key = 1
                            OR i.allow_row_locks = 0 OR i.allow_page_locks = 0
                            OR i.optimize_for_sequential_key = 1
                            OR EXISTS (SELECT 1 FROM sys.configurations
                                WHERE name = N'fill factor (%)' AND CONVERT(int, value_in_use) <> 0)
                            OR ds.type <> 'FG' OR ds.data_space_id IS NULL
                            OR s.stats_id IS NULL OR s.no_recompute = 1
                            OR s.is_incremental = 1 OR s.has_persisted_sample = 1
                            OR EXISTS (SELECT 1 FROM sys.partitions p
                                WHERE p.object_id = i.object_id AND p.index_id = i.index_id
                                    AND p.data_compression <> 0)
                            OR EXISTS (SELECT 1 FROM sys.extended_properties ep
                                WHERE ep.class = 7 AND ep.major_id = i.object_id AND ep.minor_id = i.index_id))
                )
                    THROW 50000, 'Cannot alter strColor: a dependent index has unsupported options.', 1;

                DECLARE @indexes TABLE (name sysname NOT NULL, create_sql nvarchar(max) NOT NULL);
                INSERT INTO @indexes (name, create_sql)
                SELECT i.name,
                    N'CREATE ' + CASE WHEN i.is_unique = 1 THEN N'UNIQUE ' ELSE N'' END
                    + N'NONCLUSTERED INDEX ' + QUOTENAME(i.name) + N' ON ' + @table + N' ('
                    + STUFF((
                        SELECT N', ' + QUOTENAME(c.name)
                            + CASE WHEN ic.is_descending_key = 1 THEN N' DESC' ELSE N' ASC' END
                        FROM sys.index_columns ic
                        JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                        WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.key_ordinal > 0
                        ORDER BY ic.key_ordinal
                        FOR XML PATH(''), TYPE
                    ).value('.', 'nvarchar(max)'), 1, 2, N'') + N')'
                    + CASE WHEN EXISTS (
                        SELECT 1 FROM sys.index_columns ic
                        WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 1
                    ) THEN N' INCLUDE (' + STUFF((
                        SELECT N', ' + QUOTENAME(c.name)
                        FROM sys.index_columns ic
                        JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                        WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 1
                        ORDER BY ic.index_column_id
                        FOR XML PATH(''), TYPE
                    ).value('.', 'nvarchar(max)'), 1, 2, N'') + N')' ELSE N'' END
                    + N' WITH (PAD_INDEX = OFF, IGNORE_DUP_KEY = OFF, '
                    + N'ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, DATA_COMPRESSION = NONE) ON '
                    + QUOTENAME(ds.name)
                FROM sys.indexes i
                JOIN sys.data_spaces ds ON ds.data_space_id = i.data_space_id
                WHERE i.object_id = @objectId AND EXISTS (
                    SELECT 1 FROM sys.index_columns ic
                    JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                    WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND c.name = N'strColor'
                );

                DECLARE @name sysname, @createSql nvarchar(max);
                DECLARE indexes_to_rebuild CURSOR LOCAL FAST_FORWARD FOR SELECT name, create_sql FROM @indexes;
                OPEN indexes_to_rebuild;
                FETCH NEXT FROM indexes_to_rebuild INTO @name, @createSql;
                WHILE @@FETCH_STATUS = 0
                BEGIN
                    SET @sql = N'DROP INDEX ' + QUOTENAME(@name) + N' ON ' + @table;
                    EXEC sp_executesql @sql;
                    FETCH NEXT FROM indexes_to_rebuild INTO @name, @createSql;
                END;
                CLOSE indexes_to_rebuild;

                SET @sql = N'ALTER TABLE ' + @table + N' ALTER COLUMN [strColor] nvarchar({{length}}) COLLATE '
                    + @collation + N' NULL';
                EXEC sp_executesql @sql;

                OPEN indexes_to_rebuild;
                FETCH NEXT FROM indexes_to_rebuild INTO @name, @createSql;
                WHILE @@FETCH_STATUS = 0
                BEGIN
                    EXEC sp_executesql @createSql;
                    FETCH NEXT FROM indexes_to_rebuild INTO @name, @createSql;
                END;
                CLOSE indexes_to_rebuild;
                DEALLOCATE indexes_to_rebuild;
                """);
        }

        private static void RunIsolatedSql(MigrationBuilder migrationBuilder, string sql)
        {
            migrationBuilder.Sql($"EXEC(N'{sql.Replace("'", "''")}');");
        }
    }
}
