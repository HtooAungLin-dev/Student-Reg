using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using RegistrationApp.Models;

namespace RegistrationApp.Data
{
    public class DuplicateNricException : Exception
    {
        public DuplicateNricException(string nric)
            : base("NRIC " + nric + " is already registered.") { }
    }

    public class StudentRepository
    {
        private static string ConnectionString
        {
            get { return ConfigurationManager.ConnectionStrings["RegistrationDb"].ConnectionString; }
        }

        private const string AgeExpr =
            "DATEDIFF(YEAR, s.Birthday, GETDATE()) - " +
            "CASE WHEN DATEADD(YEAR, DATEDIFF(YEAR, s.Birthday, GETDATE()), s.Birthday) > CAST(GETDATE() AS DATE) THEN 1 ELSE 0 END";

        // ---------------------------------------------------------------- subjects
        public IList<SubjectItem> GetSubjects()
        {
            var list = new List<SubjectItem>();
            using (var con = new SqlConnection(ConnectionString))
            using (var cmd = new SqlCommand("SELECT Id, Name FROM dbo.[Subject] ORDER BY Id", con))
            {
                con.Open();
                using (var r = cmd.ExecuteReader())
                    while (r.Read())
                        list.Add(new SubjectItem { Id = r.GetInt32(0), Name = r.GetString(1) });
            }
            return list;
        }

