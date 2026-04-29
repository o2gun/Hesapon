using ConstruxERP.Services;
using System;
using System.Windows;

namespace ConstruxERP.Dialogs
{
    public partial class RecordPaymentDialog : Window
    {
        private readonly CustomerService _service    = new();
        private readonly int             _customerId;

        public RecordPaymentDialog(int customerId)
        {
            InitializeComponent();
            _customerId = customerId;

            var customer = _service.GetById(customerId);
            if (customer != null)
            {
                TxtCustomerName.Text = customer.Name;
                TxtOutstanding.Text  = customer.TotalDebt.ToString("C", new System.Globalization.CultureInfo("tr-TR"));
                // Pre-fill the full outstanding amount as a convenience
                TxtAmount.Text = customer.TotalDebt.ToString("F2");
            }
        }

        private void BtnRecord_Click(object sender, RoutedEventArgs e)
        {
            TxtError.Visibility = Visibility.Collapsed;

            if (!decimal.TryParse(TxtAmount.Text, out decimal amount) || amount <= 0)
            { ShowError("Lütfen sýfýrdan büyük geçerli bir ödeme tutarý girin."); return; }

            try
            {
                _service.RecordPayment(_customerId, amount, notes: TxtNotes.Text.Trim());
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        }

        private void ShowError(string msg)
        {
            TxtError.Text       = msg;
            TxtError.Visibility = Visibility.Visible;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
