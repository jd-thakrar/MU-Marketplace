using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;

namespace MU_Marketplace.Models.ViewModels
{
    // ---------- Browse / search ----------

    public class MarketplaceFilter
    {
        public string Search { get; set; }
        public int? CategoryId { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public string Sort { get; set; }          // newest | price_asc | price_desc
        public int Page { get; set; } = 1;
    }

    public class ProductCardViewModel
    {
        public int ProductId { get; set; }
        public string Title { get; set; }
        public decimal Price { get; set; }
        public string CategoryName { get; set; }
        public string SellerName { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class MarketplaceIndexViewModel
    {
        public MarketplaceFilter Filter { get; set; }
        public List<ProductCardViewModel> Products { get; set; }
        public SelectList Categories { get; set; }

        public int TotalItems { get; set; }
        public int PageSize { get; set; }
        public int TotalPages
        {
            get { return Math.Max(1, (int)Math.Ceiling(TotalItems / (double)PageSize)); }
        }
    }

    // ---------- Details ----------

    public class ProductDetailsViewModel
    {
        public int ProductId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public byte ProductCondition { get; set; }
        public string PickupLocation { get; set; }
        public ProductStatus Status { get; set; }
        public string CategoryName { get; set; }
        public string SellerName { get; set; }
        public int SellerId { get; set; }
        public DateTime CreatedDate { get; set; }

        public bool IsOwner { get; set; }
        public bool CanBuy { get; set; }

        public string ConditionLabel
        {
            get
            {
                switch (ProductCondition)
                {
                    case 0: return "New";
                    case 1: return "Like New";
                    case 2: return "Good";
                    default: return "Fair";
                }
            }
        }
    }

    // ---------- Selling ----------

    public class ProductFormViewModel
    {
        public int ProductId { get; set; }

        [Required, StringLength(150)]
        public string Title { get; set; }

        [StringLength(2000)]
        [DataType(DataType.MultilineText)]
        public string Description { get; set; }

        [Required]
        [Range(1, 10000000, ErrorMessage = "Enter a valid price.")]
        [DataType(DataType.Currency)]
        public decimal? Price { get; set; }

        [Required(ErrorMessage = "Please choose a category.")]
        [Display(Name = "Category")]
        public int? CategoryId { get; set; }

        [Display(Name = "Condition")]
        [Range(0, 3, ErrorMessage = "Select a valid condition.")]
        public byte ProductCondition { get; set; } = 2;

        [Display(Name = "Pickup location")]
        [StringLength(150)]
        public string PickupLocation { get; set; } = "MU Campus";

        public SelectList Categories { get; set; }
    }

    public class MyListingViewModel
    {
        public int ProductId { get; set; }
        public string Title { get; set; }
        public decimal Price { get; set; }
        public ProductStatus Status { get; set; }
        public DateTime CreatedDate { get; set; }
        public string CategoryName { get; set; }

        // Filled only when the item is sold (pickup coordination)
        public string BuyerName { get; set; }
        public string BuyerPhone { get; set; }
    }

    // ---------- Buying ----------

    public class PurchaseHistoryViewModel
    {
        public int PurchaseId { get; set; }
        public int ProductId { get; set; }
        public string Title { get; set; }
        public decimal AmountPaid { get; set; }
        public DateTime PurchaseDate { get; set; }
        public string SellerName { get; set; }
        public string SellerPhone { get; set; }
    }
}
