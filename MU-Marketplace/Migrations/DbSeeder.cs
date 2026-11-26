using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using MU_Marketplace.Infrastructure;
using MU_Marketplace.Models;

namespace MU_Marketplace.Migrations
{
    /// <summary>
    /// Wipes all transactional data on every startup and re-seeds fresh demo data.
    /// Two demo accounts (admin + student) are always preserved.
    /// </summary>
    internal static class DbSeeder
    {
        public static void Seed(ApplicationDbContext context)
        {
            // ── 0. Run only once — skip if data already exists ──────────────────
            if (context.Products.Any()) return;

            // ── 1. WIPE transactional rows in FK-safe order ─────────────────────
            WipeTransactionalData(context);

            // ── 2. Categories (idempotent – only inserted once) ─────────────────
            EnsureCategory(context, "Books",             "Textbooks, reference books and study material");
            EnsureCategory(context, "Electronics",       "Phones, laptops and other electronic devices");
            EnsureCategory(context, "Calculators",       "Scientific and other calculators");
            EnsureCategory(context, "Furniture",         "Furniture and room essentials");
            EnsureCategory(context, "Bicycles",          "Bicycles and cycling accessories");
            EnsureCategory(context, "Sports",            "Sports equipment and accessories");
            EnsureCategory(context, "Hostel Essentials", "Items useful in student accommodation");
            EnsureCategory(context, "Clothing",          "Clothing and wearable items");
            EnsureCategory(context, "Other",             "Other permitted student marketplace items");
            context.SaveChanges();

            // ── 3. Demo accounts (always kept) ──────────────────────────────────
            var admin   = EnsureUser(context, "admin@example.com", "Admin@123", "MU Admin",      UserRole.Admin,   "98765 00001");
            var demo    = EnsureUser(context, "user@example.com",  "User@123",  "Demo Student",  UserRole.Student, "98765 00002");
            context.SaveChanges();

            // ── 4. Extra student sellers (fresh each run) ────────────────────────
            var rahul   = EnsureUser(context, "rahul.patel@marwadi.edu.in",   "Student@123", "Rahul Patel",    UserRole.Student, "91234 56701");
            var priya   = EnsureUser(context, "priya.shah@marwadi.edu.in",    "Student@123", "Priya Shah",     UserRole.Student, "91234 56702");
            var arjun   = EnsureUser(context, "arjun.mehta@marwadi.edu.in",   "Student@123", "Arjun Mehta",    UserRole.Student, "91234 56703");
            var neha    = EnsureUser(context, "neha.joshi@marwadi.edu.in",    "Student@123", "Neha Joshi",     UserRole.Student, "91234 56704");
            var karan   = EnsureUser(context, "karan.verma@marwadi.edu.in",   "Student@123", "Karan Verma",    UserRole.Student, "91234 56705");
            context.SaveChanges();

            // ── 5. Fresh product listings ────────────────────────────────────────
            // ProductCondition: 0=New, 1=LikeNew, 2=Good, 3=Fair
            // ProductStatus: 0=Pending, 1=Approved, 2=Rejected, 3=Sold, 4=Suspended

            // BOOKS
            AddProduct(context, rahul,  "Books", "Engineering Mathematics – B.S. Grewal (44th Ed.)",
                "Barely used, no highlights or underlines. Ideal for 1st-year students. All pages intact.",
                420m, 1, "Main Block Lobby", ProductStatus.Approved, 10);

            AddProduct(context, priya, "Books", "Physics for Engineers – S.L. Gupta",
                "Complete textbook with solved examples. A few pencil marks, easily erased. Good for Sem-1.",
                280m, 2, "Library Entrance", ProductStatus.Approved, 8);

            AddProduct(context, arjun, "Books", "C Programming Language – Dennis Ritchie (2nd Ed.)",
                "Classic reference. No torn pages. Some sticky notes on key chapters — left for your benefit!",
                350m, 2, "Block A Ground Floor", ProductStatus.Approved, 7);

            AddProduct(context, neha, "Books", "Organic Chemistry – Morrison & Boyd",
                "3rd-year Chem textbook. Lightly annotated in a few chapters. Cover slightly worn.",
                500m, 3, "Science Block", ProductStatus.Pending, 1);

            AddProduct(context, karan, "Books", "Data Structures Using C – Reema Thareja",
                "Mint condition, bought last semester but switched course. Unused.",
                320m, 0, "IT Department Lobby", ProductStatus.Approved, 5);

            // ELECTRONICS
            AddProduct(context, demo, "Electronics", "Redmi Note 12 (6 GB / 128 GB) – Moonlight White",
                "11 months old. Always kept with case + screen guard. No scratches. Battery health 94%. Box & bill available.",
                9500m, 1, "Canteen Area", ProductStatus.Approved, 6);

            AddProduct(context, rahul, "Electronics", "HP 15s Laptop – i5 11th Gen, 8 GB RAM, 512 GB SSD",
                "Purchased Jan 2024. Runs smoothly, battery ~5 hrs. Charger + sleeve bag included. No dead pixels.",
                26500m, 2, "MU Campus Gate", ProductStatus.Approved, 4);

            AddProduct(context, arjun, "Electronics", "boAt Airdopes 141 Wireless Earbuds",
                "7 months old. Clear sound, mic works great for calls. Charging case included. Bought for ₹1399.",
                700m, 1, "Boys Hostel C Block", ProductStatus.Approved, 3);

            AddProduct(context, priya, "Electronics", "MI 10000 mAh Power Bank (22.5 W Fast Charge)",
                "About 8 months of use. Charges phones quickly, no cable fraying. Original cable included.",
                600m, 2, "Girls Hostel Reception", ProductStatus.Approved, 9);

            AddProduct(context, karan, "Electronics", "USB 3-Port Hub with SD Card Reader",
                "Brand new, sealed box. Bought two by mistake. Compatible with Windows, Mac and Linux.",
                250m, 0, "IT Block", ProductStatus.Approved, 2);

            // CALCULATORS
            AddProduct(context, demo, "Calculators", "Casio FX-570ES Plus 2nd Edition",
                "Exam-approved scientific calculator. Works perfectly. Hard case included.",
                700m, 1, "Library Entrance", ProductStatus.Approved, 11);

            AddProduct(context, neha, "Calculators", "Casio FX-991EX Classwiz",
                "High-end scientific calculator with spreadsheet feature. Like new – used only for one semester.",
                1050m, 1, "Science Block", ProductStatus.Approved, 6);

            // SPORTS
            AddProduct(context, arjun, "Sports", "SS Ton Cricket Bat (English Willow, Grade 3)",
                "One season old, excellent knocking. Minimal edge damage. Bat cover included. Weight ~1.1 kg.",
                1800m, 2, "Sports Complex", ProductStatus.Approved, 14);

            AddProduct(context, karan, "Sports", "Nivia Force Football (Size 5)",
                "Used for about 3 months on the MU ground. Still holds air well, good grip on the outer surface.",
                400m, 2, "Sports Ground Gate", ProductStatus.Approved, 7);

            AddProduct(context, rahul, "Sports", "Yonex Voltric 1DG Badminton Racquet",
                "Lightweight racquet with fresh strings done last month. Full zip cover included.",
                1100m, 1, "Indoor Badminton Court", ProductStatus.Approved, 5);

            // HOSTEL ESSENTIALS
            AddProduct(context, priya, "Hostel Essentials", "Orient Electric Table Fan (400 mm)",
                "2 years old but motor is strong and silent. 3-speed settings, all working. No oscillation wobble.",
                700m, 2, "Girls Hostel A Wing", ProductStatus.Approved, 20);

            AddProduct(context, demo, "Hostel Essentials", "Prestige Electric Kettle 1.5 L",
                "Stainless steel body, auto shut-off. Used about a year. Heats water in under 3 min. No limescale.",
                450m, 2, "Girls Hostel B Wing", ProductStatus.Approved, 13);

            AddProduct(context, neha, "Hostel Essentials", "Milton Thermosteel Flip Lid Flask 1 L",
                "Keeps hot for 24 hrs. No dents or leaks. Lid locks securely. Great for morning chai.",
                320m, 1, "Girls Hostel C Wing", ProductStatus.Approved, 4);

            // BICYCLES
            AddProduct(context, karan, "Bicycles", "Atlas Cycle (21-Speed Mountain Bike)",
                "New tyres last semester, brakes recently adjusted. Ideal for the stretch between hostels and classrooms.",
                3200m, 2, "Hostel Parking Gate", ProductStatus.Approved, 18);

            // CLOTHING
            AddProduct(context, arjun, "Clothing", "MU Engineering Lab Coat (White, Size L)",
                "One semester of chemistry lab use. Washed and clean. No stains. Lab initials written on tag only.",
                180m, 2, "Block B Ground Floor", ProductStatus.Approved, 9);

            AddProduct(context, rahul, "Clothing", "Campus Hoodie – Marwadi University (Size M, Grey)",
                "Official MU hoodie from freshers fest. Worn twice. Soft fleece inside.",
                350m, 1, "Main Block Lobby", ProductStatus.Pending, 1);

            // FURNITURE
            AddProduct(context, demo, "Furniture", "Folding Study Table with Drawer",
                "Compact table, fits perfectly in a hostel room. Drawer for stationery. No wobble. Easy to fold away.",
                1300m, 2, "Boys Hostel B Block", ProductStatus.Approved, 16);

            // OTHER
            AddProduct(context, priya, "Other", "Drawing Board A1 Size + Drafting Set",
                "1st and 2nd year civil/mech drawing board. Full drafting kit: T-square, set squares, dividers. Good condition.",
                650m, 2, "Civil Dept Lobby", ProductStatus.Approved, 12);

            context.SaveChanges();
        }

