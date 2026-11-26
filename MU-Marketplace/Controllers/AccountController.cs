using System;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using MU_Marketplace.Infrastructure;
using MU_Marketplace.Models;
using MU_Marketplace.Models.ViewModels;

namespace MU_Marketplace.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext db = new ApplicationDbContext();

        // GET: Account
        public ActionResult Index()
        {
            return RedirectToAction("Login");
        }

        // GET /Account/Login
        [HttpGet, AllowAnonymous]
        [OutputCache(NoStore = true, Duration = 0, VaryByParam = "*")]
        public ActionResult Login(string returnUrl)
        {
            if (Request.IsAuthenticated) return RedirectToLocal(returnUrl);

            ViewBag.ReturnUrl = returnUrl;
            return View(new LoginViewModel());
        }

        // POST /Account/Login
        [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
        public ActionResult Login(LoginViewModel model, string returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;
            if (!ModelState.IsValid) return View(model);

            string email = NormalizeEmail(model.Email);
            var user = db.Users.FirstOrDefault(u => u.Email.ToLower() == email);

            bool valid;
            if (user == null)
            {
                PasswordHasher.VerifyDummy(model.Password);   // same timing as a wrong password
                valid = false;
            }
            else
            {
                valid = PasswordHasher.Verify(model.Password, user.PasswordHash);
            }

            if (!valid)
            {
                ModelState.AddModelError("", "Invalid email or password.");
                return View(model);
            }

            if (user.Status == UserStatus.Suspended)
            {
                ModelState.AddModelError("", "Your account is suspended. Please contact an administrator.");
                return View(model);
            }

            user.LastLoginAt = DateTime.UtcNow;
            db.SaveChanges();

            SignIn(user, model.RememberMe);

            if (user.Role == UserRole.Admin)
            {
                Session["AdminUserId"] = user.UserId;
                Session["AdminName"] = user.FullName;
                if (string.IsNullOrEmpty(returnUrl))
                {
                    return RedirectToAction("Dashboard", "Admin");
                }
            }

            return RedirectToLocal(returnUrl);
        }

        // GET /Account/Register   (shown as "Sign Up")
        [HttpGet, AllowAnonymous]
        [OutputCache(NoStore = true, Duration = 0, VaryByParam = "*")]
        public ActionResult Register()
        {
            if (Request.IsAuthenticated) return RedirectToAction("Index", "Home");
            return View(new RegisterViewModel());
        }

        // POST /Account/Register
        [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
        public ActionResult Register(RegisterViewModel model)
        {
            string email = NormalizeEmail(model.Email);

            if (ModelState.IsValidField("Email") && db.Users.Any(u => u.Email.ToLower() == email))
                ModelState.AddModelError("Email", "An account with this email already exists.");

            if (!ModelState.IsValid) return View(model);

            var user = new User
            {
                FullName = model.FullName.Trim(),
                Email = email,
                PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim(),
                PasswordHash = PasswordHasher.Hash(model.Password),
                Role = UserRole.Student,
                Status = UserStatus.Active,
                CreatedAt = DateTime.UtcNow
            };
            db.Users.Add(user);

            try
            {
                db.SaveChanges();
            }
            catch (DbUpdateException)
            {
                // UNIQUE(Email) caught a sign-up that raced with ours
                ModelState.AddModelError("Email", "An account with this email already exists.");
                return View(model);
            }

            SignIn(user, false);
            TempData["Success"] = "Welcome to MU-Marketplace, " + user.FullName + "! Your account is ready.";
            return RedirectToAction("Index", "Home");
        }

        // POST /Account/Logout
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Logout()
        {
            FormsAuthentication.SignOut();
            if (Session != null) Session.Abandon();

            TempData["Success"] = "You have been logged out.";
            return RedirectToAction("Index", "Home");
        }

        // ---------------------------------------------------------------------

        private static string NormalizeEmail(string email)
        {
            return (email ?? string.Empty).Trim().ToLowerInvariant();
        }

        // Forms auth cookie: Name = Email (what MarketplaceController expects), UserData = FullName (for the navbar)
        private void SignIn(User user, bool rememberMe)
        {
            var now = DateTime.Now;
            var ticket = new FormsAuthenticationTicket(
                1, user.Email, now, now.Add(FormsAuthentication.Timeout),
                rememberMe, user.FullName ?? string.Empty, FormsAuthentication.FormsCookiePath);

            var cookie = new HttpCookie(FormsAuthentication.FormsCookieName, FormsAuthentication.Encrypt(ticket))
            {
                HttpOnly = true,
                Secure = FormsAuthentication.RequireSSL,
                Path = FormsAuthentication.FormsCookiePath
            };
            if (!string.IsNullOrEmpty(FormsAuthentication.CookieDomain)) cookie.Domain = FormsAuthentication.CookieDomain;
            if (rememberMe) cookie.Expires = ticket.Expiration;

            Response.Cookies.Add(cookie);
        }

        private ActionResult RedirectToLocal(string returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
            return RedirectToAction("Index", "Home");
        }

        protected override void OnException(ExceptionContext filterContext)
        {
            if (filterContext.Exception is HttpAntiForgeryException && !filterContext.ExceptionHandled)
            {
                filterContext.ExceptionHandled = true;
                if (Request.IsAuthenticated)
                {
                    if (Session["AdminUserId"] != null)
                    {
                        filterContext.Result = RedirectToAction("Dashboard", "Admin");
                        return;
                    }

                    string email = NormalizeEmail(User.Identity.Name);
                    var currentUser = db.Users.AsNoTracking().FirstOrDefault(u => u.Email.ToLower() == email);
                    if (currentUser != null && currentUser.Role == UserRole.Admin)
                    {
                        Session["AdminUserId"] = currentUser.UserId;
                        Session["AdminName"] = currentUser.FullName;
                        filterContext.Result = RedirectToAction("Dashboard", "Admin");
                        return;
                    }

                    filterContext.Result = RedirectToAction("Index", "Home");
                    return;
                }

                filterContext.Result = RedirectToAction("Login", "Account");
                return;
            }

            base.OnException(filterContext);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
