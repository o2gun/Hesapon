using System;
using System.Collections.Generic;
using ConstruxERP.Models;
using ConstruxERP.Repositories;

namespace ConstruxERP.Services
{
    public class SupplierService
    {
        // Repo bağlantımız (Sadece 1 tane olmalı)
        private readonly SupplierRepository _repo = new();

        // --- TEMEL CRUD İŞLEMLERİ ---

        public List<Supplier> GetAll(string search, bool searchName, bool searchContact, decimal minDebt, int page, int pageSize)
        {
            return _repo.GetAll(search, searchName, searchContact, minDebt, page, pageSize);
        }

        public List<Supplier> GetAll()
        {
            // Arka planda gidip yine asıl metodu çağırır, ama varsayılan değerlerle.
            // Arama yok, sadece isimde ara, borç sınırı yok, 1. sayfa, 10.000 kayıt getir.
            return _repo.GetAll("", true, false, decimal.MinValue, 1, 10000);
        }

        public int CountAll(string search, bool searchName, bool searchContact, decimal minDebt)
        {
            return _repo.CountAll(search, searchName, searchContact, minDebt);
        }

        public Supplier GetById(int id)
        {
            return _repo.GetById(id);
        }

        public void AddSupplier(Supplier s)
        {
            if (string.IsNullOrWhiteSpace(s.Name)) return;
            _repo.Insert(s);
        }

        public void UpdateSupplier(Supplier s)
        {
            if (string.IsNullOrWhiteSpace(s.Name)) return;
            _repo.Update(s);
        }


        // --- ÖDEME GEÇMİŞİ VE DETAY İŞLEMLERİ ---

