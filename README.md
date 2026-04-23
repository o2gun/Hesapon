# ConstruxERP — Offline WPF Desktop Application

Fully offline construction materials ERP built with **.NET 8 + WPF + SQLite**.  
No internet required. All data stored locally at `%AppData%\ConstruxERP\construx.db`.

---

## Complete File Structure

```
ConstruxERP/
│
├── App.xaml                              ← Global styles and resource dictionary
├── App.xaml.cs                           ← Startup: DatabaseContext.Initialize()
├── MainWindow.xaml                       ← Shell: sidebar + ContentControl host
├── MainWindow.xaml.cs                    ← Navigation logic (Tag-based routing)
├── ConstruxERP.csproj                    ← .NET 8 WPF project + NuGet references
│
├── Models/
│   ├── Product.cs                        ← Product entity
│   ├── Customer.cs                       ← Customer entity
│   ├── Sale.cs                           ← Sale transaction entity
│   ├── StockMovement.cs                  ← Stock movement log
│   └── DebtPayment.cs                    ← Debt payment record
│
├── Repositories/
│   ├── DatabaseContext.cs                ← SQLite connection factory + schema migration
│   ├── ProductRepository.cs              ← CRUD + stock adjustment
│   ├── CustomerRepository.cs             ← CRUD + debt adjustment
│   └── SaleRepository.cs                 ← Insert (with transaction) + summary queries
│
├── Services/
│   ├── SaleService.cs                    ← Business logic: validate → create sale
│   ├── InventoryService.cs               ← Business logic: product + stock operations
│   ├── CustomerService.cs                ← Business logic: customer + debt payments
│   ├── ReportService.cs                  ← Report data + Excel/CSV export
│   └── BackupService.cs                  ← Daily auto-backup + manual backup/restore
│
├── Views/
│   ├── DashboardView.xaml / .cs          ← KPI cards, low stock, recent sales, debtors
│   ├── SalesView.xaml / .cs              ← Sales table, search, pagination
│   ├── InventoryView.xaml / .cs          ← Product table, low-stock indicators
│   ├── CustomersView.xaml / .cs          ← Customer list, debt chips, pay button
│   ├── ReportsView.xaml / .cs            ← Bar charts, category breakdown, export
│   ├── DebtReportsView.xaml / .cs        ← All customers with outstanding debt
│   └── BackupView.xaml / .cs             ← List backups, create, restore, delete
│
└── Dialogs/
    ├── AddSaleDialog.xaml / .cs          ← New sale form with auto-calc totals
    ├── AddEditProductDialog.xaml / .cs   ← Add / edit product form
    ├── AddEditCustomerDialog.xaml / .cs  ← Add / edit customer form
    └── RecordPaymentDialog.xaml / .cs    ← Record a debt payment
```

---

## Quick Start

### Prerequisites
- Windows 10 or 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Visual Studio 2022 (Community or higher) **or** JetBrains Rider

### Build & Run

```bash
# 1. Unzip / clone the project
cd ConstruxERP

# 2. Restore NuGet packages
dotnet restore

# 3. Build
dotnet build

# 4. Run
dotnet run
```

The SQLite database is created automatically at:
```
%AppData%\ConstruxERP\construx.db
```

Backups are stored at:
```
%AppData%\ConstruxERP\backups\backup_YYYY_MM_DD_HHmmss.db
```

---

## NuGet Packages

| Package | Version | Purpose |
|---|---|---|
| `Microsoft.Data.Sqlite` | 8.0.0 | SQLite database (fully offline) |
| `EPPlus` | 7.2.0 | Excel (.xlsx) export |
| `CsvHelper` | 33.0.1 | CSV export |

---

## Database Schema

```sql
customers       id, name, phone, email, address, total_debt, created_at
products        id, name, category, unit, purchase_price, sale_price,
                stock_qty, min_stock, supplier_name, sku, notes, created_at, updated_at
sales           id, customer_id, product_id, qty, unit_price, total_price,
                amount_paid, remaining_debt, payment_type, sale_date
stock_movements id, product_id, qty_change, reason, reference, moved_at
debt_payments   id, customer_id, sale_id, amount, paid_at, notes
```

---

## Architecture

```
┌─────────────────────────────────────────┐
│         WPF Views (XAML + code-behind)  │
│  Dashboard · Sales · Inventory          │
│  Customers · Reports · Backup · Dialogs │
└──────────────────┬──────────────────────┘
                   │
┌──────────────────▼──────────────────────┐
│            Services (Business Logic)    │
│  SaleService · InventoryService         │
│  CustomerService · ReportService        │
│  BackupService                          │
└──────────────────┬──────────────────────┘
                   │
┌──────────────────▼──────────────────────┐
│         Repositories (Data Access)      │
│  ProductRepository · SaleRepository     │
│  CustomerRepository · DatabaseContext   │
└──────────────────┬──────────────────────┘
                   │
┌──────────────────▼──────────────────────┐
│    SQLite — construx.db (local file)    │
└─────────────────────────────────────────┘
```

---

## Key Features

| Feature | Location |
|---|---|
| Live KPI dashboard | `DashboardView` |
| Full sales CRUD with debt tracking | `SalesView` + `AddSaleDialog` |
| Low-stock warning (red indicator) | `InventoryView` |
| Customer debt management + payments | `CustomersView` + `RecordPaymentDialog` |
| Top products bar chart | `ReportsView` |
| Excel + CSV export | `ReportsView` → `ReportService` |
| Automatic daily backup | `App.xaml.cs` → `BackupService` |
| Manual backup / restore | `BackupView` |
| All units supported | `AddEditProductDialog` ComboBox |
