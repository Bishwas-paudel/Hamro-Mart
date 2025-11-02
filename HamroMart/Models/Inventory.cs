using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HamroMart.Models
{
    public enum InventoryAction
    {
        [Display(Name = "Stock Addition")]
        StockAddition,

        [Display(Name = "Stock Reduction")]
        StockReduction,

        [Display(Name = "Stock Adjustment")]
        StockAdjustment,

        [Display(Name = "Sale")]
        Sale,

        [Display(Name = "Return")]
        Return,

        [Display(Name = "Damage")]
        Damage,

        [Display(Name = "Expiry")]
        Expiry
    }

    public class Inventory
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ProductId { get; set; }

        [Required]
        [Display(Name = "Action Type")]
        public InventoryAction Action { get; set; }

        [Required]
        [Display(Name = "Quantity Change")]
        public int QuantityChange { get; set; } // Positive for addition, negative for deduction

        [Required]
        [Display(Name = "Previous Quantity")]
        public int PreviousQuantity { get; set; }

        [Required]
        [Display(Name = "New Quantity")]
        public int NewQuantity { get; set; }

        [Required]
        [StringLength(500)]
        public string Reason { get; set; }

        [StringLength(1000)]
        [Display(Name = "Additional Notes")]
        public string Notes { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "Performed By")]
        public string PerformedBy { get; set; }

        // Reference information
        [Display(Name = "Related Order")]
        public int? OrderId { get; set; }

        [Display(Name = "Related Purchase Order")]
        public int? PurchaseOrderId { get; set; }

        // Cost information
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Unit Cost")]
        public decimal? UnitCost { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Total Cost")]
        public decimal? TotalCost { get; set; }

        // Metadata
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual Product Product { get; set; }
        public virtual Order Order { get; set; }

        // Computed Properties
        [NotMapped]
        [Display(Name = "Action Type Display")]
        public string ActionDisplay => Action.ToString();

        [NotMapped]
        public bool IsAddition => QuantityChange > 0;

        [NotMapped]
        public bool IsReduction => QuantityChange < 0;

        [NotMapped]
        [Display(Name = "Absolute Change")]
        public int AbsoluteChange => Math.Abs(QuantityChange);
    }
}