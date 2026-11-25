using System.Data.Entity;

namespace MU_Marketplace.Models
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext() : base("DefaultConnection") { }

        public DbSet<User> Users { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Purchase> Purchases { get; set; }
        public DbSet<Report> Reports { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Users -> Products (seller)
            modelBuilder.Entity<Product>()
                .HasRequired(p => p.Seller)
                .WithMany()
                .HasForeignKey(p => p.SellerId)
                .WillCascadeOnDelete(false);

            // Users -> Products (reviewer)
            modelBuilder.Entity<Product>()
                .HasOptional(p => p.Reviewer)
                .WithMany()
                .HasForeignKey(p => p.ReviewedBy)
                .WillCascadeOnDelete(false);

            // Categories -> Products
            modelBuilder.Entity<Product>()
                .HasRequired(p => p.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryId)
                .WillCascadeOnDelete(false);

            // Products -> Purchases
            modelBuilder.Entity<Purchase>()
                .HasRequired(p => p.Product)
                .WithMany(p => p.Purchases)
                .HasForeignKey(p => p.ProductId)
                .WillCascadeOnDelete(false);

            // Users -> Purchases (buyer)
            modelBuilder.Entity<Purchase>()
                .HasRequired(p => p.Buyer)
                .WithMany()
                .HasForeignKey(p => p.BuyerId)
                .WillCascadeOnDelete(false);

            // Products -> Reports
            modelBuilder.Entity<Report>()
                .HasRequired(r => r.Product)
                .WithMany(p => p.Reports)
                .HasForeignKey(r => r.ProductId)
                .WillCascadeOnDelete(false);

            // Users -> Reports (reporter)
            modelBuilder.Entity<Report>()
                .HasRequired(r => r.Reporter)
                .WithMany()
                .HasForeignKey(r => r.ReporterId)
                .WillCascadeOnDelete(false);

            // Users -> Reports (reviewer)
            modelBuilder.Entity<Report>()
                .HasOptional(r => r.Reviewer)
                .WithMany()
                .HasForeignKey(r => r.ReviewedBy)
                .WillCascadeOnDelete(false);

            // DECIMAL(18,2)
            modelBuilder.Entity<Product>().Property(p => p.Price).HasPrecision(18, 2);
            modelBuilder.Entity<Purchase>().Property(p => p.PurchasePrice).HasPrecision(18, 2);

            // DATETIME2 columns to match MUMarketplaceDB schema
            modelBuilder.Entity<User>().Property(u => u.ResetTokenExpiry).HasColumnType("datetime2");
            modelBuilder.Entity<User>().Property(u => u.CreatedAt).HasColumnType("datetime2");
            modelBuilder.Entity<User>().Property(u => u.UpdatedAt).HasColumnType("datetime2");
            modelBuilder.Entity<User>().Property(u => u.LastLoginAt).HasColumnType("datetime2");

            modelBuilder.Entity<Category>().Property(c => c.CreatedAt).HasColumnType("datetime2");
            modelBuilder.Entity<Category>().Property(c => c.UpdatedAt).HasColumnType("datetime2");

            modelBuilder.Entity<Product>().Property(p => p.ReviewedAt).HasColumnType("datetime2");
            modelBuilder.Entity<Product>().Property(p => p.CreatedAt).HasColumnType("datetime2");
            modelBuilder.Entity<Product>().Property(p => p.UpdatedAt).HasColumnType("datetime2");

            modelBuilder.Entity<Purchase>().Property(p => p.PurchaseDate).HasColumnType("datetime2");

            modelBuilder.Entity<Report>().Property(r => r.ReviewedAt).HasColumnType("datetime2");
            modelBuilder.Entity<Report>().Property(r => r.CreatedAt).HasColumnType("datetime2");
        }
    }
}
