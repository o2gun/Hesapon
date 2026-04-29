using ConstruxERP.Models;
using ConstruxERP.Services;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ConstruxERP.Dialogs
{
    public partial class AddEditProductDialog : Window
    {
        private readonly InventoryService _service = new();
        private readonly Product? _editProduct;
        private readonly SupplierService _supplierService = new();

        /// <summary>Constructor for ADD mode.</summary>
        public AddEditProductDialog()
        {
            InitializeComponent();
            TxtTitle.Text = "Yeni Ürün Ekle";

            CmbSupplier.ItemsSource = _supplierService.GetAll();
            CmbSupplier.DisplayMemberPath = "Name";
            CmbSupplier.SelectedValuePath = "Id";
        }

        /// <summary>Constructor for EDIT mode.</summary>
        public AddEditProductDialog(Product product) : this()
        {
            TxtTitle.Text = "Ürünü Düzenle";

            if (product != null)
            {
                // Pre-fill fields
                _editProduct = product;
                TxtName.Text = product.Name;
                TxtCategory.Text = product.Category;
                CmbUnit.Text = product.Unit;
                TxtPurchasePrice.Text = product.PurchasePrice.ToString("F2");
                TxtSalePrice.Text = product.SalePrice.ToString("F2");
                TxtStock.Text = product.StockQty.ToString("G");
                TxtMinStock.Text = product.MinStock.ToString("G");
                TxtSku.Text = product.Sku;
                TxtNotes.Text = product.Notes;

                var supplier = _supplierService.GetAll().FirstOrDefault(s => s.Name == product.SupplierName);
                if (supplier != null)
                    CmbSupplier.SelectedValue = supplier.Id;
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            TxtError.Visibility = Visibility.Collapsed;

            if (string.IsNullOrWhiteSpace(TxtName.Text))
            { ShowError("Ürün adı zorunludur."); return; }

            string unit = CmbUnit.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(unit))
            { ShowError("Birim zorunludur."); return; }

            if (!decimal.TryParse(TxtPurchasePrice.Text, out decimal pp) || pp < 0)
            { ShowError("Geçerli bir satın alma fiyatı girin (≥ 0)."); return; }

            if (!decimal.TryParse(TxtSalePrice.Text, out decimal sp) || sp < 0)
            { ShowError("Geçerli bir satış fiyatı girin (≥ 0)."); return; }

            if (!decimal.TryParse(TxtStock.Text, out decimal stock) || stock < 0)
            { ShowError("Geçerli bir stok miktarı girin (≥ 0)."); return; }

            if (!decimal.TryParse(TxtMinStock.Text, out decimal minStock) || minStock < 0)
            { ShowError("Geçerli bir minimum stok seviyesi girin (≥ 0)."); return; }

            if (CmbSupplier.SelectedItem == null)
            { ShowError("Lütfen aldığınız şirketi listeden seçin."); return; }

            if (!decimal.TryParse(txtAmountPaid.Text, out decimal amountPaid) || amountPaid < 0)
            { ShowError("Geçerli bir ödenen miktar girin (≥ 0)."); return; }

            try
            {
                var selectedSupplier = (Supplier)CmbSupplier.SelectedItem;

                var product = new Product
                {
                    Id = _editProduct?.Id ?? 0,
                    Name = TxtName.Text.Trim(),
                    Category = TxtCategory.Text.Trim(),
                    Unit = unit,
                    PurchasePrice = pp,
                    SalePrice = sp,
                    StockQty = stock,
                    MinStock = minStock,
                    Sku = TxtSku.Text.Trim(),
                    Notes = TxtNotes.Text.Trim(),
                    SupplierName = selectedSupplier.Name
                };

                if (_editProduct == null)
                {
                    var initialPurchase = new Purchase
                    {
                        SupplierId = selectedSupplier.Id,
                        Qty = stock,
                        UnitPrice = pp,
                        AmountPaid = amountPaid,
                        Note = TxtNotes.Text.Trim()
                    };

                    _service.CreateProductWithInitialStock(product, initialPurchase);
                }
                else
                {
                    _service.UpdateProduct(product);
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        }

        private void ShowError(string message)
        {
            TxtError.Text       = message;
            TxtError.Visibility = Visibility.Visible;
        }

        private void NumberTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb && tb.Text == "0")
            {
                tb.Text = string.Empty;
            }
        }

        private void NumberTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb && string.IsNullOrWhiteSpace(tb.Text))
            {
                tb.Text = "0";
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (e.OriginalSource is TextBox tb && tb.AcceptsReturn)
                    return;

                if (e.OriginalSource is Button)
                    return;

                e.Handled = true;
                
                var request = new TraversalRequest(FocusNavigationDirection.Next);
                if (Keyboard.FocusedElement is UIElement element)
                {
                    element.MoveFocus(request);
                }
            }
        }

        private void OnCalculationInputsChanged(object sender, TextChangedEventArgs e)
        {
            UpdateTotalLabel();
        }

        private void UpdateTotalLabel()
        {
            if (TxtPurchasePrice == null || TxtStock == null || lblTotalCalculation == null)
                return;

            if (decimal.TryParse(TxtPurchasePrice.Text, out decimal price) &&
                decimal.TryParse(TxtStock.Text, out decimal qty))
            {
                lblTotalCalculation.Text = (qty * price).ToString("C2", new CultureInfo("tr-TR"));
            }
            else
            {
                lblTotalCalculation.Text = "0.00 TL";
            }
        }

    }
}
