using ConstruxERP.Models;
using ConstruxERP.Services;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ConstruxERP.Views
{
    public partial class InventoryView : UserControl
    {
        private readonly InventoryService _service = new();

        private int    _currentPage = 1;
        private int    _totalPages  = 1;
        private const int PageSize  = 20;
        private string _search      = "";

        public InventoryView()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e) => LoadData();

        private void LoadData()
        {
            var products = _service.GetProducts(_search);
            int total    = products.Count;

            // Client-side pagination (dataset is small)
            _totalPages  = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
            _currentPage = Math.Min(_currentPage, _totalPages);

            TxtProductCount.Text = $"{total} Total Item{(total == 1 ? "" : "s")}";
            TxtPagingInfo.Text   = $"Showing {Math.Min(PageSize, total - (_currentPage-1)*PageSize)} of {total} results";
            TxtPageNum.Text      = $"Page {_currentPage} / {_totalPages}";
            BtnPrev.IsEnabled    = _currentPage > 1;
            BtnNext.IsEnabled    = _currentPage < _totalPages;

            var page = products
                .Skip((_currentPage - 1) * PageSize)
                .Take(PageSize)
                .ToList();

            TxtEmpty.Visibility  = page.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            ProductList.ItemsSource = page.Select(p => new
            {
                p.Id,
                p.Name,
                p.Category,
                p.Unit,
                p.Sku,
                p.SupplierName,
                StockQty            = p.StockQty.ToString("G"),
                MinStock            = p.MinStock.ToString("G"),
                PurchasePriceDisplay = p.PurchasePrice.ToString("C", new System.Globalization.CultureInfo("tr-TR")),
                SalePriceDisplay    = p.SalePrice.ToString("C", new System.Globalization.CultureInfo("tr-TR")),
                StockColor          = p.IsLowStock ? "#EF4444" : "#0F172A",
                StripColor          = p.IsLowStock ? "#EF4444" : "Transparent"
            });
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            _search = TxtSearch.Text.Trim();
            _currentPage = 1;
            LoadData();
        }

        private void BtnAddProduct_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Dialogs.AddEditProductDialog { Owner = Window.GetWindow(this) };
            if (dlg.ShowDialog() == true) LoadData();
        }

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int id)
            {
                var product = _service.GetProduct(id);
                if (product == null) return;
                var dlg = new Dialogs.AddEditProductDialog(product) { Owner = Window.GetWindow(this) };
                if (dlg.ShowDialog() == true) LoadData();
            }
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
