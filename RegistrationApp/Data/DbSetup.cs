using System;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Text.RegularExpressions;

namespace RegistrationApp.Data
{
    /// <summary>
    /// First-run helper: creates the database and runs Database/01_Schema.sql if the tables don't exist.
    /// Safe to call on every start-up (does nothing when everything is already there).
    /// </summary>
    public static class DbSetup
    {
        public static void EnsureCreated(string schemaScriptPath)
        {
            try
            {
                string cs = ConfigurationManager.ConnectionStrings["RegistrationDb"].ConnectionString;
                var builder = new SqlConnectionStringBuilder(cs);
                string dbName = builder.InitialCatalog;

                // 1) create the database if missing
                builder.InitialCatalog = "master";
                using (var master = new SqlConnection(builder.ConnectionString))
                {
                    master.Open();
                    using (var exists = new SqlCommand("SELECT DB_ID(@n)", master))
                    {
                        exists.Parameters.AddWithValue("@n", dbName);
                        if (exists.ExecuteScalar() == DBNull.Value)
                        {
                            using (var create = new SqlCommand("CREATE DATABASE [" + dbName.Replace("]", "]]") + "]", master))
                                create.ExecuteNonQuery();
                        }
                    }
                }

                // 2) create tables + seed data if missing
                using (var con = new SqlConnection(cs))
                {
                    con.Open();
                    using (var check = new SqlCommand("SELECT OBJECT_ID(N'dbo.Student')", con))
                        if (check.ExecuteScalar() != DBNull.Value) return;

                    string script = File.ReadAllText(schemaScriptPath);
                    foreach (string raw in Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
                    {
                        string batch = raw.Trim();
                        if (batch.Length == 0) continue;
                        // the script's own CREATE DATABASE / USE batches are for SSMS; skip them here
                        if (batch.IndexOf("CREATE DATABASE", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                        if (Regex.IsMatch(batch, @"^\s*USE\s", RegexOptions.IgnoreCase)) continue;

                        using (var cmd = new SqlCommand(batch, con))
                            cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                // Don't crash start-up; the real error will be logged on the first request that needs the DB.
                AppLogger.Error(ex, "DbSetup.EnsureCreated");
            }
        }
    }
}
