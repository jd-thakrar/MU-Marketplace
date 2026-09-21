# 🛒 MU-Marketplace

### Marwadi University Campus Marketplace

MU-Marketplace is a web-based campus marketplace designed specifically for students of **Marwadi University**.

The platform allows students to browse products listed by other students, purchase items they need, and optionally list their own used products for sale. Every new product listing is reviewed and approved by an administrator before it becomes visible to other students.

---

## 🎯 Objective

The main objective of MU-Marketplace is to provide a simple and organized platform for students to buy and sell pre-owned items within the university community.

Examples include:

- 📚 Books
- 🧮 Scientific Calculators
- 💻 Laptops & Electronics
- 📱 Mobile Phones
- 🚲 Bicycles
- 🪑 Furniture
- 🏸 Sports Equipment
- 🎒 Hostel Accessories
- 📦 Other Student Items

---

## ✨ Core Features

### 👨‍🎓 Student

- Student registration and login
- Student dashboard
- Browse approved marketplace products
- Search and filter products
- View product details
- Purchase available products
- View purchase history
- Create product listings
- Manage personal listings
- Report inappropriate or problematic products
- Manage profile

### 🛡️ Administrator

- Secure admin login
- Admin dashboard
- Review submitted product listings
- Approve or reject listings
- Monitor student accounts
- Review reported products
- Take appropriate action on reported listings
- Manage product categories

---

## 🔄 Marketplace Flow

```text
Student Registers
       │
       ▼
    Login
       │
       ▼
Student Dashboard
       │
       ├──────────────► Browse Marketplace
       │                       │
       │                       ▼
       │                 Product Details
       │                       │
       │                       ▼
       │                    Purchase
       │
       └──────────────► List Product
                              │
                              ▼
                        Admin Review
                         │        │
                      Approve   Reject
                         │
                         ▼
                    Marketplace
                         │
                         ▼
                      Purchase
                         │
                         ▼
                   Product Sold
```

Approved products are visible in the marketplace. Once a product is purchased, it is marked as sold and is no longer available for purchase.

---

## 🏗️ Technology Stack

| Layer | Technology |
|---|---|
| Frontend | HTML, CSS, JavaScript |
| Backend | ASP.NET MVC 5 |
| Framework | .NET Framework |
| Language | C# |
| Database | SQL Server |
| IDE | Visual Studio |
| Version Control | Git & GitHub |

---

## 📁 Project Structure

```text
MU-Marketplace
│
├── App_Start
│   ├── BundleConfig.cs
│   └── RouteConfig.cs
│
├── Controllers
│   ├── HomeController.cs
│   ├── AccountController.cs
│   ├── StudentController.cs
│   ├── MarketplaceController.cs
│   └── AdminController.cs
│
├── Models
│   ├── User.cs
│   ├── Product.cs
│   ├── Category.cs
│   ├── Purchase.cs
│   └── Report.cs
│
├── Views
│   ├── Account
│   ├── Admin
│   ├── Home
│   ├── Marketplace
│   ├── Shared
│   └── Student
│
├── Content
│   └── Site.css
│
├── Scripts
│   └── Site.js
│
├── App_Data
├── Global.asax
└── Web.config
```

The project follows the **Model-View-Controller (MVC)** architecture, where controllers handle requests, models represent application data, and views provide the user interface.

---

## 👥 Development Team

The project is being developed collaboratively by three developers.

| Developer | Module |
|---|---|
| Developer 1 | Authentication & Student Module |
| Developer 2 | Marketplace, Selling & Buying |
| Developer 3 | Admin & Moderation |

Each developer is responsible for the frontend, backend, and database work related to their assigned module.

---

## 🌿 Git Workflow

The project uses Git and GitHub for collaborative development.

The `main` branch contains the integrated project.

Developers work on separate feature branches:

```text
main
 │
 ├── feature/developer-1
 ├── feature/developer-2
 └── feature/developer-3
```

General workflow:

```text
Create Branch
      ↓
Develop Feature
      ↓
Test Locally
      ↓
Commit Changes
      ↓
Push Branch
      ↓
Pull Request
      ↓
Review & Merge
```

This allows all three developers to work on the same repository while keeping the main project stable.

---

## 🚀 Getting Started

### Prerequisites

- Visual Studio
- .NET Framework
- SQL Server
- Git

### Clone the Repository

```bash
git clone https://github.com/jd-thakrar/MU-Marketplace.git
```

Open the project in Visual Studio and restore the required packages.

The database configuration and setup instructions will be added once the database schema is finalized.

---

## 🎓 Project Context

**MU-Marketplace** is developed as a mini project for **Marwadi University** using ASP.NET MVC and .NET Framework.

The project focuses on learning and applying:

- MVC architecture
- C# and ASP.NET MVC
- Database integration
- CRUD operations
- Authentication and authorization
- Git-based collaborative development
- Modular software development

---

## 🚧 Project Status

**Currently:** Project architecture and initial MVC structure completed.

### Next Steps

- Finalize database design
- Connect SQL Server database
- Implement authentication
- Develop student module
- Develop marketplace module
- Develop admin module
- Integrate all modules
- Testing and final deployment

---

### Built for the campus. Designed for students. 🚀

**MU-Marketplace — A student marketplace for Marwadi University.**
