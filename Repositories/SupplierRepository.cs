using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using ConstruxERP.Models;

namespace ConstruxERP.Repositories
{
    public class SupplierRepository
    {
        // 1. LİSTELEME (Arama, Sayfalama, Alt Sorgular ile Bakiye Hesaplama)
        public List<Supplier> GetAll(string search, bool searchName, bool searchContact, decimal minDebt, int page, int pageSize)
        {
            var list = new List<Supplier>();
            using var conn = DatabaseContext.GetConnection();
            using var cmd = conn.CreateCommand();

            string searchCondition = "";
            if (!string.IsNullOrWhiteSpace(search))
            {
                var conditions = new List<string>();
                if (searchName) conditions.Add("s.name LIKE @search");
                if (searchContact) conditions.Add("s.contact_name LIKE @search");

                // Telefon veya adres araması da eklenebilir, UI'a göre burayı esnetebilirsin.
                // Şimdilik isim ve yetkili kişi içinde arıyoruz.
                if (conditions.Count > 0)
                {
                    searchCondition = " AND (" + string.Join(" OR ", conditions) + ")";
                    cmd.Parameters.AddWithValue("@search", $"%{search}%");
                }
            }

            // SubQuery (Alt sorgu) ile Alımları ve Ödemeleri topluyoruz ki Kartezyen Çarpım hatası olmasın!
            cmd.CommandText = $@"
                SELECT s.id, s.name, s.contact_name, s.phone, s.email, s.address, s.total_debt, s.created_at,
                       (SELECT COALESCE(SUM(total_price), 0) FROM purchases WHERE supplier_id = s.id) AS total_purchased,
                       (SELECT COALESCE(SUM(amount), 0) FROM supplier_payments WHERE supplier_id = s.id) AS total_paid
                FROM suppliers s
                WHERE s.total_debt >= @minDebt {searchCondition}
                ORDER BY s.name
                LIMIT @limit OFFSET @offset";

            cmd.Parameters.AddWithValue("@minDebt", minDebt);
            cmd.Parameters.AddWithValue("@limit", pageSize);
            cmd.Parameters.AddWithValue("@offset", (page - 1) * pageSize);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new Supplier
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    ContactName = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    Phone = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    Email = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    Address = reader.IsDBNull(5) ? "" : reader.GetString(5),
                    TotalDebt = reader.GetDecimal(6),
                    CreatedAt = reader.IsDBNull(7) ? "" : reader.GetString(7),

                    // Bu iki property modelinde yoksa, Supplier.cs içine eklemelisin!
                    TotalPurchased = reader.GetDecimal(8),
                    TotalPaid = reader.GetDecimal(9)
                });
            }
            return list;
        }

        // 2. SAYFALAMA İÇİN TOPLAM KAYIT SAYISINI ALMA
        public int CountAll(string search, bool searchName, bool searchContact, decimal minDebt)
        {
            using var conn = DatabaseContext.GetConnection();
            using var cmd = conn.CreateCommand();

            string searchCondition = "";
            if (!string.IsNullOrWhiteSpace(search))
            {
                var conditions = new List<string>();
                if (searchName) conditions.Add("name LIKE @search");
                if (searchContact) conditions.Add("contact_name LIKE @search");

                if (conditions.Count > 0)
                {
                    searchCondition = " AND (" + string.Join(" OR ", conditions) + ")";
                    cmd.Parameters.AddWithValue("@search", $"%{search}%");
                }
            }

            cmd.CommandText = $"SELECT COUNT(*) FROM suppliers WHERE total_debt >= @minDebt {searchCondition}";
            cmd.Parameters.AddWithValue("@minDebt", minDebt);

            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        // 3. ID'YE GÖRE TEK TEDARİKÇİ GETİRME
        public Supplier GetById(int id)
        {
            using var conn = DatabaseContext.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT s.id, s.name, s.contact_name, s.phone, s.email, s.address, s.total_debt, s.created_at,
                       (SELECT COALESCE(SUM(total_price), 0) FROM purchases WHERE supplier_id = s.id) AS total_purchased,
                       (SELECT COALESCE(SUM(amount), 0) FROM supplier_payments WHERE supplier_id = s.id) AS total_paid
                FROM suppliers s
                WHERE s.id = @id";
            cmd.Parameters.AddWithValue("@id", id);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new Supplier
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    ContactName = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    Phone = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    Email = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    Address = reader.IsDBNull(5) ? "" : reader.GetString(5),
                    TotalDebt = reader.GetDecimal(6),
                    CreatedAt = reader.IsDBNull(7) ? "" : reader.GetString(7),
                    TotalPurchased = reader.GetDecimal(8),
                    TotalPaid = reader.GetDecimal(9)
                };
            }
            return null;
        }

        // 4. YENİ TEDARİKÇİ EKLEME
        public void Insert(Supplier supplier)
        {
            using var conn = DatabaseContext.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO suppliers (name, contact_name, phone, email, address, total_debt, created_at)
                VALUES (@name, @contact_name, @phone, @email, @address, @total_debt, @created_at)";

            cmd.Parameters.AddWithValue("@name", supplier.Name ?? "");
            cmd.Parameters.AddWithValue("@contact_name", supplier.ContactName ?? "");
            cmd.Parameters.AddWithValue("@phone", supplier.Phone ?? "");
            cmd.Parameters.AddWithValue("@email", supplier.Email ?? "");
            cmd.Parameters.AddWithValue("@address", supplier.Address ?? "");
            cmd.Parameters.AddWithValue("@total_debt", supplier.TotalDebt);
            cmd.Parameters.AddWithValue("@created_at", string.IsNullOrWhiteSpace(supplier.CreatedAt) ? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") : supplier.CreatedAt);

            cmd.ExecuteNonQuery();
        }

        // 5. TEDARİKÇİ BİLGİLERİNİ GÜNCELLEME
        public void Update(Supplier supplier)
        {
            using var conn = DatabaseContext.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE suppliers 
                SET name = @name, contact_name = @contact_name, phone = @phone, 
                    email = @email, address = @address, total_debt = @total_debt
                WHERE id = @id";

            cmd.Parameters.AddWithValue("@name", supplier.Name ?? "");
            cmd.Parameters.AddWithValue("@contact_name", supplier.ContactName ?? "");
            cmd.Parameters.AddWithValue("@phone", supplier.Phone ?? "");
            cmd.Parameters.AddWithValue("@email", supplier.Email ?? "");
            cmd.Parameters.AddWithValue("@address", supplier.Address ?? "");
            cmd.Parameters.AddWithValue("@total_debt", supplier.TotalDebt);
            cmd.Parameters.AddWithValue("@id", supplier.Id);

            cmd.ExecuteNonQuery();
        }

        // 6. TEDARİKÇİ SİLME (Cascade Delete - Alt kayıtları ile birlikte silme)
        public void Delete(int id)
        {
            using var conn = DatabaseContext.GetConnection();
            using var tx = conn.BeginTransaction();
            try
            {
                // 1. Önce bu tedarikçiye ait tüm ödeme geçmişini sil
                using var cmdPay = conn.CreateCommand();
                cmdPay.Transaction = tx;
                cmdPay.CommandText = "DELETE FROM supplier_payments WHERE supplier_id = @id";
                cmdPay.Parameters.AddWithValue("@id", id);
                cmdPay.ExecuteNonQuery();

                // 2. Sonra bu tedarikçiden yapılan tüm alım (purchase) geçmişini sil
                using var cmdPur = conn.CreateCommand();
                cmdPur.Transaction = tx;
                cmdPur.CommandText = "DELETE FROM purchases WHERE supplier_id = @id";
                cmdPur.Parameters.AddWithValue("@id", id);
                cmdPur.ExecuteNonQuery();

                // 3. En son tedarikçinin kendisini sil
                using var cmdSup = conn.CreateCommand();
                cmdSup.Transaction = tx;
                cmdSup.CommandText = "DELETE FROM suppliers WHERE id = @id";
                cmdSup.Parameters.AddWithValue("@id", id);
                cmdSup.ExecuteNonQuery();

                tx.Commit(); // Her şey başarılıysa veritabanına yaz
            }
            catch
            {
                tx.Rollback(); // Hata olursa hiçbir şeyi silme, geri al
                throw;
            }
        }
    }
}