using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace MU_Marketplace.Models
{
    public class Product
    {
        public int ProductId { get; set; }

        public string Title { get; set; }

        public string Description { get; set; }

        public decimal Price { get; set; }

        // 0 = New, 1 = Like New, 2 = Good, 3 = Fair
        public byte ProductCondition { get; set; }

        public byte[] ImageData { get; set; }

        public string ImageContentType { get; set; }

        public string PickupLocation { get; set; }

        // 0 = Pending, 1 = Approved, 2 = Rejected,
        // 3 = Sold, 4 = Removed
        public byte Status { get; set; }

        public int SellerId { get; set; }

        public int CategoryId { get; set; }

        public string AdminNotes { get; set; }

        public int? ReviewedBy { get; set; }

        public DateTime? ReviewedAt { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        // Display properties (retrieved using SQL JOIN)
        public string SellerName { get; set; }

        public string CategoryName { get; set; }
    }
}