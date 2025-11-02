using Azure.Core;
using Microsoft.Data.SqlClient;
using Registration.Repository.Interfaces;
using System;
using System.Net;
using System.Net.Mail;

namespace Registration.Repository
{
    public class ResetPassword : IResetPassword
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;
        private readonly IOTP _otp;
        private readonly ICreateUser _createUser;

        public ResetPassword(IConfiguration configuration, IOTP otp, ICreateUser createUser)
        {
            _configuration = configuration;
            _otp = otp;
            _createUser = createUser;
            _connectionString = _configuration.GetConnectionString("SqlConnection");
        }

        public async Task<bool> VerifyResetTokenAsync(string email, string token)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = @"
                    SELECT COUNT(*) FROM PasswordResetTokens prt
                    INNER JOIN Users u ON prt.UserId = u.UserId
                    WHERE u.Email = @Email AND prt.Token = @Token 
                    AND prt.ExpiryDate > GETDATE() AND prt.IsUsed = 0";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Email", email);
                    cmd.Parameters.AddWithValue("@Token", token);

                    int count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                    return count > 0;
                }
            }
        }

        public async Task UpdatePasswordAsync(string email, string newPassword)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = @"
                    UPDATE Users 
                    SET PasswordHash = @Password, UpdatedAt = @UpdatedAt
                    WHERE Email = @Email";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Email", email);
                    cmd.Parameters.AddWithValue("@Password", newPassword); // Hash in production
                    cmd.Parameters.AddWithValue("@UpdatedAt", DateTime.Now);

                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task InvalidateResetTokenAsync(string token)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = "UPDATE PasswordResetTokens SET IsUsed = 1 WHERE Token = @Token";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Token", token);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task SavePasswordResetTokenAsync(int userId, string token)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                // First, create the table if it doesn't exist
                string createTable = @"
                    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PasswordResetTokens')
                    CREATE TABLE PasswordResetTokens (
                        TokenId INT PRIMARY KEY IDENTITY(1,1),
                        UserId INT NOT NULL,
                        Token NVARCHAR(500) NOT NULL,
                        ExpiryDate DATETIME NOT NULL,
                        IsUsed BIT NOT NULL DEFAULT 0,
                        CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
                        FOREIGN KEY (UserId) REFERENCES Users(UserId)
                    )";

                using (SqlCommand cmd = new SqlCommand(createTable, conn))
                {
                    await cmd.ExecuteNonQueryAsync();
                }

                // Insert token
                string query = @"
                    INSERT INTO PasswordResetTokens (UserId, Token, ExpiryDate)
                    VALUES (@UserId, @Token, @ExpiryDate)";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    cmd.Parameters.AddWithValue("@Token", token);
                    cmd.Parameters.AddWithValue("@ExpiryDate", DateTime.Now.AddHours(0.1));

                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }
    }
}
