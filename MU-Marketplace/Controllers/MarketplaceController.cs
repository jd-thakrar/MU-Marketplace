using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using MU_Marketplace.Models;
using MU_Marketplace.Models.ViewModels;

namespace MU_Marketplace.Controllers
{
    [RoutePrefix("marketplace")]
    public class MarketplaceController : Controller
    {
        private readonly ApplicationDbContext db = new ApplicationDbContext();
        private const int PageSize = 12;

        // =====================================================================
        //  BROWSE (PUBLIC - NO LOGIN REQUIRED)
        // =====================================================================

        // GET /marketplace
        [AllowAnonymous]
        [Route("")]
        public ActionResult Index(MarketplaceFilter filter)
        {
            var vm = BuildResults(filter);
            vm.Categories = CategoryList(vm.Filter.CategoryId);
            vm.CartItemCount = GetCartItemCount();
            return View(vm);
        }

        // GET /marketplace/search   (AJAX: returns only the product grid)
        [AllowAnonymous]
        [Route("search")]
        public ActionResult Search(MarketplaceFilter filter)
        {
            return PartialView("_ProductGrid", BuildResults(filter));
        }

        // GET /marketplace/details/5
        [AllowAnonymous]
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

            bool isInCart = false;
            if (me.HasValue)
            {
                isInCart = db.CartItems.AsNoTracking()
                    .Any(ci => ci.Cart.UserId == me.Value && ci.ProductId == id);
            }

            var vm = new ProductDetailsViewModel
            {
                ProductId = p.ProductId,
                Title = p.Title,
                Description = p.Description,
                Price = p.Price,
                ProductCondition = p.ProductCondition,
                PickupLocation = p.PickupLocation,
                Status = p.Status,
                CategoryName = p.Category.CategoryName,
                SellerName = p.Seller.FullName,
                SellerId = p.SellerId,
                CreatedDate = p.CreatedAt,
                IsOwner = isOwner,
                IsLoggedIn = me.HasValue,
                IsInCart = isInCart,
                HasImage = p.ImageData != null,
                CanBuy = !isOwner && p.Status == ProductStatus.Approved
            };
            return View(vm);
        }

        // GET /marketplace/image/5
        [AllowAnonymous]
        [Route("image/{id:int}")]
        public ActionResult Image(int id)
        {
            var img = db.Products.AsNoTracking()
                .Where(p => p.ProductId == id)
                .Select(p => new { p.ImageData, p.ImageContentType })
                .FirstOrDefault();

            if (img == null || img.ImageData == null || string.IsNullOrWhiteSpace(img.ImageContentType))
            {
                return HttpNotFound();
            }

            return File(img.ImageData, img.ImageContentType);
        }

        // =====================================================================
        //  CART (LOGIN REQUIRED)
        // =====================================================================

        // GET /marketplace/cart
        [Authorize]
        [Route("cart")]
        public ActionResult Cart()
        {
            int? me = CurrentUserId();
            if (!me.HasValue) return new HttpUnauthorizedResult();

            var items = db.CartItems.AsNoTracking()
                .Where(ci => ci.Cart.UserId == me.Value)
                .OrderByDescending(ci => ci.AddedAt)
                .Select(ci => new CartItemViewModel
                {
                    CartItemId = ci.CartItemId,
                    ProductId = ci.ProductId,
                    Title = ci.Product.Title,
                    Price = ci.Product.Price,
                    CategoryName = ci.Product.Category.CategoryName,
                    SellerName = ci.Product.Seller.FullName,
                    Status = ci.Product.Status,
                    AddedAt = ci.AddedAt
                })
                .ToList();

            var availableItems = items.Where(i => i.Status == ProductStatus.Approved).ToList();

            var vm = new CartPageViewModel
            {
                Items = items,
                Total = availableItems.Sum(i => i.Price),
                AvailableCount = availableItems.Count
            };

            return View(vm);
        }

