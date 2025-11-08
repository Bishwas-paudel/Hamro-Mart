using System.ComponentModel.DataAnnotations;

namespace HamroMart.ViewModels
{
    public class RegisterViewModel
    {
        [Required]
        [Display(Name = "First Name")]
        [MaxLength(100)]
        public string FirstName { get; set; }

        [Required]
        [Display(Name = "Last Name")]
        [MaxLength(100)]
        public string LastName { get; set; }

        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Required]
        [StringLength(100, MinimumLength = 6)]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{6,}$",
            ErrorMessage = "Password must be at least 6 characters and include uppercase, lowercase, and a digit.")]
        public string Password { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirm Password")]
        [Compare("Password", ErrorMessage = "Passwords do not match")]
        public string ConfirmPassword { get; set; }

        [Phone]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; }

        [Display(Name = "Address")]
        [MaxLength(200)]
        public string Address { get; set; }

        [MaxLength(100)]
        public string City { get; set; }

        [Display(Name = "Postal Code")]
        [MaxLength(10)]
        public string PostalCode { get; set; }
    }
}