        // ---------------------------------------------------------------- create
        /// <summary>Inserts the student and subjects in ONE transaction (all or nothing).</summary>
        public int Create(StudentFormModel m)
        {
            using (var con = new SqlConnection(ConnectionString))
            {
                con.Open();
                using (var tx = con.BeginTransaction())
                {
                    try
                    {
                        int id;
                        using (var cmd = new SqlCommand(
                            @"INSERT dbo.Student (NRIC, Name, Gender, Birthday, AvailableDate)
                              OUTPUT INSERTED.Id
                              VALUES (@NRIC, @Name, @Gender, @Birthday, @AvailableDate);", con, tx))
                        {
                            AddStudentParams(cmd, m);
                            id = (int)cmd.ExecuteScalar();
                        }
                        InsertSubjects(con, tx, id, m.SelectedSubjectIds);
                        tx.Commit();
                        return id;
                    }
                    catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
                    {
                        throw new DuplicateNricException(m.NRIC);   // tx disposed => rolled back
                    }
                }
            }
        }

        // ---------------------------------------------------------------- read one
        public StudentFormModel GetById(int id)
        {
            StudentFormModel m = null;
            using (var con = new SqlConnection(ConnectionString))
            using (var cmd = new SqlCommand(
                @"SELECT Id, NRIC, Name, Gender, Birthday, AvailableDate, RowVer FROM dbo.Student WHERE Id = @Id;
                  SELECT SubjectId FROM dbo.StudentSubject WHERE StudentId = @Id;", con))
            {
                cmd.Parameters.Add("@Id", SqlDbType.Int).Value = id;
                con.Open();
                using (var r = cmd.ExecuteReader())
                {
                    if (r.Read())
                    {
                        m = new StudentFormModel
                        {
                            Id = r.GetInt32(0),
                            NRIC = r.GetString(1),
                            Name = r.GetString(2),
                            Gender = r.GetString(3),
                            Birthday = r.GetDateTime(4),
                            AvailableDate = r.IsDBNull(5) ? (DateTime?)null : r.GetDateTime(5),
                            RowVersion = (byte[])r.GetValue(6)
                        };
                        r.NextResult();
                        while (r.Read()) m.SelectedSubjectIds.Add(r.GetInt32(0));
                    }
                }
            }
            return m;
        }

        // ---------------------------------------------------------------- update
        /// <returns>false if the row was changed/deleted by someone else (optimistic concurrency).</returns>
        public bool Update(StudentFormModel m)
        {
            using (var con = new SqlConnection(ConnectionString))
            {
                con.Open();
                using (var tx = con.BeginTransaction())
                {
                    try
                    {
                        int rows;
                        using (var cmd = new SqlCommand(
                            @"UPDATE dbo.Student
                                 SET NRIC = @NRIC, Name = @Name, Gender = @Gender, Birthday = @Birthday,
                                     AvailableDate = @AvailableDate, UpdatedAt = SYSUTCDATETIME()
                               WHERE Id = @Id AND RowVer = @RowVer;", con, tx))
                        {
                            AddStudentParams(cmd, m);
                            cmd.Parameters.Add("@Id", SqlDbType.Int).Value = m.Id;
                            cmd.Parameters.Add("@RowVer", SqlDbType.Binary, 8).Value = m.RowVersion;
                            rows = cmd.ExecuteNonQuery();
                        }
                        if (rows == 0) return false;                       // tx rolled back on dispose

                        using (var del = new SqlCommand("DELETE dbo.StudentSubject WHERE StudentId = @Id", con, tx))
                        {
                            del.Parameters.Add("@Id", SqlDbType.Int).Value = m.Id;
                            del.ExecuteNonQuery();
                        }
                        InsertSubjects(con, tx, m.Id, m.SelectedSubjectIds);
                        tx.Commit();
                        return true;
                    }
                    catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
                    {
                        throw new DuplicateNricException(m.NRIC);
                    }
                }
            }
        }

        // ---------------------------------------------------------------- list / search
        public IList<StudentListItem> Search(string term)
        {
            string pattern = string.IsNullOrWhiteSpace(term) ? null : "%" + EscapeLike(term.Trim()) + "%";

            string sql = @"
SELECT ROW_NUMBER() OVER (ORDER BY s.Id) AS SN, s.Id, s.NRIC, s.Name, s.Gender,
       " + AgeExpr + @" AS Age,
       COUNT(ss.SubjectId) AS SubjectCount
FROM   dbo.Student s
LEFT JOIN dbo.StudentSubject ss ON ss.StudentId = s.Id
WHERE  @Pattern IS NULL
    OR s.NRIC LIKE @Pattern ESCAPE '\'
    OR s.Name LIKE @Pattern ESCAPE '\'
GROUP BY s.Id, s.NRIC, s.Name, s.Gender, s.Birthday
ORDER BY s.Id
OPTION (RECOMPILE);";

            var list = new List<StudentListItem>();
            using (var con = new SqlConnection(ConnectionString))
            using (var cmd = new SqlCommand(sql, con))
            {
                var p = cmd.Parameters.Add("@Pattern", SqlDbType.NVarChar, 220);
                p.Value = (object)pattern ?? DBNull.Value;
                con.Open();
                using (var r = cmd.ExecuteReader())
                    while (r.Read())
                        list.Add(new StudentListItem
                        {
                            SN = Convert.ToInt32(r["SN"]),
                            Id = Convert.ToInt32(r["Id"]),
                            NRIC = r.GetString(2),
                            Name = r.GetString(3),
                            Gender = r.GetString(4),
                            Age = Convert.ToInt32(r["Age"]),
                            SubjectCount = Convert.ToInt32(r["SubjectCount"])
                        });
            }
            return list;
        }

        // ---------------------------------------------------------------- helpers
        private static void AddStudentParams(SqlCommand cmd, StudentFormModel m)
        {
            cmd.Parameters.Add("@NRIC", SqlDbType.VarChar, 9).Value = m.NRIC;
            cmd.Parameters.Add("@Name", SqlDbType.NVarChar, 100).Value = m.Name;
            cmd.Parameters.Add("@Gender", SqlDbType.Char, 1).Value = m.Gender;
            cmd.Parameters.Add("@Birthday", SqlDbType.Date).Value = m.Birthday.Value.Date;
            cmd.Parameters.Add("@AvailableDate", SqlDbType.Date).Value =
                m.AvailableDate.HasValue ? (object)m.AvailableDate.Value.Date : DBNull.Value;
        }

        private static void InsertSubjects(SqlConnection con, SqlTransaction tx, int studentId, IEnumerable<int> subjectIds)
        {
            foreach (int subjectId in (subjectIds ?? Enumerable.Empty<int>()).Distinct())
            {
                using (var cmd = new SqlCommand(
                    "INSERT dbo.StudentSubject (StudentId, SubjectId) VALUES (@s, @sub)", con, tx))
                {
                    cmd.Parameters.Add("@s", SqlDbType.Int).Value = studentId;
                    cmd.Parameters.Add("@sub", SqlDbType.Int).Value = subjectId;
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private static string EscapeLike(string s)
        {
            return s.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_").Replace("[", @"\[");
        }
    }
}