        public List<SupplierPayment> GetPaymentHistory(int supplierId)
        {
            var list = new List<SupplierPayment>();
            using var conn = DatabaseContext.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, supplier_id, amount, notes, paid_at FROM supplier_payments WHERE supplier_id = @sid ORDER BY paid_at DESC";
            cmd.Parameters.AddWithValue("@sid", supplierId);

            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new SupplierPayment
                {
                    Id = r.GetInt32(0),
                    SupplierId = r.GetInt32(1),
                    Amount = r.GetDecimal(2),
                    Notes = r.IsDBNull(3) ? "" : r.GetString(3),
                    PaidAt = r.GetString(4)
                });
            }
            return list;
        }

        public SupplierPayment GetPaymentById(int paymentId)
        {
            using var conn = DatabaseContext.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, supplier_id, amount, notes, paid_at FROM supplier_payments WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", paymentId);

            using var r = cmd.ExecuteReader();
            if (r.Read())
            {
                return new SupplierPayment
                {
                    Id = r.GetInt32(0),
                    SupplierId = r.GetInt32(1),
                    Amount = r.GetDecimal(2),
                    Notes = r.IsDBNull(3) ? "" : r.GetString(3),
                    PaidAt = r.GetString(4)
                };
            }
            return null;
        }

        public void DeleteSupplierPayment(int paymentId)
        {
            using var conn = DatabaseContext.GetConnection();
            using var tx = conn.BeginTransaction();
            try
            {
                using var cmdGet = conn.CreateCommand();
                cmdGet.Transaction = tx;
                cmdGet.CommandText = "SELECT supplier_id, purchase_id, amount FROM supplier_payments WHERE id = @id";
                cmdGet.Parameters.AddWithValue("@id", paymentId);

                int supplierId = 0;
                int? purchaseId = null;
                decimal amount = 0;

                using (var reader = cmdGet.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        supplierId = reader.GetInt32(0);
                        purchaseId = reader.IsDBNull(1) ? null : reader.GetInt32(1);
                        amount = reader.GetDecimal(2);
                    }
                    else return;
                }

                // Ödeme silindiği için şirketin tedarikçiye olan borcu GERİ ARTAR
                using var cmdSup = conn.CreateCommand();
                cmdSup.Transaction = tx;
                cmdSup.CommandText = "UPDATE suppliers SET total_debt = total_debt + @amt WHERE id = @sid";
                cmdSup.Parameters.AddWithValue("@amt", amount);
                cmdSup.Parameters.AddWithValue("@sid", supplierId);
                cmdSup.ExecuteNonQuery();

                if (purchaseId.HasValue)
                {
                    using var cmdPur = conn.CreateCommand();
                    cmdPur.Transaction = tx;
                    cmdPur.CommandText = "UPDATE purchases SET amount_paid = amount_paid - @amt, remaining_debt = remaining_debt + @amt WHERE id = @pid";
                    cmdPur.Parameters.AddWithValue("@amt", amount);
                    cmdPur.Parameters.AddWithValue("@pid", purchaseId.Value);
                    cmdPur.ExecuteNonQuery();
                }

                using var cmdDel = conn.CreateCommand();
                cmdDel.Transaction = tx;
                cmdDel.CommandText = "DELETE FROM supplier_payments WHERE id = @id";
                cmdDel.Parameters.AddWithValue("@id", paymentId);
                cmdDel.ExecuteNonQuery();

                tx.Commit();
            }
            catch { tx.Rollback(); throw; }
        }

        public void EditSupplierPayment(int paymentId, decimal newAmount, string newNotes)
        {
            if (newAmount == 0) throw new ArgumentException("Tutar sıfır olamaz. (İade için - kullanın)");

            using var conn = DatabaseContext.GetConnection();
            using var tx = conn.BeginTransaction();
            try
            {
                using var cmdGet = conn.CreateCommand();
                cmdGet.Transaction = tx;
                cmdGet.CommandText = "SELECT supplier_id, purchase_id, amount FROM supplier_payments WHERE id = @id";
                cmdGet.Parameters.AddWithValue("@id", paymentId);

                int supplierId = 0;
                int? purchaseId = null;
                decimal oldAmount = 0;

                using (var reader = cmdGet.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        supplierId = reader.GetInt32(0);
                        purchaseId = reader.IsDBNull(1) ? null : reader.GetInt32(1);
                        oldAmount = reader.GetDecimal(2);
                    }
                    else return;
                }

                decimal diff = newAmount - oldAmount;

                using var cmdUpdatePay = conn.CreateCommand();
                cmdUpdatePay.Transaction = tx;
                cmdUpdatePay.CommandText = "UPDATE supplier_payments SET amount = @amt, notes = @notes WHERE id = @id";
                cmdUpdatePay.Parameters.AddWithValue("@amt", newAmount);
                cmdUpdatePay.Parameters.AddWithValue("@notes", newNotes);
                cmdUpdatePay.Parameters.AddWithValue("@id", paymentId);
                cmdUpdatePay.ExecuteNonQuery();

                // Borçtan farkı düşüyoruz (Fazla ödediysek borç azalır)
                using var cmdSup = conn.CreateCommand();
                cmdSup.Transaction = tx;
                cmdSup.CommandText = "UPDATE suppliers SET total_debt = total_debt - @diff WHERE id = @sid";
                cmdSup.Parameters.AddWithValue("@diff", diff);
                cmdSup.Parameters.AddWithValue("@sid", supplierId);
                cmdSup.ExecuteNonQuery();

                if (purchaseId.HasValue)
                {
                    using var cmdPur = conn.CreateCommand();
                    cmdPur.Transaction = tx;
                    cmdPur.CommandText = @"
                        UPDATE purchases SET
                            remaining_debt = MAX(0, remaining_debt - @diff),
                            amount_paid    = amount_paid + @diff
                        WHERE id = @pid";
                    cmdPur.Parameters.AddWithValue("@diff", diff);
                    cmdPur.Parameters.AddWithValue("@pid", purchaseId.Value);
                    cmdPur.ExecuteNonQuery();
                }

                tx.Commit();
            }
            catch { tx.Rollback(); throw; }
        }
    }
}