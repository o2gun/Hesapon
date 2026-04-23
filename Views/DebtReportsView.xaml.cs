using ConstruxERP.Services;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ConstruxERP.Views
{
    public partial class DebtReportsView : UserControl
    {
        private readonly CustomerService _service = new();

        public DebtReportsView() => InitializeComponent();

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            var debtors = _service.GetDebtors();
            decimal total = _service.GetTotalOutstandingDebt();

            TxtTotal.Text = $"Toplam: {total.ToString("C", new CultureInfo("tr-TR"))}";

            TxtNone.Visibility = debtors.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            DebtList.ItemsSource = debtors.Select(c => new
            {
                c.Name,
                c.Phone,
                DebtDisplay = c.TotalDebt.ToString("C", new CultureInfo("tr-TR"))
            });
        }
    }
}