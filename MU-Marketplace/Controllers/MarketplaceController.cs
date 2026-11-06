using System;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Web.Mvc;
using MU_Marketplace.Models;
using MU_Marketplace.Models.ViewModels;

namespace MU_Marketplace.Controllers
{
    [Authorize]
    [RoutePrefix("marketplace")]
    public class MarketplaceController : Controller
    {
        private readonly ApplicationDbContext db = new ApplicationDbContext();
        private const int PageSize = 12;

        // =====================================================================
        //  BROWSE
        // =====================================================================

        // GET /marketplace
        [Route("")]
        public ActionResult Index(MarketplaceFilter filter)
        {
            var vm = BuildResults(filter);
            vm.Categories = CategoryList(vm.Filter.CategoryId);
            return View(vm);
        }

        // GET /marketplace/search   (AJAX: returns only the product grid)
        [Route("search")]
        public ActionResult Search(MarketplaceFilter filter)
        {
            return PartialView("_ProductGrid", BuildResults(filter));
        }

        // GET /marketplace/details/5
        [Route("details/{id:int}")]
        public ActionResult Details(int id)
        {
            var p = db.Products.AsNoTracking()
                .Include(x => x.Seller)
                .Include(x => x.Category)
                .FirstOrDefault(x => x.ProductId == id);

            if (p == null) return HttpNotFound();

            int? me = CurrentUserId();
            bool isOwner = me.HasValue && me.Value == p.SellerId;

            // Public: Approved or Sold. The seller can open their own item in any state.
            bool isPublic = p.Status == ProductStatus.Approved || p.Status == ProductStatus.Sold;
            if (!isPublic && !isOwner) return HttpNotFound();

            var vm = new ProductDetailsViewModel
            {
                ProductId = p.ProductId,
                Title = p.Title,
                Description = p.Description,
                Price = p.Price,
                Status = p.Status,
                CategoryName = p.Category.CategoryName,
                SellerName = p.Seller.FullName,
                SellerId = p.SellerId,
                CreatedDate = p.CreatedDate,
                IsOwner = isOwner,
                CanBuy = !isOwner && p.Status == ProductStatus.Approved
            };
            return View(vm);
        }

        // =====================================================================
        //  BUY
        // =====================================================================

        // POST /marketplace/buy/5
        [HttpPost, ValidateAntiForgeryToken]
        [Route("buy/{id:int}")]
        public ActionResult Buy(int id)
        {
            int? me = CurrentUserId();
            if (!me.HasValue) return new HttpUnauthorizedResult();

            if (!IsActive(me.Value))
            {
                TempData["Error"] = "Your account is suspended and cannot make purchases.";
                return RedirectToAction("Index");
            }

            var product = db.Products.AsNoTracking().FirstOrDefault(p => p.ProductId == id);
            if (product == null) return HttpNotFound();

            if (product.SellerId == me.Value)
            {
                TempData["Error"] = "You cannot buy your own listing.";
                return RedirectToAction("Details", new { id });
            }
            if (product.Status != ProductStatus.Approved)
            {
                TempData["Error"] = "Sorry, this item is no longer available.";
                return RedirectToAction("Details", new { id });
            }

            try
            {
                using (var tx = db.Database.BeginTransaction())
                {
                    // Atomic claim: only ONE request can change Approved -> Sold.
                    // Everyone else gets 0 rows affected.
                    int claimed = db.Database.ExecuteSqlCommand(
                        "UPDATE Products SET Status = {0} WHERE ProductId = {1} AND Status = {2}",
                        (byte)ProductStatus.Sold, id, (byte)ProductStatus.Approved);

                    if (claimed == 0)
                    {
                        tx.Rollback();
                        TempData["Error"] = "Someone else just bought this item.";
                        return RedirectToAction("Details", new { id });
                    }

                    db.Purchases.Add(new Purchase
                    {
                        ProductId = id,
                        BuyerId = me.Value,
                        AmountPaid = product.Price,
                        PurchaseDate = DateTime.Now
                    });
                    db.SaveChanges();   // UNIQUE(ProductId) is the final safety net
                    tx.Commit();
                }
            }
            catch (DbUpdateException)
            {
                TempData["Error"] = "The purchase could not be completed. Please try again.";
                return RedirectToAction("Details", new { id });
            }

            TempData["Success"] = "Purchase complete! Contact the seller to arrange pickup.";
            return RedirectToAction("MyPurchases");
        }

