using System;
using System.IO;
using System.Text;
using FirebirdSql.Data.FirebirdClient;
using System.Collections.Generic;

namespace DbMetaTool
{
    public static class DatabaseHelpers
    {
        public static string MapFirebirdTypeToSql(int fbType, int length)
        {
            return fbType switch
            {
                7 => "SMALLINT",
                8 => "INTEGER",
                10 => "FLOAT",
                12 => "DATE",
                13 => "TIME",
                14 => $"CHAR({length})",
                16 => "BIGINT",
                27 => "DOUBLE PRECISION",
                35 => "TIMESTAMP",
                37 => $"VARCHAR({length})",
                261 => "BLOB",
                _ => "VARCHAR(100)"
            };
        }

        public static string ExtractDomains(FbConnection connection)
        {
            var result = new StringBuilder();

            string sql = @"
                SELECT TRIM(F.RDB$FIELD_NAME) AS DOMAIN_NAME,
                       F.RDB$FIELD_TYPE,
                       F.RDB$FIELD_LENGTH
                FROM RDB$FIELDS F
                WHERE F.RDB$FIELD_NAME NOT STARTING WITH 'RDB$'
                  AND F.RDB$FIELD_NAME NOT STARTING WITH 'MON$'
                  AND F.RDB$FIELD_NAME NOT STARTING WITH 'SEC$'
                ORDER BY F.RDB$FIELD_NAME";

            using (var cmd = new FbCommand(sql, connection))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    string domainName = reader.GetString(0);
                    int fieldType = reader.GetInt16(1);
                    int fieldLength = reader.IsDBNull(2) ? 0 : reader.GetInt16(2);

                    string sqlType = MapFirebirdTypeToSql(fieldType, fieldLength);
                    result.AppendLine($"CREATE DOMAIN {domainName} AS {sqlType};");
                }
            }

