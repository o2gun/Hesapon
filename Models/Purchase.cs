using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConstruxERP.Models
{
    public class Purchase
    {
        public int Id { get; set; }
        public int SupplierId { get; set; }
        public int ProductId { get; set; }
        public decimal Qty { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public decimal AmountPaid { get; set; }
        public decimal RemainingDebt { get; set; }
        public string Note { get; set; } = "";
        public string PurchaseDate { get; set; } = "";

        // UI için ek alanlar (JOIN ile dondurulacak)
        public string SupplierName { get; set; } = "";
        public string ProductName { get; set; } = "";
        public string ProductUnit { get; set; } = "";
    }
}