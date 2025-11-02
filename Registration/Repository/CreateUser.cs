using Microsoft.Data.SqlClient;
using Registration.Models;
using Registration.Repository;

namespace Registration.Repository
{
    public class CreateUser : ICreateUser
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;
        public CreateUser(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("SqlConnection");
        }
        public async Task<bool> EmailExistsAsync(string email)
        {
            using (SqlConnection con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();
                string query = "select count(*) from users where email=@email";
                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@email", email);
                    int count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                    
                    return count > 0;
                }


            }
        }
        public async Task CreateUserAsync(string email, string fullName, string password)
        {
            using (SqlConnection con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();
                string query = @"
                    INSERT INTO Users (Email, PasswordHash, FullName, Role, IsActive, EmailConfirmed, CreatedAt)
                    VALUES (@Email, @Password, @FullName, 'Blogger', 1, 1, @CreatedAt)";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@Email", email);
                    cmd.Parameters.AddWithValue("@Password", password); // Hash in production
                    cmd.Parameters.AddWithValue("@FullName", fullName);
                    cmd.Parameters.AddWithValue("@CreatedAt", DateTime.Now);

                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }


        public async Task<UserDto> GetUserByEmailAsync(string email)
        {
            UserDto user = null;

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string query = "SELECT UserId, Email, FullName, Role FROM Users WHERE Email = @Email AND IsActive = 1";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Email", email);

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            user = new UserDto
                            {
                                UserId = Convert.ToInt32(reader["UserId"]),
                                Email = reader["Email"].ToString(),
                                FullName = reader["FullName"].ToString(),
                                Role = reader["Role"].ToString()
                            };
                        }
                    }
                }
            }

            return user;
        }
    }
}
