using ConstruxERP.Models;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;

namespace ConstruxERP.Repositories
{
    public class CustomerRepository
    {
        // ─── Read ─────────────────────────────────────────────────────────────

        public List<Customer> GetAll(string search = "", bool searchName = true, bool searchPhone = true, bool searchAddress = true, decimal minDebt = 0, int page = 1, int pageSize = 100)
        {
            var list = new List<Customer>();
            int offset = (page - 1) * pageSize;
            using var conn = DatabaseContext.GetConnection();
            using var cmd = conn.CreateCommand();

            string searchCondition = "";
            if (!string.IsNullOrWhiteSpace(search))
            {
                var conditions = new List<string>();
                if (searchName) conditions.Add("c.name LIKE @s");
                if (searchPhone) conditions.Add("c.phone LIKE @s");
                if (searchAddress) conditions.Add("c.address LIKE @s");

                if (conditions.Count > 0)
                {
                    searchCondition = "AND (" + string.Join(" OR ", conditions) + ")";
                    cmd.Parameters.AddWithValue("@s", $"%{search}%");
                }
            }

            cmd.CommandText = $@"
                SELECT c.id, c.name, c.phone, c.email,
                       c.address, c.billing_address,
                       c.total_debt, c.created_at,
                       (SELECT COALESCE(SUM(total_price), 0) FROM sales WHERE customer_id = c.id) AS total_purchases,
                       (SELECT COALESCE(SUM(amount), 0) FROM debt_payments WHERE customer_id = c.id) AS total_paid
                FROM customers c
                LEFT JOIN sales s ON s.customer_id = c.id
                WHERE c.total_debt >= @minDebt {searchCondition}
                GROUP BY c.id
                ORDER BY c.name
                LIMIT @limit OFFSET @offset";

            cmd.Parameters.AddWithValue("@minDebt", minDebt);
            cmd.Parameters.AddWithValue("@limit", pageSize);
            cmd.Parameters.AddWithValue("@offset", offset);

            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(MapFull(r));
            return list;
        }

        public int CountAll(string search = "", bool searchName = true, bool searchPhone = true, bool searchAddress = true, decimal minDebt = 0)
        {
            using var conn = DatabaseContext.GetConnection();
            using var cmd = conn.CreateCommand();

            string searchCondition = "";
            if (!string.IsNullOrWhiteSpace(search))
            {
                var conditions = new List<string>();
                if (searchName) conditions.Add("c.name LIKE @s");
                if (searchPhone) conditions.Add("c.phone LIKE @s");
                if (searchAddress) conditions.Add("c.address LIKE @s");

                if (conditions.Count > 0)
                {
                    searchCondition = "AND (" + string.Join(" OR ", conditions) + ")";
                    cmd.Parameters.AddWithValue("@s", $"%{search}%");
                }
            }

            cmd.CommandText = $@"
                SELECT COUNT(*)
                FROM customers c
                WHERE c.total_debt >= @minDebt {searchCondition}";

            cmd.Parameters.AddWithValue("@minDebt", minDebt);

            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public List<Customer> GetWithDebt()
        {
            var list = new List<Customer>();
            using var conn = DatabaseContext.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT c.id, c.name, c.phone, c.email,
                       c.address, c.billing_address,
                       c.total_debt, c.created_at,
                       (SELECT COALESCE(SUM(total_price), 0) FROM sales WHERE customer_id = c.id) AS total_purchases,
                       (SELECT COALESCE(SUM(amount), 0) FROM debt_payments WHERE customer_id = c.id) AS total_paid
                FROM customers c
                LEFT JOIN sales s ON s.customer_id = c.id
                WHERE c.total_debt > 0
                GROUP BY c.id
                ORDER BY c.total_debt DESC";
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(MapFull(r));
            return list;
        }

        public Customer? GetById(int id)
        {
            using var conn = DatabaseContext.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT c.id, c.name, c.phone, c.email,
                       c.address, c.billing_address,
                       c.total_debt, c.created_at,
                       COALESCE(SUM(s.total_price), 0),
                       COALESCE(SUM(s.amount_paid),  0)
                FROM customers c
                LEFT JOIN sales s ON s.customer_id = c.id
                WHERE c.id = @id
                GROUP BY c.id";
            cmd.Parameters.AddWithValue("@id", id);
            using var r = cmd.ExecuteReader();
            return r.Read() ? MapFull(r) : null;
        }

        // ─── Sales history ─────────────────────────────────────────────────────

        public List<Sale> GetSaleHistory(int customerId)
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
                WHERE s.customer_id = @cid
                ORDER BY s.sale_date DESC";
            cmd.Parameters.AddWithValue("@cid", customerId);
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(new Sale
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
                });
            return list;
        }

