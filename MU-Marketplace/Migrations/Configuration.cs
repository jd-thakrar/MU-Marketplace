using System.Data.Entity.Migrations;
using MU_Marketplace.Models;

namespace MU_Marketplace.Migrations
{
    internal sealed class Configuration : DbMigrationsConfiguration<ApplicationDbContext>
    {
        public Configuration()
        {
            AutomaticMigrationsEnabled = false;
        }

        protected override void Seed(ApplicationDbContext context)
        {
            // ---- Developer 2: default categories ----
            context.Categories.AddOrUpdate(c => c.CategoryName,
                new Category { CategoryName = "Books" },
                new Category { CategoryName = "Scientific Calculators" },
                new Category { CategoryName = "Laptops & Electronics" },
                new Category { CategoryName = "Mobile Phones" },
                new Category { CategoryName = "Bicycles" },
                new Category { CategoryName = "Furniture" },
                new Category { CategoryName = "Sports Equipment" },
                new Category { CategoryName = "Hostel Accessories" },
                new Category { CategoryName = "Other Student Items" }
            );
        }
    }
}