        // GET /my-purchases
        [Route("~/my-purchases")]
        public ActionResult MyPurchases()
        {
            int? me = CurrentUserId();
            if (!me.HasValue) return new HttpUnauthorizedResult();

            var items = db.Purchases.AsNoTracking()
                .Where(x => x.BuyerId == me.Value)
                .OrderByDescending(x => x.PurchaseDate)
                .Select(x => new PurchaseHistoryViewModel
                {
                    PurchaseId = x.PurchaseId,
                    ProductId = x.ProductId,
                    Title = x.Product.Title,
                    AmountPaid = x.AmountPaid,
                    PurchaseDate = x.PurchaseDate,
                    SellerName = x.Product.Seller.FullName,
                    SellerPhone = x.Product.Seller.PhoneNumber
                })
                .ToList();

            return View(items);
        }

        // =====================================================================
        //  SELL  (my listings)
        // =====================================================================

        // GET /my-listings
        [Route("~/my-listings")]
        public ActionResult MyListings()
        {
            int? me = CurrentUserId();
            if (!me.HasValue) return new HttpUnauthorizedResult();

            var items = db.Products.AsNoTracking()
                .Where(p => p.SellerId == me.Value && p.Status != ProductStatus.Removed)
                .OrderByDescending(p => p.CreatedDate)
                .Select(p => new MyListingViewModel
                {
                    ProductId = p.ProductId,
                    Title = p.Title,
                    Price = p.Price,
                    Status = p.Status,
                    CreatedDate = p.CreatedDate,
                    CategoryName = p.Category.CategoryName,
                    BuyerName = p.Purchases.Select(x => x.Buyer.FullName).FirstOrDefault(),
                    BuyerPhone = p.Purchases.Select(x => x.Buyer.PhoneNumber).FirstOrDefault()
                })
                .ToList();

            return View(items);
        }

        // GET /my-listings/create
        [HttpGet]
        [Route("~/my-listings/create")]
        public ActionResult Create()
        {
            return View(new ProductFormViewModel { Categories = CategoryList(null) });
        }

        // POST /my-listings/create
        [HttpPost, ValidateAntiForgeryToken]
        [Route("~/my-listings/create")]
        public ActionResult Create(ProductFormViewModel form)
        {
            int? me = CurrentUserId();
            if (!me.HasValue) return new HttpUnauthorizedResult();

            if (!IsActive(me.Value))
            {
                TempData["Error"] = "Your account is suspended and cannot create listings.";
                return RedirectToAction("MyListings");
            }

            if (form.CategoryId.HasValue && !db.Categories.Any(c => c.CategoryId == form.CategoryId.Value))
                ModelState.AddModelError("CategoryId", "Selected category does not exist.");

            if (!ModelState.IsValid)
            {
                form.Categories = CategoryList(form.CategoryId);
                return View(form);
            }

            db.Products.Add(new Product
            {
                Title = form.Title.Trim(),
                Description = string.IsNullOrWhiteSpace(form.Description) ? null : form.Description.Trim(),
                Price = form.Price.Value,
                CategoryId = form.CategoryId.Value,
                SellerId = me.Value,                    // never taken from the form
                Status = ProductStatus.Pending,         // always starts in admin review
                CreatedDate = DateTime.Now
            });
            db.SaveChanges();

            TempData["Success"] = "Listing submitted. It will appear in the marketplace once an admin approves it.";
            return RedirectToAction("MyListings");
        }

        // GET /my-listings/edit/5
        [HttpGet]
        [Route("~/my-listings/edit/{id:int}")]
        public ActionResult Edit(int id)
        {
            int? me = CurrentUserId();
            if (!me.HasValue) return new HttpUnauthorizedResult();

            var p = db.Products.AsNoTracking()
                .FirstOrDefault(x => x.ProductId == id && x.SellerId == me.Value);
            if (p == null) return HttpNotFound();

            if (!IsEditable(p.Status))
            {
                TempData["Error"] = "This listing can no longer be edited.";
                return RedirectToAction("MyListings");
            }

            return View(new ProductFormViewModel
            {
                ProductId = p.ProductId,
                Title = p.Title,
                Description = p.Description,
                Price = p.Price,
                CategoryId = p.CategoryId,
                Categories = CategoryList(p.CategoryId)
            });
        }

        // POST /my-listings/edit/5
        [HttpPost, ValidateAntiForgeryToken]
        [Route("~/my-listings/edit/{id:int}")]
        public ActionResult Edit(int id, ProductFormViewModel form)
        {
            int? me = CurrentUserId();
            if (!me.HasValue) return new HttpUnauthorizedResult();

            var product = db.Products.FirstOrDefault(x => x.ProductId == id && x.SellerId == me.Value);
            if (product == null) return HttpNotFound();

            if (!IsEditable(product.Status))
            {
                TempData["Error"] = "This listing can no longer be edited.";
                return RedirectToAction("MyListings");
            }

            if (form.CategoryId.HasValue && !db.Categories.Any(c => c.CategoryId == form.CategoryId.Value))
                ModelState.AddModelError("CategoryId", "Selected category does not exist.");

            if (!ModelState.IsValid)
            {
                form.ProductId = id;
                form.Categories = CategoryList(form.CategoryId);
                return View(form);
            }

            product.Title = form.Title.Trim();
            product.Description = string.IsNullOrWhiteSpace(form.Description) ? null : form.Description.Trim();
            product.Price = form.Price.Value;
            product.CategoryId = form.CategoryId.Value;
            product.Status = ProductStatus.Pending;     // any change goes back to admin review

            try
            {
                db.SaveChanges();   // [ConcurrencyCheck] on Status protects against a sale in between
            }
            catch (DbUpdateConcurrencyException)
            {
                TempData["Error"] = "This listing changed while you were editing (it may have just been sold).";
                return RedirectToAction("MyListings");
            }

            TempData["Success"] = "Listing updated and sent for admin review.";
            return RedirectToAction("MyListings");
        }

