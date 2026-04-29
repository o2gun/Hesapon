using ConstruxERP.Models;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;

namespace ConstruxERP.Repositories
{
    public class SaleRepository
    {
        // ─── Read ─────────────────────────────────────────────────────────────

        public List<Sale> GetAll(string search = "", int page = 1, int pageSize = 50)
        {
            var list = new List<Sale>();
            int offset = (page - 1) * pageSize;
            using var conn = DatabaseContext.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT s.id, s.customer_id, s.product_id, s.qty, s.unit_price,
                       s.total_price, s.amount_paid, s.remaining_debt,
                       s.payment_type, COALESCE(s.note,''), s.sale_date,
                       c.name, p.name, p.unit
                FROM sales s
                JOIN customers c ON c.id = s.customer_id
                JOIN products  p ON p.id = s.product_id
                WHERE (@s = '' OR c.name LIKE @s OR p.name LIKE @s)
                ORDER BY s.sale_date DESC
                LIMIT @limit OFFSET @offset";
            cmd.Parameters.AddWithValue("@s",
                string.IsNullOrWhiteSpace(search) ? "" : $"%{search}%");
            cmd.Parameters.AddWithValue("@limit", pageSize);
            cmd.Parameters.AddWithValue("@offset", offset);
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(Map(r));
            return list;
        }

        public Sale? GetById(int id)
        {
            using var conn = DatabaseContext.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT s.id, s.customer_id, s.product_id, s.qty, s.unit_price,
                       s.total_price, s.amount_paid, s.remaining_debt,
                       s.payment_type, COALESCE(s.note,''), s.sale_date,
                       c.name, p.name, p.unit
                FROM sales s
                JOIN customers c ON c.id = s.customer_id
                JOIN products  p ON p.id = s.product_id
                WHERE s.id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var r = cmd.ExecuteReader();
            return r.Read() ? Map(r) : null;
        }

        public List<Sale> GetByDateRange(DateTime from, DateTime to)
        {
            var list = new List<Sale>();
            using var conn = DatabaseContext.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT s.id, s.customer_id, s.product_id, s.qty, s.unit_price,
                       s.total_price, s.amount_paid, s.remaining_debt,
                       s.payment_type, COALESCE(s.note,''), s.sale_date,
                       c.name, p.name, p.unit
                FROM sales s
                JOIN customers c ON c.id = s.customer_id
                JOIN products  p ON p.id = s.product_id
                WHERE s.sale_date BETWEEN @from AND @to
                ORDER BY s.sale_date DESC";
            cmd.Parameters.AddWithValue("@from", from.ToString("yyyy-MM-dd 00:00:00"));
            cmd.Parameters.AddWithValue("@to", to.ToString("yyyy-MM-dd 23:59:59"));
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(Map(r));
            return list;
        }

