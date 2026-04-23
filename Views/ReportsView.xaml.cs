using ConstruxERP.Services;
using Microsoft.Win32;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ConstruxERP.Views
{
    public partial class ReportsView : UserControl
    {
        private readonly ReportService _reportService = new();
        private readonly SaleService _saleService = new();

        public ReportsView()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e) => LoadData();

        private void CmbPeriod_SelectionChanged(object sender, SelectionChangedEventArgs e) => LoadData();

        private void LoadData()
        {
            // Guard: CmbPeriod.SelectionChanged fires during InitializeComponent()
            // before any TextBlock controls are created. Exit early in that case.
            if (TxtRevenue == null) return;

            var (from, to) = GetDateRange();
            var data = _reportService.GetReport(from, to);

            // KPI cards
            TxtRevenue.Text = data.TotalRevenue.ToString("C", new System.Globalization.CultureInfo("tr-TR"));
            TxtPaid.Text = data.TotalPaid.ToString("C", new System.Globalization.CultureInfo("tr-TR"));
            TxtDebt.Text = data.TotalDebt.ToString("C", new System.Globalization.CultureInfo("tr-TR"));
            TxtTxCount.Text = data.TransactionCount.ToString();

            // Top products bar chart
            decimal maxRev = data.TopProducts.Count > 0
                ? data.TopProducts.Max(p => p.Revenue) : 1;

            TxtNoProducts.Visibility = data.TopProducts.Count == 0
                ? Visibility.Visible : Visibility.Collapsed;

            TopProductsList.ItemsSource = data.TopProducts.Select(p => new
            {
                p.Product,
                Revenue = p.Revenue.ToString("C", new System.Globalization.CultureInfo("tr-TR")),
                BarWidth = Math.Max(8.0, (double)(p.Revenue / maxRev) * 320)
            });

            // Category bars
            string[] colours = { "#2563EB", "#7C3AED", "#EF4444", "#F59E0B", "#10B981" };
            decimal maxCat = data.ByCategory.Count > 0
                ? data.ByCategory.Max(c => c.Revenue) : 1;

            TxtNoCat.Visibility = data.ByCategory.Count == 0
                ? Visibility.Visible : Visibility.Collapsed;

            CategoryList.ItemsSource = data.ByCategory
                .Select((c, i) => new
                {
                    Category = string.IsNullOrWhiteSpace(c.Category) ? "Uncategorised" : c.Category,
                    Revenue = c.Revenue.ToString("C", new System.Globalization.CultureInfo("tr-TR")),
                    BarWidth = Math.Max(8.0, (double)(c.Revenue / maxCat) * 320),
                    BarColor = colours[i % colours.Length]
                });
        }

        private (DateTime from, DateTime to) GetDateRange()
        {
            int idx = CmbPeriod?.SelectedIndex ?? 1;
            var now = DateTime.Now;
            return idx switch
            {
                0 => (DateTime.Today, DateTime.Today),
                1 => (new DateTime(now.Year, now.Month, 1), DateTime.Today),
                2 => (DateTime.Today.AddMonths(-3), DateTime.Today),
                3 => (DateTime.Today.AddMonths(-12), DateTime.Today),
                _ => (new DateTime(now.Year, now.Month, 1), DateTime.Today)
            };
        }

        private void BtnExportExcel_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Filter = "Excel Files|*.xlsx",
                FileName = $"Sales_Report_{DateTime.Now:yyyy_MM_dd}.xlsx"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var (from, to) = GetDateRange();
                var sales = _saleService.GetSales(page: 1, pageSize: 100_000);
                _reportService.ExportSalesToExcel(sales, dlg.FileName);
                MessageBox.Show("Excel export saved successfully.", "Export Complete",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnExportCsv_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Filter = "CSV Files|*.csv",
                FileName = $"Sales_Report_{DateTime.Now:yyyy_MM_dd}.csv"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var sales = _saleService.GetSales(page: 1, pageSize: 100_000);
                _reportService.ExportSalesToCsv(sales, dlg.FileName);
                MessageBox.Show("CSV export saved successfully.", "Export Complete",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}