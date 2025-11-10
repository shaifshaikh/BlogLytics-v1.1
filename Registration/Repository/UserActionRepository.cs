using Microsoft.Data.SqlClient;
using Registration.Repository.Interfaces;
using System;
using System.Threading.Tasks;

namespace Registration.Repository
{
    public class UserActionRepository : IUserActionRepository
    {
        private readonly string _connectionString;

        public UserActionRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("SqlConnection");
        }

        public async Task RecordActionAsync(int userId, int blogId, string actionType)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = @"
                    INSERT INTO UserActions (UserId, BlogId, ActionType, CreatedAt)
                    VALUES (@UserId, @BlogId, @ActionType, @CreatedAt)";

                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    cmd.Parameters.AddWithValue("@BlogId", blogId);
                    cmd.Parameters.AddWithValue("@ActionType", actionType);
                    cmd.Parameters.AddWithValue("@CreatedAt", DateTime.Now);

                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task<bool> HasUserLikedBlogAsync(int userId, int blogId)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = @"
                    SELECT COUNT(*) 
                    FROM UserActions 
                    WHERE UserId = @UserId AND BlogId = @BlogId AND ActionType = 'like'";

                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    cmd.Parameters.AddWithValue("@BlogId", blogId);

                    int count = (int)await cmd.ExecuteScalarAsync();
                    return count > 0;
                }
            }
        }

        public async Task RemoveLikeAsync(int userId, int blogId)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = @"
                    DELETE FROM UserActions 
                    WHERE UserId = @UserId AND BlogId = @BlogId AND ActionType = 'like'";

                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    cmd.Parameters.AddWithValue("@BlogId", blogId);

                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task UpdateUserInterestScoresAsync(int userId, int categoryId, string actionType)
        {
            // Define weights for different actions
            var actionWeights = new Dictionary<string, double>
            {
                { "view", 1 },
                { "like", 5 },
                { "comment", 7 },
                { "share", 10 },
                { "save", 8 }
            };

            if (!actionWeights.ContainsKey(actionType))
                return;

            double weight = actionWeights[actionType];

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                // Check if score entry exists
                string checkQuery = @"
                    SELECT COUNT(*) 
                    FROM UserInterestScores 
                    WHERE UserId = @UserId AND CategoryId = @CategoryId";

                using (var checkCmd = new SqlCommand(checkQuery, conn))
                {
                    checkCmd.Parameters.AddWithValue("@UserId", userId);
                    checkCmd.Parameters.AddWithValue("@CategoryId", categoryId);

                    int count = (int)await checkCmd.ExecuteScalarAsync();

                    if (count > 0)
                    {
                        // Update existing score
                        string updateQuery = @"
                            UPDATE UserInterestScores 
                            SET Score = CASE 
                                WHEN Score + @Weight > 100 THEN 100 
                                ELSE Score + @Weight 
                            END,
                            LastUpdated = @LastUpdated
                            WHERE UserId = @UserId AND CategoryId = @CategoryId";

                        using (var updateCmd = new SqlCommand(updateQuery, conn))
                        {
                            updateCmd.Parameters.AddWithValue("@UserId", userId);
                            updateCmd.Parameters.AddWithValue("@CategoryId", categoryId);
                            updateCmd.Parameters.AddWithValue("@Weight", weight);
                            updateCmd.Parameters.AddWithValue("@LastUpdated", DateTime.Now);

                            await updateCmd.ExecuteNonQueryAsync();
                        }
                    }
                    else
                    {
                        // Insert new score
                        string insertQuery = @"
                            INSERT INTO UserInterestScores (UserId, CategoryId, Score, LastUpdated)
                            VALUES (@UserId, @CategoryId, @Score, @LastUpdated)";

                        using (var insertCmd = new SqlCommand(insertQuery, conn))
                        {
                            insertCmd.Parameters.AddWithValue("@UserId", userId);
                            insertCmd.Parameters.AddWithValue("@CategoryId", categoryId);
                            insertCmd.Parameters.AddWithValue("@Score", Math.Min(50 + weight, 100)); // Start at 50
                            insertCmd.Parameters.AddWithValue("@LastUpdated", DateTime.Now);

                            await insertCmd.ExecuteNonQueryAsync();
                        }
                    }
                }
            }
        }
    }
}


