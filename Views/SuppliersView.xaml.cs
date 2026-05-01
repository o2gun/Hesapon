using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ConstruxERP.Models;
using ConstruxERP.Services;

namespace ConstruxERP.Views
{
    public partial class SuppliersView : UserControl
    {
        private readonly SupplierService _service = new();
        private readonly PurchaseService _purchaseService = new();

        private int _currentPage = 1;
        private int _pageSize = 20;
        private int _totalPages = 1;

        // Detayı açılan tedarikçinin ID'si (0 ise Liste modundayız)
        private int _selectedDetailSupplierId = 0;
        private System.Globalization.CultureInfo _tr = new("tr-TR");

        public SuppliersView()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            LoadData();
        }

        public void LoadData()
        {
            if (!IsLoaded) return;

            // Eğer Detay ekranındaysak Ana Listeyi yüklemeye gerek yok
            if (GridDetail.Visibility == Visibility.Visible)
            {
                LoadSupplierDetail();
                return;
            }

            string search = TxtSearch?.Text?.Trim() ?? "";
            bool isAdvanced = TglAdvancedFilters?.IsChecked == true;

            bool searchName = true;
            bool searchContact = false;
            decimal minDebt = decimal.MinValue; // Filtre kapalıysa sınır yok

            if (isAdvanced)
            {
                searchName = ChkSearchName?.IsChecked == true;
                searchContact = ChkSearchContact?.IsChecked == true;
                if (!searchName && !searchContact) searchName = searchContact = true;

                if (decimal.TryParse(TxtMinDebt?.Text, out var parsedDebt))
                    minDebt = parsedDebt;
            }

            int totalRecords = _service.CountAll(search, searchName, searchContact, minDebt);
            _totalPages = (int)Math.Ceiling(totalRecords / (double)_pageSize);
            if (_totalPages < 1) _totalPages = 1;
            if (_currentPage > _totalPages) _currentPage = _totalPages;

            TxtPageInfo.Text = $"Sayfa {_currentPage} / {_totalPages}";

            var suppliers = _service.GetAll(search, searchName, searchContact, minDebt, _currentPage, _pageSize);
            GridList.ItemsSource = suppliers;
        }

        private void FilterChanged(object sender, RoutedEventArgs e)
        {
            _currentPage = 1;
            LoadData();
        }

        // --- SAYFALAMA ---
        private void BtnPrev_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 1) { _currentPage--; LoadData(); }
        }

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage < _totalPages) { _currentPage++; LoadData(); }
        }


        // --- DETAY GÖRÜNTÜLEME VE KAPATMA İŞLEMLERİ ---

        private void BtnShowDetail_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int id)
            {
                var sup = _service.GetById(id);
                if (sup != null)
                {
                    _selectedDetailSupplierId = sup.Id;

                    // Listeyi ve Sayfalamayı Gizle, Detayı Göster
                    GridList.Visibility = Visibility.Collapsed;
                    PnlPagination.Visibility = Visibility.Collapsed;
                    GridDetail.Visibility = Visibility.Visible;

                    LoadSupplierDetail();
                }
            }
        }

        private void BtnCloseDetail_Click(object sender, RoutedEventArgs e)
        {
            // Detayı Gizle, Listeyi Geri Getir
            GridDetail.Visibility = Visibility.Collapsed;
            GridList.Visibility = Visibility.Visible;
            PnlPagination.Visibility = Visibility.Visible;

            _selectedDetailSupplierId = 0;
            LoadData();
        }

        private void LoadSupplierDetail()
        {
            if (_selectedDetailSupplierId == 0) return;

            // En güncel tedarikçi bilgisini al (Başlık için)
            var sup = _service.GetById(_selectedDetailSupplierId);
            if (sup != null)
            {
                TxtDetailTitle.Text = $"{sup.Name} - Cari Detayı (Güncel Borcumuz: {sup.TotalDebt.ToString("C", _tr)})";
            }

            // Alım (Purchase) Listesini Çek
            var purchases = _purchaseService.GetAll("", true, false, decimal.MinValue, 1, 1000)
                                            .Where(p => p.SupplierId == _selectedDetailSupplierId)
                                            .OrderByDescending(p => p.Id).ToList();

            DetailPurchaseList.ItemsSource = purchases.Select(p => new {
                PurchaseId = p.Id,
                DateShort = DateTime.TryParse(p.PurchaseDate, out var dt) ? dt.ToString("dd.MM.yyyy") : p.PurchaseDate,
                ProductName = p.ProductName,
                TotalDisplay = p.TotalPrice.ToString("C", _tr),
                DebtDisplay = p.RemainingDebt.ToString("C", _tr)
            });

            // Ödeme (Payment) Listesini Çek
            var payments = _service.GetPaymentHistory(_selectedDetailSupplierId);
            DetailPaymentList.ItemsSource = payments.Select(p => new {
                PaymentId = p.Id,
                DateShort = DateTime.TryParse(p.PaidAt, out var dt) ? dt.ToString("dd.MM.yyyy") : p.PaidAt,
                Notes = string.IsNullOrWhiteSpace(p.Notes) ? "Ödeme" : p.Notes,
                AmountDisplay = p.Amount.ToString("C", _tr)
            });
        }


        // --- ANA TEDARİKÇİ CRUD ---

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Dialogs.AddEditSupplierDialog { Owner = Window.GetWindow(this) };
            if (dlg.ShowDialog() == true) LoadData();
        }

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int id)
            {
                var dlg = new Dialogs.AddEditSupplierDialog(id) { Owner = Window.GetWindow(this) };
                if (dlg.ShowDialog() == true) LoadData();
            }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int id)
            {
                if (MessageBox.Show("Tedarikçiyi silmek istediğinize emin misiniz? Alım ve ödeme kayıtları da silinecektir!", "Onay", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    new Repositories.SupplierRepository().Delete(id);
                    LoadData();
                }
            }
        }


        // --- ALIM (PURCHASE) VE ÖDEME (PAYMENT) CRUD ---

        private void BtnAddPurchase_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Alım Ekleme Ekranı Hazırlanacak.");
            // var dlg = new Dialogs.AddPurchaseDialog(_selectedDetailSupplierId) { Owner = Window.GetWindow(this) };
            // if (dlg.ShowDialog() == true) LoadSupplierDetail();
        }

        private void BtnEditPurchase_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int purchaseId)
            {
                MessageBox.Show("Alım Düzenleme Ekranı Hazırlanacak.");
            }
        }

        private void BtnDeletePurchase_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int purchaseId)
            {
                if (MessageBox.Show("Alım kaydını silmek istediğinize emin misiniz? Stok ve borç geri alınacaktır.", "Alım Sil", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    _purchaseService.DeletePurchase(purchaseId);
                    LoadSupplierDetail();
                }
            }
        }

        private void BtnMakePayment_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Tedarikçi Ödeme Ekleme Ekranı Hazırlanacak.");
            // var dlg = new Dialogs.RecordSupplierPaymentDialog(_selectedDetailSupplierId) { Owner = Window.GetWindow(this) };
            // if (dlg.ShowDialog() == true) LoadSupplierDetail();
        }

        private void BtnEditPayment_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int paymentId)
            {
                MessageBox.Show("Tedarikçi Ödeme Düzenleme Ekranı Hazırlanacak.");
            }
        }

        private void BtnDeletePayment_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int paymentId)
            {
                if (MessageBox.Show("Ödemeyi silmek istediğinize emin misiniz? Şirketin borcu geri artacaktır.", "Ödeme Sil", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    _service.DeleteSupplierPayment(paymentId);
                    LoadSupplierDetail();
                }
            }
        }
    }
}