        // ─── Payment history ───────────────────────────────────────────────────

        public List<DebtPayment> GetPaymentHistory(int customerId)
        {
            var list = new List<DebtPayment>();
            using var conn = DatabaseContext.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT dp.id, dp.customer_id, dp.sale_id, dp.amount,
                       dp.paid_at, dp.notes, c.name
                FROM debt_payments dp
                JOIN customers c ON c.id = dp.customer_id
                WHERE dp.customer_id = @cid
                ORDER BY dp.paid_at DESC";
            cmd.Parameters.AddWithValue("@cid", customerId);
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(new DebtPayment
                {
                    Id = r.GetInt32(0),
                    CustomerId = r.GetInt32(1),
                    SaleId = r.IsDBNull(2) ? null : r.GetInt32(2),
                    Amount = r.GetDecimal(3),
                    PaidAt = r.GetString(4),
                    Notes = r.GetString(5),
                    CustomerName = r.GetString(6)
                });
            return list;
        }

        // ─── Write ────────────────────────────────────────────────────────────

        public int Insert(Customer c)
        {
            using var conn = DatabaseContext.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO customers (name, phone, email, address, billing_address)
                VALUES (@name, @phone, @email, @address, @billing);
                SELECT last_insert_rowid();";
            BindParams(cmd, c);
            return Convert.ToInt32(cmd.ExecuteScalar()); // BUG FIXED HERE
        }

        public void Update(Customer c)
        {
            using var conn = DatabaseContext.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE customers
                SET name=@name, phone=@phone, email=@email,
                    address=@address, billing_address=@billing
                WHERE id=@id";
            BindParams(cmd, c);
            cmd.Parameters.AddWithValue("@id", c.Id);
            cmd.ExecuteNonQuery();
        }

        public void AdjustDebt(int customerId, decimal delta, SqliteConnection? conn = null)
        {
            bool owned = conn == null;
            conn ??= DatabaseContext.GetConnection();
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    UPDATE customers
                    SET total_debt = total_debt + @delta
                    WHERE id = @id";
                cmd.Parameters.AddWithValue("@delta", delta);
                cmd.Parameters.AddWithValue("@id", customerId);
                cmd.ExecuteNonQuery();
            }
            finally { if (owned) conn.Dispose(); }
        }

        public void Delete(int id)
        {
            using var conn = DatabaseContext.GetConnection();
            using var tx = conn.BeginTransaction();
            try
            {
                // 1. Önce bu müşteriye ait tüm ödeme geçmişini sil
                using var cmdPay = conn.CreateCommand();
                cmdPay.Transaction = tx;
                cmdPay.CommandText = "DELETE FROM debt_payments WHERE customer_id = @id";
                cmdPay.Parameters.AddWithValue("@id", id);
                cmdPay.ExecuteNonQuery();

                // 2. Sonra bu müşteriye ait tüm satış geçmişini sil
                using var cmdSale = conn.CreateCommand();
                cmdSale.Transaction = tx;
                cmdSale.CommandText = "DELETE FROM sales WHERE customer_id = @id";
                cmdSale.Parameters.AddWithValue("@id", id);
                cmdSale.ExecuteNonQuery();

                // 3. En son müşterinin kendisini sil
                using var cmdCust = conn.CreateCommand();
                cmdCust.Transaction = tx;
                cmdCust.CommandText = "DELETE FROM customers WHERE id = @id";
                cmdCust.Parameters.AddWithValue("@id", id);
                cmdCust.ExecuteNonQuery();

                tx.Commit(); // Her şey başarılıysa onayla
            }
            catch
            {
                tx.Rollback(); // Hata çıkarsa işlemi geri al
                throw;
            }
        }

        // ─── Helpers ──────────────────────────────────────────────────────────

        private static Customer MapFull(SqliteDataReader r) => new()
        {
            Id = r.GetInt32(0),
            Name = r.GetString(1),
            Phone = r.GetString(2),
            Email = r.GetString(3),
            Address = r.GetString(4),
            BillingAddress = r.GetString(5),
            TotalDebt = r.GetDecimal(6),
            CreatedAt = r.GetString(7),
            TotalPurchases = r.GetDecimal(8),
            TotalPaid = r.GetDecimal(9)
        };

        private static void BindParams(SqliteCommand cmd, Customer c)
        {
            cmd.Parameters.AddWithValue("@name", c.Name);
            cmd.Parameters.AddWithValue("@phone", c.Phone);
            cmd.Parameters.AddWithValue("@email", c.Email);
            cmd.Parameters.AddWithValue("@address", c.Address);
            cmd.Parameters.AddWithValue("@billing", c.BillingAddress);
        }
    }
}