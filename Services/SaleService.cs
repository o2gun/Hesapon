using ConstruxERP.Models;
using ConstruxERP.Repositories;
using System;
using System.Collections.Generic;

namespace ConstruxERP.Services
{
    /// <summary>
    /// Business-logic layer for sales operations.
    /// Validates input then delegates to SaleRepository.
    /// </summary>
    public class SaleService
    {
        private readonly SaleRepository _saleRepo = new();
        private readonly ProductRepository _productRepo = new();
        private readonly CustomerRepository _customerRepo = new();

        /// <summary>Creates a new sale after validating stock and customer existence.</summary>
        public int CreateSale(Sale sale)
        {
            // Validate customer
            var customer = _customerRepo.GetById(sale.CustomerId)
                ?? throw new ArgumentException($"Customer ID {sale.CustomerId} not found.");

            // Validate product and stock
            var product = _productRepo.GetById(sale.ProductId)
                ?? throw new ArgumentException($"Product ID {sale.ProductId} not found.");

            if (product.StockQty < sale.Qty)
                throw new InvalidOperationException(
                    $"Insufficient stock for '{product.Name}'. " +
                    $"Available: {product.StockQty} {product.Unit}, Requested: {sale.Qty}");

            // Auto-calculate totals
            sale.TotalPrice = Math.Round(sale.Qty * sale.UnitPrice, 2);
            sale.AmountPaid = Math.Max(0, Math.Min(sale.AmountPaid, sale.TotalPrice));
            sale.RemainingDebt = Math.Round(sale.TotalPrice - sale.AmountPaid, 2);

            // Derive payment type
            if (sale.AmountPaid == 0)
                sale.PaymentType = "credit";
            else if (sale.AmountPaid >= sale.TotalPrice)
                sale.PaymentType = "cash";
            else
                sale.PaymentType = "partial";

            return _saleRepo.Insert(sale);
        }

        public List<Sale> GetSales(string search = "", int page = 1, int pageSize = 50)
            => _saleRepo.GetAll(search, page, pageSize);

        public Sale? GetSaleById(int id) => _saleRepo.GetById(id);

        /// <summary>
        /// Updates the paid amount on an existing sale and syncs customer debt.
        /// </summary>
        public void UpdatePayment(int saleId, decimal newAmountPaid, string paymentType = "")
        {
            if (newAmountPaid < 0)
                throw new ArgumentException("Payment amount cannot be negative.");
            _saleRepo.UpdatePayment(saleId, newAmountPaid, paymentType);
        }

        public void DeleteSale(int saleId) => _saleRepo.Delete(saleId);

        public int CountSales(string search = "")
            => _saleRepo.CountAll(search);

        public decimal GetTodayTotal() => _saleRepo.GetTotalSalesToday();
        public decimal GetMonthlyTotal() => _saleRepo.GetTotalSalesThisMonth();
        public int GetTodayOrders() => _saleRepo.GetActiveOrdersToday();
    }
}
