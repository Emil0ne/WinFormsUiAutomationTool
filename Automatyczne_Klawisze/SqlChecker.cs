using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;

namespace Automatyczne_Klawisze
{
    public static class SqlChecker
    {
        public static string ZbudujConnectionString(string serwer, string sqlLogin, string sqlPassword)
        {
            var builder = new SqlConnectionStringBuilder
            {
                DataSource = serwer,
                InitialCatalog = "master",
                ConnectTimeout = 4,
                IntegratedSecurity = false,
                UserID = sqlLogin,
                Password = sqlPassword,
                TrustServerCertificate = true
            };

            return builder.ConnectionString;
        }

        public static bool CzyBazaMaAktywnychUzytkownikow(string connectionString, string nazwaBazy, out List<string> listaUzytkownikow)
        {
            listaUzytkownikow = new List<string>();

            string query = @"
                SELECT DISTINCT host_name, login_name, program_name 
                FROM sys.dm_exec_sessions 
                WHERE database_id = DB_ID(@DbName) 
                  AND session_id <> @@SPID 
                  AND is_user_process = 1;";

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@DbName", nazwaBazy);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string host = reader["host_name"]?.ToString() ?? "NieznanyHost";
                                string login = reader["login_name"]?.ToString() ?? "NieznanyLogin";
                                string prog = reader["program_name"]?.ToString() ?? "";

                                listaUzytkownikow.Add($"User: {login} | Komputer: {host} ({prog})");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                listaUzytkownikow.Add($"BŁĄD POŁĄCZENIA SQL: {ex.Message}");
                return true; // W razie błędu połączenia traktujemy bazę jako zablokowaną
            }

            return listaUzytkownikow.Count > 0;
        }
    }
}