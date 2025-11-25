using System;
using System.IO;
using System.Text;
using FirebirdSql.Data.FirebirdClient;
using System.Collections.Generic;

namespace DbMetaTool
{
    public static class Program
    {
        // Przykładowe wywołania:
        // DbMetaTool build-db --db-dir "C:\db\fb5" --scripts-dir "C:\scripts"
        // DbMetaTool export-scripts --connection-string "..." --output-dir "C:\out"
        // DbMetaTool update-db --connection-string "..." --scripts-dir "C:\scripts"
        public static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Użycie:");
                Console.WriteLine("  build-db --db-dir <ścieżka> --scripts-dir <ścieżka>");
                Console.WriteLine("  export-scripts --connection-string <connStr> --output-dir <ścieżka>");
                Console.WriteLine("  update-db --connection-string <connStr> --scripts-dir <ścieżka>");
                return 1;
            }

            try
            {
                var command = args[0].ToLowerInvariant();

                switch (command)
                {
                    case "build-db":
                        {
                            string dbDir = GetArgValue(args, "--db-dir");
                            string scriptsDir = GetArgValue(args, "--scripts-dir");

                            BuildDatabase(dbDir, scriptsDir);
                            Console.WriteLine("Baza danych została zbudowana pomyślnie.");
                            return 0;
                        }

                    case "export-scripts":
                        {
                            string connStr = GetArgValue(args, "--connection-string");
                            string outputDir = GetArgValue(args, "--output-dir");

                            ExportScripts(connStr, outputDir);
                            Console.WriteLine("Skrypty zostały wyeksportowane pomyślnie.");
                            return 0;
                        }

                    case "update-db":
                        {
                            string connStr = GetArgValue(args, "--connection-string");
                            string scriptsDir = GetArgValue(args, "--scripts-dir");

                            UpdateDatabase(connStr, scriptsDir);
                            Console.WriteLine("Baza danych została zaktualizowana pomyślnie.");
                            return 0;
                        }

                    default:
                        Console.WriteLine($"Nieznane polecenie: {command}");
                        return 1;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Błąd: " + ex.Message);
                return -1;
            }
        }

        private static string GetArgValue(string[] args, string name)
        {
            int idx = Array.IndexOf(args, name);
            if (idx == -1 || idx + 1 >= args.Length)
                throw new ArgumentException($"Brak wymaganego parametru {name}");
            return args[idx + 1];
        }

        /// <summary>
        /// Buduje nową bazę danych Firebird 5.0 na podstawie skryptów.
        /// </summary>
        public static void BuildDatabase(string databaseDirectory, string scriptsDirectory)
        {
            Directory.CreateDirectory(databaseDirectory);

            string dbPath = Path.Combine(databaseDirectory, "database.fdb");
            string connectionString = $"DataSource=localhost;Port=3050;User=SYSDBA;Password=masterkey;" +
                                     $"Database={dbPath};Charset=UTF8;";

            FbConnection.CreateDatabase(connectionString, overwrite: true);

            using (var connection = new FbConnection(connectionString))
            {
                connection.Open();

                DatabaseHelpers.ExecuteScriptFile(connection, Path.Combine(scriptsDirectory, "domains.sql"));
                DatabaseHelpers.ExecuteScriptFile(connection, Path.Combine(scriptsDirectory, "tables.sql"));
                DatabaseHelpers.ExecuteScriptFile(connection, Path.Combine(scriptsDirectory, "procedures.sql"));
            }
        }

        /// <summary>
        /// Generuje skrypty metadanych z istniejącej bazy danych Firebird 5.0.
        /// </summary>
        public static void ExportScripts(string connectionString, string outputDirectory)
        {
            Directory.CreateDirectory(outputDirectory);

            using (var connection = new FbConnection(connectionString))
            {
                connection.Open();

                string domains = DatabaseHelpers.ExtractDomains(connection);
                File.WriteAllText(Path.Combine(outputDirectory, "domains.sql"), domains);

                string tables = DatabaseHelpers.ExtractTables(connection);
                File.WriteAllText(Path.Combine(outputDirectory, "tables.sql"), tables);

                string procedures = DatabaseHelpers.ExtractProcedures(connection);
                File.WriteAllText(Path.Combine(outputDirectory, "procedures.sql"), procedures);
            }
        }

        /// <summary>
        /// Aktualizuje istniejącą bazę danych Firebird 5.0 na podstawie skryptów.
        /// </summary>
        public static void UpdateDatabase(string connectionString, string scriptsDirectory)
        {
            using (var connection = new FbConnection(connectionString))
            {
                connection.Open();

                DatabaseHelpers.ExecuteScriptFile(connection, Path.Combine(scriptsDirectory, "domains.sql"));
                DatabaseHelpers.ExecuteScriptFile(connection, Path.Combine(scriptsDirectory, "tables.sql"));
                DatabaseHelpers.ExecuteScriptFile(connection, Path.Combine(scriptsDirectory, "procedures.sql"));
            }
        }
    }
}
