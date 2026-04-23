using ConstruxERP.Models;
using ConstruxERP.Services;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ConstruxERP.Views
{
    public partial class SalesView : UserControl
    {
        private readonly SaleService _saleService = new();
        private readonly CustomerService _customerService = new();

        private int _currentPage = 1;
        private int _totalPages = 1;
        private const int PageSize = 20;
        private string _search = "";

        public SalesView()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e) => LoadData();

        private void LoadData()
        {
            // KPI strip
            TxtDailyTotal.Text = _saleService.GetTodayTotal().ToString("C", new System.Globalization.CultureInfo("tr-TR"));
            TxtPendingDebt.Text = _customerService.GetTotalOutstandingDebt().ToString("C", new System.Globalization.CultureInfo("tr-TR"));

            int total = _saleService.CountSales(_search);
            TxtTxCount.Text = total.ToString();

            // Calculate paid-today from sales service
            TxtPaidToday.Text = _saleService.GetTodayTotal().ToString("C", new System.Globalization.CultureInfo("tr-TR"));

            // Paging
            _totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
            _currentPage = Math.Min(_currentPage, _totalPages);
            TxtPagingInfo.Text = $"Showing page {_currentPage} of {_totalPages}  ({total} records)";
            TxtPageNum.Text = $"Page {_currentPage} / {_totalPages}";
            BtnPrev.IsEnabled = _currentPage > 1;
            BtnNext.IsEnabled = _currentPage < _totalPages;

            // Sales rows
            var sales = _saleService.GetSales(_search, _currentPage, PageSize);
            TxtEmpty.Visibility = sales.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            SalesList.ItemsSource = sales.Select(s => new
            {
                DateShort = s.SaleDate.Length >= 10 ? s.SaleDate[..10] : s.SaleDate,
                s.Id,
                s.CustomerName,
                s.ProductName,
                QtyDisplay = $"{s.Qty} {s.ProductUnit}",
                UnitPriceDisplay = s.UnitPrice.ToString("C", new System.Globalization.CultureInfo("tr-TR")),
                TotalDisplay = s.TotalPrice.ToString("C", new System.Globalization.CultureInfo("tr-TR")),
                PaidDisplay = s.AmountPaid.ToString("C", new System.Globalization.CultureInfo("tr-TR")),
                DebtDisplay = s.RemainingDebt.ToString("C", new System.Globalization.CultureInfo("tr-TR")),
                DebtBg = s.RemainingDebt == 0 ? "Transparent" : "#FEE2E2",
                DebtFg = s.RemainingDebt == 0 ? "#94A3B8" : "#DC2626",
                RowBg = s.RemainingDebt == 0 ? "White" : "#FFFBFB"
            });
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            _search = TxtSearch.Text.Trim();
            _currentPage = 1;
            LoadData();
        }

        private void BtnAddSale_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Dialogs.AddSaleDialog { Owner = Window.GetWindow(this) };
            if (dlg.ShowDialog() == true)
            {
                _currentPage = 1;
                LoadData();
            }
        }

        private void BtnEditSale_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;

            // Tag is bound to the sale Id in the DataTemplate
            if (btn.Tag is not int saleId) return;

            var sale = _saleService.GetSaleById(saleId);
            if (sale == null)
            {
                MessageBox.Show("Sale record not found.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var dlg = new Dialogs.EditSaleDialog(sale) { Owner = Window.GetWindow(this) };
            if (dlg.ShowDialog() == true)
                LoadData();   // Refresh table after edit / delete
        }

        private void BtnPrev_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 1) { _currentPage--; LoadData(); }
        }

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage < _totalPages) { _currentPage++; LoadData(); }
        }
    }
}
