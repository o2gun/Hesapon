using ConstruxERP.Services;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ConstruxERP.Views
{
    public partial class CustomersView : UserControl
    {
        private readonly CustomerService _service = new();
        private string _search = "";

        private static readonly CultureInfo _tr = new("tr-TR");

        public CustomersView() => InitializeComponent();

        private void UserControl_Loaded(object sender, RoutedEventArgs e) => LoadData();

        public void LoadData()
        {
            // GetAll artık tek sorguda LEFT JOIN ile TotalPurchases ve TotalPaid getiriyor
            var customers = _service.GetAll(_search);

            TxtPagingInfo.Text = $"{customers.Count} müşteri gösteriliyor";
            TxtEmpty.Visibility = customers.Count == 0
                ? Visibility.Visible : Visibility.Collapsed;

            CustomerList.ItemsSource = customers.Select(c =>
            {
                var words = c.Name.Split(' ');
                string init = words.Length >= 2
                    ? $"{words[0][0]}{words[1][0]}".ToUpper()
                    : c.Name.Length >= 2 ? c.Name[..2].ToUpper() : c.Name.ToUpper();

                bool hasDebt = c.TotalDebt > 0;

                return new
                {
                    c.Id,
                    c.Name,
                    Phone = string.IsNullOrWhiteSpace(c.Phone) ? "—" : c.Phone,

                    TotalPurchasesDisplay = c.TotalPurchases > 0
                        ? c.TotalPurchases.ToString("C", _tr) : "—",
                    TotalPaidDisplay = c.TotalPaid > 0
                        ? c.TotalPaid.ToString("C", _tr) : "—",

                    Initials = init,
                    AvatarBg = hasDebt ? "#FEE2E2" : "#DBEAFE",
                    AvatarFg = hasDebt ? "#DC2626" : "#1D4ED8",

                    DebtDisplay = c.TotalDebt.ToString("C", _tr),
                    DebtBg = hasDebt ? "#FEE2E2" : "Transparent",
                    DebtFg = hasDebt ? "#DC2626" : "#94A3B8",
                    RowBg = hasDebt ? "#FFF5F5" : "White",
                    PayBtnVisibility = hasDebt
                        ? Visibility.Visible : Visibility.Collapsed
                };
            }).ToList();
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            _search = TxtSearch.Text.Trim();
            LoadData();
        }

        private void BtnAddCustomer_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Dialogs.AddEditCustomerDialog { Owner = Window.GetWindow(this) };
            if (dlg.ShowDialog() == true) LoadData();
        }

        private void BtnView_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int id)
            {
                var dlg = new Dialogs.CustomerDetailDialog(id) { Owner = Window.GetWindow(this) };
                dlg.ShowDialog();
                LoadData();
            }
        }

        private void BtnPayDebt_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int id)
            {
                var dlg = new Dialogs.RecordPaymentDialog(id) { Owner = Window.GetWindow(this) };
                if (dlg.ShowDialog() == true) LoadData();
            }
        }
    }
}
