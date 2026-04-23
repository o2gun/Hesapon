namespace ConstruxERP.Models
{
    /// <summary>
    /// A single sale transaction line (one product per record).
    /// </summary>
    public class Sale
    {
        public int     Id             { get; set; }
        public int     CustomerId     { get; set; }
        public int     ProductId      { get; set; }
        public decimal Qty            { get; set; }
        public decimal UnitPrice      { get; set; }
        public decimal TotalPrice     { get; set; }
        public decimal AmountPaid     { get; set; }
        public decimal RemainingDebt  { get; set; }
        /// <summary>cash | credit | partial</summary>
        public string  PaymentType    { get; set; } = "cash";
        public string  Note { get; set; } = string.Empty;
        public string  SaleDate       { get; set; } = string.Empty;

        // Navigation / display helpers (populated by joins, not stored)
        public string  CustomerName   { get; set; } = string.Empty;
        public string  ProductName    { get; set; } = string.Empty;
        public string  ProductUnit    { get; set; } = string.Empty;
    }
}
