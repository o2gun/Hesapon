using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConstruxERP.Models
{
    public class Supplier
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Email { get; set; } = "";
        public string Address { get; set; } = "";
        public string BillingAddress { get; set; } = "";
        public decimal TotalDebt { get; set; }
        public string CreatedAt { get; set; } = "";
    }
}