using ConstruxERP.Models;
using Microsoft.Data.Sqlite;
using System.Collections.Generic;

namespace ConstruxERP.Repositories
{
    /// <summary>
    /// Data-access layer for the products table.
    /// </summary>
    public class ProductRepository
    {
        // ─── Read ─────────────────────────────────────────────────────────────

        public List<Product> GetAll(string search = "")
        {
            var list = new List<Product>();
            using var conn = DatabaseContext.GetConnection();
            using var cmd  = conn.CreateCommand();

            cmd.CommandText = @"
                SELECT id, name, category, unit, purchase_price, sale_price,
                       stock_qty, min_stock, supplier_name, sku, notes, created_at, updated_at
                FROM products
                WHERE (@search = '' OR name LIKE @search OR sku LIKE @search OR category LIKE @search)
                ORDER BY name";
            cmd.Parameters.AddWithValue("@search",
                string.IsNullOrWhiteSpace(search) ? "" : $"%{search}%");

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Map(reader));

            return list;
        }

        public List<Product> GetLowStock()
        {
            var list = new List<Product>();
            using var conn = DatabaseContext.GetConnection();
            using var cmd  = conn.CreateCommand();

            cmd.CommandText = @"
                SELECT id, name, category, unit, purchase_price, sale_price,
                       stock_qty, min_stock, supplier_name, sku, notes, created_at, updated_at
                FROM products
                WHERE stock_qty < min_stock
                ORDER BY (min_stock - stock_qty) DESC";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(Map(reader));

            return list;
        }

        public Product? GetById(int id)
        {
            using var conn = DatabaseContext.GetConnection();
            using var cmd  = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT id, name, category, unit, purchase_price, sale_price,
                       stock_qty, min_stock, supplier_name, sku, notes, created_at, updated_at
                FROM products WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);

            using var reader = cmd.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }

        // ─── Write ────────────────────────────────────────────────────────────

        public int Insert(Product p)
        {
            using var conn = DatabaseContext.GetConnection();
            using var cmd  = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO products
                    (name, category, unit, purchase_price, sale_price,
                     stock_qty, min_stock, supplier_name, sku, notes)
                VALUES
                    (@name, @cat, @unit, @pp, @sp, @qty, @min, @sup, @sku, @notes);
                SELECT last_insert_rowid();";

            BindParams(cmd, p);
            return (int)(long)cmd.ExecuteScalar()!;
        }

        public void Update(Product p)
        {
            using var conn = DatabaseContext.GetConnection();
            using var cmd  = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE products SET
                    name           = @name,
                    category       = @cat,
                    unit           = @unit,
                    purchase_price = @pp,
                    sale_price     = @sp,
                    stock_qty      = @qty,
                    min_stock      = @min,
                    supplier_name  = @sup,
                    sku            = @sku,
                    notes          = @notes,
                    updated_at     = datetime('now','localtime')
                WHERE id = @id";
            BindParams(cmd, p);
            cmd.Parameters.AddWithValue("@id", p.Id);
            cmd.ExecuteNonQuery();
        }

        /// <summary>Adjusts stock_qty by delta (positive = add, negative = remove).</summary>
        public void AdjustStock(int productId, decimal delta, SqliteConnection? conn = null)
        {
            bool owned = conn == null;
            conn ??= DatabaseContext.GetConnection();
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    UPDATE products
                    SET stock_qty  = stock_qty + @delta,
                        updated_at = datetime('now','localtime')
                    WHERE id = @id";
                cmd.Parameters.AddWithValue("@delta", delta);
                cmd.Parameters.AddWithValue("@id",    productId);
                cmd.ExecuteNonQuery();
            }
            finally { if (owned) conn.Dispose(); }
        }

        public void Delete(int id)
        {
            using var conn = DatabaseContext.GetConnection();
            using var cmd  = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM products WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        // ─── Helpers ──────────────────────────────────────────────────────────

        private static Product Map(SqliteDataReader r) => new()
        {
            Id            = r.GetInt32(0),
            Name          = r.GetString(1),
            Category      = r.GetString(2),
            Unit          = r.GetString(3),
            PurchasePrice = r.GetDecimal(4),
            SalePrice     = r.GetDecimal(5),
            StockQty      = r.GetDecimal(6),
            MinStock      = r.GetDecimal(7),
            SupplierName  = r.GetString(8),
            Sku           = r.GetString(9),
            Notes         = r.GetString(10),
            CreatedAt     = r.GetString(11),
            UpdatedAt     = r.GetString(12)
        };

        private static void BindParams(SqliteCommand cmd, Product p)
        {
            cmd.Parameters.AddWithValue("@name",  p.Name);
            cmd.Parameters.AddWithValue("@cat",   p.Category);
            cmd.Parameters.AddWithValue("@unit",  p.Unit);
            cmd.Parameters.AddWithValue("@pp",    p.PurchasePrice);
            cmd.Parameters.AddWithValue("@sp",    p.SalePrice);
            cmd.Parameters.AddWithValue("@qty",   p.StockQty);
            cmd.Parameters.AddWithValue("@min",   p.MinStock);
            cmd.Parameters.AddWithValue("@sup",   p.SupplierName);
            cmd.Parameters.AddWithValue("@sku",   p.Sku);
            cmd.Parameters.AddWithValue("@notes", p.Notes);
        }
    }
}
