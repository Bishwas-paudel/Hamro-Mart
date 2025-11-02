using System.ComponentModel.DataAnnotations;

namespace HamroMart.ViewModels
{
    public class VerifyOTPViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "OTP must be 6 digits")]
        [Display(Name = "OTP Code")]
        public string OTP { get; set; }
    }
}