        // POST /marketplace/cart/add/5
        [Authorize]
        [HttpPost, ValidateAntiForgeryToken]
        [Route("cart/add/{id:int}")]
        public ActionResult AddToCart(int id)
        {
            int? me = CurrentUserId();
            if (!me.HasValue) return new HttpUnauthorizedResult();

            if (!IsActive(me.Value))
            {
                TempData["Error"] = "Your account is suspended and cannot add items to cart.";
                return RedirectToAction("Details", new { id });
            }

            var product = db.Products.AsNoTracking().FirstOrDefault(p => p.ProductId == id);
            if (product == null) return HttpNotFound();

            if (product.SellerId == me.Value)
            {
                TempData["Error"] = "You cannot add your own listing to your cart.";
                return RedirectToAction("Details", new { id });
            }

            if (product.Status != ProductStatus.Approved)
            {
                TempData["Error"] = "This item is no longer available.";
                return RedirectToAction("Details", new { id });
            }

            var cart = db.Carts.FirstOrDefault(c => c.UserId == me.Value);
            if (cart == null)
            {
                cart = new Cart
                {
                    UserId = me.Value,
                    CreatedAt = DateTime.UtcNow
                };
                db.Carts.Add(cart);
                db.SaveChanges();
            }

            bool exists = db.CartItems.Any(ci => ci.CartId == cart.CartId && ci.ProductId == id);
            if (!exists)
            {
                db.CartItems.Add(new CartItem
                {
                    CartId = cart.CartId,
                    ProductId = id,
                    AddedAt = DateTime.UtcNow
                });
                cart.UpdatedAt = DateTime.UtcNow;
                db.SaveChanges();
                TempData["Success"] = "Item added to your cart.";
            }
            else
            {
                TempData["Success"] = "This item is already in your cart.";
            }

            return RedirectToAction("Cart");
        }

        // POST /marketplace/cart/remove/5
        [Authorize]
        [HttpPost, ValidateAntiForgeryToken]
        [Route("cart/remove/{cartItemId:int}")]
        public ActionResult RemoveFromCart(int cartItemId)
        {
            int? me = CurrentUserId();
            if (!me.HasValue) return new HttpUnauthorizedResult();

            var item = db.CartItems.FirstOrDefault(ci => ci.CartItemId == cartItemId && ci.Cart.UserId == me.Value);
            if (item != null)
            {
                db.CartItems.Remove(item);
                db.SaveChanges();
                TempData["Success"] = "Item removed from cart.";
            }

            return RedirectToAction("Cart");
        }

