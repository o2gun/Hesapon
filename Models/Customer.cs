namespace ConstruxERP.Models
{
    /// <summary>
    /// A customer of the business, including their outstanding debt balance.
    /// </summary>
    public class Customer
    {
        public int     Id          { get; set; }
        public string  Name        { get; set; } = string.Empty;
        public string  Phone       { get; set; } = string.Empty;
        public string  Email       { get; set; } = string.Empty;
        public string  BillingAddress { get; set; } = string.Empty;
        public string  Address     { get; set; } = string.Empty;
        public decimal TotalDebt   { get; set; }
        public string  CreatedAt   { get; set; } = string.Empty;
        public decimal TotalPurchases { get; set; }
        public decimal TotalPaid { get; set; }

        public override string ToString() => Name;
    }
}
