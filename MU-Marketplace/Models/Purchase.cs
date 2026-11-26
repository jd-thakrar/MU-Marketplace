using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MU_Marketplace.Models
{
    public enum EscrowStatus : byte
    {
        Held            = 0,
        Released        = 1,
        RefundRequested = 2,
        Refunded        = 3
    }

    public class Purchase
    {
        public int PurchaseId { get; set; }

        public int ProductId { get; set; }

        [Index("IX_Purchases_Buyer_Date", 1)]
        public int BuyerId { get; set; }

        public decimal PurchasePrice { get; set; }

        [Index("IX_Purchases_Buyer_Date", 2)]
        public DateTime PurchaseDate { get; set; }

        public EscrowStatus EscrowStatus { get; set; }

        [StringLength(200)]
        public string StripeSessionId { get; set; }

        [StringLength(200)]
        public string StripePaymentIntentId { get; set; }

        [StringLength(500)]
        public string RefundReason { get; set; }

        public DateTime? ReleasedAt { get; set; }

        public DateTime? RefundedAt { get; set; }

        public virtual Product Product { get; set; }
        public virtual User Buyer { get; set; }

        public Purchase()
        {
            PurchaseDate = DateTime.UtcNow;
            EscrowStatus = EscrowStatus.Held;
        }
    }
}
