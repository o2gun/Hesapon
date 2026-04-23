using ConstruxERP.Models;
using ConstruxERP.Services;
using System;
using System.Windows;

namespace ConstruxERP.Dialogs
{
    public partial class AddEditCustomerDialog : Window
    {
        private readonly CustomerService _service = new();
        private readonly Customer? _editCustomer;

        /// <summary>ADD mode.</summary>
        public AddEditCustomerDialog()
        {
            InitializeComponent();
            TxtTitle.Text = "Yeni Müþteri Ekle";
        }

        /// <summary>EDIT mode.</summary>
        public AddEditCustomerDialog(Customer customer) : this()
        {
            _editCustomer = customer;
            TxtTitle.Text = "Müþteri Bilgilerini Düzenle";
            TxtName.Text = customer.Name;
            TxtPhone.Text = customer.Phone;
            TxtEmail.Text = customer.Email;
            TxtAddress.Text = customer.Address;
            TxtBillingAddress.Text = customer.BillingAddress;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            TxtError.Visibility = Visibility.Collapsed;

            if (string.IsNullOrWhiteSpace(TxtName.Text))
            { ShowError("Müþteri adý zorunludur."); return; }

            try
            {
                var c = new Customer
                {
                    Id = _editCustomer?.Id ?? 0,
                    Name = TxtName.Text.Trim(),
                    Phone = TxtPhone.Text.Trim(),
                    Email = TxtEmail.Text.Trim(),
                    Address = TxtAddress.Text.Trim(),
                    BillingAddress = TxtBillingAddress.Text.Trim()
                };

                if (_editCustomer == null) _service.AddCustomer(c);
                else _service.UpdateCustomer(c);

                DialogResult = true;
                Close();
            }
            catch (Exception ex) { ShowError(ex.Message); }
        }

        private void ShowError(string msg)
        {
            TxtError.Text       = msg;
            TxtError.Visibility = Visibility.Visible;
        }
    }
}
