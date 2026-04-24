using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConstruxERP.Models
{
    public class SupplierPayment
    {
        public int Id { get; set; }
        public int SupplierId { get; set; }
        public int? PurchaseId { get; set; }
        public decimal Amount { get; set; }
        public string PaidAt { get; set; } = "";
        public string Notes { get; set; } = "";
    }
}