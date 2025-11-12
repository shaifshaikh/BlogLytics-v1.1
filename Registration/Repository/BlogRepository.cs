using Microsoft.Data.SqlClient;
using Registration.Models;
using Registration.Repository.Interfaces;

namespace Registration.Repository
{
    public class BlogRepository : IBlogRepository
    {
        private readonly string _connectionString;

        public BlogRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("SqlConnection");
        }

        public async Task<int> CreateBlogAsync(Blog blog)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = @"
                    INSERT INTO Blogs (UserId, Title, Content, ImageUrl, CategoryId, Tags, CreatedAt, UpdatedAt)
                    VALUES (@UserId, @Title, @Content, @ImageUrl, @CategoryId, @Tags, @CreatedAt, @UpdatedAt);
                    SELECT CAST(SCOPE_IDENTITY() as int);";

                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@UserId", blog.UserId);
                    cmd.Parameters.AddWithValue("@Title", blog.Title);
                    cmd.Parameters.AddWithValue("@Content", blog.Content);
                    cmd.Parameters.AddWithValue("@ImageUrl", (object)blog.ImageUrl ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@CategoryId", blog.CategoryId);
                    cmd.Parameters.AddWithValue("@Tags", (object)blog.Tags ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@CreatedAt", DateTime.Now);
                    cmd.Parameters.AddWithValue("@UpdatedAt", DateTime.Now);

                    return (int)await cmd.ExecuteScalarAsync();
                }
            }
        }

        public async Task<Blog> GetBlogByIdAsync(int blogId)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = @"
                    SELECT b.*, u.FullName as AuthorName, u.ProfileImage as AuthorProfilePicture, 
                           c.CategoryName
                    FROM Blogs b
                    INNER JOIN Users u ON b.UserId = u.UserId
                    INNER JOIN Categories c ON b.CategoryId = c.CategoryId
                    WHERE b.BlogId = @BlogId AND b.IsDeleted = 0";

                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@BlogId", blogId);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return MapBlogFromReader(reader);
                        }
                    }
                }
            }
            return null;
        }

        public async Task<List<Blog>> GetFeedForUserAsync(int userId, int page, int pageSize)
        {
            var blogs = new List<Blog>();

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                // Complex query with scoring algorithm
                string query = @"
                    WITH ScoredBlogs AS (
                        SELECT 
                            b.*,
                            u.FullName as AuthorName,
                            u.ProfileImage as AuthorProfilePicture,
                            c.CategoryName,
                            -- Calculate Score
                            (
                                -- Category Match (40%)
                                CASE WHEN EXISTS (
                                    SELECT 1 FROM UserInterests ui 
                                    WHERE ui.UserId = @UserId AND ui.CategoryId = b.CategoryId
                                ) 
                                THEN 40 * ISNULL((
                                    SELECT TOP 1 Score / 100.0 
                                    FROM UserInterestScores 
                                    WHERE UserId = @UserId AND CategoryId = b.CategoryId
                                ), 0.5)
                                ELSE 0 END
                                
                                -- Recency (20%)
                                + (20 - (DATEDIFF(HOUR, b.CreatedAt, GETDATE()) / 24.0))
                                
                                -- Engagement (30%)
                                + LEAST(30, (b.LikeCount + b.CommentCount * 2 + b.ShareCount * 3) / 10.0)
                                
                                -- Following (10%)
                                + CASE WHEN EXISTS (
                                    SELECT 1 FROM Follows 
                                    WHERE FollowerId = @UserId AND FollowingId = b.UserId
                                ) THEN 10 ELSE 0 END
                            ) AS Score,
                            
                            -- Check if current user liked this blog
                            CASE WHEN EXISTS (
                                SELECT 1 FROM UserActions 
                                WHERE UserId = @UserId AND BlogId = b.BlogId AND ActionType = 'like'
                            ) THEN 1 ELSE 0 END AS IsLikedByCurrentUser,
                            
                            -- Check if following author
                            CASE WHEN EXISTS (
                                SELECT 1 FROM Follows 
                                WHERE FollowerId = @UserId AND FollowingId = b.UserId
                            ) THEN 1 ELSE 0 END AS IsFollowingAuthor
                            
                        FROM Blogs b
                        INNER JOIN Users u ON b.UserId = u.UserId
                        INNER JOIN Categories c ON b.CategoryId = c.CategoryId
                        WHERE b.IsDeleted = 0 
                        AND b.CreatedAt >= DATEADD(DAY, -30, GETDATE()) -- Only last 30 days
                    )
                    SELECT * FROM ScoredBlogs
                    ORDER BY Score DESC, CreatedAt DESC
                    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    cmd.Parameters.AddWithValue("@Offset", (page - 1) * pageSize);
                    cmd.Parameters.AddWithValue("@PageSize", pageSize);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var blog = MapBlogFromReader(reader);
                            blog.IsLikedByCurrentUser = reader.GetInt32(reader.GetOrdinal("IsLikedByCurrentUser")) == 1;
                            blog.IsFollowingAuthor = reader.GetInt32(reader.GetOrdinal("IsFollowingAuthor")) == 1;
                            blogs.Add(blog);
                        }
                    }
                }
            }

            return blogs;
        }

        public async Task<List<Blog>> GetTrendingBlogsAsync(int page, int pageSize)
        {
            var blogs = new List<Blog>();

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = @"
                    SELECT b.*, u.FullName as AuthorName, u.ProfileImage as AuthorProfilePicture, 
                           c.CategoryName,
                           (b.LikeCount + b.CommentCount * 2 + b.ShareCount * 3 + b.ViewCount * 0.1) AS TrendingScore
                    FROM Blogs b
                    INNER JOIN Users u ON b.UserId = u.UserId
                    INNER JOIN Categories c ON b.CategoryId = c.CategoryId
                    WHERE b.IsDeleted = 0 
                    AND b.CreatedAt >= DATEADD(DAY, -7, GETDATE())
                    ORDER BY TrendingScore DESC, b.CreatedAt DESC
                    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Offset", (page - 1) * pageSize);
                    cmd.Parameters.AddWithValue("@PageSize", pageSize);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            blogs.Add(MapBlogFromReader(reader));
                        }
                    }
                }
            }

            return blogs;
        }

        public async Task<List<Blog>> GetBlogsByCategoryAsync(int categoryId, int page, int pageSize)
        {
            var blogs = new List<Blog>();

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = @"
                    SELECT b.*, u.FullName as AuthorName, u.ProfileImageas AuthorProfilePicture, 
                           c.CategoryName
                    FROM Blogs b
                    INNER JOIN Users u ON b.UserId = u.UserId
                    INNER JOIN Categories c ON b.CategoryId = c.CategoryId
                    WHERE b.CategoryId = @CategoryId AND b.IsDeleted = 0
                    ORDER BY b.CreatedAt DESC
                    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@CategoryId", categoryId);
                    cmd.Parameters.AddWithValue("@Offset", (page - 1) * pageSize);
                    cmd.Parameters.AddWithValue("@PageSize", pageSize);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            blogs.Add(MapBlogFromReader(reader));
                        }
                    }
                }
            }

            return blogs;
        }

        public async Task<List<Blog>> GetBlogsByUserAsync(int userId)
        {
            var blogs = new List<Blog>();

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = @"
                    SELECT b.*, u.FullName as AuthorName, u.ProfileImage as AuthorProfilePicture, 
                           c.CategoryName
                    FROM Blogs b
                    INNER JOIN Users u ON b.UserId = u.UserId
                    INNER JOIN Categories c ON b.CategoryId = c.CategoryId
                    WHERE b.UserId = @UserId AND b.IsDeleted = 0
                    ORDER BY b.CreatedAt DESC";

                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            blogs.Add(MapBlogFromReader(reader));
                        }
                    }
                }
            }

            return blogs;
        }

        public async Task IncrementViewCountAsync(int blogId)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string query = "UPDATE Blogs SET ViewCount = ViewCount + 1 WHERE BlogId = @BlogId";
                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@BlogId", blogId);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task IncrementLikeCountAsync(int blogId)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string query = "UPDATE Blogs SET LikeCount = LikeCount + 1 WHERE BlogId = @BlogId";
                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@BlogId", blogId);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task DecrementLikeCountAsync(int blogId)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string query = "UPDATE Blogs SET LikeCount = CASE WHEN LikeCount > 0 THEN LikeCount - 1 ELSE 0 END WHERE BlogId = @BlogId";
                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@BlogId", blogId);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task IncrementCommentCountAsync(int blogId)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string query = "UPDATE Blogs SET CommentCount = CommentCount + 1 WHERE BlogId = @BlogId";
                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@BlogId", blogId);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task IncrementShareCountAsync(int blogId)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string query = "UPDATE Blogs SET ShareCount = ShareCount + 1 WHERE BlogId = @BlogId";
                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@BlogId", blogId);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task UpdateBlogAsync(Blog blog)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = @"
                    UPDATE Blogs 
                    SET Title = @Title, Content = @Content, ImageUrl = @ImageUrl, 
                        CategoryId = @CategoryId, Tags = @Tags, UpdatedAt = @UpdatedAt
                    WHERE BlogId = @BlogId";

                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@BlogId", blog.BlogId);
                    cmd.Parameters.AddWithValue("@Title", blog.Title);
                    cmd.Parameters.AddWithValue("@Content", blog.Content);
                    cmd.Parameters.AddWithValue("@ImageUrl", (object)blog.ImageUrl ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@CategoryId", blog.CategoryId);
                    cmd.Parameters.AddWithValue("@Tags", (object)blog.Tags ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@UpdatedAt", DateTime.Now);

                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task DeleteBlogAsync(int blogId)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string query = "UPDATE Blogs SET IsDeleted = 1 WHERE BlogId = @BlogId";
                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@BlogId", blogId);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task<int> GetTotalBlogsCountAsync()
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string query = "SELECT COUNT(*) FROM Blogs WHERE IsDeleted = 0";
                using (var cmd = new SqlCommand(query, conn))
                {
                    return (int)await cmd.ExecuteScalarAsync();
                }
            }
        }

        public async Task<int> GetUserBlogsCountAsync(int userId)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string query = "SELECT COUNT(*) FROM Blogs WHERE UserId = @UserId AND IsDeleted = 0";
                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    return (int)await cmd.ExecuteScalarAsync();
                }
            }
        }

        private Blog MapBlogFromReader(SqlDataReader reader)
        {
            return new Blog
            {
                BlogId = reader.GetInt32(reader.GetOrdinal("BlogId")),
                UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
                Title = reader.GetString(reader.GetOrdinal("Title")),
                Content = reader.GetString(reader.GetOrdinal("Content")),
                ImageUrl = reader.IsDBNull(reader.GetOrdinal("ImageUrl")) ? null : reader.GetString(reader.GetOrdinal("ImageUrl")),
                CategoryId = reader.GetInt32(reader.GetOrdinal("CategoryId")),
                Tags = reader.IsDBNull(reader.GetOrdinal("Tags")) ? null : reader.GetString(reader.GetOrdinal("Tags")),
                LikeCount = reader.GetInt32(reader.GetOrdinal("LikeCount")),
                CommentCount = reader.GetInt32(reader.GetOrdinal("CommentCount")),
                ShareCount = reader.GetInt32(reader.GetOrdinal("ShareCount")),
                ViewCount = reader.GetInt32(reader.GetOrdinal("ViewCount")),
                IsDeleted = reader.GetBoolean(reader.GetOrdinal("IsDeleted")),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                UpdatedAt = reader.GetDateTime(reader.GetOrdinal("UpdatedAt")),
                AuthorName = reader.GetString(reader.GetOrdinal("AuthorName")),
                AuthorProfilePicture = reader.IsDBNull(reader.GetOrdinal("AuthorProfilePicture")) ? null : reader.GetString(reader.GetOrdinal("AuthorProfilePicture")),
                CategoryName = reader.GetString(reader.GetOrdinal("CategoryName"))
            };
        }


    }
}

