using System.Data.Entity;

namespace MU_Marketplace.Models
{
    public class ApplicationDbContext : DbContext
    {
        // Connection string name must exist in Web.config
        public ApplicationDbContext() : base("DefaultConnection") { }

        public DbSet<User> Users { get; set; }             // Developer 1
        public DbSet<Product> Products { get; set; }       // Developer 2
        public DbSet<Category> Categories { get; set; }    // Developer 2
        public DbSet<Purchase> Purchases { get; set; }     // Developer 2
        // public DbSet<Report> Reports { get; set; }      // Developer 3

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ---- Developer 2 relationships ----

            // Users -> Products (seller): CASCADE
            modelBuilder.Entity<Product>()
                .HasRequired(p => p.Seller)
                .WithMany()
                .HasForeignKey(p => p.SellerId)
                .WillCascadeOnDelete(true);

            // Categories -> Products: RESTRICT
            modelBuilder.Entity<Product>()
                .HasRequired(p => p.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryId)
                .WillCascadeOnDelete(false);

            // Products -> Purchases: RESTRICT
            modelBuilder.Entity<Purchase>()
                .HasRequired(p => p.Product)
                .WithMany(p => p.Purchases)
                .HasForeignKey(p => p.ProductId)
                .WillCascadeOnDelete(false);

            // Users -> Purchases (buyer): RESTRICT (avoids multiple cascade paths)
            modelBuilder.Entity<Purchase>()
                .HasRequired(p => p.Buyer)
                .WithMany()
                .HasForeignKey(p => p.BuyerId)
                .WillCascadeOnDelete(false);

            // DECIMAL(18,2)
            modelBuilder.Entity<Product>().Property(p => p.Price).HasPrecision(18, 2);
            modelBuilder.Entity<Purchase>().Property(p => p.AmountPaid).HasPrecision(18, 2);
        }
    }
}
