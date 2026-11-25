using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MU_Marketplace.Models
{
    public class Product
    {
        public int ProductId { get; set; }

        [Required, StringLength(150)]
        public string Title { get; set; }

        [Required, StringLength(2000)]
        public string Description { get; set; }

        [Range(1, 10000000, ErrorMessage = "Enter a valid price.")]
        [DataType(DataType.Currency)]
        [Index("IX_Products_Marketplace", 3)]
        public decimal Price { get; set; }

        // 0 = New, 1 = Like New, 2 = Good, 3 = Fair
        public byte ProductCondition { get; set; }

        public byte[] ImageData { get; set; }

        [StringLength(100)]
        public string ImageContentType { get; set; }

        [Required, StringLength(150)]
        public string PickupLocation { get; set; }

        // ConcurrencyCheck: UPDATE ... WHERE Status = <value we loaded>
        [ConcurrencyCheck]
        [Index("IX_Products_Marketplace", 1)]
        [Index("IX_Products_Seller_Status", 2)]
        public ProductStatus Status { get; set; }

        [Index("IX_Products_Seller_Status", 1)]
        public int SellerId { get; set; }

        [Index("IX_Products_Marketplace", 2)]
        public int CategoryId { get; set; }

        [StringLength(500)]
        public string AdminNotes { get; set; }

        public int? ReviewedBy { get; set; }

        public DateTime? ReviewedAt { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public virtual User Seller { get; set; }
        public virtual Category Category { get; set; }
        public virtual User Reviewer { get; set; }
        public virtual ICollection<Purchase> Purchases { get; set; }
        public virtual ICollection<Report> Reports { get; set; }

        public Product()
        {
            Status = ProductStatus.Pending;
            ProductCondition = 2; // Good
            PickupLocation = "MU Campus";
            Description = string.Empty;
            CreatedAt = DateTime.UtcNow;
            Purchases = new HashSet<Purchase>();
            Reports = new HashSet<Report>();
        }
    }
}
