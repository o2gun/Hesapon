using ConstruxERP.Services;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace ConstruxERP.Views
{
    public partial class ReportsView : UserControl
    {
        private readonly ReportService _reportService = new();
        private bool _isUpdatingDates = false;
        private static readonly CultureInfo _tr = new("tr-TR");

        public ReportsView()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Varsayýlan olarak Bu Ay filtresini uygula
            ApplyQuickFilter("ThisMonth");
        }

        private void BtnQuickFilter_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag != null)
            {
                ApplyQuickFilter(btn.Tag.ToString() ?? "");
            }
        }

        private void ApplyQuickFilter(string filterType)
        {
            _isUpdatingDates = true;
            DateTime today = DateTime.Today;

            switch (filterType)
            {
                case "Today":
                    DpStart.SelectedDate = today;
                    DpEnd.SelectedDate = today;
                    break;
                case "ThisMonth":
                    DpStart.SelectedDate = new DateTime(today.Year, today.Month, 1);
                    DpEnd.SelectedDate = new DateTime(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));
                    break;
                case "3Months":
                    DpStart.SelectedDate = today.AddMonths(-3);
                    DpEnd.SelectedDate = today;
                    break;
                case "12Months":
                    DpStart.SelectedDate = today.AddMonths(-12);
                    DpEnd.SelectedDate = today;
                    break;
            }

            _isUpdatingDates = false;
            LoadReportData();
        }

        private void DpDate_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isUpdatingDates)
            {
                LoadReportData();
            }
        }

        private void LoadReportData()
        {
            DateTime start = DpStart.SelectedDate ?? DateTime.Today;
            DateTime end = DpEnd.SelectedDate ?? DateTime.Today;

            // Raporlama servisi saatleri dikkate aldýðý için Bitiþ Tarihinin tam olarak 23:59 olmasý saðlanýr 
            // (Bu iþlem Service içinde yapýldýðý için burada sadece parametre gönderiyoruz)

            try
            {
                // 1. Özet Verileri Çek
                var summary = _reportService.GetSummary(start, end);

                TxtTotalSales.Text = summary.TotalSales.ToString("C", _tr);
                TxtTotalCollections.Text = summary.TotalCollections.ToString("C", _tr);
                TxtTotalPayments.Text = summary.TotalSupplierPayments.ToString("C", _tr);

                TxtNetCashFlow.Text = summary.NetCashFlow.ToString("C", _tr);
                // Net nakit akýþý eksiye düþerse kýrmýzý yapalým
                TxtNetCashFlow.Foreground = summary.NetCashFlow >= 0
                    ? new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#0F172A"))
                    : new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#EF4444"));

                // 2. En Çok Satan Ürünleri Çek
                ListTopProducts.ItemsSource = _reportService.GetTopSellingProducts(start, end, 10);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Raporlar yüklenirken hata oluþtu: " + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}