        // ────────────────────────────────────────────────────────────────────────
        //  WIPE helper — raw SQL, correct FK order
        // ────────────────────────────────────────────────────────────────────────
        private static void WipeTransactionalData(ApplicationDbContext context)
        {
            // Keep admin@example.com and user@example.com; delete extra students
            // Delete order must respect FK constraints:
            //   CartItems → Carts → Reports → Purchases → Products → extra Users
            var wipeStatements = new[]
            {
                "DELETE FROM dbo.CartItems;",
                "DELETE FROM dbo.Carts;",
                "DELETE FROM dbo.Reports;",
                "DELETE FROM dbo.Purchases;",
                "DELETE FROM dbo.Products;",
                // Remove extra seeded student accounts so they get fresh IDs
                "DELETE FROM dbo.Users WHERE Email NOT IN ('admin@example.com','user@example.com');"
            };

            foreach (var sql in wipeStatements)
            {
                context.Database.ExecuteSqlCommand(sql);
            }
        }

        // ────────────────────────────────────────────────────────────────────────
        //  EnsureCategory – inserts only if name absent
        // ────────────────────────────────────────────────────────────────────────
        private static void EnsureCategory(ApplicationDbContext context, string name, string description)
        {
            if (context.Categories.Any(c => c.CategoryName == name) ||
                context.Categories.Local.Any(c => c.CategoryName == name))
                return;

            context.Categories.Add(new Category
            {
                CategoryName = name,
                Description  = description,
                IsActive     = true,
                CreatedAt    = DateTime.UtcNow
            });
        }

