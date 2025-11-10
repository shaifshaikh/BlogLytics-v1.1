using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;
using Registration.Models;
using Registration.Repository;
using Registration.Repository.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Mail;
using System.Security.Claims;
using System.Text;


namespace Registration.Controllers
{
    public class AccountController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;
        private readonly IOTP _otp;
        private readonly ICreateUser _createUser;
        private readonly IResetPassword _resetPassword;
        public AccountController(IConfiguration configuration,IOTP otp,ICreateUser createUser,IResetPassword resetPassword)
        {
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("SqlConnection");
            _otp = otp;
            _createUser = createUser;
            _resetPassword = resetPassword;
        }


        [HttpGet]
        public IActionResult Login()
        {
            if (HttpContext.Session.GetString("UserId") != null)
            {
                return RedirectToAction("Dashboard", "Dashboard");
            }
            return View("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            try
            {
                var user = await ValidateUserAsync(model.Email, model.Password);

                if (user != null)
                {
                    //await UpdateLastLoginAsync(user.UserId);

                    var token = GenerateJwtToken(user);

                    Response.Cookies.Append("AuthToken", token, new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = true,
                        SameSite = SameSiteMode.Strict,
                        Expires = model.RememberMe
                            ? DateTimeOffset.UtcNow.AddDays(30)
                            : DateTimeOffset.UtcNow.AddHours(8)
                    });

                    HttpContext.Session.SetString("UserId", user.UserId.ToString());
                    HttpContext.Session.SetString("UserName", user.FullName);
                    HttpContext.Session.SetString("UserEmail", user.Email);
                    HttpContext.Session.SetString("UserRole", user.Role);

                    TempData["SuccessMessage"] = $"Welcome back, {user.FullName}!";

                    if (user.Role == "Admin")
                    {
                        return RedirectToAction("Index", "Admin");
                    }
                    else
                    {
                        return RedirectToAction("Dashboard", "Dashboard");
                    }
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Invalid email or password.");
                    TempData["ErrorMessage"] = "Invalid email or password. Please try again.";
                    return View("Index", model);
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, "An error occurred during login.");
                TempData["ErrorMessage"] = "An error occurred. Please try again later.";
                return View("Index", model);
            }
        }





        private async Task<UserDto> ValidateUserAsync(string email, string password)
        {
            //For Safer Side.
            UserDto user = null;

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = @"
                    SELECT UserId, Email, PasswordHash, FullName, Role, IsActive, EmailConfirmed 
                    FROM Users 
                    WHERE Email = @Email AND IsActive = 1 AND EmailConfirmed = 1";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Email", email);
                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            string storedPassword = reader["PasswordHash"].ToString();
                            if (storedPassword == password)
                            {
                                user = new UserDto
                                {
                                    UserId = Convert.ToInt32(reader["UserId"]),
                                    Email = reader["Email"].ToString(),
                                    FullName = reader["FullName"].ToString(),
                                    Role = reader["Role"].ToString(),
                                    IsActive = Convert.ToBoolean(reader["IsActive"])
                                };
                            }
                        }
                    }
                }
            }

            return user;
        }

        private string GenerateJwtToken(UserDto user)
        {
            var securityKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(8),
                signingCredentials: credentials
            );
            //mysign:ghpmEn1xaHm5p8KkKCJcOLecUtvvcBm3kKOBmjj29s0
            var x = new JwtSecurityTokenHandler().WriteToken(token);
            return new JwtSecurityTokenHandler().WriteToken(token);
        }


        [HttpGet]
        public async Task<IActionResult> ForgotPassword()
        {
            return View();
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var user = await _createUser.GetUserByEmailAsync(model.Email);

                if (user != null)
                {
                    // Generate reset token
                    string token = Guid.NewGuid().ToString();

                    // Store token in database
                    await _resetPassword.SavePasswordResetTokenAsync(user.UserId, token);

                    // Send reset email
                    bool emailSent = await SendPasswordResetEmailAsync(model.Email, user.FullName, token);

                    if (emailSent)
                    {
                        TempData["SuccessMessage"] = "Password reset link has been sent to your email.";
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "Failed to send email. Please try again.";
                    }
                }
                else
                {
                    // Don't reveal if email exists for security
                    TempData["SuccessMessage"] = "If the email exists, a password reset link has been sent.";
                }

                return View();
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred. Please try again.";
                return View(model);
            }
        }






        [HttpGet]
        public async Task<IActionResult> ResetPassword(string token,string email)
        {
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(email))
            {
                TempData["ErrorMessage"] = "Invalid reset link.";
                return RedirectToAction("Login");
            }

            // Verify token
            bool isValid = await _resetPassword.VerifyResetTokenAsync(email, token);
            if (!isValid)
            {
                TempData["ErrorMessage"] = "Reset link is invalid or has expired.";
                return RedirectToAction("Login");
            }

            var model = new ResetPasswordViewModel
            {
                Email = email,
                Token = token
            };

            return View();
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                bool isValid = await _resetPassword.VerifyResetTokenAsync(model.Email, model.Token);
                if (!isValid)
                {
                    TempData["ErrorMessage"] = "Reset link is invalid or has expired.";
                    return RedirectToAction("Login");
                }

                // Update password
                await _resetPassword.UpdatePasswordAsync(model.Email, model.NewPassword);

                // Invalidate token
                await _resetPassword.InvalidateResetTokenAsync(model.Token);

                TempData["SuccessMessage"] = "Password reset successful! Please login with your new password.";
                return RedirectToAction("Login");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred. Please try again.";
                return View(model);
            }
        }
        public async Task<bool> SendPasswordResetEmailAsync(string toEmail, string userName, string token)
        {
            try
            {
                string resetLink = Url.Action("ResetPassword", "Account",
                    new { token = token, email = toEmail }, Request.Scheme);

                var smtpClient = new SmtpClient("smtp.gmail.com")
                {
                    Port = 587,
                    Credentials = new NetworkCredential(
                        _configuration["Email:Username"],
                        _configuration["Email:Password"]),
                    EnableSsl = true,
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_configuration["Email:FromEmail"], "Bloglytics"),
                    Subject = "Password Reset Request",
                    Body = $@"
                        <html>
                        <body style='font-family: Arial, sans-serif;'>
                            <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                                <h2 style='color: #667eea;'>Password Reset Request</h2>
                                <p>Hello {userName},</p>
                                <p>We received a request to reset your password. Click the button below to reset it:</p>
                                <div style='text-align: center; margin: 30px 0;'>
                                    <a href='{resetLink}' style='background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 15px 40px; text-decoration: none; border-radius: 8px; display: inline-block; font-weight: 600;'>
                                        Reset Password
                                    </a>
                                </div>
                                <p>This link will expire in 1 hour.</p>
                                <p>If you didn't request a password reset, please ignore this email.</p>
                                <p style='color: #999; font-size: 12px; margin-top: 30px;'>
                                    If the button doesn't work, copy and paste this link into your browser:<br/>
                                    {resetLink}
                                </p>
                                <br/>
                                <p>Best regards,<br/>The Bloglytics Team</p>
                            </div>
                        </body>
                        </html>
                    ",
                    IsBodyHtml = true,
                };

                mailMessage.To.Add(toEmail);

                await smtpClient.SendMailAsync(mailMessage);
                return true;
            }
            catch (Exception ex)
            {
                // Log error
                return false;
            }
        }
    }
}
