using ConstruxERP.Models;
using ConstruxERP.Repositories;

namespace ConstruxERP.Services
{
    public class PurchaseService
    {

        // Tedarikçinin alım geçmişini listelemek için metod
        public List<Purchase> GetAll(string search, bool searchProductName, bool searchSupplierName, decimal minDebt, int page, int pageSize)
        {
            var list = new List<Purchase>();
            using var conn = Repositories.DatabaseContext.GetConnection();
            using var cmd = conn.CreateCommand();

            // Ürün adını da getirmek için LEFT JOIN yapıyoruz
            cmd.CommandText = @"
                SELECT p.id, p.supplier_id, p.product_id, p.qty, p.unit_price, 
                       p.total_price, p.amount_paid, p.remaining_debt, p.purchase_date, pr.name
                FROM purchases p
                LEFT JOIN products pr ON p.product_id = pr.id
                ORDER BY p.purchase_date DESC";

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
                    PurchaseDate = r.GetString(8),
                    ProductName = r.IsDBNull(9) ? "Silinmiş Ürün" : r.GetString(9)
                });
            }
            return list;
        }

        public void DeletePurchase(int purchaseId)
        {
            using var conn = DatabaseContext.GetConnection();
            using var tx = conn.BeginTransaction();
            try
            {
                // 1. Alım bilgilerini al
                using var cmdGet = conn.CreateCommand();
                cmdGet.Transaction = tx;
                cmdGet.CommandText = "SELECT supplier_id, product_id, qty, remaining_debt FROM purchases WHERE id = @id";
                cmdGet.Parameters.AddWithValue("@id", purchaseId);

                int supplierId = 0, productId = 0;
                decimal qty = 0, remainingDebt = 0;

                using (var r = cmdGet.ExecuteReader())
                {
                    if (r.Read())
                    {
                        supplierId = r.GetInt32(0);
                        productId = r.GetInt32(1);
                        qty = r.GetDecimal(2);
                        remainingDebt = r.GetDecimal(3);
                    }
                    else return;
                }

                // 2. Stoğu geri düş (Çünkü mal alımı iptal ediliyor)
                using var cmdStock = conn.CreateCommand();
                cmdStock.Transaction = tx;
                cmdStock.CommandText = "UPDATE products SET stock_quantity = stock_quantity - @qty WHERE id = @pid";
                cmdStock.Parameters.AddWithValue("@qty", qty);
                cmdStock.Parameters.AddWithValue("@pid", productId);
                cmdStock.ExecuteNonQuery();

                // 3. Tedarikçi borcunu düş
                using var cmdSup = conn.CreateCommand();
                cmdSup.Transaction = tx;
                cmdSup.CommandText = "UPDATE suppliers SET total_debt = total_debt - @debt WHERE id = @sid";
                cmdSup.Parameters.AddWithValue("@debt", remainingDebt);
                cmdSup.Parameters.AddWithValue("@sid", supplierId);
                cmdSup.ExecuteNonQuery();

                // 4. Alım kaydını sil
                using var cmdDel = conn.CreateCommand();
                cmdDel.Transaction = tx;
                cmdDel.CommandText = "DELETE FROM purchases WHERE id = @id";
                cmdDel.Parameters.AddWithValue("@id", purchaseId);
                cmdDel.ExecuteNonQuery();

                tx.Commit();
            }
            catch { tx.Rollback(); throw; }
        }

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

        public void UpdatePurchase(Purchase purchase)
        {
            using var conn = DatabaseContext.GetConnection();
            using var tx = conn.BeginTransaction();
            try
            {
                // 1. ESKİ ALIM VERİSİNİ AL (Farkları bulmak için)
                using var cmdGet = conn.CreateCommand();
                cmdGet.Transaction = tx;
                cmdGet.CommandText = "SELECT supplier_id, product_id, quantity, remaining_debt FROM purchases WHERE id = @id";
                cmdGet.Parameters.AddWithValue("@id", purchase.Id);

                int oldSupplierId = 0, oldProductId = 0;
                decimal oldQty = 0, oldRemainingDebt = 0;

                using (var reader = cmdGet.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        oldSupplierId = reader.GetInt32(0);
                        oldProductId = reader.GetInt32(1);
                        oldQty = reader.GetDecimal(2);
                        oldRemainingDebt = reader.GetDecimal(3);
                    }
                    else throw new Exception("Düzenlenmek istenen alım kaydı bulunamadı.");
                }

                // 2. TEDARİKÇİ VEYA BORÇ DEĞİŞİMİNİ YÖNET
                if (oldSupplierId == purchase.SupplierId)
                {
                    decimal debtDiff = purchase.RemainingDebt - oldRemainingDebt;
                    if (debtDiff != 0)
                    {
                        using var cmdSup = conn.CreateCommand();
                        cmdSup.Transaction = tx;
                        cmdSup.CommandText = "UPDATE suppliers SET total_debt = total_debt + @diff WHERE id = @sid";
                        cmdSup.Parameters.AddWithValue("@diff", debtDiff);
                        cmdSup.Parameters.AddWithValue("@sid", purchase.SupplierId);
                        cmdSup.ExecuteNonQuery();
                    }
                }
                else
                {
                    // Tedarikçi tamamen değiştirilmiş!
                    using var cmdOldSup = conn.CreateCommand();
                    cmdOldSup.Transaction = tx;
                    cmdOldSup.CommandText = "UPDATE suppliers SET total_debt = total_debt - @oldDebt WHERE id = @sid";
                    cmdOldSup.Parameters.AddWithValue("@oldDebt", oldRemainingDebt);
                    cmdOldSup.Parameters.AddWithValue("@sid", oldSupplierId);
                    cmdOldSup.ExecuteNonQuery();

                    using var cmdNewSup = conn.CreateCommand();
                    cmdNewSup.Transaction = tx;
                    cmdNewSup.CommandText = "UPDATE suppliers SET total_debt = total_debt + @newDebt WHERE id = @sid";
                    cmdNewSup.Parameters.AddWithValue("@newDebt", purchase.RemainingDebt);
                    cmdNewSup.Parameters.AddWithValue("@sid", purchase.SupplierId);
                    cmdNewSup.ExecuteNonQuery();
                }

                // 3. ÜRÜN VEYA STOK (MİKTAR) DEĞİŞİMİNİ YÖNET
                if (oldProductId == purchase.ProductId)
                {
                    // ALIM yapıldığı için stok artar. Fark pozitifse stok eklenir, negatifse düşülür.
                    decimal qtyDiff = purchase.Qty - oldQty;
                    if (qtyDiff != 0)
                    {
                        using var cmdProd = conn.CreateCommand();
                        cmdProd.Transaction = tx;
                        cmdProd.CommandText = "UPDATE products SET stock_quantity = stock_quantity + @diff WHERE id = @pid";
                        cmdProd.Parameters.AddWithValue("@diff", qtyDiff);
                        cmdProd.Parameters.AddWithValue("@pid", purchase.ProductId);
                        cmdProd.ExecuteNonQuery();
                    }
                }
                else
                {
                    // Ürün tamamen değiştirilmiş!
                    // a) Eski ürünün stoğunu GERİ DÜŞ (Çünkü o alım iptal oldu)
                    using var cmdOldProd = conn.CreateCommand();
                    cmdOldProd.Transaction = tx;
                    cmdOldProd.CommandText = "UPDATE products SET stock_quantity = stock_quantity - @oldQty WHERE id = @pid";
                    cmdOldProd.Parameters.AddWithValue("@oldQty", oldQty);
                    cmdOldProd.Parameters.AddWithValue("@pid", oldProductId);
                    cmdOldProd.ExecuteNonQuery();

                    // b) Yeni ürünün stoğuna EKLE
                    using var cmdNewProd = conn.CreateCommand();
                    cmdNewProd.Transaction = tx;
                    cmdNewProd.CommandText = "UPDATE products SET stock_quantity = stock_quantity + @newQty WHERE id = @pid";
                    cmdNewProd.Parameters.AddWithValue("@newQty", purchase.Qty);
                    cmdNewProd.Parameters.AddWithValue("@pid", purchase.ProductId);
                    cmdNewProd.ExecuteNonQuery();
                }

                // 4. ALIMIN KENDİSİNİ GÜNCELLE
                using var cmdUpdate = conn.CreateCommand();
                cmdUpdate.Transaction = tx;
                cmdUpdate.CommandText = @"
                    UPDATE purchases 
                    SET supplier_id = @sid, product_id = @pid, qty = @qty, 
                        unit_price = @up, total_price = @tp, amount_paid = @ap, 
                        remaining_debt = @rd, purchase_date = @pd 
                    WHERE id = @id";
                cmdUpdate.Parameters.AddWithValue("@sid", purchase.SupplierId);
                cmdUpdate.Parameters.AddWithValue("@pid", purchase.ProductId);
                cmdUpdate.Parameters.AddWithValue("@qty", purchase.Qty);
                cmdUpdate.Parameters.AddWithValue("@up", purchase.UnitPrice);
                cmdUpdate.Parameters.AddWithValue("@tp", purchase.TotalPrice);
                cmdUpdate.Parameters.AddWithValue("@ap", purchase.AmountPaid);
                cmdUpdate.Parameters.AddWithValue("@rd", purchase.RemainingDebt);

                // Modelindeki tarih alanının adı neyse (PurchaseDate, CreatedAt vb.) onu yazmalısın.
                cmdUpdate.Parameters.AddWithValue("@pd", purchase.PurchaseDate);
                cmdUpdate.Parameters.AddWithValue("@id", purchase.Id);
                cmdUpdate.ExecuteNonQuery();

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }
    }
}
