using Microsoft.Data.SqlClient;
using Registration.Models;
using Registration.Repository.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Registration.Repository
{
    public class CommentRepository : ICommentRepository
    {
        private readonly string _connectionString;

        public CommentRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("SqlConnection");
        }

        public async Task<int> AddCommentAsync(Comment comment)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = @"
                    INSERT INTO Comments (UserId, BlogId, Content, ParentCommentId, CreatedAt)
                    VALUES (@UserId, @BlogId, @Content, @ParentCommentId, @CreatedAt);
                    SELECT CAST(SCOPE_IDENTITY() as int);";

                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@UserId", comment.UserId);
                    cmd.Parameters.AddWithValue("@BlogId", comment.BlogId);
                    cmd.Parameters.AddWithValue("@Content", comment.Content);
                    cmd.Parameters.AddWithValue("@ParentCommentId",
                        comment.ParentCommentId.HasValue ? (object)comment.ParentCommentId.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@CreatedAt", DateTime.Now);

                    return (int)await cmd.ExecuteScalarAsync();
                }
            }
        }

        public async Task<List<Comment>> GetCommentsByBlogIdAsync(int blogId)
        {
            var comments = new List<Comment>();

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                // Get all comments (both parent and replies)
                string query = @"
                    SELECT c.CommentId, c.UserId, c.BlogId, c.Content, c.ParentCommentId, c.CreatedAt,
                           u.FullName as UserName, u.ProfilePicture as UserProfilePicture
                    FROM Comments c
                    INNER JOIN Users u ON c.UserId = u.UserId
                    WHERE c.BlogId = @BlogId
                    ORDER BY c.CreatedAt ASC";

                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@BlogId", blogId);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            comments.Add(new Comment
                            {
                                CommentId = reader.GetInt32(0),
                                UserId = reader.GetInt32(1),
                                BlogId = reader.GetInt32(2),
                                Content = reader.GetString(3),
                                ParentCommentId = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                                CreatedAt = reader.GetDateTime(5),
                                UserName = reader.GetString(6),
                                UserProfilePicture = reader.IsDBNull(7) ? null : reader.GetString(7)
                            });
                        }
                    }
                }
            }

            // Organize comments into parent-child structure
            var parentComments = comments.Where(c => c.ParentCommentId == null).ToList();

            foreach (var parent in parentComments)
            {
                parent.Replies = comments.Where(c => c.ParentCommentId == parent.CommentId).ToList();
            }

            return parentComments;
        }

        public async Task DeleteCommentAsync(int commentId)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                // Delete replies first, then parent
                string query = @"
                    DELETE FROM Comments WHERE ParentCommentId = @CommentId;
                    DELETE FROM Comments WHERE CommentId = @CommentId;";

                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@CommentId", commentId);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }
    }
}