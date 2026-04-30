using ConstruxERP.Models;
using ConstruxERP.Repositories;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;

namespace ConstruxERP.Services
{
    public class CustomerService
    {
        private readonly CustomerRepository _repo = new();

        public List<Customer> GetAll(string search = "", bool searchName = true, bool searchPhone = true, bool searchAddress = true, decimal minDebt = 0, int page = 1, int pageSize = 100)
        => _repo.GetAll(search, searchName, searchPhone, searchAddress, minDebt, page, pageSize);

        public int CountAll(string search = "", bool searchName = true, bool searchPhone = true, bool searchAddress = true, decimal minDebt = 0)
            => _repo.CountAll(search, searchName, searchPhone, searchAddress, minDebt);
        public List<Customer> GetDebtors() => _repo.GetWithDebt();
        public Customer? GetById(int id) => _repo.GetById(id);

        public List<Sale> GetSaleHistory(int customerId) => _repo.GetSaleHistory(customerId);
        public List<DebtPayment> GetPaymentHistory(int customerId) => _repo.GetPaymentHistory(customerId);

        public void AddCustomer(Customer c)
        {
            if (string.IsNullOrWhiteSpace(c.Name))
                throw new ArgumentException("Müþteri adý zorunludur.");
            _repo.Insert(c);
        }

        public void UpdateCustomer(Customer c)
        {
            if (string.IsNullOrWhiteSpace(c.Name))
                throw new ArgumentException("Müþteri adý zorunludur.");
            _repo.Update(c);
        }

        public void RecordPayment(int customerId, decimal amount, int? saleId = null, string notes = "")
        {
            if (amount == 0)
                throw new ArgumentException("Tutar sýfýr olamaz. Ýade/Para çýkýþý iþlemleri için tutarýn baþýna eksi (-) koyarak girebilirsiniz.");

            var customer = _repo.GetById(customerId)
                ?? throw new ArgumentException("Müþteri bulunamadý.");

            using var conn = DatabaseContext.GetConnection();
            using var tx = conn.BeginTransaction();
            try
            {
                using var cmdPay = conn.CreateCommand();
                cmdPay.Transaction = tx;
                cmdPay.CommandText = @"
                    INSERT INTO debt_payments (customer_id, sale_id, amount, notes)
                    VALUES (@cid, @sid, @amt, @notes)";
                cmdPay.Parameters.AddWithValue("@cid", customerId);
                cmdPay.Parameters.AddWithValue("@sid", saleId.HasValue ? saleId.Value : (object)System.DBNull.Value);
                cmdPay.Parameters.AddWithValue("@amt", amount);
                cmdPay.Parameters.AddWithValue("@notes", notes);
                cmdPay.ExecuteNonQuery();

                if (saleId.HasValue)
                {
                    using var cmdSale = conn.CreateCommand();
                    cmdSale.Transaction = tx;
                    cmdSale.CommandText = @"
                        UPDATE sales SET
                            remaining_debt = MAX(0, remaining_debt - @amt),
                            amount_paid    = amount_paid + @amt
                        WHERE id = @sid";
                    cmdSale.Parameters.AddWithValue("@amt", amount);
                    cmdSale.Parameters.AddWithValue("@sid", saleId.Value);
                    cmdSale.ExecuteNonQuery();
                }

                using var cmdCust = conn.CreateCommand();
                cmdCust.Transaction = tx;
                cmdCust.CommandText = "UPDATE customers SET total_debt = total_debt - @amt WHERE id = @cid";
                cmdCust.Parameters.AddWithValue("@amt", amount);
                cmdCust.Parameters.AddWithValue("@cid", customerId);
                cmdCust.ExecuteNonQuery();

                tx.Commit();
            }
            catch { tx.Rollback(); throw; }
        }

