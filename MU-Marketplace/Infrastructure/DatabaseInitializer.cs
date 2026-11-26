using System;
using System.Configuration;
using System.Data.SqlClient;
using MU_Marketplace.Migrations;
using MU_Marketplace.Models;

namespace MU_Marketplace.Infrastructure
{
    public static class DatabaseInitializer
    {
        public static void EnsureDatabaseAndSeed()
        {
            string connStr = ConfigurationManager.ConnectionStrings["MarketplaceConnection"].ConnectionString;
            var builder = new SqlConnectionStringBuilder(connStr);
            string dbName = string.IsNullOrWhiteSpace(builder.InitialCatalog) ? "MUMarketplaceDB" : builder.InitialCatalog;

            // 1. Ensure database exists on master
            var masterBuilder = new SqlConnectionStringBuilder(connStr) { InitialCatalog = "master" };
            using (var masterCon = new SqlConnection(masterBuilder.ConnectionString))
            {
                masterCon.Open();
                using (var cmd = new SqlCommand(
                    "IF DB_ID(@db) IS NULL BEGIN DECLARE @sql NVARCHAR(MAX) = N'CREATE DATABASE [' + REPLACE(@db, N']', N']]') + N']'; EXEC(@sql); END;",
                    masterCon))
                {
                    cmd.Parameters.AddWithValue("@db", dbName);
                    cmd.ExecuteNonQuery();
                }
            }

            // 2. Ensure all 7 tables from MUMarketplaceDB schema exist (idempotent)
            using (var con = new SqlConnection(connStr))
            {
                con.Open();
                foreach (string stmt in SchemaBatches)
                {
                    using (var cmd = new SqlCommand(stmt, con))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
            }

            // 3. Seed default categories, demo accounts & sample products
            using (var db = new ApplicationDbContext())
            {
                DbSeeder.Seed(db);
            }
        }

        private static readonly string[] SchemaBatches = new[]
        {
            @"IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.Users
                (
                    UserId              INT IDENTITY(1,1) NOT NULL,
                    FullName            NVARCHAR(100) NOT NULL,
                    Email               NVARCHAR(150) NOT NULL,
                    EnrollmentNo        NVARCHAR(30) NULL,
                    Department          NVARCHAR(100) NULL,
                    PhoneNumber         NVARCHAR(20) NULL,
                    PasswordHash        NVARCHAR(255) NOT NULL,
                    [Role]              TINYINT NOT NULL CONSTRAINT DF_Users_Role DEFAULT (0),
                    [Status]            TINYINT NOT NULL CONSTRAINT DF_Users_Status DEFAULT (0),
                    ResetTokenHash      NVARCHAR(255) NULL,
                    ResetTokenExpiry    DATETIME2(0) NULL,
                    CreatedAt           DATETIME2(0) NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT (SYSUTCDATETIME()),
                    UpdatedAt           DATETIME2(0) NULL,
                    LastLoginAt         DATETIME2(0) NULL,
                    CONSTRAINT PK_Users PRIMARY KEY CLUSTERED (UserId),
                    CONSTRAINT UQ_Users_Email UNIQUE (Email),
                    CONSTRAINT CK_Users_Role CHECK ([Role] IN (0, 1)),
                    CONSTRAINT CK_Users_Status CHECK ([Status] IN (0, 1))
                );
                CREATE UNIQUE INDEX UX_Users_EnrollmentNo ON dbo.Users(EnrollmentNo) WHERE EnrollmentNo IS NOT NULL;
            END;",

            @"IF OBJECT_ID(N'dbo.Categories', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.Categories
                (
                    CategoryId      INT IDENTITY(1,1) NOT NULL,
                    CategoryName    NVARCHAR(100) NOT NULL,
                    [Description]   NVARCHAR(500) NULL,
                    IsActive        BIT NOT NULL CONSTRAINT DF_Categories_IsActive DEFAULT (1),
                    CreatedAt       DATETIME2(0) NOT NULL CONSTRAINT DF_Categories_CreatedAt DEFAULT (SYSUTCDATETIME()),
                    UpdatedAt       DATETIME2(0) NULL,
                    CONSTRAINT PK_Categories PRIMARY KEY CLUSTERED (CategoryId),
                    CONSTRAINT UQ_Categories_CategoryName UNIQUE (CategoryName)
                );
            END;",

            @"IF OBJECT_ID(N'dbo.Products', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.Products
                (
                    ProductId           INT IDENTITY(1,1) NOT NULL,
                    Title               NVARCHAR(150) NOT NULL,
                    [Description]       NVARCHAR(2000) NOT NULL,
                    Price               DECIMAL(18,2) NOT NULL,
                    ProductCondition    TINYINT NOT NULL,
                    ImageData           VARBINARY(MAX) NULL,
                    ImageContentType    NVARCHAR(100) NULL,
                    PickupLocation      NVARCHAR(150) NOT NULL,
                    [Status]            TINYINT NOT NULL CONSTRAINT DF_Products_Status DEFAULT (0),
                    SellerId            INT NOT NULL,
                    CategoryId          INT NOT NULL,
                    AdminNotes          NVARCHAR(500) NULL,
                    ReviewedBy          INT NULL,
                    ReviewedAt          DATETIME2(0) NULL,
                    CreatedAt           DATETIME2(0) NOT NULL CONSTRAINT DF_Products_CreatedAt DEFAULT (SYSUTCDATETIME()),
                    UpdatedAt           DATETIME2(0) NULL,
                    CONSTRAINT PK_Products PRIMARY KEY CLUSTERED (ProductId),
                    CONSTRAINT CK_Products_Price CHECK (Price > 0),
                    CONSTRAINT CK_Products_Condition CHECK (ProductCondition IN (0, 1, 2, 3)),
                    CONSTRAINT CK_Products_Status CHECK ([Status] IN (0, 1, 2, 3, 4)),
                    CONSTRAINT CK_Products_ImageContentType CHECK (ImageContentType IS NULL OR ImageContentType IN (N'image/jpeg', N'image/png', N'image/webp')),
                    CONSTRAINT CK_Products_ImagePair CHECK ((ImageData IS NULL AND ImageContentType IS NULL) OR (ImageData IS NOT NULL AND ImageContentType IS NOT NULL)),
                    CONSTRAINT FK_Products_Seller FOREIGN KEY (SellerId) REFERENCES dbo.Users(UserId),
                    CONSTRAINT FK_Products_Category FOREIGN KEY (CategoryId) REFERENCES dbo.Categories(CategoryId),
                    CONSTRAINT FK_Products_ReviewedBy FOREIGN KEY (ReviewedBy) REFERENCES dbo.Users(UserId)
                );
                CREATE INDEX IX_Products_Marketplace ON dbo.Products([Status], CategoryId, Price) INCLUDE (Title, ProductCondition, CreatedAt, SellerId);
                CREATE INDEX IX_Products_Seller_Status ON dbo.Products(SellerId, [Status], CreatedAt DESC);
            END;",

            @"IF OBJECT_ID(N'dbo.Carts', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.Carts
                (
                    CartId      INT IDENTITY(1,1) NOT NULL,
                    UserId      INT NOT NULL,
                    CreatedAt   DATETIME2(0) NOT NULL CONSTRAINT DF_Carts_CreatedAt DEFAULT (SYSUTCDATETIME()),
                    UpdatedAt   DATETIME2(0) NULL,
                    CONSTRAINT PK_Carts PRIMARY KEY CLUSTERED (CartId),
                    CONSTRAINT UQ_Carts_UserId UNIQUE (UserId),
                    CONSTRAINT FK_Carts_User FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId)
                );
            END;",

            @"IF OBJECT_ID(N'dbo.CartItems', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.CartItems
                (
                    CartItemId  INT IDENTITY(1,1) NOT NULL,
                    CartId      INT NOT NULL,
                    ProductId   INT NOT NULL,
                    AddedAt     DATETIME2(0) NOT NULL CONSTRAINT DF_CartItems_AddedAt DEFAULT (SYSUTCDATETIME()),
                    CONSTRAINT PK_CartItems PRIMARY KEY CLUSTERED (CartItemId),
                    CONSTRAINT UQ_CartItems_Cart_Product UNIQUE (CartId, ProductId),
                    CONSTRAINT FK_CartItems_Cart FOREIGN KEY (CartId) REFERENCES dbo.Carts(CartId),
                    CONSTRAINT FK_CartItems_Product FOREIGN KEY (ProductId) REFERENCES dbo.Products(ProductId)
                );
                CREATE INDEX IX_CartItems_ProductId ON dbo.CartItems(ProductId);
            END;",

            @"IF OBJECT_ID(N'dbo.Purchases', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.Purchases
                (
                    PurchaseId     INT IDENTITY(1,1) NOT NULL,
                    ProductId      INT NOT NULL,
                    BuyerId        INT NOT NULL,
                    PurchasePrice  DECIMAL(18,2) NOT NULL,
                    PurchaseDate   DATETIME2(0) NOT NULL CONSTRAINT DF_Purchases_PurchaseDate DEFAULT (SYSUTCDATETIME()),
                    CONSTRAINT PK_Purchases PRIMARY KEY CLUSTERED (PurchaseId),
                    CONSTRAINT UQ_Purchases_ProductId UNIQUE (ProductId),
                    CONSTRAINT CK_Purchases_PurchasePrice CHECK (PurchasePrice > 0),
                    CONSTRAINT FK_Purchases_Product FOREIGN KEY (ProductId) REFERENCES dbo.Products(ProductId),
                    CONSTRAINT FK_Purchases_Buyer FOREIGN KEY (BuyerId) REFERENCES dbo.Users(UserId)
                );
                CREATE INDEX IX_Purchases_Buyer_Date ON dbo.Purchases(BuyerId, PurchaseDate DESC);
            END;",

            @"IF OBJECT_ID(N'dbo.Reports', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.Reports
                (
                    ReportId       INT IDENTITY(1,1) NOT NULL,
                    ProductId      INT NOT NULL,
                    ReporterId     INT NOT NULL,
                    Reason         NVARCHAR(500) NOT NULL,
                    [Status]       TINYINT NOT NULL CONSTRAINT DF_Reports_Status DEFAULT (0),
                    AdminNotes     NVARCHAR(500) NULL,
                    ReviewedBy     INT NULL,
                    ReviewedAt     DATETIME2(0) NULL,
                    CreatedAt      DATETIME2(0) NOT NULL CONSTRAINT DF_Reports_CreatedAt DEFAULT (SYSUTCDATETIME()),
                    CONSTRAINT PK_Reports PRIMARY KEY CLUSTERED (ReportId),
                    CONSTRAINT CK_Reports_Status CHECK ([Status] IN (0, 1, 2)),
                    CONSTRAINT FK_Reports_Product FOREIGN KEY (ProductId) REFERENCES dbo.Products(ProductId),
                    CONSTRAINT FK_Reports_Reporter FOREIGN KEY (ReporterId) REFERENCES dbo.Users(UserId),
                    CONSTRAINT FK_Reports_ReviewedBy FOREIGN KEY (ReviewedBy) REFERENCES dbo.Users(UserId)
                );
                CREATE INDEX IX_Reports_Status_CreatedAt ON dbo.Reports([Status], CreatedAt DESC);
                CREATE INDEX IX_Reports_ProductId ON dbo.Reports(ProductId);
            END;"
        };
    }
}
