namespace Registration.Repository
{
    public interface IOTP
    {
        string GenerateOTP();
        Task<bool> SendOTPEmailAsync(string email, string fullName, string otp);
    }
}
