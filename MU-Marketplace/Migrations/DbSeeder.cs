using System;
using System.Linq;
using MU_Marketplace.Infrastructure;
using MU_Marketplace.Models;

namespace MU_Marketplace.Migrations
{
    /// <summary>
    /// Idempotent demo data: default categories, two accounts, and sample listings.
    /// Existing rows are never overwritten, so changed passwords or edited items are kept.
    /// </summary>
    internal static class DbSeeder
    {
        public static void Seed(ApplicationDbContext context)
        {
            EnsureCategory(context, "Books", "Textbooks, reference books and study material");
            EnsureCategory(context, "Electronics", "Phones, laptops and other electronic devices");
            EnsureCategory(context, "Calculators", "Scientific and other calculators");
            EnsureCategory(context, "Furniture", "Furniture and room essentials");
            EnsureCategory(context, "Bicycles", "Bicycles and cycling accessories");
            EnsureCategory(context, "Sports", "Sports equipment and accessories");
            EnsureCategory(context, "Hostel Essentials", "Items useful in student accommodation");
            EnsureCategory(context, "Clothing", "Clothing and wearable items");
            EnsureCategory(context, "Other", "Other permitted student marketplace items");
            context.SaveChanges();

            var admin = EnsureUser(context, "admin@example.com", "Admin@123", "MU Admin", UserRole.Admin, "98765 00001");
            var student = EnsureUser(context, "user@example.com", "User@123", "Demo Student", UserRole.Student, "98765 00002");
            context.SaveChanges();

            EnsureProduct(context, student, "Books", "Engineering Mathematics by B.S. Grewal (44th ed.)",
                "Barely used, no highlights. Perfect for first-year maths.", 450m, 1, "Main Block Lobby", ProductStatus.Approved, 1);
            EnsureProduct(context, student, "Calculators", "Casio fx-991ES Plus",
                "Exam-approved calculator, works perfectly. Cover included.", 750m, 1, "Library Entrance", ProductStatus.Approved, 2);
            EnsureProduct(context, student, "Bicycles", "Hercules Roadeo bicycle",
                "21-speed, new tyres last semester. Great for getting around campus.", 3500m, 2, "Hostel Gate", ProductStatus.Approved, 4);
            EnsureProduct(context, student, "Furniture", "Study table with drawer",
                "Compact study table, fits in any hostel room.", 1200m, 2, "Boys Hostel B", ProductStatus.Pending, 0);

            EnsureProduct(context, admin, "Electronics", "HP 15s laptop (i5, 8 GB RAM)",
                "11th-gen i5, 512 GB SSD, battery lasts around 5 hours. Charger included.", 28000m, 1, "MU Campus", ProductStatus.Approved, 3);
            EnsureProduct(context, admin, "Sports", "Yonex badminton racquet",
                "Lightweight racquet with a fresh string job and a full cover.", 900m, 1, "Sports Complex", ProductStatus.Approved, 5);
            EnsureProduct(context, admin, "Hostel Essentials", "Electric kettle 1.5 L",
                "Stainless steel, auto shut-off. Ideal for late-night Maggi.", 450m, 2, "Girls Hostel A", ProductStatus.Approved, 6);
            EnsureProduct(context, admin, "Electronics", "Redmi Note 12 (6/128 GB)",
                "One year old, always used with a case and screen guard. Box and bill available.", 8500m, 1, "Canteen Area", ProductStatus.Approved, 7);

            context.SaveChanges();
        }

        private static void EnsureCategory(ApplicationDbContext context, string name, string description)
        {
            if (context.Categories.Any(c => c.CategoryName == name) ||
                context.Categories.Local.Any(c => c.CategoryName == name))
            {
                return;
            }

            context.Categories.Add(new Category
            {
                CategoryName = name,
                Description = description,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
        }

        private static User EnsureUser(ApplicationDbContext context, string email, string password, string fullName, UserRole role, string phoneNumber = null)
        {
            var user = context.Users.FirstOrDefault(u => u.Email == email)
                       ?? context.Users.Local.FirstOrDefault(u => u.Email == email);
            if (user != null)
            {
                // Back-fill phone if missing
                if (phoneNumber != null && string.IsNullOrWhiteSpace(user.PhoneNumber))
                {
                    user.PhoneNumber = phoneNumber;
                }
                return user;
            }

            user = new User
            {
                Email = email,
                FullName = fullName,
                PhoneNumber = phoneNumber,
                PasswordHash = PasswordHasher.Hash(password),
                Role = role,
                Status = UserStatus.Active,
                CreatedAt = DateTime.UtcNow
            };
            context.Users.Add(user);
            return user;
        }

        private static void EnsureProduct(
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
            if (context.Products.Any(p => p.Title == title && p.SellerId == seller.UserId)) return;

            var category = context.Categories.FirstOrDefault(c => c.CategoryName == categoryName)
                           ?? context.Categories.FirstOrDefault();
            if (category == null) return;

            context.Products.Add(new Product
            {
                Title = title,
                Description = description,
                Price = price,
                ProductCondition = condition,
                PickupLocation = pickupLocation,
                Status = status,
                SellerId = seller.UserId,
                CategoryId = category.CategoryId,
                CreatedAt = DateTime.UtcNow.AddDays(-daysAgo)
            });
        }
    }
}
