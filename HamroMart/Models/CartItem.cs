namespace HamroMart.Models
{
    public class CartItem
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public DateTime AddedOn { get; set; } = DateTime.Now;
        public virtual ApplicationUser User { get; set; }
        public virtual Product Product { get; set; }
    }
}