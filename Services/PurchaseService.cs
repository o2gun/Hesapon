using ConstruxERP.Models;
using ConstruxERP.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConstruxERP.Services
{
    public class PurchaseService
    {
        public List<Purchase> GetPurchasesByProduct(int productId)
        {
            var list = new List<Purchase>();
            using var conn = DatabaseContext.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT p.id, p.supplier_id, p.product_id, p.qty, p.unit_price, p.total_price, 
                       p.amount_paid, p.remaining_debt, p.note, p.purchase_date,
                       s.name AS supplier_name
                FROM purchases p
                JOIN suppliers s ON s.id = p.supplier_id
                WHERE p.product_id = @pid
                ORDER BY p.purchase_date DESC";
            cmd.Parameters.AddWithValue("@pid", productId);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new Purchase
                {
                    Id = r.GetInt32(0),
                    SupplierId = r.GetInt32(1),
                    ProductId = r.GetInt32(2),
                    Qty = r.GetDecimal(3),
                    UnitPrice = r.GetDecimal(4),
                    TotalPrice = r.GetDecimal(5),
                    AmountPaid = r.GetDecimal(6),
                    RemainingDebt = r.GetDecimal(7),
                    Note = r.GetString(8),
                    PurchaseDate = r.GetString(9),
                    SupplierName = r.GetString(10)
                });
            }
            return list;
        }

        // Finansal olarak güvenli Alım Düzenleme İşlemi (Delta Hesaplamalı)
        public void UpdatePurchase(int purchaseId, decimal newQty, decimal newUnitPrice, string newDate)
        {
            using var conn = DatabaseContext.GetConnection();
            using var tx = conn.BeginTransaction();
            try
            {
                using var cmdGet = conn.CreateCommand();
                cmdGet.Transaction = tx;
                cmdGet.CommandText = "SELECT product_id, supplier_id, qty, total_price, amount_paid, remaining_debt FROM purchases WHERE id = @id";
                cmdGet.Parameters.AddWithValue("@id", purchaseId);
                using var r = cmdGet.ExecuteReader();
                if (!r.Read()) throw new Exception("Alım kaydı bulunamadı.");

                int productId = r.GetInt32(0);
                int supplierId = r.GetInt32(1);
                decimal oldQty = r.GetDecimal(2);
                decimal oldRemainingDebt = r.GetDecimal(5);
                decimal amountPaid = r.GetDecimal(4);
                r.Close();

                decimal newTotalPrice = newQty * newUnitPrice;
                decimal newRemainingDebt = Math.Max(0, newTotalPrice - amountPaid);

                decimal qtyDelta = newQty - oldQty;
                decimal debtDelta = newRemainingDebt - oldRemainingDebt;

                // 1. Alım kaydını güncelle
                using var cmdUpd = conn.CreateCommand();
                cmdUpd.Transaction = tx;
                cmdUpd.CommandText = @"UPDATE purchases SET qty=@qty, unit_price=@up, total_price=@tp, remaining_debt=@rd, purchase_date=@date WHERE id=@id";
                cmdUpd.Parameters.AddWithValue("@qty", newQty);
                cmdUpd.Parameters.AddWithValue("@up", newUnitPrice);
                cmdUpd.Parameters.AddWithValue("@tp", newTotalPrice);
                cmdUpd.Parameters.AddWithValue("@rd", newRemainingDebt);
                cmdUpd.Parameters.AddWithValue("@date", newDate);
                cmdUpd.Parameters.AddWithValue("@id", purchaseId);
                cmdUpd.ExecuteNonQuery();

                // 2. Ürün Stoğunu Güncelle
                using var cmdStock = conn.CreateCommand();
                cmdStock.Transaction = tx;
                cmdStock.CommandText = "UPDATE products SET stock_qty = stock_qty + @delta WHERE id = @pid";
                cmdStock.Parameters.AddWithValue("@delta", qtyDelta);
                cmdStock.Parameters.AddWithValue("@pid", productId);
                cmdStock.ExecuteNonQuery();

                // 3. Tedarikçi Borcunu Güncelle
                using var cmdDebt = conn.CreateCommand();
                cmdDebt.Transaction = tx;
                cmdDebt.CommandText = "UPDATE suppliers SET total_debt = total_debt + @delta WHERE id = @sid";
                cmdDebt.Parameters.AddWithValue("@delta", debtDelta);
                cmdDebt.Parameters.AddWithValue("@sid", supplierId);
                cmdDebt.ExecuteNonQuery();

                tx.Commit();
            }
            catch { tx.Rollback(); throw; }
        }
    }
}
