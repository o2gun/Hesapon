using ConstruxERP.Models;
using ConstruxERP.Services;
using OfficeOpenXml;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ConstruxERP.Dialogs;

namespace ConstruxERP.Views
{
    public partial class CustomersView : UserControl
    {
        private readonly CustomerService _service = new();
        private int _currentPage = 1;
        private int _pageSize = 100;
        private int _totalPages = 1;

        // Detay görünümü için stateler
        private int _selectedDetailCustomerId = 0;
        private Customer _selectedCustomerObj;
        private List<LedgerItem> _fullTimeline = new();
        private List<LedgerItem> _filteredTimeline = new();

        private static readonly CultureInfo _tr = new("tr-TR");

        public CustomersView()
        {
            InitializeComponent();
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e) => LoadData();

        private void FilterChanged(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            _currentPage = 1;
            LoadData();
        }

        public void LoadData()
        {
            if (!IsLoaded || GridList.Visibility != Visibility.Visible) return;

            string search = TxtSearch?.Text?.Trim() ?? "";
            bool searchName = ChkSearchName?.IsChecked == true;
            bool searchPhone = ChkSearchPhone?.IsChecked == true;
            bool searchAddress = ChkSearchAddress?.IsChecked == true;

            if (!searchName && !searchPhone && !searchAddress) searchName = searchPhone = searchAddress = true;

            decimal minDebt = 0;
            if (decimal.TryParse(TxtMinDebt?.Text, out var parsedDebt)) minDebt = parsedDebt;

            int totalRecords = _service.CountAll(search, searchName, searchPhone, searchAddress, minDebt);
            _totalPages = (int)Math.Ceiling(totalRecords / (double)_pageSize);
            if (_totalPages < 1) _totalPages = 1;
            if (_currentPage > _totalPages) _currentPage = _totalPages;

            var customers = _service.GetAll(search, searchName, searchPhone, searchAddress, minDebt, _currentPage, _pageSize);

            if (TxtPagingInfo != null)
            {
                int startIdx = totalRecords == 0 ? 0 : (_currentPage - 1) * _pageSize + 1;
                int endIdx = Math.Min(_currentPage * _pageSize, totalRecords);
                TxtPagingInfo.Text = $"Toplam {totalRecords} kaydın {startIdx} - {endIdx} arası gösteriliyor";
            }
            if (TxtPageNumber != null) TxtPageNumber.Text = $"Sayfa {_currentPage} / {_totalPages}";
            if (BtnPrevPage != null) BtnPrevPage.IsEnabled = _currentPage > 1;
            if (BtnNextPage != null) BtnNextPage.IsEnabled = _currentPage < _totalPages;
            if (TxtEmpty != null) TxtEmpty.Visibility = customers.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            if (CustomerList != null)
            {
                CustomerList.ItemsSource = customers.Select(c =>
                {
                    var words = c.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    string init = words.Length >= 2 ? $"{words[0][0]}{words[1][0]}".ToUpper() : c.Name.Length >= 2 ? c.Name[..2].ToUpper() : c.Name.ToUpper();
                    bool hasDebt = c.TotalDebt > 0;

                    return new
                    {
                        c.Id,
                        c.Name,
                        Phone = string.IsNullOrWhiteSpace(c.Phone) ? "—" : c.Phone,
                        TotalPurchasesDisplay = c.TotalPurchases > 0 ? c.TotalPurchases.ToString("C", _tr) : "—",
                        TotalPaidDisplay = c.TotalPaid > 0 ? c.TotalPaid.ToString("C", _tr) : "—",
                        Initials = init,
                        AvatarBg = hasDebt ? "#FEE2E2" : "#DBEAFE",
                        AvatarFg = hasDebt ? "#DC2626" : "#1D4ED8",
                        DebtDisplay = c.TotalDebt.ToString("C", _tr),
                        DebtBg = hasDebt ? "#FEE2E2" : "Transparent",
                        DebtFg = hasDebt ? "#DC2626" : "#94A3B8",
                        RowBg = hasDebt ? "#FFF5F5" : "White"
                    };
                }).ToList();
            }
        }

        private void BtnPrevPage_Click(object sender, RoutedEventArgs e) { if (_currentPage > 1) { _currentPage--; LoadData(); } }
        private void BtnNextPage_Click(object sender, RoutedEventArgs e) { if (_currentPage < _totalPages) { _currentPage++; LoadData(); } }

        private void BtnAddCustomer_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Dialogs.AddEditCustomerDialog { Owner = Window.GetWindow(this) };
            if (dlg.ShowDialog() == true) LoadData();
        }

        // ─── ALT BÖLÜM 60% : LİSTE YERİNE DETAYI GÖSTER (UX TALEBİ) ───