        public int CountAll(string search = "")
        {
            using var conn = DatabaseContext.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT COUNT(*) FROM sales s
                JOIN customers c ON c.id = s.customer_id
                JOIN products  p ON p.id = s.product_id
                WHERE (@s = '' OR c.name LIKE @s OR p.name LIKE @s)";
            cmd.Parameters.AddWithValue("@s",
                string.IsNullOrWhiteSpace(search) ? "" : $"%{search}%");
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        // ─── Write ────────────────────────────────────────────────────────────

        public int Insert(Sale sale, SqliteTransaction? existingTx = null)
        {
            SqliteConnection? conn = existingTx != null ? existingTx.Connection : DatabaseContext.GetConnection();

            SqliteTransaction? localTx = null;
            if (existingTx == null)
            {
                if (conn.State != System.Data.ConnectionState.Open) conn.Open();
                localTx = conn.BeginTransaction();
            }

            var tx = existingTx ?? localTx;

            try
            {
                using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = @"
            INSERT INTO sales
                (customer_id, product_id, qty, unit_price, total_price,
                 amount_paid, remaining_debt, payment_type, note, sale_date)
            VALUES
                (@cid, @pid, @qty, @up, @tp, @ap, @rd, @pt, @note, @sdate);
            SELECT last_insert_rowid();";

                cmd.Parameters.AddWithValue("@cid", sale.CustomerId);
                cmd.Parameters.AddWithValue("@pid", sale.ProductId);
                cmd.Parameters.AddWithValue("@qty", sale.Qty);
                cmd.Parameters.AddWithValue("@up", sale.UnitPrice);
                cmd.Parameters.AddWithValue("@tp", sale.TotalPrice);
                cmd.Parameters.AddWithValue("@ap", sale.AmountPaid);
                cmd.Parameters.AddWithValue("@rd", sale.RemainingDebt);
                cmd.Parameters.AddWithValue("@pt", sale.PaymentType);
                cmd.Parameters.AddWithValue("@note", sale.Note ?? "");
                cmd.Parameters.AddWithValue("@sdate", string.IsNullOrWhiteSpace(sale.SaleDate) ? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") : sale.SaleDate);

                int saleId = Convert.ToInt32(cmd.ExecuteScalar());

                using var cmdStock = conn.CreateCommand();
                cmdStock.Transaction = tx;
                cmdStock.CommandText = "UPDATE products SET stock_qty = stock_qty - @qty WHERE id = @pid";
                cmdStock.Parameters.AddWithValue("@qty", sale.Qty);
                cmdStock.Parameters.AddWithValue("@pid", sale.ProductId);
                cmdStock.ExecuteNonQuery();

                using var cmdMov = conn.CreateCommand();
                cmdMov.Transaction = tx;
                cmdMov.CommandText = "INSERT INTO stock_movements (product_id, qty_change, reason, reference) VALUES (@pid, @qty, 'sale', @ref)";
                cmdMov.Parameters.AddWithValue("@pid", sale.ProductId);
                cmdMov.Parameters.AddWithValue("@qty", -sale.Qty);
                cmdMov.Parameters.AddWithValue("@ref", saleId);
                cmdMov.ExecuteNonQuery();

                if (sale.AmountPaid > 0)
                {
                    using var cmdPay = conn.CreateCommand();
                    cmdPay.Transaction = tx;
                    cmdPay.CommandText = "INSERT INTO debt_payments (customer_id, sale_id, amount, notes) VALUES (@cid, @sid, @amt, @notes)";
                    cmdPay.Parameters.AddWithValue("@cid", sale.CustomerId);
                    cmdPay.Parameters.AddWithValue("@sid", saleId);
                    cmdPay.Parameters.AddWithValue("@amt", sale.AmountPaid);
                    cmdPay.Parameters.AddWithValue("@notes", $"İlk ödeme — Satış #{saleId}");
                    cmdPay.ExecuteNonQuery();
                }

                if (sale.RemainingDebt != 0)
                {
                    using var cmdDebt = conn.CreateCommand();
                    cmdDebt.Transaction = tx;
                    cmdDebt.CommandText = "UPDATE customers SET total_debt = total_debt + @debt WHERE id = @cid";
                    cmdDebt.Parameters.AddWithValue("@debt", sale.RemainingDebt);
                    cmdDebt.Parameters.AddWithValue("@cid", sale.CustomerId);
                    cmdDebt.ExecuteNonQuery();
                }

                localTx?.Commit();
                return saleId;
            }
            catch
            {
                localTx?.Rollback();
                throw;
            }
            finally
            {
                if (existingTx == null) conn.Close();
            }
        }

        public void UpdatePayment(int saleId, decimal newAmountPaid, string paymentType)
        {
            using var conn = DatabaseContext.GetConnection();
            using var tx = conn.BeginTransaction();
            try
            {
                // Mevcut satış bilgilerini oku
                using var cmdGet = conn.CreateCommand();
                cmdGet.Transaction = tx;
                cmdGet.CommandText = @"
                    SELECT total_price, amount_paid, remaining_debt, customer_id
                    FROM sales WHERE id = @id";
                cmdGet.Parameters.AddWithValue("@id", saleId);
                using var r = cmdGet.ExecuteReader();
                if (!r.Read()) throw new Exception("Satış bulunamadı.");
                decimal totalPrice = r.GetDecimal(0);
                decimal oldAmountPaid = r.GetDecimal(1);
                decimal oldDebt = r.GetDecimal(2);
                int customerId = r.GetInt32(3);
                r.Close();

                decimal safeNewPaid = Math.Max(0, Math.Min(newAmountPaid, totalPrice));
                decimal newDebt = Math.Round(totalPrice - safeNewPaid, 2);
                decimal debtDelta = newDebt - oldDebt;       // negatif = borç azaldı

                // Gerçekten ödenen ek miktar (yeni toplam - eski toplam)
                decimal additionalPaid = safeNewPaid - oldAmountPaid;

                if (string.IsNullOrWhiteSpace(paymentType))
                    paymentType = safeNewPaid >= totalPrice ? "cash"
                                : safeNewPaid == 0 ? "credit" : "partial";

                // 1. Sales tablosunu güncelle
                using var cmdUpd = conn.CreateCommand();
                cmdUpd.Transaction = tx;
                cmdUpd.CommandText = @"
                    UPDATE sales
                    SET amount_paid=@ap, remaining_debt=@rd, payment_type=@pt
                    WHERE id=@id";
                cmdUpd.Parameters.AddWithValue("@ap", safeNewPaid);
                cmdUpd.Parameters.AddWithValue("@rd", newDebt);
                cmdUpd.Parameters.AddWithValue("@pt", paymentType);
                cmdUpd.Parameters.AddWithValue("@id", saleId);
                cmdUpd.ExecuteNonQuery();

                // 2. Ek ödeme debt_payments tablosuna yaz
                //    (additionalPaid > 0 = yeni para geldi; ≤ 0 = sadece tip değişti, kayıt açma)
                if (additionalPaid > 0)
                {
                    using var cmdPay = conn.CreateCommand();
                    cmdPay.Transaction = tx;
                    cmdPay.CommandText = @"
                        INSERT INTO debt_payments (customer_id, sale_id, amount, notes)
                        VALUES (@cid, @sid, @amt, @notes)";
                    cmdPay.Parameters.AddWithValue("@cid", customerId);
                    cmdPay.Parameters.AddWithValue("@sid", saleId);
                    cmdPay.Parameters.AddWithValue("@amt", additionalPaid);
                    cmdPay.Parameters.AddWithValue("@notes", $"Ek ödeme — Satış #{saleId}");
                    cmdPay.ExecuteNonQuery();
                }

                // 3. Müşteri toplam borcunu güncelle
                using var cmdCust = conn.CreateCommand();
                cmdCust.Transaction = tx;
                cmdCust.CommandText = @"
                    UPDATE customers
                    SET total_debt = MAX(0, total_debt + @delta)
                    WHERE id = @cid";
                cmdCust.Parameters.AddWithValue("@delta", debtDelta);
                cmdCust.Parameters.AddWithValue("@cid", customerId);
                cmdCust.ExecuteNonQuery();

                tx.Commit();
            }
            catch { tx.Rollback(); throw; }
        }

        public void Delete(int saleId)
        {
            using var conn = DatabaseContext.GetConnection();
            using var tx = conn.BeginTransaction();
            try
            {
                using var cmdGet = conn.CreateCommand();
                cmdGet.Transaction = tx;
                cmdGet.CommandText = "SELECT remaining_debt, customer_id FROM sales WHERE id = @id";
                cmdGet.Parameters.AddWithValue("@id", saleId);
                using var r = cmdGet.ExecuteReader();
                if (!r.Read()) throw new Exception("Satış bulunamadı.");
                decimal debt = r.GetDecimal(0);
                int customerId = r.GetInt32(1);
                r.Close();

                using var cmdDel = conn.CreateCommand();
                cmdDel.Transaction = tx;
                cmdDel.CommandText = "DELETE FROM sales WHERE id = @id";
                cmdDel.Parameters.AddWithValue("@id", saleId);
                cmdDel.ExecuteNonQuery();

                if (debt > 0)
                {
                    using var cmdCust = conn.CreateCommand();
                    cmdCust.Transaction = tx;
                    cmdCust.CommandText = @"
                        UPDATE customers SET total_debt = MAX(0, total_debt - @debt) WHERE id = @cid";
                    cmdCust.Parameters.AddWithValue("@debt", debt);
                    cmdCust.Parameters.AddWithValue("@cid", customerId);
                    cmdCust.ExecuteNonQuery();
                }

                tx.Commit();
            }
            catch { tx.Rollback(); throw; }
        }

        // ─── Summary ─────────────────────────────────────────────────────────

        public decimal GetTotalSalesToday()
        {
            using var conn = DatabaseContext.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT COALESCE(SUM(total_price),0) FROM sales
                WHERE date(sale_date) = date('now','localtime')";
            return Convert.ToDecimal(cmd.ExecuteScalar());
        }

        public decimal GetTotalSalesThisMonth()
        {
            using var conn = DatabaseContext.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT COALESCE(SUM(total_price),0) FROM sales
                WHERE strftime('%Y-%m', sale_date) = strftime('%Y-%m','now','localtime')";
            return Convert.ToDecimal(cmd.ExecuteScalar());
        }

        public int GetActiveOrdersToday()
        {
            using var conn = DatabaseContext.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT COUNT(*) FROM sales
                WHERE date(sale_date) = date('now','localtime')";
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        // ─── Helpers ─────────────────────────────────────────────────────────

        private static Sale Map(SqliteDataReader r) => new()
        {
            Id = r.GetInt32(0),
            CustomerId = r.GetInt32(1),
            ProductId = r.GetInt32(2),
            Qty = r.GetDecimal(3),
            UnitPrice = r.GetDecimal(4),
            TotalPrice = r.GetDecimal(5),
            AmountPaid = r.GetDecimal(6),
            RemainingDebt = r.GetDecimal(7),
            PaymentType = r.GetString(8),
            Note = r.GetString(9),
            SaleDate = r.GetString(10),
            CustomerName = r.GetString(11),
            ProductName = r.GetString(12),
            ProductUnit = r.GetString(13)
        };
    }
}
