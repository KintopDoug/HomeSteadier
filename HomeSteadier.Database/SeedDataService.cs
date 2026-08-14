using System.Text;
using Npgsql;

namespace HomeSteadier.Database;

public class SeedDataService
{
    public async Task<SeedResult> SeedAsync(string connectionString, string seedsDirectory)
    {
        var result = new SeedResult();

        if (!Directory.Exists(seedsDirectory))
        {
            result.Success = false;
            result.Error = $"Seeds directory not found: {seedsDirectory}";
            result.Message = "Error importing seed data";
            return result;
        }

        var csvFiles = Directory.GetFiles(seedsDirectory, "*.csv").OrderBy(f => f).ToList();
        if (csvFiles.Count == 0)
        {
            result.Success = true;
            result.Message = "No seed files found.";
            return result;
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        foreach (var file in csvFiles)
        {
            var tableName = Path.GetFileNameWithoutExtension(file);
            try
            {
                var rowsImported = await SeedTableAsync(connection, tableName, file);
                result.Succeeded.Add(new SeedSuccess(tableName, rowsImported));
            }
            catch (Exception ex)
            {
                result.Failed.Add(new SeedFailure(tableName, ex.Message));
            }
        }

        result.Success = result.Failed.Count == 0;
        result.Message = result.Success ? "Seed data imported successfully!" : "Some seed files failed to import.";
        return result;
    }

    private static async Task<int> SeedTableAsync(NpgsqlConnection connection, string table, string filePath)
    {
        var lines = (await File.ReadAllLinesAsync(filePath))
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        if (lines.Count == 0)
            throw new InvalidOperationException("CSV file is empty.");

        var headers = ParseCsvLine(lines[0]);

        var tableColumns = await GetTableColumnsAsync(connection, table);
        if (tableColumns.Count == 0)
            throw new InvalidOperationException($"No table named \"{table}\" exists in the database schema.");

        var unknownColumns = headers.Where(h => !tableColumns.ContainsKey(h)).ToList();
        if (unknownColumns.Count > 0)
        {
            throw new InvalidOperationException(
                $"CSV column(s) not found on table \"{table}\": {string.Join(", ", unknownColumns)}. " +
                $"Available columns: {string.Join(", ", tableColumns.Values.Select(c => c.Name))}");
        }

        var columns = headers.Select(h => tableColumns[h]).ToList();
        var columnList = string.Join(", ", columns.Select(c => $"\"{c.Name}\""));
        var paramList = string.Join(", ", columns.Select((_, i) => $"@p{i}"));
        var insertSql = $"INSERT INTO \"public\".\"{table}\" ({columnList}) VALUES ({paramList}){await BuildUpsertClauseAsync(connection, table, columns)}";

        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            var rowsImported = 0;
            for (var i = 1; i < lines.Count; i++)
            {
                var values = ParseCsvLine(lines[i]);
                if (values.Count != headers.Count)
                {
                    throw new InvalidOperationException(
                        $"Row {i + 1} has {values.Count} value(s), expected {headers.Count} to match the header.");
                }

                await using var insert = new NpgsqlCommand(insertSql, connection, transaction);
                for (var c = 0; c < values.Count; c++)
                {
                    try
                    {
                        insert.Parameters.AddWithValue($"p{c}", ConvertValue(values[c], columns[c].DataType));
                    }
                    catch (Exception ex) when (ex is FormatException or OverflowException)
                    {
                        throw new InvalidOperationException(
                            $"Row {i + 1}, column \"{columns[c].Name}\": \"{values[c]}\" is not a valid {columns[c].DataType} value.");
                    }
                }

                await insert.ExecuteNonQueryAsync();
                rowsImported++;
            }

            await transaction.CommitAsync();
            return rowsImported;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // Npgsql infers a text parameter's server type from the .NET value's type (string -> text),
    // and Postgres has no implicit text -> integer/numeric/boolean cast, so CSV values headed for
    // non-text columns must be parsed into the matching .NET type before being bound.
    private static object ConvertValue(string value, string dataType)
    {
        if (string.IsNullOrEmpty(value))
            return DBNull.Value;

        return dataType switch
        {
            "smallint" => short.Parse(value),
            "integer" => int.Parse(value),
            "bigint" => long.Parse(value),
            "numeric" or "real" or "double precision" => decimal.Parse(value),
            "boolean" => bool.Parse(value),
            _ => value
        };
    }

    // Re-seeding must update existing rows in place rather than delete-then-insert, since a delete
    // would cascade to (or be blocked by) foreign keys held by other tables. So on a re-run, rows
    // that collide with an existing unique constraint/index are updated instead of rejected — via
    // ON CONFLICT — as long as that constraint's columns are all present in the CSV.
    private static async Task<string> BuildUpsertClauseAsync(NpgsqlConnection connection, string table, List<ColumnInfo> columns)
    {
        var columnNames = new HashSet<string>(columns.Select(c => c.Name), StringComparer.OrdinalIgnoreCase);
        var uniqueGroups = await GetUniqueIndexColumnGroupsAsync(connection, table);

        var conflictColumns = uniqueGroups
            .Where(g => g.All(columnNames.Contains))
            .OrderBy(g => g.Count)
            .FirstOrDefault();

        if (conflictColumns == null)
            return string.Empty;

        var conflictList = string.Join(", ", conflictColumns.Select(c => $"\"{c}\""));
        var updateColumns = columns.Where(c => !conflictColumns.Contains(c.Name, StringComparer.OrdinalIgnoreCase)).ToList();

        if (updateColumns.Count == 0)
            return $" ON CONFLICT ({conflictList}) DO NOTHING";

        var updateList = string.Join(", ", updateColumns.Select(c => $"\"{c.Name}\" = EXCLUDED.\"{c.Name}\""));
        return $" ON CONFLICT ({conflictList}) DO UPDATE SET {updateList}";
    }

    private static async Task<List<List<string>>> GetUniqueIndexColumnGroupsAsync(NpgsqlConnection connection, string table)
    {
        const string sql = """
            SELECT array_agg(a.attname ORDER BY x.n) AS columns
            FROM pg_index ix
            JOIN pg_class t ON t.oid = ix.indrelid
            JOIN pg_class i ON i.oid = ix.indexrelid
            JOIN pg_namespace n ON n.oid = t.relnamespace
            JOIN LATERAL unnest(ix.indkey) WITH ORDINALITY AS x(attnum, n) ON true
            JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = x.attnum
            WHERE t.relname = @table
              AND n.nspname = 'public'
              AND ix.indisunique
            GROUP BY i.relname;
            """;

        var groups = new List<List<string>>();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("table", table);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            groups.Add(reader.GetFieldValue<string[]>(0).ToList());

        return groups;
    }

    private static async Task<Dictionary<string, ColumnInfo>> GetTableColumnsAsync(NpgsqlConnection connection, string table)
    {
        const string sql = """
            SELECT column_name, data_type
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = @table;
            """;

        var columns = new Dictionary<string, ColumnInfo>(StringComparer.OrdinalIgnoreCase);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("table", table);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var name = reader.GetString(0);
            columns[name] = new ColumnInfo(name, reader.GetString(1));
        }

        return columns;
    }

    private sealed record ColumnInfo(string Name, string DataType);

    // Minimal CSV parser: handles quoted fields, escaped quotes ("") and embedded commas.
    private static List<string> ParseCsvLine(string line)
    {
        var values = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(ch);
                }
            }
            else if (ch == '"')
            {
                inQuotes = true;
            }
            else if (ch == ',')
            {
                values.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }

        values.Add(current.ToString().Trim());
        return values;
    }
}

public class SeedResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<SeedSuccess> Succeeded { get; set; } = [];
    public List<SeedFailure> Failed { get; set; } = [];
}

public record SeedSuccess(string Table, int RowsImported);

public record SeedFailure(string Table, string Error);
