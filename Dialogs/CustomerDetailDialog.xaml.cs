using ConstruxERP.Models;
using ConstruxERP.Services;
using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ConstruxERP.Dialogs
{
    public partial class CustomerDetailDialog : Window
    {
        private readonly CustomerService _service = new();
        private readonly int             _customerId;
        private static readonly CultureInfo _tr = new("tr-TR");

        public CustomerDetailDialog(int customerId)
        {
            InitializeComponent();
            _customerId = customerId;
            LoadAll();
        }

        // ─── Full reload ──────────────────────────────────────────────────────

        private void LoadAll()
        {
            var customer = _service.GetById(_customerId);
            if (customer == null) { Close(); return; }

            LoadHeader(customer);
            LoadAddresses(customer);
            LoadSales(customer);
            LoadPayments();
        }

        // ─── Header & KPI cards ───────────────────────────────────────────────

        private void LoadHeader(Customer customer)
        {
            // Initials (max 2 chars)
            var words = customer.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            TxtInitials.Text = words.Length >= 2
                ? $"{words[0][0]}{words[1][0]}".ToUpper()
                : customer.Name.Length >= 2 ? customer.Name[..2].ToUpper() : customer.Name.ToUpper();

            TxtName.Text  = customer.Name;
            TxtPhone.Text = string.IsNullOrWhiteSpace(customer.Phone) ? "Telefon yok" : customer.Phone;
            TxtEmail.Text = string.IsNullOrWhiteSpace(customer.Email) ? "E-posta yok" : customer.Email;

            TxtMemberSince.Text = $"Kayıt tarihi: {customer.CreatedAt[..10]}";
            TxtDebt.Text        = customer.TotalDebt.ToString("C", _tr);
        }

        // ─── Address block ────────────────────────────────────────────────────

        private void LoadAddresses(Customer customer)
        {
            TxtAddress.Text = string.IsNullOrWhiteSpace(customer.Address)
                ? "Adres girilmemiş" : customer.Address;

            if (string.IsNullOrWhiteSpace(customer.BillingAddress))
            {
                TxtBillingAddress.Text       = customer.Address;
                TxtBillingNote.Visibility    = Visibility.Visible;
            }
            else
            {
                TxtBillingAddress.Text       = customer.BillingAddress;
                TxtBillingNote.Visibility    = Visibility.Collapsed;
            }
        }

        // ─── Purchase history ─────────────────────────────────────────────────

        private void LoadSales(Customer customer)
        {
            var sales = _service.GetSaleHistory(_customerId);

            decimal totalPurchases = sales.Sum(s => s.TotalPrice);
            decimal totalPaid      = sales.Sum(s => s.AmountPaid);

            TxtTotalPurchases.Text = totalPurchases.ToString("C", _tr);
            TxtTotalPaid.Text      = totalPaid.ToString("C", _tr);
            TxtTxCount.Text        = sales.Count.ToString();

            TxtNoSales.Visibility = sales.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            SalesList.ItemsSource = sales.Select(s => new
            {
                DateShort   = s.SaleDate.Length >= 10 ? s.SaleDate[..10] : s.SaleDate,
                s.ProductName,
                QtyDisplay  = $"{s.Qty} {s.ProductUnit}",
                Total       = s.TotalPrice.ToString("C", _tr),
                Paid        = s.AmountPaid.ToString("C", _tr),
                Debt        = s.RemainingDebt.ToString("C", _tr),
                DebtFg      = s.RemainingDebt > 0 ? "#DC2626" : "#94A3B8",
                RowBg       = s.RemainingDebt > 0 ? "#FFF5F5" : "White",
                Note        = string.IsNullOrWhiteSpace(s.Note) ? "" : $"📝 {s.Note}",
                NoteVisibility = string.IsNullOrWhiteSpace(s.Note)
                    ? Visibility.Collapsed : Visibility.Visible
            }).ToList();
        }

        // ─── Payment history ──────────────────────────────────────────────────

        private void LoadPayments()
        {
            var payments = _service.GetPaymentHistory(_customerId);

            TxtNoPayments.Visibility = payments.Count == 0
                ? Visibility.Visible : Visibility.Collapsed;

            PaymentList.ItemsSource = payments.Select(p => new
            {
                DateShort      = p.PaidAt.Length >= 10 ? p.PaidAt[..10] : p.PaidAt,
                AmountDisplay  = p.Amount.ToString("C", _tr),
                Notes          = string.IsNullOrWhiteSpace(p.Notes) ? "" : p.Notes,
                NotesVisibility = string.IsNullOrWhiteSpace(p.Notes)
                    ? Visibility.Collapsed : Visibility.Visible
            }).ToList();
        }

        // ─── Edit button ──────────────────────────────────────────────────────

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            var customer = _service.GetById(_customerId);
            if (customer == null) return;

            var dlg = new AddEditCustomerDialog(customer) { Owner = this };
            if (dlg.ShowDialog() == true)
                LoadAll();   // Refresh after edit
        }

        // ─── Pay debt button ──────────────────────────────────────────────────

        private void BtnPayDebt_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new RecordPaymentDialog(_customerId) { Owner = this };
            if (dlg.ShowDialog() == true)
                LoadAll();   // Refresh after payment
        }
    }
}