#region Old code commented
//public async Task<List<Blog>> GetAllBlogsAsync()
//{
//    var blogs = new List<Blog>();

//    using (SqlConnection conn = new SqlConnection(_connectionString))
//    {
//        string query = @"
//                    SELECT 
//                        b.BlogId, b.Title, b.ShortDescription, b.FullDescription,
//                        b.ThumbnailImage, b.ViewCount, b.PublishedDate, b.IsActive,
//                        c.CategoryId, c.CategoryName,
//                        a.AuthorId, a.AuthorName
//                    FROM Blogs b
//                    INNER JOIN BlogCategories c ON b.CategoryId = c.CategoryId
//                    INNER JOIN Authors a ON b.AuthorId = a.AuthorId
//                    WHERE b.IsActive = 1
//                    ORDER BY b.PublishedDate DESC";

//        using (SqlCommand cmd = new SqlCommand(query, conn))
//        {
//            await conn.OpenAsync();
//            using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
//            {
//                while (await reader.ReadAsync())
//                {
//                    var blog = new Blog
//                    {
//                        BlogId = reader.GetInt32(0),
//                        Title = reader.GetString(1),
//                        ShortDescription = reader.GetString(2),
//                        FullDescription = reader.GetString(3),
//                        ThumbnailImage = reader.IsDBNull(4) ? null : reader.GetString(4),
//                        ViewCount = reader.GetInt32(5),
//                        PublishedDate = reader.GetDateTime(6),
//                        IsActive = reader.GetBoolean(7),
//                        CategoryId = reader.GetInt32(8),
//                        CategoryName = reader.GetString(9),
//                        AuthorId = reader.GetInt32(10),
//                        AuthorName = reader.GetString(11)
//                    };
//                    blogs.Add(blog);
//                }
//            }
//        }

