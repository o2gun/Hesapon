using ConstruxERP.Models;
using Microsoft.Data.Sqlite;
using System.Collections.Generic;

namespace ConstruxERP.Repositories
{
    public class CustomerRepository
    {
        // ─── Read ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns all customers with aggregated TotalPurchases and TotalPaid
        /// calculated in a single LEFT JOIN query — no N+1 problem.
        /// </summary>
        public List<Customer> GetAll(string search = "")
        {
            var list = new List<Customer>();
            using var conn = DatabaseContext.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT c.id, c.name, c.phone, c.email,
                       c.address, c.billing_address,
                       c.total_debt, c.created_at,
                       COALESCE(SUM(s.total_price), 0) AS total_purchases,
                       COALESCE(SUM(s.amount_paid),  0) AS total_paid
                FROM customers c
                LEFT JOIN sales s ON s.customer_id = c.id
                WHERE (@s = '' OR c.name LIKE @s OR c.phone LIKE @s)
                GROUP BY c.id
                ORDER BY c.name";
            cmd.Parameters.AddWithValue("@s",
                string.IsNullOrWhiteSpace(search) ? "" : $"%{search}%");
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(MapFull(r));
            return list;
        }

        /// <summary>Customers with outstanding debt, including aggregated totals.</summary>
        public List<Customer> GetWithDebt()
        {
            var list = new List<Customer>();
            using var conn = DatabaseContext.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT c.id, c.name, c.phone, c.email,
                       c.address, c.billing_address,
                       c.total_debt, c.created_at,
                       COALESCE(SUM(s.total_price), 0) AS total_purchases,
                       COALESCE(SUM(s.amount_paid),  0) AS total_paid
                FROM customers c
                LEFT JOIN sales s ON s.customer_id = c.id
                WHERE c.total_debt > 0
                GROUP BY c.id
                ORDER BY c.total_debt DESC";
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(MapFull(r));
            return list;
        }

        /// <summary>Single customer by id (no aggregation needed here).</summary>
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
            return (int)(long)cmd.ExecuteScalar()!;
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

        public void AdjustDebt(int customerId, decimal delta,
            SqliteConnection? conn = null)
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
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM customers WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        // ─── Helpers ──────────────────────────────────────────────────────────

        /// <summary>Maps a reader that includes the two aggregated columns at indices 8 and 9.</summary>
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
