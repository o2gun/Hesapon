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

        public void UpdateSale(Sale sale)
        {
            using var conn = DatabaseContext.GetConnection();
            using var tx = conn.BeginTransaction();
            try
            {
                using var cmdGet = conn.CreateCommand();
                cmdGet.Transaction = tx;
                cmdGet.CommandText = "SELECT customer_id, product_id, quantity, remaining_debt FROM sales WHERE id = @id";
                cmdGet.Parameters.AddWithValue("@id", sale.Id);

                int oldCustomerId = 0, oldProductId = 0;
                decimal oldQty = 0, oldRemainingDebt = 0;

                using (var reader = cmdGet.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        oldCustomerId = reader.GetInt32(0);
                        oldProductId = reader.GetInt32(1);
                        oldQty = reader.GetDecimal(2);
                        oldRemainingDebt = reader.GetDecimal(3);
                    }
                    else throw new Exception("Düzenlenmek istenen satýþ kaydý bulunamadý.");
                }

                if (oldCustomerId == sale.CustomerId)
                {
                    decimal debtDiff = sale.RemainingDebt - oldRemainingDebt;
                    if (debtDiff != 0)
                    {
                        using var cmdCust = conn.CreateCommand();
                        cmdCust.Transaction = tx;
                        cmdCust.CommandText = "UPDATE customers SET total_debt = total_debt + @diff WHERE id = @cid";
                        cmdCust.Parameters.AddWithValue("@diff", debtDiff);
                        cmdCust.Parameters.AddWithValue("@cid", sale.CustomerId);
                        cmdCust.ExecuteNonQuery();
                    }
                }
                else
                {
                    using var cmdOldCust = conn.CreateCommand();
                    cmdOldCust.Transaction = tx;
                    cmdOldCust.CommandText = "UPDATE customers SET total_debt = total_debt - @oldDebt WHERE id = @cid";
                    cmdOldCust.Parameters.AddWithValue("@oldDebt", oldRemainingDebt);
                    cmdOldCust.Parameters.AddWithValue("@cid", oldCustomerId);
                    cmdOldCust.ExecuteNonQuery();

                    using var cmdNewCust = conn.CreateCommand();
                    cmdNewCust.Transaction = tx;
                    cmdNewCust.CommandText = "UPDATE customers SET total_debt = total_debt + @newDebt WHERE id = @cid";
                    cmdNewCust.Parameters.AddWithValue("@newDebt", sale.RemainingDebt);
                    cmdNewCust.Parameters.AddWithValue("@cid", sale.CustomerId);
                    cmdNewCust.ExecuteNonQuery();
                }

                if (oldProductId == sale.ProductId)
                {
                    decimal qtyDiff = sale.Qty - oldQty;
                    if (qtyDiff != 0)
                    {
                        using var cmdProd = conn.CreateCommand();
                        cmdProd.Transaction = tx;
                        cmdProd.CommandText = "UPDATE products SET stock_quantity = stock_quantity - @diff WHERE id = @pid";
                        cmdProd.Parameters.AddWithValue("@diff", qtyDiff);
                        cmdProd.Parameters.AddWithValue("@pid", sale.ProductId);
                        cmdProd.ExecuteNonQuery();
                    }
                }
                else
                {
                    using var cmdOldProd = conn.CreateCommand();
                    cmdOldProd.Transaction = tx;
                    cmdOldProd.CommandText = "UPDATE products SET stock_quantity = stock_quantity + @oldQty WHERE id = @pid";
                    cmdOldProd.Parameters.AddWithValue("@oldQty", oldQty);
                    cmdOldProd.Parameters.AddWithValue("@pid", oldProductId);
                    cmdOldProd.ExecuteNonQuery();

                    using var cmdNewProd = conn.CreateCommand();
                    cmdNewProd.Transaction = tx;
                    cmdNewProd.CommandText = "UPDATE products SET stock_quantity = stock_quantity - @newQty WHERE id = @pid";
                    cmdNewProd.Parameters.AddWithValue("@newQty", sale.Qty);
                    cmdNewProd.Parameters.AddWithValue("@pid", sale.ProductId);
                    cmdNewProd.ExecuteNonQuery();
                }

                using var cmdUpdate = conn.CreateCommand();
                cmdUpdate.Transaction = tx;
                cmdUpdate.CommandText = @"
            UPDATE sales 
            SET customer_id = @cid, product_id = @pid, quantity = @qty, 
                unit_price = @up, total_price = @tp, amount_paid = @ap, 
                remaining_debt = @rd, sale_date = @sd 
            WHERE id = @id";
                cmdUpdate.Parameters.AddWithValue("@cid", sale.CustomerId);
                cmdUpdate.Parameters.AddWithValue("@pid", sale.ProductId);
                cmdUpdate.Parameters.AddWithValue("@qty", sale.Qty);
                cmdUpdate.Parameters.AddWithValue("@up", sale.UnitPrice);
                cmdUpdate.Parameters.AddWithValue("@tp", sale.TotalPrice);
                cmdUpdate.Parameters.AddWithValue("@ap", sale.AmountPaid);
                cmdUpdate.Parameters.AddWithValue("@rd", sale.RemainingDebt);
                cmdUpdate.Parameters.AddWithValue("@sd", sale.SaleDate);
                cmdUpdate.Parameters.AddWithValue("@id", sale.Id);
                cmdUpdate.ExecuteNonQuery();

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        public int CountSales(string search = "")
            => _saleRepo.CountAll(search);

        public decimal GetTodayTotal() => _saleRepo.GetTotalSalesToday();
        public decimal GetMonthlyTotal() => _saleRepo.GetTotalSalesThisMonth();
        public int GetTodayOrders() => _saleRepo.GetActiveOrdersToday();
    }
}
