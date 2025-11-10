namespace Registration.Repository.Interfaces
{
    public interface IUserActionRepository
    {
        Task RecordActionAsync(int userId, int blogId, string actionType);
        Task<bool> HasUserLikedBlogAsync(int userId, int blogId);
        Task RemoveLikeAsync(int userId, int blogId);
        Task UpdateUserInterestScoresAsync(int userId, int categoryId, string actionType);
    }
}
