using System.Linq;
using System.Web.Mvc;
using MU_Marketplace.Models;
using MU_Marketplace.Models.ViewModels;

namespace MU_Marketplace.Controllers
{
    public class HomeController : Controller
    {
        // GET: Home
        public ActionResult Index()
        {
            using (var db = new ApplicationDbContext())
            {
                var approved = db.Products.AsNoTracking().Where(p => p.Status == ProductStatus.Approved);

                var vm = new HomeViewModel
                {
                    LatestProducts = approved
                        .OrderByDescending(p => p.CreatedAt)
                        .Take(6)
                        .Select(p => new ProductCardViewModel
                        {
                            ProductId = p.ProductId,
                            Title = p.Title,
                            Price = p.Price,
                            CategoryName = p.Category.CategoryName,
                            SellerName = p.Seller.FullName,
                            CreatedDate = p.CreatedAt
                        })
                        .ToList(),
                    ActiveListings = approved.Count(),
                    CategoryCount = db.Categories.Count(c => c.IsActive),
                    StudentCount = db.Users.Count(u => u.Role == UserRole.Student)
                };
                return View(vm);
            }
        }
    }
}