        // POST /marketplace/cart/checkout
        [Authorize]
        [HttpPost, ValidateAntiForgeryToken]
        [Route("cart/checkout")]
        public ActionResult CheckoutCart()
        {
            int? me = CurrentUserId();
            if (!me.HasValue) return new HttpUnauthorizedResult();

            if (!IsActive(me.Value))
            {
                TempData["Error"] = "Your account is suspended and cannot complete purchases.";
                return RedirectToAction("Cart");
            }

            var cart = db.Carts.Include(c => c.CartItems).FirstOrDefault(c => c.UserId == me.Value);
            if (cart == null || !cart.CartItems.Any())
            {
                TempData["Error"] = "Your cart is empty.";
                return RedirectToAction("Cart");
            }

            var productIds = cart.CartItems.Select(ci => ci.ProductId).ToList();
            var products = db.Products.Where(p => productIds.Contains(p.ProductId)).ToList();

            int purchasedCount = 0;
            var skippedTitles = new List<string>();

            using (var tx = db.Database.BeginTransaction())
            {
                try
                {
                    foreach (var ci in cart.CartItems.ToList())
                    {
                        var prod = products.FirstOrDefault(p => p.ProductId == ci.ProductId);
                        if (prod == null || prod.SellerId == me.Value || prod.Status != ProductStatus.Approved)
                        {
                            if (prod != null) skippedTitles.Add(prod.Title);
                            db.CartItems.Remove(ci);
                            continue;
                        }

                        int claimed = db.Database.ExecuteSqlCommand(
                            "UPDATE Products SET Status = {0}, UpdatedAt = SYSUTCDATETIME() WHERE ProductId = {1} AND Status = {2}",
                            (byte)ProductStatus.Sold, prod.ProductId, (byte)ProductStatus.Approved);

                        if (claimed == 0)
                        {
                            skippedTitles.Add(prod.Title);
                            db.CartItems.Remove(ci);
                            continue;
                        }

                        db.Purchases.Add(new Purchase
                        {
                            ProductId = prod.ProductId,
                            BuyerId = me.Value,
                            PurchasePrice = prod.Price,
                            PurchaseDate = DateTime.UtcNow
                        });

                        // Remove from all carts once sold
                        db.Database.ExecuteSqlCommand("DELETE FROM CartItems WHERE ProductId = {0}", prod.ProductId);
                        purchasedCount++;
                    }

                    cart.UpdatedAt = DateTime.UtcNow;
                    db.SaveChanges();
                    tx.Commit();
                }
                catch (DbUpdateException)
                {
                    tx.Rollback();
                    TempData["Error"] = "Checkout could not be completed. Please try again.";
                    return RedirectToAction("Cart");
                }
            }

            if (purchasedCount > 0)
            {
                TempData["Success"] = purchasedCount + " item(s) purchased! Contact the seller(s) below to arrange campus pickup.";
                if (skippedTitles.Any())
                {
                    TempData["Error"] = "Some items were no longer available and were removed: " + string.Join(", ", skippedTitles);
                }
                return RedirectToAction("MyPurchases");
            }

            TempData["Error"] = "None of the items in your cart were available anymore.";
            return RedirectToAction("Cart");
        }

        // =====================================================================
        //  BUY (DIRECT PURCHASE - LOGIN REQUIRED)
        // =====================================================================

