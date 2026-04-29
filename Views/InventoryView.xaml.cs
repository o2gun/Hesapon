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
        private readonly PurchaseService _purchaseService = new();
        private int _selectedProductId = 0;

        public InventoryView() { InitializeComponent(); }

        private void UserControl_Loaded(object sender, RoutedEventArgs e) => LoadData();

        private void LoadData()
        {
            if (GridList.Visibility != Visibility.Visible) return;
            var products = _service.GetProducts(TxtSearch.Text.Trim());
            TxtProductCount.Text = $"{products.Count} Ürün";

            ProductList.ItemsSource = products.Select(p => new {
                p.Id,
                p.Name,
                p.Category,
                StockDisplay = $"{p.StockQty} {p.Unit}",
                PurchasePriceDisplay = p.PurchasePrice.ToString("C", new System.Globalization.CultureInfo("tr-TR")),
                SalePriceDisplay = p.SalePrice.ToString("C", new System.Globalization.CultureInfo("tr-TR")),
                StockColor = p.IsLowStock ? "#EF4444" : "#0F172A"
            });
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e) => LoadData();

        private void BtnAddProduct_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Dialogs.AddEditProductDialog { Owner = Window.GetWindow(this) };
            if (dlg.ShowDialog() == true) LoadData();
        }

        private void BtnViewDetail_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int id)
            {
                _selectedProductId = id;
                var product = _service.GetProduct(id);
                TxtDetailProductName.Text = $"{product.Name} - Stok: {product.StockQty} {product.Unit}";

                GridList.Visibility = Visibility.Collapsed;
                GridDetail.Visibility = Visibility.Visible;
                LoadPurchaseHistory();
            }
        }

        private void BtnBackToList_Click(object sender, RoutedEventArgs e)
        {
            GridDetail.Visibility = Visibility.Collapsed;
            GridList.Visibility = Visibility.Visible;
            _selectedProductId = 0;
            LoadData();
        }

        private void LoadPurchaseHistory()
        {
            var purchases = _purchaseService.GetPurchasesByProduct(_selectedProductId);
            PurchaseList.ItemsSource = purchases;
        }

        private void BtnEditPurchase_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int purchaseId)
            {
                // Düzenleme iþlemi için basitleþtirilmiþ Input varsayýmý
                // Gerçek senaryoda bu iþlem için EditPurchaseDialog tasarlayabilirsiniz.
                // Þimdilik test amaçlý deðerlerin deðiþtirildiðini simüle eden bir arayüz veya Dialog gerekecek.
                MessageBox.Show("Alým düzenleme ekraný açýlacak. Veritabaný PurchaseService.UpdatePurchase altyapýsý hazýrdýr.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                // Örnek Çaðrý: _purchaseService.UpdatePurchase(purchaseId, 1200, 50, "2024-05-15");
                // LoadPurchaseHistory();
            }
        }
    }
}
