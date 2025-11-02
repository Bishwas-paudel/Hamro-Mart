using System.ComponentModel.DataAnnotations;

namespace HamroMart.Models
{
    public class AuditLog
    {
        public int Id { get; set; }

        [Required]
        [StringLength(450)]
        public string UserId { get; set; }

        [Required]
        [StringLength(100)]
        public string Action { get; set; } // Created, Updated, Deleted, etc.

        [Required]
        [StringLength(100)]
        public string Entity { get; set; } // Product, Order, User, etc.

        public int EntityId { get; set; }

        [StringLength(1000)]
        public string Description { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.Now;

        [StringLength(45)]
        public string IpAddress { get; set; }

        // Navigation property
        public virtual ApplicationUser User { get; set; }
    }
}