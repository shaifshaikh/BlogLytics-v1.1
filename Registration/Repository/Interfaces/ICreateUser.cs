using Registration.Models;

namespace Registration.Repository
{ 
    public interface ICreateUser
    {
        Task<bool> EmailExistsAsync(string email);
        Task CreateUserAsync(string email, string fullName, string password);

        Task<UserDto> GetUserByEmailAsync(string email);
    }
}
