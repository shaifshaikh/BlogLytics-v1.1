using Microsoft.Data.SqlClient;
using Registration.Repository;
using System.Net;
using System.Net.Mail;

namespace Registration.Repository
{
    public class OTP : IOTP
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;  
        public OTP(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("SqlConnection");
        }
        public async Task<bool> SendOTPEmailAsync(string email, string fullName, string otp)
        {
            try
            {
                var smtpClient = new SmtpClient("smtp.gmail.com")
                {
                    Port = 587,

                    EnableSsl = true,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(
                       _configuration["Email:Username"],
                       _configuration["Email:Password"]),
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_configuration["Email:FromEmail"], "Bloglytics"),
                    Subject = "Email Verification - OTP Code",
                    Body = $@"
                        <html>
                        <body style='font-family: Arial, sans-serif;'>
                            <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                                <h2 style='color: #667eea;'>Welcome to Bloglytics!</h2>
                                <p>Hello {fullName},</p>
                                <p>Thank you for registering with Bloglytics. Please use the following OTP to verify your email address:</p>
                                <div style='background: #f8f9fa; padding: 20px; text-align: center; margin: 20px 0;'>
                                    <h1 style='color: #667eea; font-size: 36px; letter-spacing: 10px; margin: 0;'>{otp}</h1>
                                </div>
                                <p>This OTP is valid for 10 minutes.</p>
                                <p>If you didn't request this, please ignore this email.</p>
                                <br/>
                                <p>Best regards,<br/>The Bloglytics Team</p>
                            </div>
                        </body>
                        </html>
                    ",
                    IsBodyHtml = true,
                };

                mailMessage.To.Add(email);

                await smtpClient.SendMailAsync(mailMessage);
                return true;
            }
            catch (Exception ex)
            {
                //ViewData["ErrorMail"] = "Error While Sending Mail! Please try again later";
                return false;
            }
        }

        public string GenerateOTP()
        {
            Random rand = new Random();
            var result = rand.Next(100000, 999999).ToString();
            return result;

        }

    }
}
