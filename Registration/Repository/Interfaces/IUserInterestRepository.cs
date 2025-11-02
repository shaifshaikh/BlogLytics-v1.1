using Registration.Models;

namespace Registration.Repository
{
    public interface IUserInterestRepository
    {
        Task<List<Category>> GetAllActiveCategoriesAsync();
        Task<bool> HasUserSelectedInterestsAsync(int userId);
        Task<bool> HasCompletedInterestSelectionAsync(string userId);
        Task SaveUserInterestsAsync(string userId, List<int> categoryIds);
        Task MarkInterestSelectionAsSkippedAsync(string userId);
        Task<List<UserInterest>> GetUserInterestsAsync(int userId);
    }
}
