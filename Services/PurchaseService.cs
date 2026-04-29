using ConstruxERP.Models;
using ConstruxERP.Repositories;

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

        public void CreatePurchase(Purchase p)
        {
            p.TotalPrice = Math.Round(p.Qty * p.UnitPrice, 2);
            p.AmountPaid = Math.Max(0, Math.Min(p.AmountPaid, p.TotalPrice));
            p.RemainingDebt = Math.Round(p.TotalPrice - p.AmountPaid, 2);

            if (string.IsNullOrWhiteSpace(p.PurchaseDate))
                p.PurchaseDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            using var conn = DatabaseContext.GetConnection();
            using var tx = conn.BeginTransaction();
            try
            {
                using var cmdInsert = conn.CreateCommand();
                cmdInsert.Transaction = tx;
                cmdInsert.CommandText = @"
            INSERT INTO purchases (supplier_id, product_id, qty, unit_price, total_price, amount_paid, remaining_debt, note, purchase_date)
            VALUES (@sid, @pid, @qty, @up, @tp, @ap, @rd, @note, @date)";
                cmdInsert.Parameters.AddWithValue("@sid", p.SupplierId);
                cmdInsert.Parameters.AddWithValue("@pid", p.ProductId);
                cmdInsert.Parameters.AddWithValue("@qty", p.Qty);
                cmdInsert.Parameters.AddWithValue("@up", p.UnitPrice);
                cmdInsert.Parameters.AddWithValue("@tp", p.TotalPrice);
                cmdInsert.Parameters.AddWithValue("@ap", p.AmountPaid);
                cmdInsert.Parameters.AddWithValue("@rd", p.RemainingDebt);
                cmdInsert.Parameters.AddWithValue("@note", p.Note ?? "");
                cmdInsert.Parameters.AddWithValue("@date", p.PurchaseDate);
                cmdInsert.ExecuteNonQuery();

                using var cmdStock = conn.CreateCommand();
                cmdStock.Transaction = tx;
                cmdStock.CommandText = "UPDATE products SET stock_qty = stock_qty + @qty, purchase_price = @up WHERE id = @pid";
                cmdStock.Parameters.AddWithValue("@qty", p.Qty);
                cmdStock.Parameters.AddWithValue("@up", p.UnitPrice);
                cmdStock.Parameters.AddWithValue("@pid", p.ProductId);
                cmdStock.ExecuteNonQuery();

                using var cmdDebt = conn.CreateCommand();
                cmdDebt.Transaction = tx;
                cmdDebt.CommandText = "UPDATE suppliers SET total_debt = total_debt + @rd WHERE id = @sid";
                cmdDebt.Parameters.AddWithValue("@rd", p.RemainingDebt);
                cmdDebt.Parameters.AddWithValue("@sid", p.SupplierId);
                cmdDebt.ExecuteNonQuery();

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

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
