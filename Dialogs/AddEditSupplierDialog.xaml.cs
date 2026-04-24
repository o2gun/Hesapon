using ConstruxERP.Models;
using ConstruxERP.Services;
using System.Windows;

namespace ConstruxERP.Dialogs
{
    public partial class AddEditSupplierDialog : Window
    {
        private readonly SupplierService _service = new();
        public AddEditSupplierDialog() { InitializeComponent(); }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtName.Text))
            {
                MessageBox.Show("Firma adı zorunludur.");
                return;
            }

            var supplier = new Supplier
            {
                Name = TxtName.Text,
                Phone = TxtPhone.Text,
                Email = TxtEmail.Text,
                Address = TxtAddress.Text,
                BillingAddress = TxtBillingAddress.Text
            };

            _service.AddSupplier(supplier);
            DialogResult = true;
            Close();
        }
    }
}