//        // Load tags for each blog
//        foreach (var blog in blogs)
//        {
//            blog.Tags = await GetBlogTagsAsync(conn, blog.BlogId);
//        }
//    }

//    return blogs;
//}

//public async Task<Blog> GetBlogByIdAsync(int blogId)
//{
//    Blog blog = null;

//    using (SqlConnection conn = new SqlConnection(_connectionString))
//    {
//        string query = @"
//                    SELECT 
//                        b.BlogId, b.Title, b.ShortDescription, b.FullDescription,
//                        b.ThumbnailImage, b.ViewCount, b.PublishedDate, b.IsActive,
//                        c.CategoryId, c.CategoryName,
//                        a.AuthorId, a.AuthorName, a.Email, a.Bio
//                    FROM Blogs b
//                    INNER JOIN BlogCategories c ON b.CategoryId = c.CategoryId
//                    INNER JOIN Authors a ON b.AuthorId = a.AuthorId
//                    WHERE b.BlogId = @BlogId AND b.IsActive = 1";

//        using (SqlCommand cmd = new SqlCommand(query, conn))
//        {
//            cmd.Parameters.AddWithValue("@BlogId", blogId);

//            await conn.OpenAsync();
//            using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
//            {
//                if (await reader.ReadAsync())
//                {
//                    blog = new Blog
//                    {
//                        BlogId = reader.GetInt32(0),
//                        Title = reader.GetString(1),
//                        ShortDescription = reader.GetString(2),
//                        FullDescription = reader.GetString(3),
//                        ThumbnailImage = reader.IsDBNull(4) ? null : reader.GetString(4),
//                        ViewCount = reader.GetInt32(5),
//                        PublishedDate = reader.GetDateTime(6),
//                        IsActive = reader.GetBoolean(7),
//                        CategoryId = reader.GetInt32(8),
//                        CategoryName = reader.GetString(9),
//                        AuthorId = reader.GetInt32(10),
//                        AuthorName = reader.GetString(11)
//                    };
//                }
//            }
//        }

