using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MU_Marketplace.Models
{
    public class Category
    {
        public int CategoryId { get; set; }

        [Required, StringLength(100)]
        [Index("IX_Categories_CategoryName", IsUnique = true)]
        [Display(Name = "Category")]
        public string CategoryName { get; set; }

        public virtual ICollection<Product> Products { get; set; }

        public Category()
        {
            Products = new HashSet<Product>();
        }
    }
}
