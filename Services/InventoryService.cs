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

        public void AddProduct(Product p)
        {
            Validate(p);
            _repo.Insert(p);
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
                throw new ArgumentException("Product name is required.");
            if (string.IsNullOrWhiteSpace(p.Unit))
                throw new ArgumentException("Unit is required.");
            if (p.SalePrice < 0)
                throw new ArgumentException("Sale price cannot be negative.");
            if (p.StockQty < 0)
                throw new ArgumentException("Stock quantity cannot be negative.");
        }
    }
}
