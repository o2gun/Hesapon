using ConstruxERP.Services;
using ConstruxERP.Views;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace ConstruxERP
{
    public partial class MainWindow : Window
    {
        private Button? _activeBtn;

        // Nav-tag → View factory map
        private readonly Dictionary<string, System.Func<UIElement>> _viewFactory;

        public MainWindow()
        {
            InitializeComponent();

            _viewFactory = new()
            {
                ["Dashboard"]   = () => new DashboardView(),
                ["Sales"]       = () => new SalesView(),
                ["Inventory"]   = () => new InventoryView(),
                ["Customers"]   = () => new CustomersView(),
                ["Reports"]     = () => new ReportsView(),
                ["DebtReports"] = () => new DebtReportsView(),
                ["Backup"]      = () => new BackupView(),
            };

            // Start on Dashboard
            _activeBtn = BtnDashboard;
            MainContent.Content = new DashboardView();

            // Run auto-backup silently
            _ = System.Threading.Tasks.Task.Run(() =>
                new BackupService().AutoBackupIfNeeded());
        }

        private void NavButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            string tag = btn.Tag?.ToString() ?? "";

            // Update button styles
            if (_activeBtn != null)
                _activeBtn.Style = (Style)FindResource("NavButton");
            btn.Style  = (Style)FindResource("NavButtonActive");
            _activeBtn = btn;

            // Swap content
            if (_viewFactory.TryGetValue(tag, out var factory))
                MainContent.Content = factory();
        }
    }
}
