using Registration.Models;

namespace Registration.Repository.Interfaces
{
    public interface IBlogRepository
    {
        Task<int> CreateBlogAsync(Blog blog);

        // Read
        Task<Blog> GetBlogByIdAsync(int blogId);
        Task<List<Blog>> GetFeedForUserAsync(int userId, int page, int pageSize);
        Task<List<Blog>> GetBlogsByCategoryAsync(int categoryId, int page, int pageSize);
        Task<List<Blog>> GetBlogsByUserAsync(int userId);
        Task<List<Blog>> GetTrendingBlogsAsync(int page, int pageSize);

        // Update
        Task UpdateBlogAsync(Blog blog);
        Task IncrementViewCountAsync(int blogId);
        Task IncrementLikeCountAsync(int blogId);
        Task DecrementLikeCountAsync(int blogId);
        Task IncrementCommentCountAsync(int blogId);
        Task IncrementShareCountAsync(int blogId);

        // Delete
        Task DeleteBlogAsync(int blogId);

        // Stats
        Task<int> GetTotalBlogsCountAsync();
        Task<int> GetUserBlogsCountAsync(int userId);
    }
}
//Task<List<Blog>> GetAllBlogsAsync();
//Task<Blog> GetBlogByIdAsync(int blogId);
//Task<bool> IncrementViewCountAsync(int blogId);