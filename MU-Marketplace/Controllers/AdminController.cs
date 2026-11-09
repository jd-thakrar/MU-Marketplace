
using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web.Mvc;
using System.Security.Cryptography;
using System.Text;
namespace MU_Marketplace.Controllers
{
    public class AdminController : Controller
    {
        private readonly string connectionString =
            ConfigurationManager.ConnectionStrings["MarketplaceConnection"].ConnectionString;

        // ==========================================
        // ADMIN AUTHORIZATION
        // ==========================================

        private bool IsAdmin()
        {
            if (Session["AdminUserId"] == null)
                return false;

            int adminId = Convert.ToInt32(Session["AdminUserId"]);

            using (SqlConnection con = new SqlConnection(connectionString))
            {
                string query = @"
                    SELECT COUNT(1)
                    FROM Users
                    WHERE UserId = @UserId
                    AND [Role] = 1
                    AND [Status] = 0";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.Add("@UserId", SqlDbType.Int).Value = adminId;

                    con.Open();

                    return Convert.ToInt32(cmd.ExecuteScalar()) == 1;
                }
            }
        }

        private ActionResult CheckAdmin()
        {
            if (!IsAdmin())
            {
                Session.Remove("AdminUserId");
                return RedirectToAction("Login");
            }

            return null;
        }

        // ==========================================
        // 1. ADMIN LOGIN
        // ==========================================

        [HttpGet]
        public ActionResult Login()
        {
            if (Session["AdminUserId"] != null && IsAdmin())
                return RedirectToAction("Dashboard");

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Please enter email and password.";
                return View();
            }

            // Generate SHA-256 hash matching our SQL demo account.
            string passwordHash;

            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(password);
                byte[] hash = sha256.ComputeHash(bytes);

