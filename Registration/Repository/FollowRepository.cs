using Microsoft.Data.SqlClient;
using Registration.Repository.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Registration.Repository
{
    public class FollowRepository : IFollowRepository
    {
        private readonly string _connectionString;

        public FollowRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("SqlConnection");
        }

        public async Task FollowUserAsync(int followerId, int followingId)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = @"
                    IF NOT EXISTS (SELECT 1 FROM Follows WHERE FollowerId = @FollowerId AND FollowingId = @FollowingId)
                    BEGIN
                        INSERT INTO Follows (FollowerId, FollowingId, CreatedAt)
                        VALUES (@FollowerId, @FollowingId, @CreatedAt)
                    END";

                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@FollowerId", followerId);
                    cmd.Parameters.AddWithValue("@FollowingId", followingId);
                    cmd.Parameters.AddWithValue("@CreatedAt", DateTime.Now);

                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task UnfollowUserAsync(int followerId, int followingId)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = @"
                    DELETE FROM Follows 
                    WHERE FollowerId = @FollowerId AND FollowingId = @FollowingId";

                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@FollowerId", followerId);
                    cmd.Parameters.AddWithValue("@FollowingId", followingId);

                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task<bool> IsFollowingAsync(int followerId, int followingId)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = @"
                    SELECT COUNT(*) 
                    FROM Follows 
                    WHERE FollowerId = @FollowerId AND FollowingId = @FollowingId";

                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@FollowerId", followerId);
                    cmd.Parameters.AddWithValue("@FollowingId", followingId);

                    int count = (int)await cmd.ExecuteScalarAsync();
                    return count > 0;
                }
            }
        }

        public async Task<List<int>> GetFollowingIdsAsync(int userId)
        {
            var followingIds = new List<int>();

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = @"
                    SELECT FollowingId 
                    FROM Follows 
                    WHERE FollowerId = @UserId";

                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            followingIds.Add(reader.GetInt32(0));
                        }
                    }
                }
            }

            return followingIds;
        }

        public async Task<int> GetFollowersCountAsync(int userId)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = "SELECT COUNT(*) FROM Follows WHERE FollowingId = @UserId";

                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    return (int)await cmd.ExecuteScalarAsync();
                }
            }
        }

        public async Task<int> GetFollowingCountAsync(int userId)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = "SELECT COUNT(*) FROM Follows WHERE FollowerId = @UserId";

                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    return (int)await cmd.ExecuteScalarAsync();
                }
            }
        }
    }
}