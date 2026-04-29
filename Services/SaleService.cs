using ConstruxERP.Models;
using ConstruxERP.Repositories;
using System;
using System.Collections.Generic;

namespace ConstruxERP.Services
{
    /// <summary>
    /// Business-logic layer for sales operations.
    /// Validates input then delegates to SaleRepository.
    /// </summary>
    public class SaleService
    {
        private readonly SaleRepository _saleRepo = new();
        private readonly ProductRepository _productRepo = new();
        private readonly CustomerRepository _customerRepo = new();

        public List<Sale> GetAll(string search = "", int page = 1, int pageSize = 500)
        {
            return _saleRepo.GetAll(search, page, pageSize);
        }

        public List<Sale> GetByDateRange(DateTime from, DateTime to)
        {
            return _saleRepo.GetByDateRange(from, to);
        }

        /// <summary>Creates a new sale after validating stock and customer existence.</summary>
        public int CreateSale(Sale sale)
        {
            var customer = _customerRepo.GetById(sale.CustomerId)
                ?? throw new ArgumentException("Müþteri bulunamadý.");
            var product = _productRepo.GetById(sale.ProductId)
                ?? throw new ArgumentException("Ürün bulunamadý.");

            if (product.StockQty < sale.Qty)
                throw new InvalidOperationException($"Yetersiz stok. Mevcut: {product.StockQty}");

            sale.TotalPrice = Math.Round(sale.Qty * sale.UnitPrice, 2);
            sale.AmountPaid = Math.Max(0, Math.Min(sale.AmountPaid, sale.TotalPrice));
            sale.RemainingDebt = Math.Round(sale.TotalPrice - sale.AmountPaid, 2);

            if (string.IsNullOrWhiteSpace(sale.SaleDate))
                sale.SaleDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            if (sale.AmountPaid == 0) sale.PaymentType = "credit";
            else if (sale.AmountPaid >= sale.TotalPrice) sale.PaymentType = "cash";
            else sale.PaymentType = "partial";

            using var conn = DatabaseContext.GetConnection();
            if (conn.State != System.Data.ConnectionState.Open) conn.Open();

            using var tx = conn.BeginTransaction();
            try
            {
                int newSaleId = _saleRepo.Insert(sale, tx);

                using var cmdStock = conn.CreateCommand();
                cmdStock.Transaction = tx;
                cmdStock.CommandText = "UPDATE products SET stock_qty = stock_qty - @qty WHERE id = @pid";
                cmdStock.Parameters.AddWithValue("@qty", sale.Qty);
                cmdStock.Parameters.AddWithValue("@pid", sale.ProductId);
                cmdStock.ExecuteNonQuery();

                using var cmdDebt = conn.CreateCommand();
                cmdDebt.Transaction = tx;
                cmdDebt.CommandText = "UPDATE customers SET total_debt = total_debt + @debt WHERE id = @cid";
                cmdDebt.Parameters.AddWithValue("@debt", sale.RemainingDebt);
                cmdDebt.Parameters.AddWithValue("@cid", sale.CustomerId);
                cmdDebt.ExecuteNonQuery();

                tx.Commit();
                return newSaleId;
            }
            catch (Exception)
            {
                tx.Rollback();
                throw;
            }
        }

        public List<Sale> GetSales(string search = "", int page = 1, int pageSize = 50)
            => _saleRepo.GetAll(search, page, pageSize);

        public Sale? GetSaleById(int id) => _saleRepo.GetById(id);

        /// <summary>
        /// Updates the paid amount on an existing sale and syncs customer debt.
        /// </summary>
        public void UpdatePayment(int saleId, decimal newAmountPaid, string paymentType = "")
        {
            if (newAmountPaid < 0)
                throw new ArgumentException("Payment amount cannot be negative.");
            _saleRepo.UpdatePayment(saleId, newAmountPaid, paymentType);
        }

