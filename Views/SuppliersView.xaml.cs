using ConstruxERP.Models;
using ConstruxERP.Services;
using System.Windows;
using System.Windows.Controls;

namespace ConstruxERP.Views
{
    public partial class SuppliersView : UserControl
    {
        private readonly SupplierService _service = new();

        public SuppliersView()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e) => LoadData();

        private void LoadData()
        {
            GridSuppliers.ItemsSource = _service.GetAll();
        }

        
        private void BtnAddSupplier_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Dialogs.AddEditSupplierDialog { Owner = Window.GetWindow(this) };
            if (dlg.ShowDialog() == true)
            {
                LoadData(); // Listeyi yenile
            }
        }
    }
}