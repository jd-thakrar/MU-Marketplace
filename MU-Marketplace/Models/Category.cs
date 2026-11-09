using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace MU_Marketplace.Models
{
    public class Category
    {
        public int CategoryId { get; set; }

        public string CategoryName { get; set; }

        public string Description { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        // Display property
        public int ProductCount { get; set; }
    }
}