        public void DeleteSale(int saleId)
        {
            var sale = GetSaleById(saleId) ?? throw new Exception("Satýþ kaydý bulunamadý.");

            using var conn = DatabaseContext.GetConnection();
            using var tx = conn.BeginTransaction();
            try
            {
                // 1. Müþterinin borcunu iptal edilen satýþýn kalan borcu kadar düþ
                using var cmdCust = conn.CreateCommand();
                cmdCust.Transaction = tx;
                cmdCust.CommandText = "UPDATE customers SET total_debt = total_debt - @rd WHERE id = @cid";
                cmdCust.Parameters.AddWithValue("@rd", sale.RemainingDebt);
                cmdCust.Parameters.AddWithValue("@cid", sale.CustomerId);
                cmdCust.ExecuteNonQuery();

                // 2. Satýlan ürünleri stoða geri ekle
                using var cmdStock = conn.CreateCommand();
                cmdStock.Transaction = tx;
                cmdStock.CommandText = "UPDATE products SET stock_qty = stock_qty + @qty WHERE id = @pid";
                cmdStock.Parameters.AddWithValue("@qty", sale.Qty);
                cmdStock.Parameters.AddWithValue("@pid", sale.ProductId);
                cmdStock.ExecuteNonQuery();

                // 3. Satýþý ve bu satýþa baðlý yapýlmýþ ödeme geçmiþlerini sil
                using var cmdDel = conn.CreateCommand();
                cmdDel.Transaction = tx;
                cmdDel.CommandText = @"
                    DELETE FROM sales WHERE id = @id; 
                    DELETE FROM debt_payments WHERE sale_id = @id;";
                cmdDel.Parameters.AddWithValue("@id", saleId);
                cmdDel.ExecuteNonQuery();

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        public void UpdateSale(Sale updatedSale)
        {
            using var conn = DatabaseContext.GetConnection();
            using var tx = conn.BeginTransaction();
            try
            {
                // 1. Eski satýþ kaydýný getir
                using var cmdGet = conn.CreateCommand();
                cmdGet.Transaction = tx;
                cmdGet.CommandText = "SELECT product_id, customer_id, qty, amount_paid, remaining_debt FROM sales WHERE id = @id";
                cmdGet.Parameters.AddWithValue("@id", updatedSale.Id);
                using var r = cmdGet.ExecuteReader();
                if (!r.Read()) throw new Exception("Satýþ kaydý bulunamadý.");

                int oldProductId = r.GetInt32(0);
                int oldCustomerId = r.GetInt32(1);
                decimal oldQty = r.GetDecimal(2);
                decimal oldRemainingDebt = r.GetDecimal(4);
                r.Close();

                // 2. Yeni deðerleri hesapla
                updatedSale.TotalPrice = Math.Round(updatedSale.Qty * updatedSale.UnitPrice, 2);
                updatedSale.AmountPaid = Math.Max(0, Math.Min(updatedSale.AmountPaid, updatedSale.TotalPrice));
                updatedSale.RemainingDebt = Math.Round(updatedSale.TotalPrice - updatedSale.AmountPaid, 2);

                // Aradaki farklarý (Deltalarý) bul
                decimal qtyDelta = updatedSale.Qty - oldQty; // Artýysa stoktan daha fazla düþecek
                decimal debtDelta = updatedSale.RemainingDebt - oldRemainingDebt; // Artýysa müþteri borcu artacak

                // Ürün deðiþikliði olduysa engelle (Mantýk karmaþasýný önlemek için)
                if (oldProductId != updatedSale.ProductId || oldCustomerId != updatedSale.CustomerId)
                    throw new Exception("Düzenleme sýrasýnda Müþteri veya Ürün deðiþtirilemez. Lütfen satýþý silip yeniden ekleyin.");

                // 3. Stok Miktarýný Güncelle (Satýþ arttýysa stok azalýr)
                using var cmdStock = conn.CreateCommand();
                cmdStock.Transaction = tx;
                cmdStock.CommandText = "UPDATE products SET stock_qty = stock_qty - @delta WHERE id = @pid";
                cmdStock.Parameters.AddWithValue("@delta", qtyDelta);
                cmdStock.Parameters.AddWithValue("@pid", updatedSale.ProductId);
                cmdStock.ExecuteNonQuery();

                // 4. Müþteri Borcunu Güncelle
                using var cmdDebt = conn.CreateCommand();
                cmdDebt.Transaction = tx;
                cmdDebt.CommandText = "UPDATE customers SET total_debt = total_debt + @delta WHERE id = @cid";
                cmdDebt.Parameters.AddWithValue("@delta", debtDelta);
                cmdDebt.Parameters.AddWithValue("@cid", updatedSale.CustomerId);
                cmdDebt.ExecuteNonQuery();

                // 5. Satýþ Kaydýný Güncelle
                using var cmdUpd = conn.CreateCommand();
                cmdUpd.Transaction = tx;
                cmdUpd.CommandText = @"UPDATE sales SET 
                                qty=@qty, 
                                unit_price=@up, 
                                total_price=@tp, 
                                amount_paid=@ap, 
                                remaining_debt=@rd, 
                                note=@note,
                                sale_date=@date 
                              WHERE id=@id";
                cmdUpd.Parameters.AddWithValue("@qty", updatedSale.Qty);
                cmdUpd.Parameters.AddWithValue("@up", updatedSale.UnitPrice);
                cmdUpd.Parameters.AddWithValue("@tp", updatedSale.TotalPrice);
                cmdUpd.Parameters.AddWithValue("@ap", updatedSale.AmountPaid);
                cmdUpd.Parameters.AddWithValue("@rd", updatedSale.RemainingDebt);
                cmdUpd.Parameters.AddWithValue("@note", updatedSale.Note ?? "");
                cmdUpd.Parameters.AddWithValue("@date", updatedSale.SaleDate);
                cmdUpd.Parameters.AddWithValue("@id", updatedSale.Id);
                
                int rowsAffected = cmdUpd.ExecuteNonQuery();
                if (rowsAffected == 0) throw new Exception("Güncelleme yapýlamadý, ID hatalý olabilir.");

                tx.Commit();
            }
            catch { tx.Rollback(); throw; }
        }

        public int CountSales(string search = "")
            => _saleRepo.CountAll(search);

        public decimal GetTodayTotal() => _saleRepo.GetTotalSalesToday();
        public decimal GetMonthlyTotal() => _saleRepo.GetTotalSalesThisMonth();
        public int GetTodayOrders() => _saleRepo.GetActiveOrdersToday();
    }
}
