using ConstruxERP.Models;
using ConstruxERP.Services;
using System;
using System.Globalization;
using System.Windows;

namespace ConstruxERP.Dialogs
{
    public partial class EditSaleDialog : Window
    {
        private readonly SaleService _saleService = new();
        private readonly Sale _sale;

        private static readonly CultureInfo _tr = new("tr-TR");

        public EditSaleDialog(Sale sale)
        {
            InitializeComponent();
            _sale = sale;
            PopulateReadOnlyFields();
        }

        private void PopulateReadOnlyFields()
        {
            TxtSubtitle.Text = $"Satış No: #{_sale.Id}";
            TxtDate.Text = _sale.SaleDate.Length >= 10 ? _sale.SaleDate[..10] : _sale.SaleDate;

            TxtCustomer.Text = _sale.CustomerName;
            TxtProduct.Text = _sale.ProductName;
            TxtQtyPrice.Text = $"{_sale.Qty} {_sale.ProductUnit} × {_sale.UnitPrice.ToString("C", _tr)}";

            TxtTotal.Text = _sale.TotalPrice.ToString("C", _tr);
            TxtPaidDisplay.Text = _sale.AmountPaid.ToString("C", _tr);
            TxtDebtDisplay.Text = _sale.RemainingDebt.ToString("C", _tr);

            // EK ödeme alanı — 0'dan başlar (daha önce ödenen eklenmez)
            TxtNewPaid.Text = "0";

            CmbPaymentType.SelectedIndex = _sale.PaymentType?.ToLower() switch
            {
                "cash" => 0,
                "credit" => 1,
                "partial" => 2,
                _ => 0
            };

            UpdateDebtPreview();
        }

        private void TxtNewPaid_TextChanged(object sender,
            System.Windows.Controls.TextChangedEventArgs e)
            => UpdateDebtPreview();

        private void UpdateDebtPreview()
        {
            // Kullanıcının girdiği = EK ödeme miktarı
            decimal additional = decimal.TryParse(TxtNewPaid.Text, NumberStyles.Any, _tr, out var p) ? p : 0;

            // DOĞRU HESAP:
            // Yeni toplam ödenen = onceden_odenen + ek_odeme
            // Kalan borç         = toplam_tutar   - yeni_toplam_odenen
            decimal newTotalPaid = _sale.AmountPaid + additional;
            decimal newDebt = Math.Max(0, _sale.TotalPrice - newTotalPaid);

            TxtNewDebtPreview.Text =
                $"Kalan: {newDebt.ToString("C", _tr)}";

            TxtNewDebtPreview.Foreground = newDebt > 0
                ? System.Windows.Media.Brushes.Crimson
                : System.Windows.Media.Brushes.SeaGreen;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            TxtError.Visibility = Visibility.Collapsed;

            if (!decimal.TryParse(TxtNewPaid.Text, NumberStyles.Any, _tr, out decimal additional)
                || additional < 0)
            {
                ShowError("Geçerli bir tutar girin (0 veya daha büyük olmalı).");
                return;
            }

            if (additional == 0)
            {
                ShowError("Ek ödeme tutarı 0. Değişiklik yapılmadı.");
                return;
            }

            decimal newTotalPaid = _sale.AmountPaid + additional;

            if (newTotalPaid > _sale.TotalPrice)
            {
                ShowError(
                    $"Toplam ödeme ({newTotalPaid.ToString("C", _tr)}) " +
                    $"satış tutarını ({_sale.TotalPrice.ToString("C", _tr)}) aşamaz.\n" +
                    $"En fazla {(_sale.TotalPrice - _sale.AmountPaid).ToString("C", _tr)} daha ödeyebilirsiniz.");
                return;
            }

            string paymentType = CmbPaymentType.SelectedIndex switch
            {
                0 => "cash",
                1 => "credit",
                2 => "partial",
                _ => "cash"
            };

            try
            {
                _saleService.UpdatePayment(_sale.Id, newTotalPaid, paymentType);
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
            var result = MessageBox.Show(
                $"Bu satışı silmek istediğinizden emin misiniz?\n\n" +
                $"Müşteri : {_sale.CustomerName}\n" +
                $"Ürün    : {_sale.ProductName}\n" +
                $"Toplam  : {_sale.TotalPrice.ToString("C", _tr)}\n\n" +
                "⚠ Stok otomatik geri eklenmeyecektir. Devam edilsin mi?",
                "Satışı Sil",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                _saleService.DeleteSale(_sale.Id);
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
            TxtError.Text = msg;
            TxtError.Visibility = Visibility.Visible;
        }
    }
}
