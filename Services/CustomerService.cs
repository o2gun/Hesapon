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
            if (amount <= 0)
                throw new ArgumentException("Ödeme tutarý sýfýrdan büyük olmalýdýr.");

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

        public decimal GetTotalOutstandingDebt()
        {
            using var conn = DatabaseContext.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COALESCE(SUM(total_debt),0) FROM customers";
            return Convert.ToDecimal(cmd.ExecuteScalar());
        }
    }
}
