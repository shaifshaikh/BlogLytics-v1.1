using Microsoft.Data.SqlClient;
using Registration.Models;

namespace Registration.Repository
{
    public class UserInterestRepository : IUserInterestRepository
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;
        
        public UserInterestRepository(IConfiguration configuration)
        {
            _configuration = configuration; 
            _connectionString= _configuration.GetConnectionString("SqlConnection");
        }

        public async Task<List<Category>> GetAllActiveCategoriesAsync()
        {
            var categories = new List<Category>();

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var command = new SqlCommand(
                    "SELECT CategoryId, CategoryName, Description, IsActive, CreatedAt FROM Categories WHERE IsActive = 1 ORDER BY CategoryName",
                    connection
                );

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var category = new Category
                        {
                            CategoryId = reader.GetInt32(0),
                            CategoryName = reader.GetString(1),
                            Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                            IsActive = reader.GetBoolean(3),
                            CreatedAt = reader.GetDateTime(4)
                        };

                        // Assign icon and color based on category name
                        AssignIconAndColor(category);
                        categories.Add(category);
                    }
                }
            }

            return categories;
        }

        private void AssignIconAndColor(Category category)
        {
            // Map category names to icons and colors
            var categoryName = category.CategoryName.ToLower();

            if (categoryName.Contains("tech") || categoryName.Contains("technology"))
            {
                category.IconClass = "fas fa-laptop-code";
                category.ColorClass = "color-tech";
            }
            else if (categoryName.Contains("sport"))
            {
                category.IconClass = "fas fa-football-ball";
                category.ColorClass = "color-sports";
            }
            else if (categoryName.Contains("art") || categoryName.Contains("design"))
            {
                category.IconClass = "fas fa-palette";
                category.ColorClass = "color-art";
            }
            else if (categoryName.Contains("music"))
            {
                category.IconClass = "fas fa-music";
                category.ColorClass = "color-music";
            }
            else if (categoryName.Contains("food") || categoryName.Contains("cook"))
            {
                category.IconClass = "fas fa-utensils";
                category.ColorClass = "color-food";
            }
            else if (categoryName.Contains("travel"))
            {
                category.IconClass = "fas fa-plane";
                category.ColorClass = "color-travel";
            }
            else if (categoryName.Contains("game") || categoryName.Contains("gaming"))
            {
                category.IconClass = "fas fa-gamepad";
                category.ColorClass = "color-tech";
            }
            else if (categoryName.Contains("fashion"))
            {
                category.IconClass = "fas fa-tshirt";
                category.ColorClass = "color-art";
            }
            else if (categoryName.Contains("health") || categoryName.Contains("fitness"))
            {
                category.IconClass = "fas fa-heartbeat";
                category.ColorClass = "color-sports";
            }
            else if (categoryName.Contains("book") || categoryName.Contains("read"))
            {
                category.IconClass = "fas fa-book";
                category.ColorClass = "color-default";
            }
            else if (categoryName.Contains("movie") || categoryName.Contains("film") || categoryName.Contains("tv"))
            {
                category.IconClass = "fas fa-film";
                category.ColorClass = "color-default";
            }
            else if (categoryName.Contains("photo"))
            {
                category.IconClass = "fas fa-camera";
                category.ColorClass = "color-art";
            }
            else if (categoryName.Contains("science"))
            {
                category.IconClass = "fas fa-flask";
                category.ColorClass = "color-tech";
            }
            else if (categoryName.Contains("business"))
            {
                category.IconClass = "fas fa-briefcase";
                category.ColorClass = "color-default";
            }
            else if (categoryName.Contains("education") || categoryName.Contains("learning"))
            {
                category.IconClass = "fas fa-graduation-cap";
                category.ColorClass = "color-default";
            }
            else
            {
                // Default icon and color
                category.IconClass = "fas fa-star";
                category.ColorClass = "color-default";
            }
        }

        public async Task<bool> HasUserSelectedInterestsAsync(int userId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var command = new SqlCommand(
                    "SELECT COUNT(*) FROM UserInterests WHERE UserId = @UserId",
                    connection
                );
                command.Parameters.AddWithValue("@UserId", userId);

                var count = (int)await command.ExecuteScalarAsync();
                return count > 0;
            }
        }

        public async Task<bool> HasCompletedInterestSelectionAsync(string userId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var command = new SqlCommand(
                    "SELECT HasSelectedInterests FROM Users WHERE UserId = @UserId",
                    connection
                );
                command.Parameters.AddWithValue("@UserId", userId);

                var result = await command.ExecuteScalarAsync();
                return result != null && (bool)result;
            }
        }

        public async Task SaveUserInterestsAsync(string userId, List<int> categoryIds)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // First, delete existing interests
                        var deleteCommand = new SqlCommand(
                            "DELETE FROM UserInterests WHERE UserId = @UserId",
                            connection,
                            transaction
                        );
                        deleteCommand.Parameters.AddWithValue("@UserId", userId);
                        await deleteCommand.ExecuteNonQueryAsync();

                        // Insert new interests
                        foreach (var categoryId in categoryIds)
                        {
                            var insertCommand = new SqlCommand(
                                "INSERT INTO UserInterests (UserId, CategoryId, AddedAt) VALUES (@UserId, @CategoryId, @AddedAt)",
                                connection,
                                transaction
                            );
                            insertCommand.Parameters.AddWithValue("@UserId", userId);
                            insertCommand.Parameters.AddWithValue("@CategoryId", categoryId);
                            insertCommand.Parameters.AddWithValue("@AddedAt", DateTime.Now);
                            await insertCommand.ExecuteNonQueryAsync();
                        }

                        // Update user's HasSelectedInterests flag
                        var updateUserCommand = new SqlCommand(
                            "UPDATE Users SET HasSelectedInterests = 1 WHERE UserId = @UserId",
                            connection,
                            transaction
                        );
                        updateUserCommand.Parameters.AddWithValue("@UserId", userId);
                        await updateUserCommand.ExecuteNonQueryAsync();

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        public async Task MarkInterestSelectionAsSkippedAsync(string userId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var command = new SqlCommand(
                    "UPDATE Users SET HasSelectedInterests = 1 WHERE UserId = @UserId",
                    connection
                );
                command.Parameters.AddWithValue("@UserId", userId);
                await command.ExecuteNonQueryAsync();
            }
        }

        public async Task<List<UserInterest>> GetUserInterestsAsync(int userId)
        {
            var interests = new List<UserInterest>();

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var command = new SqlCommand(
                    "SELECT InterestId, UserId, CategoryId, AddedAt FROM UserInterests WHERE UserId = @UserId",
                    connection
                );
                command.Parameters.AddWithValue("@UserId", userId);

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        interests.Add(new UserInterest
                        {
                            InterestId = reader.GetInt32(0),
                            UserId = reader.GetInt32(1),
                            CategoryId = reader.GetInt32(2),
                            AddedAt = reader.GetDateTime(3)
                        });
                    }
                }
            }

            return interests;
        }
    }
}