        // POST /marketplace/buy/5
        [Authorize]
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
                    int claimed = db.Database.ExecuteSqlCommand(
                        "UPDATE Products SET Status = {0}, UpdatedAt = SYSUTCDATETIME() WHERE ProductId = {1} AND Status = {2}",
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
                        PurchasePrice = product.Price,
                        PurchaseDate = DateTime.UtcNow
                    });
                    db.Database.ExecuteSqlCommand("DELETE FROM CartItems WHERE ProductId = {0}", id);
                    db.SaveChanges();
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

        // POST /marketplace/report/5
        [Authorize]
        [HttpPost, ValidateAntiForgeryToken]
        [Route("report/{id:int}")]
        public ActionResult Report(int id, string reason)
        {
            int? me = CurrentUserId();
            if (!me.HasValue) return new HttpUnauthorizedResult();

            if (!IsActive(me.Value))
            {
                TempData["Error"] = "Your account is suspended and cannot submit reports.";
                return RedirectToAction("Details", new { id });
            }

            var product = db.Products.AsNoTracking().FirstOrDefault(p => p.ProductId == id);
            if (product == null) return HttpNotFound();

            if (product.SellerId == me.Value)
            {
                TempData["Error"] = "You cannot report your own listing.";
                return RedirectToAction("Details", new { id });
            }

            string trimmedReason = (reason ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(trimmedReason))
            {
                TempData["Error"] = "Please provide a reason for reporting this listing.";
                return RedirectToAction("Details", new { id });
            }
            if (trimmedReason.Length > 500)
            {
                trimmedReason = trimmedReason.Substring(0, 500);
            }

            db.Reports.Add(new Report
            {
                ProductId = id,
                ReporterId = me.Value,
                Reason = trimmedReason,
                Status = 0,
                CreatedAt = DateTime.UtcNow
            });
            db.SaveChanges();

            TempData["Success"] = "Report submitted. An administrator will review this listing.";
            return RedirectToAction("Details", new { id });
        }

        // GET /my-purchases
        [Authorize]
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
                    AmountPaid = x.PurchasePrice,
                    PurchaseDate = x.PurchaseDate,
                    PickupLocation = x.Product.PickupLocation,
                    SellerName = x.Product.Seller.FullName,
                    SellerPhone = x.Product.Seller.PhoneNumber,
                    SellerEmail = x.Product.Seller.Email
                })
                .ToList();

            return View(items);
        }

        // =====================================================================
        //  SELL  (my listings)
        // =====================================================================

        // GET /my-listings
        [Authorize]
        [Route("~/my-listings")]
        public ActionResult MyListings()
        {
            int? me = CurrentUserId();
            if (!me.HasValue) return new HttpUnauthorizedResult();

            var items = db.Products.AsNoTracking()
                .Where(p => p.SellerId == me.Value && p.Status != ProductStatus.Removed)
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new MyListingViewModel
                {
                    ProductId = p.ProductId,
                    Title = p.Title,
                    Price = p.Price,
                    Status = p.Status,
                    CreatedDate = p.CreatedAt,
                    CategoryName = p.Category.CategoryName,
                    PickupLocation = p.PickupLocation,
                    BuyerName = p.Purchases.Select(x => x.Buyer.FullName).FirstOrDefault(),
                    BuyerPhone = p.Purchases.Select(x => x.Buyer.PhoneNumber).FirstOrDefault(),
                    BuyerEmail = p.Purchases.Select(x => x.Buyer.Email).FirstOrDefault()
                })
                .ToList();

            var soldItems = items.Where(x => x.Status == ProductStatus.Sold).ToList();

            var vm = new MyListingsPageViewModel
            {
                Listings = items,
                Summary = new MyListingsSummary
                {
                    TotalListed = items.Count,
                    TotalSold = soldItems.Count,
                    PendingApproval = items.Count(x => x.Status == ProductStatus.Pending),
                    TotalRevenue = soldItems.Sum(x => x.Price)
                }
            };

            return View(vm);
        }

        // GET /my-listings/create
        [Authorize]
        [HttpGet]
        [Route("~/my-listings/create")]
        public ActionResult Create()
        {
            return View(new ProductFormViewModel { Categories = CategoryList(null) });
        }

        // POST /my-listings/create
        [Authorize]
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

            if (form.CategoryId.HasValue && !db.Categories.Any(c => c.CategoryId == form.CategoryId.Value && c.IsActive))
                ModelState.AddModelError("CategoryId", "Selected category does not exist.");

            byte[] imageData;
            string imageContentType;
            string imageError;
            if (!TryReadImage(form.ImageFile, out imageData, out imageContentType, out imageError))
                ModelState.AddModelError("ImageFile", imageError);

            if (!ModelState.IsValid)
            {
                form.Categories = CategoryList(form.CategoryId);
                return View(form);
            }

            db.Products.Add(new Product
            {
                Title = form.Title.Trim(),
                Description = string.IsNullOrWhiteSpace(form.Description) ? "No description provided." : form.Description.Trim(),
                Price = form.Price.Value,
                ProductCondition = form.ProductCondition,
                PickupLocation = string.IsNullOrWhiteSpace(form.PickupLocation) ? "MU Campus" : form.PickupLocation.Trim(),
                ImageData = imageData,
                ImageContentType = imageContentType,
                CategoryId = form.CategoryId.Value,
                SellerId = me.Value,
                Status = ProductStatus.Pending,
                CreatedAt = DateTime.UtcNow
            });
            db.SaveChanges();

            TempData["Success"] = "Listing submitted. It will appear in the marketplace once an admin approves it.";
            return RedirectToAction("MyListings");
        }

        // GET /my-listings/edit/5
        [Authorize]
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
                ProductCondition = p.ProductCondition,
                PickupLocation = p.PickupLocation,
                CategoryId = p.CategoryId,
                HasExistingImage = p.ImageData != null,
                Categories = CategoryList(p.CategoryId)
            });
        }

        // POST /my-listings/edit/5
        [Authorize]
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

            byte[] imageData;
            string imageContentType;
            string imageError;
            if (!TryReadImage(form.ImageFile, out imageData, out imageContentType, out imageError))
                ModelState.AddModelError("ImageFile", imageError);

            if (!ModelState.IsValid)
            {
                form.ProductId = id;
                form.HasExistingImage = product.ImageData != null;
                form.Categories = CategoryList(form.CategoryId);
                return View(form);
            }

            product.Title = form.Title.Trim();
            product.Description = string.IsNullOrWhiteSpace(form.Description) ? "No description provided." : form.Description.Trim();
            product.Price = form.Price.Value;
            product.ProductCondition = form.ProductCondition;
            product.PickupLocation = string.IsNullOrWhiteSpace(form.PickupLocation) ? "MU Campus" : form.PickupLocation.Trim();
            product.CategoryId = form.CategoryId.Value;
            if (imageData != null)
            {
                product.ImageData = imageData;
                product.ImageContentType = imageContentType;
            }
            product.Status = ProductStatus.Pending;
            product.UpdatedAt = DateTime.UtcNow;

            try
            {
                db.SaveChanges();
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
        [Authorize]
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
            product.UpdatedAt = DateTime.UtcNow;
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

        private int GetCartItemCount()
        {
            int? me = CurrentUserId();
            if (!me.HasValue) return 0;
            return db.CartItems.AsNoTracking().Count(ci => ci.Cart.UserId == me.Value);
        }

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
                case "price_asc":  query = query.OrderBy(p => p.Price); break;
                case "price_desc": query = query.OrderByDescending(p => p.Price); break;
                default:           query = query.OrderByDescending(p => p.CreatedAt); break;
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
                    CreatedDate = p.CreatedAt,
                    HasImage = p.ImageData != null
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
            var cats = db.Categories.AsNoTracking()
                .Where(c => c.IsActive)
                .OrderBy(c => c.CategoryName)
                .ToList();
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

        private int? CurrentUserId()
        {
            if (_currentUserLoaded) return _currentUserId;
            _currentUserLoaded = true;

            if (User == null || User.Identity == null || !User.Identity.IsAuthenticated)
                return null;

            string email = User.Identity.Name;
            if (string.IsNullOrEmpty(email)) return null;

            string normalized = email.Trim().ToLowerInvariant();
            _currentUserId = db.Users.AsNoTracking()
                .Where(u => u.Email.ToLower() == normalized)
                .Select(u => (int?)u.UserId)
                .FirstOrDefault();
            return _currentUserId;
        }

        private static bool TryReadImage(HttpPostedFileBase file, out byte[] data, out string contentType, out string error)
        {
            data = null;
            contentType = null;
            error = null;

            if (file == null || file.ContentLength == 0)
                return true;

            const int maxBytes = 2 * 1024 * 1024;
            if (file.ContentLength > maxBytes)
            {
                error = "Image must be 2 MB or smaller.";
                return false;
            }

            string ext = (Path.GetExtension(file.FileName) ?? string.Empty).ToLowerInvariant();
            string mime = (file.ContentType ?? string.Empty).ToLowerInvariant();

            if (ext == ".jpg" || ext == ".jpeg" || mime == "image/jpeg" || mime == "image/jpg")
                contentType = "image/jpeg";
            else if (ext == ".png" || mime == "image/png")
                contentType = "image/png";
            else if (ext == ".webp" || mime == "image/webp")
                contentType = "image/webp";
            else
            {
                error = "Only JPG, PNG, or WEBP images are allowed.";
                return false;
            }

            using (var ms = new MemoryStream())
            {
                file.InputStream.CopyTo(ms);
                data = ms.ToArray();
            }

            return true;
        }

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
