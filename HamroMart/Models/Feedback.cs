using System.ComponentModel.DataAnnotations;

namespace HamroMart.Models
{
    public class Feedback
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public int? ProductId { get; set; }
        public int? OrderId { get; set; }

        [Required]
        [Range(1, 5)]
        public int Rating { get; set; }

        [StringLength(1000)]
        public string Comment { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public bool IsApproved { get; set; } = false;

        // Navigation properties
        public virtual ApplicationUser User { get; set; }
        public virtual Product Product { get; set; }
        public virtual Order Order { get; set; }
    }
}