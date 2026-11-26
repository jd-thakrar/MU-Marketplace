using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Security.Cryptography;
using System.Text;
using System.Web.Mvc;
using System.Web.Security;
using MU_Marketplace.Infrastructure;

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
            if (Session["AdminUserId"] == null && User != null && User.Identity != null && User.Identity.IsAuthenticated)
            {
                using (SqlConnection con = new SqlConnection(connectionString))
                {
                    string query = @"
                        SELECT UserId, FullName
                        FROM Users
                        WHERE LOWER(Email) = @Email
                        AND [Role] = 1
                        AND [Status] = 0";

                    using (SqlCommand cmd = new SqlCommand(query, con))
                    {
                        cmd.Parameters.Add("@Email", SqlDbType.NVarChar, 150).Value = User.Identity.Name.Trim().ToLowerInvariant();
                        con.Open();
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                Session["AdminUserId"] = reader.GetInt32(0);
                                Session["AdminName"] = reader.GetString(1);
                            }
                        }
                    }
                }
            }

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
                Session.Remove("AdminName");
                return RedirectToAction("Login", "Account", new { returnUrl = Request != null ? Request.RawUrl : null });
            }

            return null;
        }

        // ==========================================
        // 1. UNIFIED AUTH REDIRECT & LOGOUT
        // ==========================================

        [HttpGet]
        public ActionResult Login()
        {
            if (IsAdmin())
                return RedirectToAction("Dashboard");

            return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Dashboard", "Admin") });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Logout()
        {
            FormsAuthentication.SignOut();
            if (Session != null)
            {
                Session.Clear();
                Session.Abandon();
            }

            TempData["Success"] = "You have been logged out.";
            return RedirectToAction("Login", "Account");
        }

        // ==========================================
        // 2. ADMIN DASHBOARD
        // ==========================================

        public ActionResult Dashboard()
        {
            var access = CheckAdmin();
            if (access != null) return access;

            DataTable recentPending = new DataTable();
            DataTable recentReports = new DataTable();
            DataTable categoryStats = new DataTable();
            DataTable statusStats = new DataTable();

            using (SqlConnection con = new SqlConnection(connectionString))
            {
                con.Open();

                string statsQuery = @"
                    SELECT
                        (SELECT COUNT(*) FROM Products WHERE [Status] = 0) AS PendingListings,
                        (SELECT COUNT(*) FROM Users WHERE [Role] = 0) AS TotalStudents,
                        (SELECT COUNT(*) FROM Products WHERE [Status] = 1) AS ApprovedProducts,
                        (SELECT COUNT(*) FROM Reports WHERE [Status] = 0) AS OpenReports,
                        (SELECT COUNT(*) FROM Products WHERE [Status] = 3) AS SoldProducts,
                        (SELECT COUNT(*) FROM Categories WHERE IsActive = 1) AS ActiveCategories,
                        (SELECT ISNULL(SUM(PurchasePrice), 0) FROM Purchases) AS TotalSales,
                        (SELECT COUNT(*) FROM Purchases) AS TotalOrders";

                using (SqlCommand cmd = new SqlCommand(statsQuery, con))
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        ViewBag.PendingListings = reader["PendingListings"];
                        ViewBag.TotalStudents = reader["TotalStudents"];
                        ViewBag.ApprovedProducts = reader["ApprovedProducts"];
                        ViewBag.OpenReports = reader["OpenReports"];
                        ViewBag.SoldProducts = reader["SoldProducts"];
                        ViewBag.ActiveCategories = reader["ActiveCategories"];
                        ViewBag.TotalSales = Convert.ToDecimal(reader["TotalSales"]);
                        ViewBag.TotalOrders = reader["TotalOrders"];
                    }
                }

                string statusQuery = @"
                    SELECT [Status], COUNT(*) AS Total
                    FROM Products
                    GROUP BY [Status]";

                using (SqlCommand cmd = new SqlCommand(statusQuery, con))
                using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                {
                    da.Fill(statusStats);
                }

                string catQuery = @"
                    SELECT c.CategoryName, COUNT(p.ProductId) AS Total
                    FROM Categories c
                    LEFT JOIN Products p ON p.CategoryId = c.CategoryId
                    GROUP BY c.CategoryName
                    ORDER BY Total DESC, c.CategoryName";

                using (SqlCommand cmd = new SqlCommand(catQuery, con))
                using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                {
                    da.Fill(categoryStats);
                }

                string pendingQuery = @"
                    SELECT TOP 5
                        p.ProductId, p.Title, p.Price, p.CreatedAt,
                        u.FullName AS SellerName, c.CategoryName
                    FROM Products p
                    INNER JOIN Users u ON p.SellerId = u.UserId
                    INNER JOIN Categories c ON p.CategoryId = c.CategoryId
                    WHERE p.[Status] = 0
                    ORDER BY p.CreatedAt DESC";

                using (SqlCommand cmd = new SqlCommand(pendingQuery, con))
                using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                {
                    da.Fill(recentPending);
                }

                string reportsQuery = @"
                    SELECT TOP 5
                        r.ReportId, r.Reason, r.CreatedAt,
                        p.Title AS ProductTitle, u.FullName AS ReporterName
                    FROM Reports r
                    INNER JOIN Products p ON r.ProductId = p.ProductId
                    INNER JOIN Users u ON r.ReporterId = u.UserId
                    WHERE r.[Status] = 0
                    ORDER BY r.CreatedAt DESC";

                using (SqlCommand cmd = new SqlCommand(reportsQuery, con))
                using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                {
                    da.Fill(recentReports);
                }
            }

            ViewBag.RecentPending = recentPending;
            ViewBag.RecentReports = recentReports;
            ViewBag.CategoryStats = categoryStats;
            ViewBag.StatusStats = statusStats;

            return View();
        }

        // ==========================================
        // 3. MANAGE LISTINGS
        // ==========================================

        public ActionResult Listings(int status = 0, string search = "")
        {
            var access = CheckAdmin();
            if (access != null) return access;

            if (status < 0 || status > 4)
                status = 0;

            DataTable products = new DataTable();
            string term = (search ?? string.Empty).Trim();

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
                        c.CategoryName,
                        pu.PurchaseId,
                        pu.PurchasePrice,
                        pu.PurchaseDate,
                        buyer.FullName AS BuyerName,
                        buyer.Email AS BuyerEmail,
                        buyer.PhoneNumber AS BuyerPhone
                    FROM Products p
                    INNER JOIN Users u ON p.SellerId = u.UserId
                    INNER JOIN Categories c ON p.CategoryId = c.CategoryId
                    LEFT JOIN Purchases pu ON p.ProductId = pu.ProductId
                    LEFT JOIN Users buyer ON pu.BuyerId = buyer.UserId
                    WHERE p.[Status] = @Status
                    AND (
                        @Search = ''
                        OR p.Title LIKE '%' + @Search + '%'
                        OR u.FullName LIKE '%' + @Search + '%'
                        OR u.Email LIKE '%' + @Search + '%'
                        OR c.CategoryName LIKE '%' + @Search + '%'
                        OR ISNULL(buyer.FullName, '') LIKE '%' + @Search + '%'
                        OR ISNULL(buyer.Email, '') LIKE '%' + @Search + '%'
                    )
                    ORDER BY p.CreatedAt DESC";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.Add("@Status", SqlDbType.TinyInt).Value = status;
                    cmd.Parameters.Add("@Search", SqlDbType.NVarChar, 150).Value = term;

                    using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                    {
                        adapter.Fill(products);
                    }
                }
            }

            ViewBag.CurrentStatus = status;
            ViewBag.Search = term;

            return View(products);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ReviewListing(int productId, int status, string adminNotes)
        {
            var access = CheckAdmin();
            if (access != null) return access;

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
                        string.IsNullOrWhiteSpace(adminNotes) ? (object)DBNull.Value : adminNotes.Trim();
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RemoveListing(int productId, string adminNotes)
        {
            var access = CheckAdmin();
            if (access != null) return access;

            int adminId = Convert.ToInt32(Session["AdminUserId"]);

            using (SqlConnection con = new SqlConnection(connectionString))
            {
                string query = @"
                    UPDATE Products
                    SET
                        [Status] = 4,
                        AdminNotes = @AdminNotes,
                        ReviewedBy = @AdminId,
                        ReviewedAt = SYSUTCDATETIME(),
                        UpdatedAt = SYSUTCDATETIME()
                    WHERE ProductId = @ProductId
                    AND [Status] IN (0, 1, 2)";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.Add("@AdminNotes", SqlDbType.NVarChar, 500).Value =
                        string.IsNullOrWhiteSpace(adminNotes) ? (object)"Removed by administrator." : adminNotes.Trim();
                    cmd.Parameters.Add("@AdminId", SqlDbType.Int).Value = adminId;
                    cmd.Parameters.Add("@ProductId", SqlDbType.Int).Value = productId;

                    con.Open();
                    int rows = cmd.ExecuteNonQuery();

                    TempData[rows > 0 ? "Success" : "Error"] =
                        rows > 0 ? "Listing removed from marketplace." : "Listing could not be removed.";
                }
            }

            return RedirectToAction("Listings", new { status = 1 });
        }

        // ==========================================
        // 4. MANAGE STUDENT USERS
        // ==========================================

        public ActionResult Users(string search = "", string statusFilter = "all")
        {
            var access = CheckAdmin();
            if (access != null) return access;

            DataTable users = new DataTable();
            string term = (search ?? string.Empty).Trim();
            string filter = (statusFilter ?? "all").Trim().ToLowerInvariant();

            using (SqlConnection con = new SqlConnection(connectionString))
            {
                string query = @"
                    SELECT
                        u.UserId,
                        u.FullName,
                        u.Email,
                        u.EnrollmentNo,
                        u.Department,
                        u.PhoneNumber,
                        u.[Status],
                        u.CreatedAt,
                        u.LastLoginAt,
                        (SELECT COUNT(*) FROM Products p WHERE p.SellerId = u.UserId) AS ListingCount
                    FROM Users u
                    WHERE u.[Role] = 0
                    AND (@StatusFilter = 'all'
                         OR (@StatusFilter = 'active' AND u.[Status] = 0)
                         OR (@StatusFilter = 'suspended' AND u.[Status] = 1))
                    AND (
                        @Search = ''
                        OR u.FullName LIKE '%' + @Search + '%'
                        OR u.Email LIKE '%' + @Search + '%'
                        OR ISNULL(u.EnrollmentNo, '') LIKE '%' + @Search + '%'
                        OR ISNULL(u.Department, '') LIKE '%' + @Search + '%'
                    )
                    ORDER BY u.CreatedAt DESC";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.Add("@Search", SqlDbType.NVarChar, 150).Value = term;
                    cmd.Parameters.Add("@StatusFilter", SqlDbType.NVarChar, 20).Value = filter;

                    using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                    {
                        adapter.Fill(users);
                    }
                }
            }

            ViewBag.Search = term;
            ViewBag.StatusFilter = filter;

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
        // 5. MANAGE REPORTS & ANALYTICS CENTER
        // ==========================================

        public ActionResult Reports(string statusFilter = "all", string tab = "moderation")
        {
            var access = CheckAdmin();
            if (access != null) return access;

            DataTable reports = new DataTable();
            DataTable salesByCategory = new DataTable();
            DataTable topSellers = new DataTable();
            DataTable studentReport = new DataTable();
            DataTable inventoryByCategory = new DataTable();

            string filter = (statusFilter ?? "all").Trim().ToLowerInvariant();
            string activeTab = (tab ?? "moderation").Trim().ToLowerInvariant();

            using (SqlConnection con = new SqlConnection(connectionString))
            {
                con.Open();

                string query = @"
                    SELECT
                        r.ReportId,
                        r.ProductId,
                        r.ReporterId,
                        r.Reason,
                        r.[Status],
                        r.AdminNotes,
                        r.ReviewedAt,
                        r.CreatedAt,
                        p.Title AS ProductTitle,
                        p.[Status] AS ProductStatus,
                        u.FullName AS ReporterName,
                        u.Email AS ReporterEmail
                    FROM Reports r
                    INNER JOIN Products p ON r.ProductId = p.ProductId
                    INNER JOIN Users u ON r.ReporterId = u.UserId
                    WHERE (@StatusFilter = 'all'
                           OR (@StatusFilter = 'pending' AND r.[Status] = 0)
                           OR (@StatusFilter = 'resolved' AND r.[Status] = 1)
                           OR (@StatusFilter = 'ignored' AND r.[Status] = 2))
                    ORDER BY
                        CASE WHEN r.[Status] = 0 THEN 0 ELSE 1 END,
                        r.CreatedAt DESC";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.Add("@StatusFilter", SqlDbType.NVarChar, 20).Value = filter;
                    using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                    {
                        adapter.Fill(reports);
                    }
                }

                // Sales KPIs
                string salesKpiQuery = @"
                    SELECT
                        COUNT(*) AS TotalOrders,
                        ISNULL(SUM(PurchasePrice), 0) AS TotalRevenue,
                        ISNULL(AVG(PurchasePrice), 0) AS AvgOrderValue,
                        ISNULL(MAX(PurchasePrice), 0) AS HighestOrder
                    FROM Purchases";

                using (SqlCommand cmd = new SqlCommand(salesKpiQuery, con))
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    if (r.Read())
                    {
                        ViewBag.SalesTotalOrders = Convert.ToInt32(r["TotalOrders"]);
                        ViewBag.SalesTotalRevenue = Convert.ToDecimal(r["TotalRevenue"]);
                        ViewBag.SalesAvgOrder = Convert.ToDecimal(r["AvgOrderValue"]);
                        ViewBag.SalesHighestOrder = Convert.ToDecimal(r["HighestOrder"]);
                    }
                }

                // Sales by Category
                string salesCatQuery = @"
                    SELECT
                        c.CategoryName,
                        COUNT(pu.PurchaseId) AS OrdersCount,
                        ISNULL(SUM(pu.PurchasePrice), 0) AS Revenue
                    FROM Categories c
                    LEFT JOIN Products p ON p.CategoryId = c.CategoryId
                    LEFT JOIN Purchases pu ON pu.ProductId = p.ProductId
                    GROUP BY c.CategoryName
                    ORDER BY Revenue DESC, c.CategoryName";

                using (SqlCommand cmd = new SqlCommand(salesCatQuery, con))
                using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                {
                    da.Fill(salesByCategory);
                }

                // Top Sellers
                string topSellerQuery = @"
                    SELECT TOP 10
                        u.FullName,
                        u.Email,
                        COUNT(pu.PurchaseId) AS ItemsSold,
                        ISNULL(SUM(pu.PurchasePrice), 0) AS TotalEarned
                    FROM Users u
                    INNER JOIN Products p ON p.SellerId = u.UserId
                    INNER JOIN Purchases pu ON pu.ProductId = p.ProductId
                    GROUP BY u.UserId, u.FullName, u.Email
                    ORDER BY TotalEarned DESC, ItemsSold DESC";

                using (SqlCommand cmd = new SqlCommand(topSellerQuery, con))
                using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                {
                    da.Fill(topSellers);
                }

                // Student Activity Report
                string studentQuery = @"
                    SELECT
                        u.UserId,
                        u.FullName,
                        u.Email,
                        u.[Status],
                        u.CreatedAt,
                        u.LastLoginAt,
                        (SELECT COUNT(*) FROM Products p WHERE p.SellerId = u.UserId) AS TotalListings,
                        (SELECT COUNT(*) FROM Products p WHERE p.SellerId = u.UserId AND p.[Status] = 3) AS SoldCount,
                        (SELECT COUNT(*) FROM Purchases pu WHERE pu.BuyerId = u.UserId) AS PurchasesCount,
                        (SELECT ISNULL(SUM(pu.PurchasePrice), 0) FROM Purchases pu WHERE pu.BuyerId = u.UserId) AS TotalSpent
                    FROM Users u
                    WHERE u.[Role] = 0
                    ORDER BY TotalListings DESC, PurchasesCount DESC, u.CreatedAt DESC";

                using (SqlCommand cmd = new SqlCommand(studentQuery, con))
                using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                {
                    da.Fill(studentReport);
                }

                // Inventory by Category
                string invQuery = @"
                    SELECT
                        c.CategoryName,
                        SUM(CASE WHEN p.[Status] = 0 THEN 1 ELSE 0 END) AS PendingCount,
                        SUM(CASE WHEN p.[Status] = 1 THEN 1 ELSE 0 END) AS ApprovedCount,
                        SUM(CASE WHEN p.[Status] = 2 THEN 1 ELSE 0 END) AS RejectedCount,
                        SUM(CASE WHEN p.[Status] = 3 THEN 1 ELSE 0 END) AS SoldCount,
                        SUM(CASE WHEN p.[Status] = 4 THEN 1 ELSE 0 END) AS RemovedCount,
                        COUNT(p.ProductId) AS TotalCount
                    FROM Categories c
                    LEFT JOIN Products p ON p.CategoryId = c.CategoryId
                    GROUP BY c.CategoryName
                    ORDER BY TotalCount DESC, c.CategoryName";

                using (SqlCommand cmd = new SqlCommand(invQuery, con))
                using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                {
                    da.Fill(inventoryByCategory);
                }
            }

            ViewBag.StatusFilter = filter;
            ViewBag.ActiveTab = activeTab;
            ViewBag.SalesByCategory = salesByCategory;
            ViewBag.TopSellers = topSellers;
            ViewBag.StudentReport = studentReport;
            ViewBag.InventoryByCategory = inventoryByCategory;

            return View(reports);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ReviewReport(int reportId, string decision, string adminNotes)
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
                        string productQuery = @"
                            SELECT ProductId
                            FROM Reports
                            WHERE ReportId = @ReportId
                            AND [Status] = 0";

                        int productId;

                        using (SqlCommand cmd = new SqlCommand(productQuery, con, transaction))
                        {
                            cmd.Parameters.Add("@ReportId", SqlDbType.Int).Value = reportId;
                            object result = cmd.ExecuteScalar();
                            if (result == null)
                                throw new InvalidOperationException("Report not found or already reviewed.");
                            productId = Convert.ToInt32(result);
                        }

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

                            using (SqlCommand cmd = new SqlCommand(removeQuery, con, transaction))
                            {
                                cmd.Parameters.Add("@AdminId", SqlDbType.Int).Value = adminId;
                                cmd.Parameters.Add("@ProductId", SqlDbType.Int).Value = productId;
                                cmd.Parameters.Add("@AdminNotes", SqlDbType.NVarChar, 500).Value =
                                    string.IsNullOrWhiteSpace(adminNotes) ? (object)DBNull.Value : adminNotes.Trim();

                                if (cmd.ExecuteNonQuery() == 0)
                                    throw new InvalidOperationException("This product cannot be removed.");
                            }
                        }

                        string reportQuery = @"
                            UPDATE Reports
                            SET
                                [Status] = @Status,
                                AdminNotes = @AdminNotes,
                                ReviewedBy = @AdminId,
                                ReviewedAt = SYSUTCDATETIME()
                            WHERE ReportId = @ReportId
                            AND [Status] = 0";

                        using (SqlCommand cmd = new SqlCommand(reportQuery, con, transaction))
                        {
                            cmd.Parameters.Add("@Status", SqlDbType.TinyInt).Value = reportStatus;
                            cmd.Parameters.Add("@AdminNotes", SqlDbType.NVarChar, 500).Value =
                                string.IsNullOrWhiteSpace(adminNotes) ? (object)DBNull.Value : adminNotes.Trim();
                            cmd.Parameters.Add("@AdminId", SqlDbType.Int).Value = adminId;
                            cmd.Parameters.Add("@ReportId", SqlDbType.Int).Value = reportId;

                            if (cmd.ExecuteNonQuery() == 0)
                                throw new InvalidOperationException("Report could not be updated.");
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

        // ==========================================
        // 6. MANAGE CATEGORIES
        // ==========================================

        public ActionResult Categories()
        {
            var access = CheckAdmin();
            if (access != null) return access;

            DataTable categories = new DataTable();

            using (SqlConnection con = new SqlConnection(connectionString))
            {
                string query = @"
                    SELECT
                        c.CategoryId,
                        c.CategoryName,
                        c.[Description],
                        c.IsActive,
                        c.CreatedAt,
                        (SELECT COUNT(*) FROM Products p WHERE p.CategoryId = c.CategoryId) AS ProductCount
                    FROM Categories c
                    ORDER BY c.CategoryName";

                using (SqlCommand cmd = new SqlCommand(query, con))
                using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                {
                    adapter.Fill(categories);
                }
            }

            return View(categories);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddCategory(string categoryName, string description)
        {
            var access = CheckAdmin();
            if (access != null) return access;

            string name = (categoryName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["Error"] = "Category name is required.";
                return RedirectToAction("Categories");
            }

            try
            {
                using (SqlConnection con = new SqlConnection(connectionString))
                {
                    string query = @"
                        INSERT INTO Categories (CategoryName, [Description], IsActive, CreatedAt)
                        VALUES (@CategoryName, @Description, 1, SYSUTCDATETIME())";

                    using (SqlCommand cmd = new SqlCommand(query, con))
                    {
                        cmd.Parameters.Add("@CategoryName", SqlDbType.NVarChar, 100).Value = name;
                        cmd.Parameters.Add("@Description", SqlDbType.NVarChar, 500).Value =
                            string.IsNullOrWhiteSpace(description) ? (object)DBNull.Value : description.Trim();

                        con.Open();
                        cmd.ExecuteNonQuery();
                    }
                }

                TempData["Success"] = "Category added successfully.";
            }
            catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
            {
                TempData["Error"] = "A category with that name already exists.";
            }

            return RedirectToAction("Categories");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateCategory(int categoryId, string categoryName, string description)
        {
            var access = CheckAdmin();
            if (access != null) return access;

            string name = (categoryName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["Error"] = "Category name is required.";
                return RedirectToAction("Categories");
            }

            try
            {
                using (SqlConnection con = new SqlConnection(connectionString))
                {
                    string query = @"
                        UPDATE Categories
                        SET CategoryName = @CategoryName,
                            [Description] = @Description,
                            UpdatedAt = SYSUTCDATETIME()
                        WHERE CategoryId = @CategoryId";

                    using (SqlCommand cmd = new SqlCommand(query, con))
                    {
                        cmd.Parameters.Add("@CategoryId", SqlDbType.Int).Value = categoryId;
                        cmd.Parameters.Add("@CategoryName", SqlDbType.NVarChar, 100).Value = name;
                        cmd.Parameters.Add("@Description", SqlDbType.NVarChar, 500).Value =
                            string.IsNullOrWhiteSpace(description) ? (object)DBNull.Value : description.Trim();

                        con.Open();
                        int rows = cmd.ExecuteNonQuery();
                        TempData[rows > 0 ? "Success" : "Error"] =
                            rows > 0 ? "Category updated." : "Category not found.";
                    }
                }
            }
            catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
            {
                TempData["Error"] = "Another category already uses that name.";
            }

            return RedirectToAction("Categories");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ToggleCategoryStatus(int categoryId, bool isActive)
        {
            var access = CheckAdmin();
            if (access != null) return access;

            using (SqlConnection con = new SqlConnection(connectionString))
            {
                string query = @"
                    UPDATE Categories
                    SET IsActive = @IsActive,
                        UpdatedAt = SYSUTCDATETIME()
                    WHERE CategoryId = @CategoryId";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.Add("@IsActive", SqlDbType.Bit).Value = isActive;
                    cmd.Parameters.Add("@CategoryId", SqlDbType.Int).Value = categoryId;

                    con.Open();
                    int rows = cmd.ExecuteNonQuery();
                    TempData[rows > 0 ? "Success" : "Error"] =
                        rows > 0
                        ? (isActive ? "Category activated." : "Category deactivated.")
                        : "Category not found.";
                }
            }

            return RedirectToAction("Categories");
        }

        // ==========================================
        // 7. MANAGE ORDERS / PURCHASES
        // ==========================================

        public ActionResult Orders(string search = "")
        {
            var access = CheckAdmin();
            if (access != null) return access;

            DataTable orders = new DataTable();
            string term = (search ?? string.Empty).Trim();

            using (SqlConnection con = new SqlConnection(connectionString))
            {
                string query = @"
                    SELECT
                        pu.PurchaseId,
                        pu.ProductId,
                        pu.PurchasePrice,
                        pu.PurchaseDate,
                        p.Title AS ProductTitle,
                        p.PickupLocation,
                        c.CategoryName,
                        buyer.FullName AS BuyerName,
                        buyer.Email AS BuyerEmail,
                        seller.FullName AS SellerName,
                        seller.Email AS SellerEmail
                    FROM Purchases pu
                    INNER JOIN Products p ON pu.ProductId = p.ProductId
                    INNER JOIN Categories c ON p.CategoryId = c.CategoryId
                    INNER JOIN Users buyer ON pu.BuyerId = buyer.UserId
                    INNER JOIN Users seller ON p.SellerId = seller.UserId
                    WHERE (
                        @Search = ''
                        OR p.Title LIKE '%' + @Search + '%'
                        OR buyer.FullName LIKE '%' + @Search + '%'
                        OR buyer.Email LIKE '%' + @Search + '%'
                        OR seller.FullName LIKE '%' + @Search + '%'
                        OR c.CategoryName LIKE '%' + @Search + '%'
                    )
                    ORDER BY pu.PurchaseDate DESC";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.Add("@Search", SqlDbType.NVarChar, 150).Value = term;
                    using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                    {
                        da.Fill(orders);
                    }
                }
            }

            decimal totalRevenue = 0m;
            foreach (DataRow row in orders.Rows)
            {
                totalRevenue += Convert.ToDecimal(row["PurchasePrice"]);
            }

            ViewBag.Search = term;
            ViewBag.TotalOrders = orders.Rows.Count;
            ViewBag.TotalRevenue = totalRevenue;
            ViewBag.AvgOrder = orders.Rows.Count > 0 ? totalRevenue / orders.Rows.Count : 0m;

            return View(orders);
        }

        // ==========================================
        // 8. DOWNLOADABLE CSV REPORTS
        // ==========================================

        public ActionResult ExportOrdersCsv()
        {
            var access = CheckAdmin();
            if (access != null) return access;

            DataTable dt = new DataTable();
            using (SqlConnection con = new SqlConnection(connectionString))
            {
                string query = @"
                    SELECT
                        pu.PurchaseId AS OrderId,
                        p.Title AS Product,
                        c.CategoryName AS Category,
                        pu.PurchasePrice AS Amount,
                        buyer.FullName AS BuyerName,
                        buyer.Email AS BuyerEmail,
                        seller.FullName AS SellerName,
                        seller.Email AS SellerEmail,
                        p.PickupLocation,
                        pu.PurchaseDate
                    FROM Purchases pu
                    INNER JOIN Products p ON pu.ProductId = p.ProductId
                    INNER JOIN Categories c ON p.CategoryId = c.CategoryId
                    INNER JOIN Users buyer ON pu.BuyerId = buyer.UserId
                    INNER JOIN Users seller ON p.SellerId = seller.UserId
                    ORDER BY pu.PurchaseDate DESC";

                using (SqlDataAdapter da = new SqlDataAdapter(query, con))
                {
                    da.Fill(dt);
                }
            }

            return CsvFile(dt, "mu-marketplace-orders-" + DateTime.UtcNow.ToString("yyyyMMdd") + ".csv");
        }

        public ActionResult ExportListingsCsv(int? status)
        {
            var access = CheckAdmin();
            if (access != null) return access;

            DataTable dt = new DataTable();
            using (SqlConnection con = new SqlConnection(connectionString))
            {
                string query = @"
                    SELECT
                        p.ProductId,
                        p.Title,
                        c.CategoryName AS Category,
                        p.Price,
                        CASE p.ProductCondition
                            WHEN 0 THEN 'New'
                            WHEN 1 THEN 'Like New'
                            WHEN 2 THEN 'Good'
                            ELSE 'Fair'
                        END AS Condition,
                        CASE p.[Status]
                            WHEN 0 THEN 'Pending'
                            WHEN 1 THEN 'Approved'
                            WHEN 2 THEN 'Rejected'
                            WHEN 3 THEN 'Sold'
                            ELSE 'Removed'
                        END AS Status,
                        p.PickupLocation,
                        u.FullName AS SellerName,
                        u.Email AS SellerEmail,
                        p.CreatedAt,
                        p.AdminNotes
                    FROM Products p
                    INNER JOIN Users u ON p.SellerId = u.UserId
                    INNER JOIN Categories c ON p.CategoryId = c.CategoryId
                    WHERE (@Status IS NULL OR p.[Status] = @Status)
                    ORDER BY p.CreatedAt DESC";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.Add("@Status", SqlDbType.TinyInt).Value = status.HasValue ? (object)status.Value : DBNull.Value;
                    using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                    {
                        da.Fill(dt);
                    }
                }
            }

            return CsvFile(dt, "mu-marketplace-listings-" + DateTime.UtcNow.ToString("yyyyMMdd") + ".csv");
        }

        public ActionResult ExportUsersCsv()
        {
            var access = CheckAdmin();
            if (access != null) return access;

            DataTable dt = new DataTable();
            using (SqlConnection con = new SqlConnection(connectionString))
            {
                string query = @"
                    SELECT
                        u.UserId,
                        u.FullName,
                        u.Email,
                        u.EnrollmentNo,
                        u.Department,
                        u.PhoneNumber,
                        CASE u.[Status] WHEN 0 THEN 'Active' ELSE 'Suspended' END AS Status,
                        (SELECT COUNT(*) FROM Products p WHERE p.SellerId = u.UserId) AS ListingCount,
                        u.CreatedAt,
                        u.LastLoginAt
                    FROM Users u
                    WHERE u.[Role] = 0
                    ORDER BY u.CreatedAt DESC";

                using (SqlDataAdapter da = new SqlDataAdapter(query, con))
                {
                    da.Fill(dt);
                }
            }

            return CsvFile(dt, "mu-marketplace-students-" + DateTime.UtcNow.ToString("yyyyMMdd") + ".csv");
        }

        public ActionResult ExportReportsCsv()
        {
            var access = CheckAdmin();
            if (access != null) return access;

            DataTable dt = new DataTable();
            using (SqlConnection con = new SqlConnection(connectionString))
            {
                string query = @"
                    SELECT
                        r.ReportId,
                        p.ProductId,
                        p.Title AS ProductTitle,
                        u.FullName AS ReporterName,
                        u.Email AS ReporterEmail,
                        r.Reason,
                        CASE r.[Status]
                            WHEN 0 THEN 'Pending'
                            WHEN 1 THEN 'Resolved'
                            ELSE 'Ignored'
                        END AS Status,
                        r.AdminNotes,
                        r.CreatedAt,
                        r.ReviewedAt
                    FROM Reports r
                    INNER JOIN Products p ON r.ProductId = p.ProductId
                    INNER JOIN Users u ON r.ReporterId = u.UserId
                    ORDER BY r.CreatedAt DESC";

                using (SqlDataAdapter da = new SqlDataAdapter(query, con))
                {
                    da.Fill(dt);
                }
            }

            return CsvFile(dt, "mu-marketplace-reports-" + DateTime.UtcNow.ToString("yyyyMMdd") + ".csv");
        }

        private FileContentResult CsvFile(DataTable dt, string fileName)
        {
            var sb = new StringBuilder();

            for (int i = 0; i < dt.Columns.Count; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append(CsvEscape(dt.Columns[i].ColumnName));
            }
            sb.AppendLine();

            foreach (DataRow row in dt.Rows)
            {
                for (int i = 0; i < dt.Columns.Count; i++)
                {
                    if (i > 0) sb.Append(",");
                    sb.Append(CsvEscape(row[i]));
                }
                sb.AppendLine();
            }

            byte[] bytes = Encoding.UTF8.GetPreamble().Length > 0
                ? CombineBytes(Encoding.UTF8.GetPreamble(), Encoding.UTF8.GetBytes(sb.ToString()))
                : Encoding.UTF8.GetBytes(sb.ToString());

            return File(bytes, "text/csv", fileName);
        }

        private static string CsvEscape(object value)
        {
            if (value == null || value == DBNull.Value) return "";
            string s = Convert.ToString(value);
            if (s.Contains(",") || s.Contains("\"") || s.Contains("\n") || s.Contains("\r"))
            {
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            }
            return s;
        }

        private static byte[] CombineBytes(byte[] first, byte[] second)
        {
            byte[] ret = new byte[first.Length + second.Length];
            Buffer.BlockCopy(first, 0, ret, 0, first.Length);
            Buffer.BlockCopy(second, 0, ret, first.Length, second.Length);
            return ret;
        }
    }
}
