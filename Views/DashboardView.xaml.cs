using ConstruxERP.Services;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ConstruxERP.Views
{
    public partial class DashboardView : UserControl
    {
        private readonly SaleService _saleService = new();
        private readonly InventoryService _inventoryService = new();
        private readonly CustomerService _customerService = new();
        private bool _isUpdatingDates = false;

        public DashboardView()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            SetQuickFilter(1);
            LoadLists();
        }

        private void BtnQuickFilter_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && int.TryParse(btn.Tag?.ToString(), out int months))
            {
                SetQuickFilter(months);
            }
        }

        private void SetQuickFilter(int monthsBack)
        {
            _isUpdatingDates = true;
            DpEnd.SelectedDate = DateTime.Today;
            DpStart.SelectedDate = DateTime.Today.AddMonths(-monthsBack);
            _isUpdatingDates = false;

            LoadKPIs();
        }

        private void DpDate_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isUpdatingDates) LoadKPIs();
        }

        private void LoadKPIs()
        {
            DateTime start = DpStart.SelectedDate ?? DateTime.Today.AddMonths(-1);
            DateTime end = DpEnd.SelectedDate ?? DateTime.Today;

            end = end.Date.AddDays(1).AddTicks(-1);

            var salesInPeriod = _saleService.GetAll()
                .Where(s => {
                    if (DateTime.TryParse(s.SaleDate, out DateTime dt))
                        return dt >= start && dt <= end;
                    return false;
                }).ToList();

            decimal totalSales = salesInPeriod.Sum(s => s.TotalPrice);
            decimal totalRevenue = salesInPeriod.Sum(s => s.AmountPaid);
            decimal totalDebtGenerated = salesInPeriod.Sum(s => s.RemainingDebt);

            var tr = new System.Globalization.CultureInfo("tr-TR");
            TxtPeriodSales.Text = totalSales.ToString("C", tr);
            TxtPeriodRevenue.Text = totalRevenue.ToString("C", tr);
            TxtPeriodDebt.Text = totalDebtGenerated.ToString("C", tr);
        }

        private void LoadLists()
        {
            ListLowStock.ItemsSource = _inventoryService.GetLowStockProducts().Take(10).ToList();
            ListRecentSales.ItemsSource = _saleService.GetAll().Take(10).ToList();
            ListDebtors.ItemsSource = _customerService.GetDebtors().Take(10).ToList();
        }
    }
}