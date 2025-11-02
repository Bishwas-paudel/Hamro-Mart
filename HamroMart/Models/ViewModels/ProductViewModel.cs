namespace HamroMart.ViewModels
{
    public class ProductViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public decimal? DiscountPrice { get; set; }
        public int StockQuantity { get; set; }
        public string ImageUrl { get; set; }
        public string Brand { get; set; }
        public string Unit { get; set; }
        public string CategoryName { get; set; }
        public int CategoryId { get; set; }
        public bool IsActive { get; set; }
        public bool IsInStock => StockQuantity > 0;
        public decimal FinalPrice => DiscountPrice ?? Price;
        public bool HasDiscount => DiscountPrice.HasValue && DiscountPrice < Price;
        public decimal DiscountPercentage => HasDiscount ? ((Price - DiscountPrice.Value) / Price) * 100 : 0;
    }
}