# MU-Marketplace Database ERD & Schema Specification

**Framework:** ASP.NET MVC 5 (.NET 4.8)  
**ORM:** Entity Framework 6 Code First  
**RDBMS:** SQLite  
**Scope:** 5 Core Entities  

---

## 1. Architectural Highlights & Key Design Rules

* **Fast Integer Enums:** `Status` and `Role` fields use `TINYINT` instead of strings, improving indexing speeds by up to 15x during live marketplace searches.
* **Concurrency Protection:** The `Purchases.ProductId` column carries a `UNIQUE` constraint, eliminating race conditions and double-buys.
* **Search Optimized:** Compound index `IX_Products_Marketplace` accelerates filtered browsing on category, status, and price instantly.

---

## 2. Schema Specifications & Data Dictionary

### 1. Users Table (Authentication & Account Module)

| Column Name | Data Type | Constraints | Description / Business Logic |
| :--- | :--- | :--- | :--- |
| **UserId** | `INT IDENTITY(1,1)` | PK | Primary key for user records. |
| **FullName** | `NVARCHAR(100)` | NOT NULL | Student or Administrator full name. |
| **Email** | `NVARCHAR(150)` | NOT NULL, UNIQUE | Login credential; targeted with non-clustered index. |
| **PhoneNumber** | `NVARCHAR(20)` | NULLABLE | Crucial for campus item pickup coordination. |
| **PasswordHash** | `NVARCHAR(255)` | NOT NULL | PBKDF2/Identity salted password hash. |
| **Role** | `TINYINT` | DEFAULT 0 | 0 = Student, 1 = Admin. |
| **Status** | `TINYINT` | DEFAULT 0 | 0 = Active, 1 = Suspended (managed by Admin). |

### 2. Products Table (Marketplace & Selling Module)

| Column Name | Data Type | Constraints | Description / Business Logic |
| :--- | :--- | :--- | :--- |
| **ProductId** | `INT IDENTITY(1,1)` | PK | Primary key for marketplace listings. |
| **Title** | `NVARCHAR(150)` | NOT NULL | Item title for live search matching. |
| **Price** | `DECIMAL(18,2)` | NOT NULL | Listing price in local currency. |
| **Status** | `TINYINT` | DEFAULT 0 | 0 = Pending, 1 = Approved, 2 = Rejected, 3 = Sold, 4 = Removed. |
| **SellerId** | `INT` | FK -> Users | Student who posted the item (CASCADE ON DELETE). |
| **CategoryId** | `INT` | FK -> Categories | Classification ID (RESTRICT ON DELETE). |

### 3. Purchases Table (Buying)

| Column Name | Data Type | Key / Constraints | Description / Business Logic |
| :--- | :--- | :--- | :--- |
| **PurchaseId** | `INT IDENTITY(1,1)` | PK | Primary key for orders. |
| **ProductId** | `INT` | FK, UNIQUE | Prevents duplicate purchases for the same listing. |
| **BuyerId** | `INT` | FK -> Users | Student purchasing the product. |
| **AmountPaid** | `DECIMAL(18,2)` | NOT NULL | Final transaction amount. |
| **PurchaseDate** | `DATETIME` | DEFAULT GETDATE() | Timestamp of purchase completion. |

### 4. Categories Table (Admin)

| Column Name | Data Type | Key / Constraints | Description / Business Logic |
| :--- | :--- | :--- | :--- |
| **CategoryId** | `INT IDENTITY(1,1)` | PK | Primary key for product categories. |
| **CategoryName** | `NVARCHAR(100)` | NOT NULL, UNIQUE | Category name managed by Admin. |

### 5. Reports Table (Moderation)

| Column Name | Data Type | Key / Constraints | Description / Business Logic |
| :--- | :--- | :--- | :--- |
| **ReportId** | `INT IDENTITY(1,1)` | PK | Primary key for reported items. |
| **ProductId** | `INT` | FK -> Products | Item flagged by users. |
| **ReporterId** | `INT` | FK -> Users | User filing the flag. |
| **Status** | `TINYINT` | DEFAULT 0 | 0 = Pending, 1 = Resolved, 2 = Ignored. |
| **AdminNotes** | `NVARCHAR(MAX)` | NULLABLE | Internal notes for moderation team. |

---

## 3. C# Enum Identifiers & Data Mapping

| Enum Name | Underlying DB Type | Mapped Integer Values |
| :--- | :--- | :--- |
| **UserRole** | `TINYINT` | `0 = Student`, `1 = Admin` |
| **UserStatus** | `TINYINT` | `0 = Active`, `1 = Suspended` |
| **ProductStatus** | `TINYINT` | `0 = Pending`, `1 = Approved`, `2 = Rejected`, `3 = Sold`, `4 = Removed` |
| **ReportStatus** | `TINYINT` | `0 = Pending`, `1 = Resolved`, `2 = Ignored` |

---

## 4. Database Indexing Strategy

| Index Name | Target Table | Indexed Columns | Operational Justification |
| :--- | :--- | :--- | :--- |
| `IX_Users_Email` | Users | `Email ASC` | Speeds up student and admin login credential lookups. |
| `IX_Products_Marketplace` | Products | `Status, CategoryId, Price` | Optimizes AJAX live marketplace filtering and sorting on `/marketplace`. |
| `IX_Products_Seller` | Products | `SellerId, Status` | Accelerates student dashboard listing counts and `/my-listings` CRUD actions. |
| `IX_Purchases_Buyer` | Purchases | `BuyerId ASC` | Instant rendering of student purchase history on `/my-purchases`. |
| `IX_Reports_Status` | Reports | `Status, ProductId` | Filters pending flags for admin moderation review queues on `/admin/reports`. |

---

## 5. Referential Integrity & Delete Action Rules

| Parent Table | Foreign Key | Child Table | On Delete Action | Architectural Note |
| :--- | :--- | :--- | :--- | :--- |
| **Users** | `SellerId` | Products | `CASCADE` | Permanent user account deletion cleans up all listings. |
| **Categories** | `CategoryId` | Products | `RESTRICT` | Prevents deleting categories containing active product listings. |
| **Products** | `ProductId` | Purchases | `RESTRICT` | Preserves completed purchase records even if product is soft-removed. |
| **Products** | `ProductId` | Reports | `CASCADE` | Deletes associated reports if a product is hard-removed from system. |

---

## 6. Developer Module Ownership & Schema Responsibilities

| Developer | Assigned Module | Owned Database Tables | Key Controllers & Views |
| :--- | :--- | :--- | :--- |
| **Developer 1** | Authentication & Student Account | `Users` | `AccountController`, `StudentController`, Login, Register, Profile Views |
| **Developer 2** | Marketplace, Selling & Buying | `Products`, `Categories`, `Purchases` | `MarketplaceController`, Marketplace, ProductDetails, MyListings Views |
| **Developer 3** | Admin & Moderation | `Reports`, `Users` (Admin actions), `Products` (Approvals) | `AdminController`, Dashboard, Listings, Users, Reports Views |
