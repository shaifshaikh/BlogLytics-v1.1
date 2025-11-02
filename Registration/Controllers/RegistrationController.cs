using Microsoft.AspNetCore.Mvc;
using Registration.Models;
using Registration.Repository;

namespace Registration.Controllers
{
    public class RegistrationController : Controller
    {

        private readonly IConfiguration _configuration;
        private readonly string _connectionString;
        private readonly IOTP _otp;
        private readonly ICreateUser _createUser;

        public RegistrationController(IConfiguration configuration, IOTP otp, ICreateUser createUser)
        {
            _configuration = configuration;
            _otp = otp;
            _createUser = createUser;
            _connectionString = _configuration.GetConnectionString("SqlConnection");
        }


        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            try
            {
                if (await _createUser.EmailExistsAsync(model.Email))
                {
                    ModelState.AddModelError("Email", "This email is already registered.");
                    return View(model);
                }
                //if (await _createUser.EmailExistsAsync(model.Email))
                //{
                //    ModelState.AddModelError("Email", "This email is already registered.");

                //    // Check if the request expects JSON (API, Ajax, etc.)
                //    if (Request.Headers["Accept"].ToString().Contains("application/json") ||
                //        Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                //    {
                //        return Conflict(new { message = "This email is already registered." });
                //    }

                //    // Otherwise, return normal MVC View
                //    return View(model);
                //}
                string otp = _otp.GenerateOTP();
                HttpContext.Session.SetString("RegEmail", model.Email);
                HttpContext.Session.SetString("RegFullName", model.FullName);
                HttpContext.Session.SetString("RegPassword", model.Password);
                HttpContext.Session.SetString("RegOTP", otp);
                HttpContext.Session.SetString("OTPExpiry", DateTime.Now.AddMinutes(10).ToString());


                bool emailSent = await _otp.SendOTPEmailAsync(model.Email, model.FullName, otp);// Here, you would typically send the OTP to the user's email address.
                //if (!emailSent)
                //{
                //    return StatusCode(500, new { message = "Failed to send OTP. Please try again." });
                //}
                //return Ok(new
                //{
                //    message = "OTP sent successfully. Please verify to complete registration.",
                //    email = model.Email,
                //    otpExpiry = HttpContext.Session.Get("OTPExpiry")
                //});


                if (emailSent)
                {
                    TempData["SuccessMessage"] = "OTP has been sent to your email. Please verify to complete registration.";
                    return RedirectToAction("VerifyOTP", new { email = model.Email });
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to send OTP. Please try again.";
                    return View(model);
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred during registration.";
                return View(model);

            }

        }

        [HttpGet]

