using System.ComponentModel.DataAnnotations;

namespace HamroMart.ViewModels
{
    public class CheckoutViewModel
    {
        [Required]
        [StringLength(200)]
        public string ShippingAddress { get; set; }

        [Required]
        [StringLength(100)]
        public string City { get; set; }

        [StringLength(10)]
        public string PostalCode { get; set; }

        [Required]
        [Phone]
        public string PhoneNumber { get; set; }

        [Required]
        public string PaymentMethod { get; set; } = "CashOnDelivery";
    }
}