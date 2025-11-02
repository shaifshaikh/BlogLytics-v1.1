using System.ComponentModel.DataAnnotations;

namespace Registration.Models
{

        public class LoginViewModel
        {
            [Required(ErrorMessage = "Email is Required")]
            [EmailAddress(ErrorMessage = "Invalid Email Format")]
            public string Email { get; set; }
            [Required(ErrorMessage = "Password is Required")]
            [DataType(DataType.Password)]
            public string Password { get; set; }
            public bool RememberMe { get; set; }

        }
    public class UserDto
    {
        public int UserId { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public string FullName { get; set; }
        public string Role { get; set; }
        public bool IsActive { get; set; }
    }


    public class ForgotPasswordViewModel
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string Email { get; set; }
    }

    //[Required(ErrorMessage = "Email is required")]
    //[EmailAddress(ErrorMessage = "Invalid email format")]
    //public string Email { get; set; }

    //[Required(ErrorMessage = "Password is required")]
    //[DataType(DataType.Password)]
    //public string Password { get; set; }

    //public bool RememberMe { get; set; }

}
