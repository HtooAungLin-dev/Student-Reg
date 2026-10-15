using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Web;

namespace RegistrationApp.Data
{
    /// <summary>
    /// Writes action and error logs to the database. Logging must never break the request,
    /// so any failure falls back to Trace + a text file and is swallowed.
    /// </summary>
    public static class AppLogger
    {
        private static readonly object FileLock = new object();

        private static string ConnectionString
        {
            get { return ConfigurationManager.ConnectionStrings["RegistrationDb"].ConnectionString; }
        }

        public static void Action(string action, string details = null)
        {
            try
            {
                using (var con = new SqlConnection(ConnectionString))
                using (var cmd = new SqlCommand(
                    "INSERT dbo.ActionLog([Action], Details, UserName, IpAddress) VALUES (@a, @d, @u, @ip)", con))
                {
                    cmd.Parameters.AddWithValue("@a", Trunc(action, 100));
                    cmd.Parameters.AddWithValue("@d", (object)Trunc(details, 1000) ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@u", (object)Trunc(CurrentUser(), 100) ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ip", (object)Trunc(CurrentIp(), 45) ?? DBNull.Value);
                    con.Open();
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                Fallback("ACTION " + action + " | " + details + " | logging failed: " + ex.Message);
            }
        }

        public static void Error(Exception exception, string url = null)
        {
            if (exception == null) return;
            try
            {
                using (var con = new SqlConnection(ConnectionString))
                using (var cmd = new SqlCommand(
                    "INSERT dbo.ErrorLog(Message, StackTrace, Url, UserName, IpAddress) VALUES (@m, @s, @url, @u, @ip)", con))
                {
                    cmd.Parameters.AddWithValue("@m", Trunc(exception.Message, 2000));
                    cmd.Parameters.AddWithValue("@s", exception.ToString());
                    cmd.Parameters.AddWithValue("@url", (object)Trunc(url, 500) ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@u", (object)Trunc(CurrentUser(), 100) ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ip", (object)Trunc(CurrentIp(), 45) ?? DBNull.Value);
                    con.Open();
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception logEx)
            {
                Fallback("ERROR " + exception + " | logging failed: " + logEx.Message);
            }
        }

        private static string CurrentUser()
        {
            var ctx = HttpContext.Current;
            return ctx != null && ctx.User != null && ctx.User.Identity.IsAuthenticated
                ? ctx.User.Identity.Name : "anonymous";
        }

        private static string CurrentIp()
        {
            var ctx = HttpContext.Current;
            return ctx != null && ctx.Request != null ? ctx.Request.UserHostAddress : null;
        }

        private static string Trunc(string s, int max)
        {
            if (s == null) return null;
            return s.Length <= max ? s : s.Substring(0, max);
        }

        private static void Fallback(string text)
        {
            Trace.TraceError(text);
            try
            {
                var dir = HttpContext.Current != null
                    ? HttpContext.Current.Server.MapPath("~/App_Data")
                    : AppDomain.CurrentDomain.BaseDirectory;
                lock (FileLock)
                {
                    File.AppendAllText(Path.Combine(dir, "fallback.log"),
                        DateTime.UtcNow.ToString("u") + " " + text + Environment.NewLine);
                }
            }
            catch { /* nothing more we can do */ }
        }
    }
}