        private void BtnView_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int id)
            {
                _selectedDetailCustomerId = id;
                GridList.Visibility = Visibility.Collapsed;
                GridDetail.Visibility = Visibility.Visible;

                // Tarihleri sıfırla ve detayı yükle
                DpStart.SelectedDate = null;
                DpEnd.SelectedDate = null;
                LoadCustomerDetail();
            }
        }

        private void BtnBackToList_Click(object sender, RoutedEventArgs e)
        {
            GridDetail.Visibility = Visibility.Collapsed;
            GridList.Visibility = Visibility.Visible;
            _selectedDetailCustomerId = 0;
            LoadData(); // Borç değişmiş olabilir, listeyi yenile.
        }

        // ─── MÜŞTERİ DETAYI VE DEFTER (LEDGER) İŞLEMLERİ ───

        private class LedgerItem
        {
            public int Id { get; set; }
            public DateTime Date { get; set; }
            public bool IsSale { get; set; }
            public decimal Amount { get; set; }
            public Sale RefSale { get; set; }
            public DebtPayment RefPayment { get; set; }
            public decimal RunningDebtAfter { get; set; }
        }

        private void LoadCustomerDetail()
        {
            _selectedCustomerObj = _service.GetById(_selectedDetailCustomerId);
            if (_selectedCustomerObj == null) return;

            // Üst Bar Bilgileri
            var words = _selectedCustomerObj.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            TxtInitials.Text = words.Length >= 2 ? $"{words[0][0]}{words[1][0]}".ToUpper() : _selectedCustomerObj.Name.Length >= 2 ? _selectedCustomerObj.Name[..2].ToUpper() : _selectedCustomerObj.Name.ToUpper();
            TxtDetailName.Text = _selectedCustomerObj.Name;
            TxtDetailPhone.Text = string.IsNullOrWhiteSpace(_selectedCustomerObj.Phone) ? "Telefon yok" : _selectedCustomerObj.Phone;
            TxtDetailEmail.Text = string.IsNullOrWhiteSpace(_selectedCustomerObj.Email) ? "E-posta yok" : _selectedCustomerObj.Email;

            // Tüm Borç Net Olarak Yazılır
            TxtCurrentTotalDebt.Text = _selectedCustomerObj.TotalDebt.ToString("C", _tr);

            BuildLedgerTimeline();
        }

        private void BuildLedgerTimeline()
        {
            var sales = _service.GetSaleHistory(_selectedDetailCustomerId);
            var payments = _service.GetPaymentHistory(_selectedDetailCustomerId);

            var tempTimeline = new List<LedgerItem>();

            foreach (var s in sales)
            {
                if (DateTime.TryParse(s.SaleDate, out DateTime dt))
                    tempTimeline.Add(new LedgerItem { Date = dt, IsSale = true, Amount = s.TotalPrice, RefSale = s });
            }

            foreach (var p in payments)
            {
                if (DateTime.TryParse(p.PaidAt, out DateTime dt))
                    tempTimeline.Add(new LedgerItem { Date = dt, IsSale = false, Amount = p.Amount, RefPayment = p });
            }

            // ÖNEMLİ: Aynı saniyede hem satış hem tahsilat varsa önce satış (borç ekle) sonra tahsilat (düş) işlensin
            tempTimeline = tempTimeline.OrderBy(t => t.Date).ThenByDescending(t => t.IsSale).ToList();

            // Koşu Bakiyesi (Running Balance) Hesapla
            decimal currentDebt = 0;
            foreach (var item in tempTimeline)
            {
                if (item.IsSale) currentDebt += item.Amount;
                else currentDebt -= item.Amount;

                // Virgül küsurat hatasını önlemek için
                if (currentDebt < 0) currentDebt = 0;
                item.RunningDebtAfter = currentDebt;
            }

            _fullTimeline = tempTimeline;
            ApplyDetailFilters();
        }

        private void DetailFilterChanged(object sender, SelectionChangedEventArgs e) => ApplyDetailFilters();

        private void BtnClearDates_Click(object sender, RoutedEventArgs e)
        {
            DpStart.SelectedDate = null;
            DpEnd.SelectedDate = null;
            ApplyDetailFilters();
        }

        private void ApplyDetailFilters()
        {
            DateTime? start = DpStart.SelectedDate;
            DateTime? end = DpEnd.SelectedDate;

            _filteredTimeline = _fullTimeline.Where(t =>
                (!start.HasValue || t.Date.Date >= start.Value.Date) &&
                (!end.HasValue || t.Date.Date <= end.Value.Date)
            ).ToList();

            var filteredSales = _filteredTimeline.Where(t => t.IsSale).ToList();
            var filteredPayments = _filteredTimeline.Where(t => !t.IsSale).ToList();

            // KPI Güncellemesi (Sadece seçili döneme ait Toplamlar)
            TxtPeriodPurchases.Text = filteredSales.Sum(x => x.Amount).ToString("C", _tr);
            TxtPeriodPaid.Text = filteredPayments.Sum(x => x.Amount).ToString("C", _tr);

            // UI Tablolarını Doldur
            DetailSalesList.ItemsSource = filteredSales.Select(t => new {
                Id = t.RefSale.Id,
                DateShort = t.Date.ToString("dd.MM.yyyy"),
                t.RefSale.ProductName,
                QtyDisplay = $"{t.RefSale.Qty} {t.RefSale.ProductUnit}",
                UnitPriceDisplay = t.RefSale.UnitPrice.ToString("C", _tr), // "Birim Fiyat" gösterimi kuralı
                Total = t.RefSale.TotalPrice.ToString("C", _tr)
            });

            DetailPaymentList.ItemsSource = filteredPayments.Select(t => new {
                DateShort = t.Date.ToString("dd.MM.yyyy"),
                Notes = string.IsNullOrWhiteSpace(t.RefPayment.Notes) ? "Ödeme" : t.RefPayment.Notes,
                AmountDisplay = t.Amount.ToString("C", _tr),
                RemainingDebtDisplay = t.RunningDebtAfter.ToString("C", _tr) // Kural: Ödeme sonrası kalan borç
            });
        }

        // ─── DETAY ÜZERİNDEKİ İŞLEMLER (Satış, Tahsilat, Düzenle, Excel) ───

        private void BtnNewSale_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Dialogs.AddSaleDialog(_selectedDetailCustomerId) { Owner = Window.GetWindow(this) };
            if (dlg.ShowDialog() == true) LoadCustomerDetail();
        }

        private void BtnPayDebt_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Dialogs.RecordPaymentDialog(_selectedDetailCustomerId) { Owner = Window.GetWindow(this) };
            if (dlg.ShowDialog() == true) LoadCustomerDetail();
        }

        private void BtnEditCustomer_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Dialogs.AddEditCustomerDialog(_selectedCustomerObj) { Owner = Window.GetWindow(this) };
            if (dlg.ShowDialog() == true) LoadCustomerDetail();
        }

        private void BtnEditSale_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag != null)
            {
                if (int.TryParse(btn.Tag.ToString(), out int saleId))
                {
                    var saleService = new SaleService();
                    var saleToEdit = saleService.GetSaleById(saleId);

                    if (saleToEdit != null)
                    {
                        var dialog = new AddSaleDialog(saleToEdit);
                        dialog.Owner = Window.GetWindow(this);

                        if (dialog.ShowDialog() == true)
                        {
                            LoadCustomerDetail();
                        }
                    }
                }
                else
                {
                    MessageBox.Show("Hata: Satış ID'si çözümlenemedi. Gelen değer: " + btn.Tag.ToString());
                }
            }
        }

        private void BtnExportExcel_Click(object sender, RoutedEventArgs e)
        {
            if (_filteredTimeline.Count == 0)
            {
                MessageBox.Show("Dışa aktarılacak kayıt bulunamadı.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var sfd = new SaveFileDialog
            {
                Filter = "Excel Dosyası|*.xlsx",
                FileName = $"{_selectedCustomerObj.Name}_HesapEkstresi.xlsx"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    using var package = new ExcelPackage();
                    var ws = package.Workbook.Worksheets.Add("Hesap Ekstresi");

                    // Başlıklar
                    ws.Cells["A1"].Value = "Tarih";
                    ws.Cells["B1"].Value = "İşlem Türü";
                    ws.Cells["C1"].Value = "Detay/Ürün";
                    ws.Cells["D1"].Value = "Borç (Satış Tutarı)";
                    ws.Cells["E1"].Value = "Alacak (Tahsilat)";
                    ws.Cells["F1"].Value = "İşlem Sonrası Kalan Bakiye";

                    // Başlık Stil
                    using (var range = ws.Cells["A1:F1"])
                    {
                        range.Style.Font.Bold = true;
                        range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                    }

                    int row = 2;
                    foreach (var t in _filteredTimeline)
                    {
                        ws.Cells[row, 1].Value = t.Date.ToString("dd.MM.yyyy HH:mm");
                        ws.Cells[row, 2].Value = t.IsSale ? "Satış" : "Tahsilat";

                        ws.Cells[row, 3].Value = t.IsSale ?
                            $"{t.RefSale.ProductName} ({t.RefSale.Qty} {t.RefSale.ProductUnit} x {t.RefSale.UnitPrice:C})" :
                            (string.IsNullOrWhiteSpace(t.RefPayment.Notes) ? "Ödeme Alındı" : t.RefPayment.Notes);

                        if (t.IsSale)
                        {
                            ws.Cells[row, 4].Value = (double)t.Amount; // Borç eklendi
                        }
                        else
                        {
                            ws.Cells[row, 5].Value = (double)t.Amount; // Tahsil edildi
                        }

                        ws.Cells[row, 6].Value = (double)t.RunningDebtAfter; // Kalan Bakiye
                        row++;
                    }

                    // Para formatları (Sütun D, E, F)
                    ws.Cells[$"D2:F{row}"].Style.Numberformat.Format = "#,##0.00 ₺";
                    ws.Cells[ws.Dimension.Address].AutoFitColumns();

                    File.WriteAllBytes(sfd.FileName, package.GetAsByteArray());
                    MessageBox.Show("Müşteri ekstresi Excel'e başarıyla aktarıldı.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Excel oluşturulurken hata oluştu:\n" + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}