                passwordHash = BitConverter.ToString(hash)
                    .Replace("-", "");
            }

            using (SqlConnection con = new SqlConnection(connectionString))
            {
                string query = @"
            SELECT UserId
            FROM Users
            WHERE Email = @Email
            AND PasswordHash = @PasswordHash
            AND [Role] = 1
            AND [Status] = 0";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.Add("@Email", SqlDbType.NVarChar, 150)
                        .Value = email.Trim();

                    cmd.Parameters.Add("@PasswordHash", SqlDbType.NVarChar, 255)
                        .Value = passwordHash;

                    con.Open();

                    object result = cmd.ExecuteScalar();

                    if (result != null)
                    {
                        Session["AdminUserId"] = Convert.ToInt32(result);

                        return RedirectToAction("Dashboard");
                    }
                }
            }

            ViewBag.Error = "Invalid email or password.";
            return View();
        }

        // Login POST will be added after matching
        // the password hashing method used by registration.

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Logout()
        {
            Session.Remove("AdminUserId");

            return RedirectToAction("Login");
        }

        // ==========================================
        // 2. ADMIN DASHBOARD
        // ==========================================

        public ActionResult Dashboard()
        {
            var access = CheckAdmin();
            if (access != null) return access;

            using (SqlConnection con = new SqlConnection(connectionString))
            {
                string query = @"
                    SELECT
                        (SELECT COUNT(*) FROM Products
                         WHERE [Status] = 0) AS PendingListings,

                        (SELECT COUNT(*) FROM Users
                         WHERE [Role] = 0) AS TotalStudents,

                        (SELECT COUNT(*) FROM Products
                         WHERE [Status] = 1) AS ApprovedProducts,

                        (SELECT COUNT(*) FROM Reports
                         WHERE [Status] = 0) AS OpenReports";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    con.Open();

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            ViewBag.PendingListings = reader["PendingListings"];
                            ViewBag.TotalStudents = reader["TotalStudents"];
                            ViewBag.ApprovedProducts = reader["ApprovedProducts"];
                            ViewBag.OpenReports = reader["OpenReports"];
                        }
                    }
                }
            }

            return View();
        }

        // ==========================================
        // 3. MANAGE LISTINGS
        // ==========================================

        public ActionResult Listings(int status = 0)
        {
            var access = CheckAdmin();
            if (access != null) return access;

            if (status < 0 || status > 3)
                status = 0;

            DataTable products = new DataTable();

            using (SqlConnection con = new SqlConnection(connectionString))
            {
                string query = @"
                    SELECT
                        p.ProductId,
                        p.Title,
                        p.Description,
                        p.Price,
                        p.ProductCondition,
                        p.PickupLocation,
                        p.[Status],
                        p.CreatedAt,
                        p.AdminNotes,
                        u.FullName AS SellerName,
                        u.Email AS SellerEmail,
                        c.CategoryName
                    FROM Products p
                    INNER JOIN Users u ON p.SellerId = u.UserId
                    INNER JOIN Categories c ON p.CategoryId = c.CategoryId
                    WHERE p.[Status] = @Status
                    ORDER BY p.CreatedAt DESC";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.Add("@Status", SqlDbType.TinyInt).Value = status;

                    using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                    {
                        adapter.Fill(products);
                    }
                }
            }

            ViewBag.CurrentStatus = status;

            return View(products);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ReviewListing(
            int productId,
            int status,
            string adminNotes)
        {
            var access = CheckAdmin();
            if (access != null) return access;

            // Only approval and rejection are permitted here.
            if (status != 1 && status != 2)
            {
                TempData["Error"] = "Invalid listing action.";
                return RedirectToAction("Listings");
            }

            int adminId = Convert.ToInt32(Session["AdminUserId"]);

            using (SqlConnection con = new SqlConnection(connectionString))
            {
                string query = @"
                    UPDATE Products
                    SET
                        [Status] = @Status,
                        AdminNotes = @AdminNotes,
                        ReviewedBy = @AdminId,
                        ReviewedAt = SYSUTCDATETIME(),
                        UpdatedAt = SYSUTCDATETIME()
                    WHERE ProductId = @ProductId
                    AND [Status] = 0";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.Add("@Status", SqlDbType.TinyInt).Value = status;

                    cmd.Parameters.Add("@AdminNotes", SqlDbType.NVarChar, 500).Value =
                        string.IsNullOrWhiteSpace(adminNotes)
                            ? (object)DBNull.Value
                            : adminNotes.Trim();

                    cmd.Parameters.Add("@AdminId", SqlDbType.Int).Value = adminId;
                    cmd.Parameters.Add("@ProductId", SqlDbType.Int).Value = productId;

                    con.Open();

                    int rows = cmd.ExecuteNonQuery();

                    if (rows > 0)
                        TempData["Success"] = status == 1
                            ? "Listing approved successfully."
                            : "Listing rejected successfully.";
                    else
                        TempData["Error"] = "Listing not found or already reviewed.";
                }
            }

            return RedirectToAction("Listings");
        }

        // ==========================================
        // 4. MANAGE STUDENT USERS
        // ==========================================

        public ActionResult Users()
        {
            var access = CheckAdmin();
            if (access != null) return access;

            DataTable users = new DataTable();

            using (SqlConnection con = new SqlConnection(connectionString))
            {
                string query = @"
                    SELECT
                        UserId,
                        FullName,
                        Email,
                        EnrollmentNo,
                        Department,
                        PhoneNumber,
                        [Status],
                        CreatedAt
                    FROM Users
                    WHERE [Role] = 0
                    ORDER BY CreatedAt DESC";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                    {
                        adapter.Fill(users);
                    }
                }
            }

            return View(users);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ChangeUserStatus(int userId, int status)
        {
            var access = CheckAdmin();
            if (access != null) return access;

            if (status != 0 && status != 1)
            {
                TempData["Error"] = "Invalid account status.";
                return RedirectToAction("Users");
            }

            using (SqlConnection con = new SqlConnection(connectionString))
            {
                string query = @"
                    UPDATE Users
                    SET
                        [Status] = @Status,
                        UpdatedAt = SYSUTCDATETIME()
                    WHERE UserId = @UserId
                    AND [Role] = 0";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.Add("@Status", SqlDbType.TinyInt).Value = status;
                    cmd.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;

                    con.Open();

                    int rows = cmd.ExecuteNonQuery();

                    TempData[rows > 0 ? "Success" : "Error"] =
                        rows > 0
                        ? (status == 1 ? "Student suspended." : "Student activated.")
                        : "Student not found.";
                }
            }

            return RedirectToAction("Users");
        }

        // ==========================================
        // 5. MANAGE REPORTS
        // ==========================================

        public ActionResult Reports()
        {
            var access = CheckAdmin();
            if (access != null) return access;

            DataTable reports = new DataTable();

            using (SqlConnection con = new SqlConnection(connectionString))
            {
                string query = @"
                    SELECT
                        r.ReportId,
                        r.ProductId,
                        r.ReporterId,
                        r.Reason,
                        r.[Status],
                        r.AdminNotes,
                        r.CreatedAt,
                        p.Title AS ProductTitle,
                        p.[Status] AS ProductStatus,
                        u.FullName AS ReporterName,
                        u.Email AS ReporterEmail
                    FROM Reports r
                    INNER JOIN Products p ON r.ProductId = p.ProductId
                    INNER JOIN Users u ON r.ReporterId = u.UserId
                    ORDER BY
                        CASE WHEN r.[Status] = 0 THEN 0 ELSE 1 END,
                        r.CreatedAt DESC";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                    {
                        adapter.Fill(reports);
                    }
                }
            }

            return View(reports);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ReviewReport(
            int reportId,
            string decision,
            string adminNotes)
        {
            var access = CheckAdmin();
            if (access != null) return access;

            int reportStatus;

            if (decision == "resolve")
                reportStatus = 1;
            else if (decision == "ignore")
                reportStatus = 2;
            else if (decision == "remove")
                reportStatus = 1;
            else
            {
                TempData["Error"] = "Invalid report action.";
                return RedirectToAction("Reports");
            }

            int adminId = Convert.ToInt32(Session["AdminUserId"]);

            using (SqlConnection con = new SqlConnection(connectionString))
            {
                con.Open();

                using (SqlTransaction transaction = con.BeginTransaction())
                {
                    try
                    {
                        // Retrieve the reported product.
                        string productQuery = @"
                            SELECT ProductId
                            FROM Reports
                            WHERE ReportId = @ReportId
                            AND [Status] = 0";

                        int productId;

                        using (SqlCommand cmd = new SqlCommand(
                            productQuery, con, transaction))
                        {
                            cmd.Parameters.Add("@ReportId", SqlDbType.Int).Value = reportId;

                            object result = cmd.ExecuteScalar();

                            if (result == null)
                                throw new InvalidOperationException(
                                    "Report not found or already reviewed.");

                            productId = Convert.ToInt32(result);
                        }

                        // Remove the product if requested.
                        if (decision == "remove")
                        {
                            string removeQuery = @"
                                UPDATE Products
                                SET
                                    [Status] = 4,
                                    ReviewedBy = @AdminId,
                                    ReviewedAt = SYSUTCDATETIME(),
                                    AdminNotes = @AdminNotes,
                                    UpdatedAt = SYSUTCDATETIME()
                                WHERE ProductId = @ProductId
                                AND [Status] IN (0, 1, 2)";

                            using (SqlCommand cmd = new SqlCommand(
                                removeQuery, con, transaction))
                            {
                                cmd.Parameters.Add("@AdminId", SqlDbType.Int).Value = adminId;
                                cmd.Parameters.Add("@ProductId", SqlDbType.Int).Value = productId;

                                cmd.Parameters.Add("@AdminNotes",
                                    SqlDbType.NVarChar, 500).Value =
                                    string.IsNullOrWhiteSpace(adminNotes)
                                        ? (object)DBNull.Value
                                        : adminNotes.Trim();

                                if (cmd.ExecuteNonQuery() == 0)
                                    throw new InvalidOperationException(
                                        "This product cannot be removed.");
                            }
                        }

                        // Update report review status.
                        string reportQuery = @"
                            UPDATE Reports
                            SET
                                [Status] = @Status,
                                AdminNotes = @AdminNotes,
                                ReviewedBy = @AdminId,
                                ReviewedAt = SYSUTCDATETIME()
                            WHERE ReportId = @ReportId
                            AND [Status] = 0";

                        using (SqlCommand cmd = new SqlCommand(
                            reportQuery, con, transaction))
                        {
                            cmd.Parameters.Add("@Status", SqlDbType.TinyInt).Value =
                                reportStatus;

                            cmd.Parameters.Add("@AdminNotes",
                                SqlDbType.NVarChar, 500).Value =
                                string.IsNullOrWhiteSpace(adminNotes)
                                    ? (object)DBNull.Value
                                    : adminNotes.Trim();

                            cmd.Parameters.Add("@AdminId", SqlDbType.Int).Value = adminId;
                            cmd.Parameters.Add("@ReportId", SqlDbType.Int).Value = reportId;

                            if (cmd.ExecuteNonQuery() == 0)
                                throw new InvalidOperationException(
                                    "Report could not be updated.");
                        }

                        transaction.Commit();

                        TempData["Success"] = "Report reviewed successfully.";
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        TempData["Error"] = ex.Message;
                    }
                }
            }

            return RedirectToAction("Reports");
        }
    }
}
