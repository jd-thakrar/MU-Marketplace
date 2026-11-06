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

        [StringLength(1000)]
        public string Description { get; set; }

        // IX_Products_Marketplace = Status(1), CategoryId(2), Price(3)
        [Range(1, 10000000, ErrorMessage = "Enter a valid price.")]
        [DataType(DataType.Currency)]
        [Index("IX_Products_Marketplace", 3)]
        public decimal Price { get; set; }

        // IX_Products_Marketplace (1) and IX_Products_Seller (2)
        // ConcurrencyCheck: UPDATE ... WHERE Status = <value we loaded>
        [ConcurrencyCheck]
        [Index("IX_Products_Marketplace", 1)]
        [Index("IX_Products_Seller", 2)]
        public ProductStatus Status { get; set; }

        // IX_Products_Seller = SellerId(1), Status(2)
        [Index("IX_Products_Seller", 1)]
        public int SellerId { get; set; }

        [Index("IX_Products_Marketplace", 2)]
        public int CategoryId { get; set; }

        public DateTime CreatedDate { get; set; }

        public virtual User Seller { get; set; }         
        public virtual Category Category { get; set; }
        public virtual ICollection<Purchase> Purchases { get; set; }

        public Product()
        {
            Status = ProductStatus.Pending;
            CreatedDate = DateTime.Now;
            Purchases = new HashSet<Purchase>();
        }
    }
}