        public void EditPayment(int paymentId, decimal newAmount, string newNotes)
        {
            if (newAmount == 0)
                throw new ArgumentException("Tutar sýfýr olamaz. Ýade/Para çýkýþý iþlemleri için tutarýn baþýna eksi (-) koyarak girebilirsiniz.");

            using var conn = DatabaseContext.GetConnection();
            using var tx = conn.BeginTransaction();
            try
            {
                using var cmdGet = conn.CreateCommand();
                cmdGet.Transaction = tx;
                cmdGet.CommandText = "SELECT customer_id, sale_id, amount FROM debt_payments WHERE id = @id";
                cmdGet.Parameters.AddWithValue("@id", paymentId);

                int customerId = 0;
                int? saleId = null;
                decimal oldAmount = 0;

                using (var reader = cmdGet.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        customerId = reader.GetInt32(0);
                        saleId = reader.IsDBNull(1) ? null : reader.GetInt32(1);
                        oldAmount = reader.GetDecimal(2);
                    }
                    else return;
                }

                decimal diff = newAmount - oldAmount;

                using var cmdUpdatePay = conn.CreateCommand();
                cmdUpdatePay.Transaction = tx;
                cmdUpdatePay.CommandText = "UPDATE debt_payments SET amount = @amt, notes = @notes WHERE id = @id";
                cmdUpdatePay.Parameters.AddWithValue("@amt", newAmount);
                cmdUpdatePay.Parameters.AddWithValue("@notes", newNotes);
                cmdUpdatePay.Parameters.AddWithValue("@id", paymentId);
                cmdUpdatePay.ExecuteNonQuery();

                using var cmdCust = conn.CreateCommand();
                cmdCust.Transaction = tx;
                cmdCust.CommandText = "UPDATE customers SET total_debt = total_debt - @diff WHERE id = @cid";
                cmdCust.Parameters.AddWithValue("@diff", diff);
                cmdCust.Parameters.AddWithValue("@cid", customerId);
                cmdCust.ExecuteNonQuery();

                if (saleId.HasValue)
                {
                    using var cmdSale = conn.CreateCommand();
                    cmdSale.Transaction = tx;
                    cmdSale.CommandText = @"
                UPDATE sales SET
                    remaining_debt = MAX(0, remaining_debt - @diff),
                    amount_paid    = amount_paid + @diff
                WHERE id = @sid";
                    cmdSale.Parameters.AddWithValue("@diff", diff);
                    cmdSale.Parameters.AddWithValue("@sid", saleId.Value);
                    cmdSale.ExecuteNonQuery();
                }

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        public void DeletePayment(int paymentId)
        {
            using var conn = DatabaseContext.GetConnection();
            using var tx = conn.BeginTransaction();
            try
            {
                // 1. Önce silinecek ödemenin bilgilerini (müþteri, tutar, baðlý olduðu satýþ) alalým
                using var cmdGet = conn.CreateCommand();
                cmdGet.Transaction = tx;
                cmdGet.CommandText = "SELECT customer_id, sale_id, amount FROM debt_payments WHERE id = @id";
                cmdGet.Parameters.AddWithValue("@id", paymentId);

                int customerId = 0;
                int? saleId = null;
                decimal amount = 0;

                using (var reader = cmdGet.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        customerId = reader.GetInt32(0);
                        saleId = reader.IsDBNull(1) ? null : reader.GetInt32(1);
                        amount = reader.GetDecimal(2);
                    }
                    else
                    {
                        // Eðer id bulunamazsa sessizce çýk veya hata fýrlat
                        return;
                    }
                }

                // 2. Müþterinin toplam borcunu, sildiðimiz ödeme tutarý kadar GERÝ ARTIRALIM
                using var cmdCust = conn.CreateCommand();
                cmdCust.Transaction = tx;
                cmdCust.CommandText = "UPDATE customers SET total_debt = total_debt + @amt WHERE id = @cid";
                cmdCust.Parameters.AddWithValue("@amt", amount);
                cmdCust.Parameters.AddWithValue("@cid", customerId);
                cmdCust.ExecuteNonQuery();

                // 3. Eðer bu ödeme belirli bir iþleme/satýþa baðlý yapýldýysa, o satýþýn hesabýný da GERÝ ALALIM
                if (saleId.HasValue)
                {
                    using var cmdSale = conn.CreateCommand();
                    cmdSale.Transaction = tx;
                    cmdSale.CommandText = @"
                UPDATE sales 
                SET amount_paid = amount_paid - @amt, 
                    remaining_debt = remaining_debt + @amt 
                WHERE id = @sid";
                    cmdSale.Parameters.AddWithValue("@amt", amount);
                    cmdSale.Parameters.AddWithValue("@sid", saleId.Value);
                    cmdSale.ExecuteNonQuery();
                }

                // 4. Son olarak ödeme kaydýnýn kendisini silelim
                using var cmdDel = conn.CreateCommand();
                cmdDel.Transaction = tx;
                cmdDel.CommandText = "DELETE FROM debt_payments WHERE id = @id";
                cmdDel.Parameters.AddWithValue("@id", paymentId);
                cmdDel.ExecuteNonQuery();

                // Tüm adýmlar baþarýlýysa iþlemi onayla
                tx.Commit();
            }
            catch
            {
                // Herhangi bir adýmda hata olursa tüm iþlemleri geri al (veritabaný tutarlýlýðý için)
                tx.Rollback();
                throw;
            }
        }

        public DebtPayment GetPaymentById(int paymentId)
        {
            using var conn = DatabaseContext.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, customer_id, amount, notes, paid_at FROM debt_payments WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", paymentId);

            using var r = cmd.ExecuteReader();
            if (r.Read())
            {
                return new DebtPayment
                {
                    Id = r.GetInt32(0),
                    CustomerId = r.GetInt32(1),
                    Amount = r.GetDecimal(2),
                    Notes = r.GetString(3),
                    PaidAt = r.GetString(4)
                };
            }
            return null;
        }

        public decimal GetTotalOutstandingDebt()
        {
            using var conn = DatabaseContext.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COALESCE(SUM(total_debt),0) FROM customers";
            return Convert.ToDecimal(cmd.ExecuteScalar());
        }
    }
}
