using System;
using System.Linq;
using System.Web.Mvc;
using System.Web.Security;
using MU_Marketplace.Models;
using MU_Marketplace.Models.ViewModels;

namespace MU_Marketplace.Controllers
{
    [Authorize]
    public class StudentController : Controller
    {
        private readonly ApplicationDbContext _db = new ApplicationDbContext();

        // ------------------------------------------------------------------ //
        //  Helpers
        // ------------------------------------------------------------------ //

        private int CurrentUserId()
        {
            if (User?.Identity == null || !User.Identity.IsAuthenticated)
                return 0;

            string email = User.Identity.Name;
            if (string.IsNullOrWhiteSpace(email))
                return 0;

            string normalized = email.Trim().ToLowerInvariant();
            return _db.Users.AsNoTracking()
                .Where(u => u.Email.ToLower() == normalized)
                .Select(u => u.UserId)
                .FirstOrDefault();
        }

        // ------------------------------------------------------------------ //
        //  GET /Student/Profile
        // ------------------------------------------------------------------ //

        public new ActionResult Profile()
        {
            int uid = CurrentUserId();
            if (uid == 0) return RedirectToAction("Login", "Account");

            var user = _db.Users.Find(uid);
            if (user == null) return HttpNotFound();

            int totalListed    = _db.Products.Count(p => p.SellerId == uid);
            int totalSold      = _db.Products.Count(p => p.SellerId == uid && p.Status == ProductStatus.Sold);
            int totalPurchased = _db.Purchases.Count(p => p.BuyerId == uid);

            var vm = new ProfileViewModel
            {
                FullName       = user.FullName,
                Email          = user.Email,
                EnrollmentNo   = user.EnrollmentNo,
                Department     = user.Department,
                PhoneNumber    = user.PhoneNumber,
                TotalListed    = totalListed,
                TotalSold      = totalSold,
                TotalPurchased = totalPurchased,
                MemberSince    = user.CreatedAt
            };

            return View(vm);
        }

        // ------------------------------------------------------------------ //
        //  POST /Student/Profile
        // ------------------------------------------------------------------ //

        [HttpPost, ValidateAntiForgeryToken]
        public new ActionResult Profile(ProfileViewModel model)
        {
            int uid = CurrentUserId();
            if (uid == 0) return RedirectToAction("Login", "Account");

            // Re-load read-only stats before any early return
            var user = _db.Users.Find(uid);
            if (user == null) return HttpNotFound();

            // Keep email read-only
            model.Email = user.Email;

            // Recompute stats so the view can still show them on validation error
            model.TotalListed    = _db.Products.Count(p => p.SellerId == uid);
            model.TotalSold      = _db.Products.Count(p => p.SellerId == uid && p.Status == ProductStatus.Sold);
            model.TotalPurchased = _db.Purchases.Count(p => p.BuyerId == uid);
            model.MemberSince    = user.CreatedAt;

            if (!ModelState.IsValid)
                return View(model);

            user.FullName     = model.FullName.Trim();
            user.EnrollmentNo = string.IsNullOrWhiteSpace(model.EnrollmentNo) ? null : model.EnrollmentNo.Trim();
            user.Department   = string.IsNullOrWhiteSpace(model.Department) ? null : model.Department.Trim();
            user.PhoneNumber  = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim();
            user.UpdatedAt    = DateTime.UtcNow;

            _db.SaveChanges();

            TempData["Success"] = "Profile updated successfully.";
            return RedirectToAction("Profile");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _db.Dispose();
            base.Dispose(disposing);
        }
    }
}
