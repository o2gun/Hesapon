using ConstruxERP.Models;
using ConstruxERP.Services;
using System;
using System.Windows;

namespace ConstruxERP.Dialogs
{
    public partial class AddEditProductDialog : Window
    {
        private readonly InventoryService _service = new();
        private readonly Product? _editProduct;
        private readonly SupplierService _supplierService = new();

        /// <summary>Constructor for ADD mode.</summary>
        /// <summary>Constructor for ADD mode.</summary>
        public AddEditProductDialog()
        {
            InitializeComponent();
            TxtTitle.Text = "Yeni Ürün Ekle";

            CmbSupplier.ItemsSource = _supplierService.GetAll();
            CmbSupplier.DisplayMemberPath = "Name";
            CmbSupplier.SelectedValuePath = "Name";
        }

        /// <summary>Constructor for EDIT mode.</summary>
        public AddEditProductDialog(Product product) : this()
        {

            TxtTitle.Text = "Ürünü Düzenle"; // Başlığı düzenleme moduna uygun yapalım

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
                CmbSupplier.Text = product.SupplierName;
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            TxtError.Visibility = Visibility.Collapsed;

            // --- Validation ---
            if (string.IsNullOrWhiteSpace(TxtName.Text))
            { ShowError("Product name is required."); return; }

            string unit = CmbUnit.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(unit))
            { ShowError("Unit is required."); return; }

            if (!decimal.TryParse(TxtPurchasePrice.Text, out decimal pp) || pp < 0)
            { ShowError("Enter a valid purchase price (≥ 0)."); return; }

            if (!decimal.TryParse(TxtSalePrice.Text, out decimal sp) || sp < 0)
            { ShowError("Enter a valid sale price (≥ 0)."); return; }

            if (!decimal.TryParse(TxtStock.Text, out decimal stock) || stock < 0)
            { ShowError("Enter a valid stock quantity (≥ 0)."); return; }

            if (!decimal.TryParse(TxtMinStock.Text, out decimal minStock) || minStock < 0)
            { ShowError("Enter a valid minimum stock level (≥ 0)."); return; }

            try
            {
                string supplierName = CmbSupplier.Text.Trim();
                if (!string.IsNullOrEmpty(supplierName))
                {
                    var existing = _supplierService.GetAll().FirstOrDefault(s => s.Name.Equals(supplierName, StringComparison.OrdinalIgnoreCase));
                    if (existing == null)
                    {
                        // Eğer yazılan tedarikçi veritabanında yoksa, otomatik olarak suppliers tablosuna kaydet!
                        _supplierService.AddSupplier(new Supplier { Name = supplierName });
                    }
                }

                var product = new Product
                {
                    Id            = _editProduct?.Id ?? 0,
                    Name          = TxtName.Text.Trim(),
                    Category      = TxtCategory.Text.Trim(),
                    Unit          = unit,
                    PurchasePrice = pp,
                    SalePrice     = sp,
                    StockQty      = stock,
                    MinStock      = minStock,
                    Sku           = TxtSku.Text.Trim(),
                    Notes         = TxtNotes.Text.Trim()
                };

                if (_editProduct == null)
                    _service.AddProduct(product);
                else
                    _service.UpdateProduct(product);

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
    }
}
