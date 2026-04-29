using ConstruxERP.Models;
using ConstruxERP.Services;
using System;
using System.Windows;
using System.Globalization;

namespace ConstruxERP.Dialogs
{
    public partial class AddSaleDialog : Window
    {
        private readonly SaleService _saleService = new();
        private readonly InventoryService _inventoryService = new();
        private readonly CustomerService _customerService = new();

        private readonly Sale? _editSale = null;

        public AddSaleDialog()
        {
            InitializeComponent();
            LoadDropdowns();
            DpSaleDate.SelectedDate = DateTime.Now;
        }

        public AddSaleDialog(int customerId) : this()
        {
            CmbCustomer.SelectedValue = customerId;
        }

        public AddSaleDialog(Sale saleToEdit) : this()
        {
            _editSale = saleToEdit;
            this.Title = "Satışı Düzenle";

            BtnDelete.Visibility = Visibility.Visible;

            CmbCustomer.SelectedValue = saleToEdit.CustomerId;
            CmbProduct.SelectedValue = saleToEdit.ProductId;
            TxtQty.Text = saleToEdit.Qty.ToString("G");
            TxtUnitPrice.Text = saleToEdit.UnitPrice.ToString("F2");
            TxtPaid.Text = saleToEdit.AmountPaid.ToString("F2");

            if (DateTime.TryParse(saleToEdit.SaleDate, out DateTime dt))
            {
                DpSaleDate.SelectedDate = dt;
            }
        }

        private void LoadDropdowns()
        {
            CmbCustomer.ItemsSource = _customerService.GetAll();
            CmbCustomer.DisplayMemberPath = "Name";
            CmbCustomer.SelectedValuePath = "Id";

            CmbProduct.ItemsSource = _inventoryService.GetProducts();
            CmbProduct.DisplayMemberPath = "Name";
            CmbProduct.SelectedValuePath = "Id";
        }

        private void CmbCustomer_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // Nothing extra needed — customer selection handled on save
        }

        private void CmbProduct_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // YENİ EKLENEN KONTROL: Eğer düzenleme modundaysak ve ürün fiyatı zaten dolu geldiyse, 
            // fiyatı aniden değiştirmesini engellemek iyi olabilir. Veya kullanıcı manuel değiştirsin.
            if (CmbProduct.SelectedItem is Product p && _editSale == null)
            {
                TxtUnitPrice.Text = p.SalePrice.ToString("F2");
            }
        }

        private void RecalcTotal(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (TxtQty == null || TxtUnitPrice == null || TxtPaid == null || TxtTotal == null)
                return;

            decimal qty = decimal.TryParse(TxtQty.Text, out var q) ? q : 0;
            decimal unitPrice = decimal.TryParse(TxtUnitPrice.Text, out var u) ? u : 0;
            decimal paid = decimal.TryParse(TxtPaid.Text, out var p) ? p : 0;

            decimal total = Math.Round(qty * unitPrice, 2);
            decimal debt = Math.Max(0, total - paid);

            TxtTotal.Text = total.ToString("C2", new CultureInfo("tr-TR"));
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            TxtError.Visibility = Visibility.Collapsed;

            // Validate
            if (CmbCustomer.SelectedItem is not Customer customer)
            { ShowError("Lütfen bir müşteri seçin."); return; }

            if (CmbProduct.SelectedItem is not Product product)
            { ShowError("Lütfen bir ürün seçin."); return; }

            if (!decimal.TryParse(TxtQty.Text, out decimal qty) || qty <= 0)
            { ShowError("Lütfen 0 dan büyük bir sayı seçin."); return; }

            if (!decimal.TryParse(TxtUnitPrice.Text, out decimal unitPrice) || unitPrice <= 0)
            { ShowError("Lütfen birim fiyatı belirtin."); return; }

            decimal paid = decimal.TryParse(TxtPaid.Text, out var pd) ? pd : 0;
            DateTime selectedDate = DpSaleDate.SelectedDate ?? DateTime.Now;

            try
            {
                if (_editSale == null)
                {
                    // --- YENİ KAYIT MODU ---
                    var sale = new Sale
                    {
                        CustomerId = customer.Id,
                        ProductId = product.Id,
                        Qty = qty,
                        UnitPrice = unitPrice,
                        AmountPaid = paid,
                        SaleDate = selectedDate.ToString("yyyy-MM-dd HH:mm:ss")
                    };

                    _saleService.CreateSale(sale);
                }
                else
                {
                    // --- DÜZENLEME MODU ---
                    // Mevcut nesnenin değerlerini güncelle ve UpdateSale fonksiyonuna gönder
                    _editSale.CustomerId = customer.Id;
                    _editSale.ProductId = product.Id;
                    _editSale.Qty = qty;
                    _editSale.UnitPrice = unitPrice;
                    _editSale.AmountPaid = paid;
                    _editSale.SaleDate = selectedDate.ToString("yyyy-MM-dd HH:mm:ss");

                    _saleService.UpdateSale(_editSale); // Bir önceki adımda yazdığımız Transaction'lı güvenli metod
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (_editSale == null) return;

            var result = MessageBox.Show(
                "Bu satışı tamamen silmek istediğinize emin misiniz?\n\n" +
                "Not: Bu işlem sonucunda ürünler stoğa geri dönecek ve müşterinin borcundan düşülecektir.",
                "Satışı Sil",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    _saleService.DeleteSale(_editSale.Id);

                    MessageBox.Show("Satış başarıyla silindi, stok ve bakiye güncellendi.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);

                    DialogResult = true;
                    Close();
                }
                catch (Exception ex)
                {
                    ShowError("Silme işlemi sırasında hata oluştu: " + ex.Message);
                }
            }
        }

        private void ShowError(string message)
        {
            TxtError.Text = message;
            TxtError.Visibility = Visibility.Visible;
        }
    }
}