namespace Registration.Repository.Interfaces
{
    public interface IResetPassword
    {
        Task<bool> VerifyResetTokenAsync(string email, string token);
        Task InvalidateResetTokenAsync(string token);
        Task UpdatePasswordAsync(string email, string newPassword);

        Task SavePasswordResetTokenAsync(int userId, string token);
    }
}
