using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace HamroMart.ViewModels
{
    public class ProductCreateViewModel
    {
        [Required]
        [StringLength(200)]
        public string Name { get; set; }

        [StringLength(1000)]
        public string Description { get; set; }

        [Required]
        [Range(0.01, 10000.00)]
        public decimal Price { get; set; }

        [Range(0.01, 10000.00)]
        public decimal? DiscountPrice { get; set; }

        [Required]
        [Range(0, 10000)]
        public int StockQuantity { get; set; }

        [Display(Name = "Product Image")]
        public IFormFile ImageFile { get; set; }

        [StringLength(50)]
        public string Brand { get; set; }

        [StringLength(50)]
        public string Unit { get; set; }

        [Required]
        [Display(Name = "Category")]
        public int CategoryId { get; set; }

        public bool IsActive { get; set; } = true;
    }
}