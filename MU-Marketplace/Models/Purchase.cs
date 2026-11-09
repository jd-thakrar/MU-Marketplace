using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace MU_Marketplace.Models
{
    public class Purchase
    {
        public int PurchaseId { get; set; }

        public int ProductId { get; set; }

        public int BuyerId { get; set; }

        public decimal PurchasePrice { get; set; }

        public DateTime PurchaseDate { get; set; }

        // Display properties (retrieved using SQL JOIN)
        public string ProductTitle { get; set; }

        public string BuyerName { get; set; }

        public string BuyerEmail { get; set; }
    }
}