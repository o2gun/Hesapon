using ConstruxERP.Models;
using ConstruxERP.Repositories;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;

namespace ConstruxERP.Services
{
    /// <summary>
    /// Business-logic layer for product and stock operations.
    /// </summary>
    public class InventoryService
    {
        private readonly ProductRepository _repo = new();

        public List<Product> GetProducts(string search = "") => _repo.GetAll(search);
        public List<Product> GetLowStockProducts()           => _repo.GetLowStock();
        public Product?      GetProduct(int id)              => _repo.GetById(id);


        public void CreateProductWithInitialStock(Product product, Purchase initialPurchase)
        {
            using var conn = DatabaseContext.GetConnection();
            using var tx = conn.BeginTransaction();
            try
            {
                // 1. Ürünü ekle ve yeni ID'yi al
                using var cmdProd = conn.CreateCommand();
                cmdProd.Transaction = tx;
                cmdProd.CommandText = @"
                    INSERT INTO products (name, category, unit, purchase_price, sale_price, stock_qty, min_stock, supplier_name, sku, notes, created_at)
                    VALUES (@name, @cat, @unit, @pp, @sp, @stock, @min, @sname, @sku, @note, @date);
                    SELECT last_insert_rowid();";

                cmdProd.Parameters.AddWithValue("@name", product.Name);
                cmdProd.Parameters.AddWithValue("@cat", product.Category);
                cmdProd.Parameters.AddWithValue("@unit", product.Unit);
                cmdProd.Parameters.AddWithValue("@pp", product.PurchasePrice);
                cmdProd.Parameters.AddWithValue("@sp", product.SalePrice);
                cmdProd.Parameters.AddWithValue("@stock", product.StockQty);
                cmdProd.Parameters.AddWithValue("@min", product.MinStock);
                cmdProd.Parameters.AddWithValue("@sname", product.SupplierName);
                cmdProd.Parameters.AddWithValue("@sku", product.Sku);
                cmdProd.Parameters.AddWithValue("@note", product.Notes);
                cmdProd.Parameters.AddWithValue("@date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

                int newProductId = Convert.ToInt32(cmdProd.ExecuteScalar());

                // 2. Alým (Purchase) kaydýný oluþtur
                initialPurchase.ProductId = newProductId;
                initialPurchase.TotalPrice = initialPurchase.Qty * initialPurchase.UnitPrice;
                initialPurchase.RemainingDebt = initialPurchase.TotalPrice - initialPurchase.AmountPaid;

                using var cmdPurch = conn.CreateCommand();
                cmdPurch.Transaction = tx;
                cmdPurch.CommandText = @"
                    INSERT INTO purchases (supplier_id, product_id, qty, unit_price, total_price, amount_paid, remaining_debt, note, purchase_date)
                    VALUES (@sid, @pid, @qty, @up, @tp, @ap, @rd, @pnote, @pdate)";

                cmdPurch.Parameters.AddWithValue("@sid", initialPurchase.SupplierId);
                cmdPurch.Parameters.AddWithValue("@pid", newProductId);
                cmdPurch.Parameters.AddWithValue("@qty", initialPurchase.Qty);
                cmdPurch.Parameters.AddWithValue("@up", initialPurchase.UnitPrice);
                cmdPurch.Parameters.AddWithValue("@tp", initialPurchase.TotalPrice);
                cmdPurch.Parameters.AddWithValue("@ap", initialPurchase.AmountPaid);
                cmdPurch.Parameters.AddWithValue("@rd", initialPurchase.RemainingDebt);
                cmdPurch.Parameters.AddWithValue("@pnote", initialPurchase.Note);
                cmdPurch.Parameters.AddWithValue("@pdate", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                cmdPurch.ExecuteNonQuery();

                // 3. Tedarikçinin borcunu güncelle
                using var cmdSupp = conn.CreateCommand();
                cmdSupp.Transaction = tx;
                cmdSupp.CommandText = "UPDATE suppliers SET total_debt = total_debt + @rd WHERE id = @sid";
                cmdSupp.Parameters.AddWithValue("@rd", initialPurchase.RemainingDebt);
                cmdSupp.Parameters.AddWithValue("@sid", initialPurchase.SupplierId);
                cmdSupp.ExecuteNonQuery();

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        public void AddProduct(Product p)
        {
            Validate(p);

            var allProducts = _repo.GetAll();
            var existing = allProducts.Find(x =>
                x.Name.Equals(p.Name, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(p.Sku) && x.Sku.Equals(p.Sku, StringComparison.OrdinalIgnoreCase)));

            if (existing != null)
            {
                _repo.AdjustStock(existing.Id, p.StockQty);
                existing.PurchasePrice = p.PurchasePrice > 0 ? p.PurchasePrice : existing.PurchasePrice;
                existing.SalePrice = p.SalePrice > 0 ? p.SalePrice : existing.SalePrice;
                _repo.Update(existing);
            }
            else
            {
                _repo.Insert(p);
            }
        }

        public void UpdateProduct(Product p)
        {
            Validate(p);
            _repo.Update(p);
        }

        public void DeleteProduct(int id) => _repo.Delete(id);

        /// <summary>
        /// Records an incoming stock purchase (increases stock).
        /// </summary>
        public void ReceiveStock(int productId, decimal qty, decimal purchasePrice)
        {
            if (qty <= 0)
                throw new ArgumentException("Quantity must be greater than zero.");

            _repo.AdjustStock(productId, qty);

            // Log the movement
            using var conn = DatabaseContext.GetConnection();
            using var cmd  = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO stock_movements (product_id, qty_change, reason)
                VALUES (@pid, @qty, 'purchase')";
            cmd.Parameters.AddWithValue("@pid", productId);
            cmd.Parameters.AddWithValue("@qty", qty);
            cmd.ExecuteNonQuery();
        }

        private static void Validate(Product p)
        {
            if (string.IsNullOrWhiteSpace(p.Name))
                throw new ArgumentException("Ürün adý zorunludur.");
            if (string.IsNullOrWhiteSpace(p.Unit))
                throw new ArgumentException("Birim zorunludur.");
            if (p.SalePrice < 0)
                throw new ArgumentException("Satýþ fiyatý negatif olamaz.");
            if (p.StockQty < 0)
                throw new ArgumentException("Stok miktarý negatif olamaz.");
        }
    }
}
