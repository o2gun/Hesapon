using ConstruxERP.Models;
using ConstruxERP.Services;
using System;
using System.Windows;

namespace ConstruxERP.Dialogs
{
    public partial class RecordPaymentDialog : Window
    {
        private readonly CustomerService _service = new();
        private readonly int _customerId;
        private readonly int? _paymentId;

        public RecordPaymentDialog(int customerId, int? paymentId = null)
        {
            InitializeComponent();
            _customerId = customerId;
            _paymentId = paymentId;

            if (_paymentId.HasValue)
            {
                TxtTitle.Text = "Ödeme Düzenle";
                BtnSave.Content = "Güncelle";
                BtnSave.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#10B981"));
                LoadPaymentData();
            }
            else
            {
                TxtTitle.Text = "Yeni Tahsilat";
                BtnSave.Content = "Kaydet";
            }
        }

        private void LoadPaymentData()
        {
            var payment = _service.GetPaymentById(_paymentId.Value);
            if (payment != null)
            {
                TxtAmount.Text = payment.Amount.ToString();
                TxtNotes.Text = payment.Notes;
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (!decimal.TryParse(TxtAmount.Text, out decimal amount) || amount <= 0)
            {
                MessageBox.Show("Lütfen geçerli bir tutar giriniz.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string notes = TxtNotes.Text.Trim();

            try
            {
                if (_paymentId.HasValue)
                {
                    // DÜZENLEME MODU
                    _service.EditPayment(_paymentId.Value, amount, notes);
                }
                else
                {
                    // YENÝ KAYIT MODU
                    _service.RecordPayment(_customerId, amount, null, notes);
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Hata oluþtu: " + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => Close();
    }
}