using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MU_Marketplace.Models
{
    public class Category
    {
        public int CategoryId { get; set; }

        [Required, StringLength(100)]
        [Index("UQ_Categories_CategoryName", IsUnique = true)]
        [Display(Name = "Category")]
        public string CategoryName { get; set; }

        [StringLength(500)]
        public string Description { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public virtual ICollection<Product> Products { get; set; }

        public Category()
        {
            IsActive = true;
            CreatedAt = DateTime.UtcNow;
            Products = new HashSet<Product>();
        }
    }
}
