using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HamroMart.Models
{
    public class Review
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ProductId { get; set; }

        [Required]
        public string UserId { get; set; }

        [Required]
        [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5")]
        public int Rating { get; set; }

        [StringLength(1000, ErrorMessage = "Comment cannot exceed 1000 characters")]
        [DataType(DataType.MultilineText)]
        public string Comment { get; set; }

        [StringLength(200)]
        public string Title { get; set; }

        // Status
        public bool IsApproved { get; set; } = false;
        public bool IsVerifiedPurchase { get; set; } = false;

        // Helpfulness
        public int HelpfulCount { get; set; } = 0;
        public int NotHelpfulCount { get; set; } = 0;

        // Admin
        [StringLength(500)]
        public string AdminResponse { get; set; }
        public DateTime? AdminResponseDate { get; set; }

        // Metadata
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public virtual Product Product { get; set; }
        public virtual ApplicationUser User { get; set; }

        // Computed Properties
        [NotMapped]
        public string UserInitials => User?.FirstName?.Length > 0
            ? new string(User.FirstName.Split(' ').Select(n => n[0]).ToArray())
            : "U";

        [NotMapped]
        public int TotalVotes => HelpfulCount + NotHelpfulCount;

        [NotMapped]
        public double HelpfulnessPercentage => TotalVotes > 0
            ? (double)HelpfulCount / TotalVotes * 100
            : 0;
    }
}