        public IActionResult VerifyOTP(string email)
        {
            if (string.IsNullOrEmpty(email) || HttpContext.Session.GetString("RegEmail") != email)
            {
                return RedirectToAction("Register");
            }

            var model = new VerifyOTPViewModel { Email = email };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyOTP(VerifyOTPViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            try
            {
                string storedOTP = HttpContext.Session.GetString("RegOTP");
                string expiryStr = HttpContext.Session.GetString("OTPExpiry");

                if (string.IsNullOrEmpty(storedOTP) || string.IsNullOrEmpty(expiryStr))
                {
                    TempData["ErrorMessage"] = "OTP session expired. Please register again.";
                    return RedirectToAction("Register");
                }

                DateTime expiry = DateTime.Parse(expiryStr);
                if (DateTime.Now > expiry)
                {
                    TempData["ErrorMessage"] = "OTP has expired. Please request a new one.";
                    return View(model);
                }

                if (model.OTP == storedOTP)
                {
                    // OTP is correct, create user account
                    string email = HttpContext.Session.GetString("RegEmail");
                    string fullName = HttpContext.Session.GetString("RegFullName");
                    string password = HttpContext.Session.GetString("RegPassword");

                    await _createUser.CreateUserAsync(email, fullName, password);

                    // Clear session
                    HttpContext.Session.Remove("RegEmail");
                    HttpContext.Session.Remove("RegFullName");
                    HttpContext.Session.Remove("RegPassword");
                    HttpContext.Session.Remove("RegOTP");
                    HttpContext.Session.Remove("OTPExpiry");

                    TempData["SuccessMessage"] = "Registration successful! Please login with your credentials.";
                    return RedirectToAction("Login", "Account");
                }
                else
                {
                    TempData["ErrorMessage"] = "Invalid OTP. Please try again.";
                    return View(model);
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred during verification.";
                return View(model);
            }

        }

        //private async Task CreateUserAsync(string email, string fullName, string password)
        //{
        //    using (SqlConnection con = new SqlConnection(_connectionString))
        //    {
        //        await con.OpenAsync();
        //        string query = @"
        //            INSERT INTO Users (Email, PasswordHash, FullName, Role, IsActive, EmailConfirmed, CreatedAt)
        //            VALUES (@Email, @Password, @FullName, 'Blogger', 1, 1, @CreatedAt)";

        //        using (SqlCommand cmd = new SqlCommand(query, con))
        //        {
        //            cmd.Parameters.AddWithValue("@Email", email);
        //            cmd.Parameters.AddWithValue("@Password", password); // Hash in production
        //            cmd.Parameters.AddWithValue("@FullName", fullName);
        //            cmd.Parameters.AddWithValue("@CreatedAt", DateTime.Now);

        //            await cmd.ExecuteNonQueryAsync();
        //        }
        //    }
        //}

        //private async Task<bool> SendOTPEmailAsync(string email, string fullName, string otp)
        //{
        //    try
        //    {
        //        var smtpClient = new SmtpClient("smtp.gmail.com")
        //        {
        //            Port = 587,

        //            EnableSsl = true,
        //            DeliveryMethod = SmtpDeliveryMethod.Network,
        //            UseDefaultCredentials = false,
        //            Credentials = new NetworkCredential(
        //               _configuration["Email:Username"],
        //               _configuration["Email:Password"]),
        //        };

        //        var mailMessage = new MailMessage
        //        {
        //            From = new MailAddress(_configuration["Email:FromEmail"], "Bloglytics"),
        //            Subject = "Email Verification - OTP Code",
        //            Body = $@"
        //                <html>
        //                <body style='font-family: Arial, sans-serif;'>
        //                    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
        //                        <h2 style='color: #667eea;'>Welcome to Bloglytics!</h2>
        //                        <p>Hello {fullName},</p>
        //                        <p>Thank you for registering with Bloglytics. Please use the following OTP to verify your email address:</p>
        //                        <div style='background: #f8f9fa; padding: 20px; text-align: center; margin: 20px 0;'>
        //                            <h1 style='color: #667eea; font-size: 36px; letter-spacing: 10px; margin: 0;'>{otp}</h1>
        //                        </div>
        //                        <p>This OTP is valid for 10 minutes.</p>
        //                        <p>If you didn't request this, please ignore this email.</p>
        //                        <br/>
        //                        <p>Best regards,<br/>The Bloglytics Team</p>
        //                    </div>
        //                </body>
        //                </html>
        //            ",
        //            IsBodyHtml = true,
        //        };

        //        mailMessage.To.Add(email);

        //        await smtpClient.SendMailAsync(mailMessage);
        //        return true;
        //    }
        //    catch (Exception ex)
        //    {
        //        ViewData["ErrorMail"] = "Error While Sending Mail! Please try again later";
        //        return false;
        //    }
        //}

        //private string GenerateOTP()
        //{
        //    Random rand = new Random();
        //    var result = rand.Next(100000, 999999).ToString();
        //    return result;

        //}
        //private async Task<bool> EmailExistsAsync(string email)
        //{
        //    using (SqlConnection con = new SqlConnection(_connectionString))
        //    {
        //        await con.OpenAsync();
        //        string query = "select count(*) from users where email=@email";
        //        using (SqlCommand cmd = new SqlCommand(query, con))
        //        {
        //            cmd.Parameters.AddWithValue("@email", email);
        //            int count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        //            return count > 0;
        //        }


        //    }
        //}

    }
}
