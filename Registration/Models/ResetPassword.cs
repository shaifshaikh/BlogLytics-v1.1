using System.ComponentModel.DataAnnotations;

namespace Registration.Models
{

        public class ResetPasswordViewModel
        {
            public string Email { get; set; }
            public string Token { get; set; }

            [Required(ErrorMessage = "New password is required")]
            [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters")]
            [DataType(DataType.Password)]
            public string NewPassword { get; set; }

            [Required(ErrorMessage = "Please confirm your password")]
            [DataType(DataType.Password)]
            [Compare("NewPassword", ErrorMessage = "Passwords do not match")]
            public string ConfirmPassword { get; set; }
        
    }
}
