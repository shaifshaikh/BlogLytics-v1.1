using Registration.Models;

namespace Registration.Repository.Interfaces
{
    public interface ICommentRepository
    {
        Task<int> AddCommentAsync(Comment comment);
        Task<List<Comment>> GetCommentsByBlogIdAsync(int blogId);
        Task DeleteCommentAsync(int commentId);
    }
}
