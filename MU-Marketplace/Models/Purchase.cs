using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace MU_Marketplace.Models
{
    public class Purchase
    {
        public int PurchaseId { get; set; }

        // UNIQUE: one product can be purchased only once
        [Index("IX_Purchases_ProductId", IsUnique = true)]
        public int ProductId { get; set; }

        [Index("IX_Purchases_Buyer")]
        public int BuyerId { get; set; }

        public decimal AmountPaid { get; set; }

        public DateTime PurchaseDate { get; set; }

        public virtual Product Product { get; set; }
        public virtual User Buyer { get; set; }     

        public Purchase()
        {
            PurchaseDate = DateTime.Now;
        }
    }
}
