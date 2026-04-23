using ConstruxERP.Services;
using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ConstruxERP.Views
{
    public partial class DashboardView : UserControl
    {
        private readonly SaleService      _saleService      = new();
        private readonly InventoryService _inventoryService = new();
        private readonly CustomerService  _customerService  = new();

        public DashboardView()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e) => LoadData();

        public void LoadData()
        {
            TxtDate.Text = DateTime.Now.ToString("dddd, MMMM d, yyyy — Overview");

            // KPI cards
            TxtTodaySales.Text   = _saleService.GetTodayTotal().ToString("C", new System.Globalization.CultureInfo("tr-TR"));
            TxtMonthlySales.Text = _saleService.GetMonthlyTotal().ToString("C", new System.Globalization.CultureInfo("tr-TR"));
            TxtActiveOrders.Text = _saleService.GetTodayOrders().ToString();

            var debtors   = _customerService.GetDebtors();
            decimal total = _customerService.GetTotalOutstandingDebt();
            TxtPendingDebt.Text = total.ToString("C", new CultureInfo("tr-TR"));
            TxtDebtors.Text     = $"{debtors.Count} Müşteri";

            // Low stock warnings
            var lowStock = _inventoryService.GetLowStockProducts();
            TxtLowStockCount.Text = $"{lowStock.Count} ürün{(lowStock.Count == 1 ? "" : "s")}";

            if (lowStock.Count == 0)
            {
                TxtNoLowStock.Visibility = Visibility.Visible;
                LowStockList.ItemsSource  = null;
            }
            else
            {
                TxtNoLowStock.Visibility = Visibility.Collapsed;
                LowStockList.ItemsSource = lowStock
                    .Select(p => new
                    {
                        p.Name,
                        p.Category,
                        StockDisplay = $"{p.StockQty} / {p.MinStock}"
                    })
                    .ToList();
            }

            // Recent sales (last 8)
            var sales = _saleService.GetSales(page: 1, pageSize: 8);
            RecentSalesList.ItemsSource = sales
                .Select(s => new
                {
                    s.CustomerName,
                    s.ProductName,
                    SaleDate     = s.SaleDate.Length >= 10 ? s.SaleDate[..10] : s.SaleDate,
                    TotalDisplay = s.TotalPrice.ToString("C", new System.Globalization.CultureInfo("tr-TR")),
                    StatusLabel  = s.RemainingDebt == 0 ? "Ödendi"
                                 : s.AmountPaid   == 0 ? "Ödenmedi" : "Taksitli",
                    StatusBg     = s.RemainingDebt == 0 ? "#DCFCE7"
                                 : s.AmountPaid   == 0 ? "#FEE2E2" : "#FEF3C7",
                    StatusFg     = s.RemainingDebt == 0 ? "#059669"
                                 : s.AmountPaid   == 0 ? "#DC2626" : "#D97706"
                })
                .ToList();

            // Outstanding debtors list
            if (debtors.Count == 0)
            {
                TxtNoDebt.Visibility = Visibility.Visible;
                DebtList.ItemsSource  = null;
            }
            else
            {
                TxtNoDebt.Visibility = Visibility.Collapsed;
                DebtList.ItemsSource = debtors
                    .Select(c => new { c.Name, DebtDisplay = c.TotalDebt.ToString("C", new CultureInfo("tr-TR")) })
                    .ToList();
            }
        }
        
        private void BtnNewSale_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Dialogs.AddSaleDialog { Owner = Window.GetWindow(this) };
            if (dlg.ShowDialog() == true)
                LoadData();
        }
    }
}