            return result.ToString();
        }

        public static string ExtractTables(FbConnection connection)
        {
            var result = new StringBuilder();

            string sqlTables = @"
                SELECT TRIM(RDB$RELATION_NAME) AS TABLE_NAME
                FROM RDB$RELATIONS
                WHERE RDB$SYSTEM_FLAG = 0
                  AND RDB$VIEW_BLR IS NULL
                ORDER BY RDB$RELATION_NAME";

            var tables = new List<string>();
            using (var cmd = new FbCommand(sqlTables, connection))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                    tables.Add(reader.GetString(0));
            }

            foreach (var tableName in tables)
            {
                result.AppendLine($"CREATE TABLE {tableName} (");

                string sqlColumns = @"
                    SELECT TRIM(RF.RDB$FIELD_NAME) AS COLUMN_NAME,
                           TRIM(F.RDB$FIELD_NAME) AS DOMAIN_NAME,
                           RF.RDB$NULL_FLAG,
                           F.RDB$FIELD_TYPE,
                           F.RDB$FIELD_LENGTH
                    FROM RDB$RELATION_FIELDS RF
                    JOIN RDB$FIELDS F ON RF.RDB$FIELD_SOURCE = F.RDB$FIELD_NAME
                    WHERE RF.RDB$RELATION_NAME = @tableName
                    ORDER BY RF.RDB$FIELD_POSITION";

                var columns = new List<string>();
                using (var cmd = new FbCommand(sqlColumns, connection))
                {
                    cmd.Parameters.AddWithValue("@tableName", tableName);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string columnName = reader.GetString(0);
                            string domainName = reader.GetString(1);
                            bool notNull = !reader.IsDBNull(2);

                            // Jeśli domena systemowa (RDB$...), użyj typu zamiast nazwy
                            string columnType;
                            if (domainName.StartsWith("RDB$"))
                            {
                                int fieldType = reader.GetInt16(3);
                                int fieldLength = reader.IsDBNull(4) ? 0 : reader.GetInt16(4);
                                columnType = MapFirebirdTypeToSql(fieldType, fieldLength);
                            }
                            else
                            {
                                columnType = domainName;
                            }

                            string column = $"    {columnName} {columnType}";
                            if (notNull) column += " NOT NULL";

                            columns.Add(column);
                        }
                    }
                }

                result.AppendLine(string.Join(",\n", columns));
                result.AppendLine(");");
                result.AppendLine();
            }

            return result.ToString();
        }

        public static string ExtractProcedures(FbConnection connection)
        {
            var result = new StringBuilder();

            // Pobierz listę procedur
            string sqlProcs = @"
                SELECT TRIM(RDB$PROCEDURE_NAME) AS PROC_NAME
                FROM RDB$PROCEDURES
                WHERE RDB$SYSTEM_FLAG = 0
                ORDER BY RDB$PROCEDURE_NAME";

            var procedures = new List<string>();
            using (var cmd = new FbCommand(sqlProcs, connection))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                    procedures.Add(reader.GetString(0));
            }

            // ISQL-like extraction dla procedur
            foreach (var procName in procedures)
            {
                // Pobierz parametry wejściowe
                string sqlInputParams = @"
                    SELECT TRIM(PP.RDB$PARAMETER_NAME) AS PARAM_NAME,
                           TRIM(F.RDB$FIELD_NAME) AS DOMAIN_NAME,
                           F.RDB$FIELD_TYPE,
                           F.RDB$FIELD_LENGTH,
                           PP.RDB$PARAMETER_TYPE
                    FROM RDB$PROCEDURE_PARAMETERS PP
                    JOIN RDB$FIELDS F ON PP.RDB$FIELD_SOURCE = F.RDB$FIELD_NAME
                    WHERE PP.RDB$PROCEDURE_NAME = @procName
                      AND PP.RDB$PARAMETER_TYPE = 0
                    ORDER BY PP.RDB$PARAMETER_NUMBER";

                var inputParams = new List<string>();
                using (var cmd = new FbCommand(sqlInputParams, connection))
                {
                    cmd.Parameters.AddWithValue("@procName", procName);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string paramName = reader.GetString(0);
                            string domainName = reader.GetString(1);

                            string paramType;
                            if (domainName.StartsWith("RDB$"))
                            {
                                int fieldType = reader.GetInt16(2);
                                int fieldLength = reader.IsDBNull(3) ? 0 : reader.GetInt16(3);
                                paramType = MapFirebirdTypeToSql(fieldType, fieldLength);
                            }
                            else
                            {
                                paramType = domainName;
                            }

                            inputParams.Add($"    {paramName} {paramType}");
                        }
                    }
                }

                // Pobierz parametry wyjściowe
                string sqlOutputParams = @"
                    SELECT TRIM(PP.RDB$PARAMETER_NAME) AS PARAM_NAME,
                           TRIM(F.RDB$FIELD_NAME) AS DOMAIN_NAME,
                           F.RDB$FIELD_TYPE,
                           F.RDB$FIELD_LENGTH
                    FROM RDB$PROCEDURE_PARAMETERS PP
                    JOIN RDB$FIELDS F ON PP.RDB$FIELD_SOURCE = F.RDB$FIELD_NAME
                    WHERE PP.RDB$PROCEDURE_NAME = @procName
                      AND PP.RDB$PARAMETER_TYPE = 1
                    ORDER BY PP.RDB$PARAMETER_NUMBER";

                var outputParams = new List<string>();
                using (var cmd = new FbCommand(sqlOutputParams, connection))
                {
                    cmd.Parameters.AddWithValue("@procName", procName);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string paramName = reader.GetString(0);
                            string domainName = reader.GetString(1);

                            string paramType;
                            if (domainName.StartsWith("RDB$"))
                            {
                                int fieldType = reader.GetInt16(2);
                                int fieldLength = reader.IsDBNull(3) ? 0 : reader.GetInt16(3);
                                paramType = MapFirebirdTypeToSql(fieldType, fieldLength);
                            }
                            else
                            {
                                paramType = domainName;
                            }

                            outputParams.Add($"    {paramName} {paramType}");
                        }
                    }
                }

                // Pobierz body proc
                string sqlBody = @"
                    SELECT RDB$PROCEDURE_SOURCE
                    FROM RDB$PROCEDURES
                    WHERE RDB$PROCEDURE_NAME = @procName";

                string body = "";
                using (var cmd = new FbCommand(sqlBody, connection))
                {
                    cmd.Parameters.AddWithValue("@procName", procName);
                    var bodyResult = cmd.ExecuteScalar();
                    if (bodyResult != null && bodyResult != DBNull.Value)
                        body = bodyResult.ToString() ?? "";
                }

                // CREATE PROCEDURE
                result.AppendLine("SET TERM ^^ ;");
                result.Append($"CREATE PROCEDURE {procName}");

                if (inputParams.Count > 0)
                {
                    result.AppendLine(" (");
                    result.AppendLine(string.Join(",\n", inputParams));
                    result.Append(")");
                }

                if (outputParams.Count > 0)
                {
                    result.AppendLine();
                    result.AppendLine("RETURNS (");
                    result.AppendLine(string.Join(",\n", outputParams));
                    result.Append(")");
                }

                result.AppendLine();
                result.AppendLine("AS");
                result.AppendLine(body);
                result.AppendLine("^^");
                result.AppendLine("SET TERM ; ^^");
                result.AppendLine();
            }

            return result.ToString();
        }
        public static void ExecuteScriptFile(FbConnection connection, string scriptPath)
        {
            if (!File.Exists(scriptPath))
            {
                Console.WriteLine($"⚠ Plik nie istnieje: {scriptPath}");
                return;
            }

            string script = File.ReadAllText(scriptPath);

            // Sprawdź czy plik z procedurami
            if (script.Contains("SET TERM"))
            {
                // Podziel na bloki procedur
                var blocks = script.Split(new[] { "SET TERM ; ^^" }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var block in blocks)
                {
                    string cleanBlock = block.Trim();
                    if (string.IsNullOrWhiteSpace(cleanBlock)) continue;

                    // Usuń SET TERM ^^ ; z początku
                    cleanBlock = cleanBlock.Replace("SET TERM ^^ ;", "").Trim();

                    // Zamień ^^ na ; dla wykonania
                    cleanBlock = cleanBlock.Replace("^^", ";").Trim();

                    // Usuń końcowy średnik jeśli jest
                    if (cleanBlock.EndsWith(";"))
                        cleanBlock = cleanBlock.Substring(0, cleanBlock.Length - 1);

                    if (string.IsNullOrWhiteSpace(cleanBlock)) continue;

                    try
                    {
                        using (var cmd = new FbCommand(cleanBlock, connection))
                        {
                            cmd.ExecuteNonQuery();
                        }
                        Console.WriteLine($"✓ Wykonano procedurę");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"✗ Błąd: {ex.Message}");
                    }
                }
            }
            else
            {
                // domeny, tabele) - dziel po średniku
                var statements = script.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var statement in statements)
                {
                    string sql = statement.Trim();
                    if (string.IsNullOrWhiteSpace(sql)) continue;

                    try
                    {
                        using (var cmd = new FbCommand(sql, connection))
                        {
                            cmd.ExecuteNonQuery();
                        }
                        Console.WriteLine($"✓ Wykonano: {sql.Substring(0, Math.Min(50, sql.Length))}...");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"✗ Błąd: {ex.Message}");
                    }
                }
            }
        }
    }
    
    }