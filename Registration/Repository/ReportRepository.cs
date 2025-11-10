using Microsoft.Data.SqlClient;
using Registration.Models;
using Registration.Repository.Interfaces;
using System;
using System.Threading.Tasks;

namespace Registration.Repository
{
    public class ReportRepository : IReportRepository
    {
        private readonly string _connectionString;

        public ReportRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("SqlConnection");
        }

        public async Task<int> SubmitReportAsync(Report report)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = @"
                    INSERT INTO Reports (ReporterId, BlogId, CommentId, ReportedUserId, 
                                       Reason, Description, Status, CreatedAt)
                    VALUES (@ReporterId, @BlogId, @CommentId, @ReportedUserId, 
                           @Reason, @Description, @Status, @CreatedAt);
                    SELECT CAST(SCOPE_IDENTITY() as int);";

                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@ReporterId", report.ReporterId);
                    cmd.Parameters.AddWithValue("@BlogId",
                        report.BlogId.HasValue ? (object)report.BlogId.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@CommentId",
                        report.CommentId.HasValue ? (object)report.CommentId.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@ReportedUserId", report.ReportedUserId);
                    cmd.Parameters.AddWithValue("@Reason", report.Reason);
                    cmd.Parameters.AddWithValue("@Description",
                        string.IsNullOrEmpty(report.Description) ? (object)DBNull.Value : report.Description);
                    cmd.Parameters.AddWithValue("@Status", "pending");
                    cmd.Parameters.AddWithValue("@CreatedAt", DateTime.Now);

                    return (int)await cmd.ExecuteScalarAsync();
                }
            }
        }
    }
}