//        if (blog != null)
//        {
//            blog.Images = await GetBlogImagesAsync(conn, blogId);
//            blog.Tags = await GetBlogTagsAsync(conn, blogId);
//        }
//    }

//    return blog;
//}

//private async Task<List<BlogImage>> GetBlogImagesAsync(SqlConnection conn, int blogId)
//{
//    var images = new List<BlogImage>();
//    string query = @"
//                SELECT ImageId, BlogId, ImageUrl, Caption, DisplayOrder
//                FROM BlogImages
//                WHERE BlogId = @BlogId
//                ORDER BY DisplayOrder";

//    using (SqlCommand cmd = new SqlCommand(query, conn))
//    {
//        cmd.Parameters.AddWithValue("@BlogId", blogId);

//        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
//        {
//            while (await reader.ReadAsync())
//            {
//                images.Add(new BlogImage
//                {
//                    ImageId = reader.GetInt32(0),
//                    BlogId = reader.GetInt32(1),
//                    ImageUrl = reader.GetString(2),
//                    Caption = reader.IsDBNull(3) ? null : reader.GetString(3),
//                    DisplayOrder = reader.GetInt32(4)
//                });
//            }
//        }
//    }

//    return images;
//}

//private async Task<List<string>> GetBlogTagsAsync(SqlConnection conn, int blogId)
//{
//    var tags = new List<string>();
//    string query = @"
//                SELECT t.TagName
//                FROM BlogTags bt
//                INNER JOIN Tags t ON bt.TagId = t.TagId
//                WHERE bt.BlogId = @BlogId";

//    using (SqlCommand cmd = new SqlCommand(query, conn))
//    {
//        cmd.Parameters.AddWithValue("@BlogId", blogId);

//        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
//        {
//            while (await reader.ReadAsync())
//            {
//                tags.Add(reader.GetString(0));
//            }
//        }
//    }

//    return tags;
//}

//public async Task<bool> IncrementViewCountAsync(int blogId)
//{
//    using (SqlConnection conn = new SqlConnection(_connectionString))
//    {
//        string query = @"
//                    UPDATE Blogs 
//                    SET ViewCount = ViewCount + 1 
//                    WHERE BlogId = @BlogId";

//        using (SqlCommand cmd = new SqlCommand(query, conn))
//        {
//            cmd.Parameters.AddWithValue("@BlogId", blogId);

//            await conn.OpenAsync();
//            int rowsAffected = await cmd.ExecuteNonQueryAsync();
//            return rowsAffected > 0;
//        }
//    }
//}
#endregion