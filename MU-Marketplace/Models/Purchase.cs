using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace MU_Marketplace.Models
{
    public class Purchase
    {
        public int PurchaseId { get; set; }

        [Index("UQ_Purchases_ProductId", IsUnique = true)]
        public int ProductId { get; set; }

        [Index("IX_Purchases_Buyer_Date", 1)]
        public int BuyerId { get; set; }

        public decimal PurchasePrice { get; set; }

        [Index("IX_Purchases_Buyer_Date", 2)]
        public DateTime PurchaseDate { get; set; }

        public virtual Product Product { get; set; }
        public virtual User Buyer { get; set; }

        public Purchase()
        {
            PurchaseDate = DateTime.UtcNow;
        }
    }
}