        // ────────────────────────────────────────────────────────────────────────
        //  EnsureUser – upserts (preserves existing password / phone)
        // ────────────────────────────────────────────────────────────────────────
        private static User EnsureUser(
            ApplicationDbContext context,
            string email, string password, string fullName,
            UserRole role, string phoneNumber = null)
        {
            var user = context.Users.FirstOrDefault(u => u.Email == email)
                       ?? context.Users.Local.FirstOrDefault(u => u.Email == email);

            if (user != null)
            {
                if (phoneNumber != null && string.IsNullOrWhiteSpace(user.PhoneNumber))
                    user.PhoneNumber = phoneNumber;
                return user;
            }

            user = new User
            {
                Email        = email,
                FullName     = fullName,
                PhoneNumber  = phoneNumber,
                PasswordHash = PasswordHasher.Hash(password),
                Role         = role,
                Status       = UserStatus.Active,
                CreatedAt    = DateTime.UtcNow
            };
            context.Users.Add(user);
            return user;
        }

        // ────────────────────────────────────────────────────────────────────────
        //  AddProduct – always inserts (wipe already cleared Products table)
        // ────────────────────────────────────────────────────────────────────────
        private static void AddProduct(
            ApplicationDbContext context,
            User seller,
            string categoryName,
            string title,
            string description,
            decimal price,
            byte condition,
            string pickupLocation,
            ProductStatus status,
            int daysAgo)
        {
            var category = context.Categories.FirstOrDefault(c => c.CategoryName == categoryName)
                           ?? context.Categories.Local.FirstOrDefault(c => c.CategoryName == categoryName)
                           ?? context.Categories.FirstOrDefault();
            if (category == null) return;

            context.Products.Add(new Product
            {
                Title            = title,
                Description      = description,
                Price            = price,
                ProductCondition = condition,
                PickupLocation   = pickupLocation,
                Status           = status,
                SellerId         = seller.UserId,
                CategoryId       = category.CategoryId,
                CreatedAt        = DateTime.UtcNow.AddDays(-daysAgo)
            });
        }
    }
}
