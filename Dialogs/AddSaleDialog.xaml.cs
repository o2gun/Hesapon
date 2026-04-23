using ConstruxERP.Models;
using ConstruxERP.Services;
using System;
using System.Windows;

namespace ConstruxERP.Dialogs
{
    public partial class AddSaleDialog : Window
    {
        private readonly SaleService      _saleService      = new();
        private readonly InventoryService _inventoryService = new();
        private readonly CustomerService  _customerService  = new();

        public AddSaleDialog()
        {
            InitializeComponent();
            LoadDropdowns();
        }

        private void LoadDropdowns()
        {
            CmbCustomer.ItemsSource   = _customerService.GetAll();
            CmbCustomer.DisplayMemberPath = "Name";
            CmbCustomer.SelectedValuePath = "Id";

            CmbProduct.ItemsSource    = _inventoryService.GetProducts();
            CmbProduct.DisplayMemberPath = "Name";
            CmbProduct.SelectedValuePath = "Id";
        }

        private void CmbCustomer_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // Nothing extra needed — customer selection handled on save
        }

        private void CmbProduct_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (CmbProduct.SelectedItem is Product p)
                TxtUnitPrice.Text = p.SalePrice.ToString("F2");
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

            TxtTotal.Text = total.ToString("C", new System.Globalization.CultureInfo("tr-TR"));
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            TxtError.Visibility = Visibility.Collapsed;

            // Validate
            if (CmbCustomer.SelectedItem is not Customer customer)
            { ShowError("Please select a customer."); return; }

            if (CmbProduct.SelectedItem is not Product product)
            { ShowError("Please select a product."); return; }

            if (!decimal.TryParse(TxtQty.Text, out decimal qty) || qty <= 0)
            { ShowError("Please enter a valid quantity greater than zero."); return; }

            if (!decimal.TryParse(TxtUnitPrice.Text, out decimal unitPrice) || unitPrice <= 0)
            { ShowError("Please enter a valid unit price."); return; }

            decimal paid = decimal.TryParse(TxtPaid.Text, out var pd) ? pd : 0;

            try
            {
                var sale = new Sale
                {
                    CustomerId = customer.Id,
                    ProductId  = product.Id,
                    Qty        = qty,
                    UnitPrice  = unitPrice,
                    AmountPaid = paid
                };

                _saleService.CreateSale(sale);
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
