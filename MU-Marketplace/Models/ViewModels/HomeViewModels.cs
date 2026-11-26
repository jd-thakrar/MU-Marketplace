using System.Collections.Generic;

namespace MU_Marketplace.Models.ViewModels
{
    public class HomeViewModel
    {
        public List<ProductCardViewModel> LatestProducts { get; set; } = new List<ProductCardViewModel>();
        public int ActiveListings { get; set; }
        public int CategoryCount { get; set; }
        public int StudentCount { get; set; }
    }
}