        // POST /my-listings/remove/5   (soft delete: Status = Removed)
        [HttpPost, ValidateAntiForgeryToken]
        [Route("~/my-listings/remove/{id:int}")]
        public ActionResult Remove(int id)
        {
            int? me = CurrentUserId();
            if (!me.HasValue) return new HttpUnauthorizedResult();

            var product = db.Products.FirstOrDefault(x => x.ProductId == id && x.SellerId == me.Value);
            if (product == null) return HttpNotFound();

            if (product.Status == ProductStatus.Sold)
            {
                TempData["Error"] = "A sold item cannot be removed.";
                return RedirectToAction("MyListings");
            }

            product.Status = ProductStatus.Removed;
            try
            {
                db.SaveChanges();
            }
            catch (DbUpdateConcurrencyException)
            {
                TempData["Error"] = "This listing changed while you were removing it (it may have just been sold).";
                return RedirectToAction("MyListings");
            }

            TempData["Success"] = "Listing removed.";
            return RedirectToAction("MyListings");
        }

        // =====================================================================
        //  HELPERS
        // =====================================================================

        private MarketplaceIndexViewModel BuildResults(MarketplaceFilter filter)
        {
            filter = filter ?? new MarketplaceFilter();

            var query = db.Products.AsNoTracking()
                .Where(p => p.Status == ProductStatus.Approved);

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                string term = filter.Search.Trim();
                query = query.Where(p => p.Title.Contains(term));
            }
            if (filter.CategoryId.HasValue)
                query = query.Where(p => p.CategoryId == filter.CategoryId.Value);
            if (filter.MinPrice.HasValue)
                query = query.Where(p => p.Price >= filter.MinPrice.Value);
            if (filter.MaxPrice.HasValue)
                query = query.Where(p => p.Price <= filter.MaxPrice.Value);

            switch (filter.Sort)
            {
                case "price_asc":  query = query.OrderBy(p => p.Price);                break;
                case "price_desc": query = query.OrderByDescending(p => p.Price);      break;
                default:           query = query.OrderByDescending(p => p.CreatedDate); break;
            }

            int total = query.Count();
            int totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
            filter.Page = Math.Min(Math.Max(1, filter.Page), totalPages);

            var items = query
                .Skip((filter.Page - 1) * PageSize)
                .Take(PageSize)
                .Select(p => new ProductCardViewModel
                {
                    ProductId = p.ProductId,
                    Title = p.Title,
                    Price = p.Price,
                    CategoryName = p.Category.CategoryName,
                    SellerName = p.Seller.FullName,
                    CreatedDate = p.CreatedDate
                })
                .ToList();

            return new MarketplaceIndexViewModel
            {
                Filter = filter,
                Products = items,
                TotalItems = total,
                PageSize = PageSize
            };
        }

        private SelectList CategoryList(int? selected)
        {
            var cats = db.Categories.AsNoTracking().OrderBy(c => c.CategoryName).ToList();
            return new SelectList(cats, "CategoryId", "CategoryName", selected);
        }

        private static bool IsEditable(ProductStatus s)
        {
            return s == ProductStatus.Pending
                || s == ProductStatus.Rejected
                || s == ProductStatus.Approved;
        }

        private int? _currentUserId;
        private bool _currentUserLoaded;

        // Assumes Forms Authentication where User.Identity.Name is the user's Email.
        // If Developer 1 stores UserId in a claim or session instead, change ONLY this method.
        private int? CurrentUserId()
        {
            if (_currentUserLoaded) return _currentUserId;
            _currentUserLoaded = true;

            string email = User.Identity.Name;
            if (string.IsNullOrEmpty(email)) return null;

            _currentUserId = db.Users.AsNoTracking()
                .Where(u => u.Email == email)
                .Select(u => (int?)u.UserId)
                .FirstOrDefault();
            return _currentUserId;
        }

        // Uses UserStatus (0 = Active, 1 = Suspended) from the ERD
        private bool IsActive(int userId)
        {
            return db.Users.Any(u => u.UserId == userId && u.Status == UserStatus.Active);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
