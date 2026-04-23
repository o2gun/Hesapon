namespace ConstruxERP.Models
{
    /// <summary>
    /// Records a payment made by a customer against an outstanding debt.
    /// </summary>
    public class DebtPayment
    {
        public int     Id          { get; set; }
        public int     CustomerId  { get; set; }
        public int?    SaleId      { get; set; }
        public decimal Amount      { get; set; }
        public string  PaidAt      { get; set; } = string.Empty;
        public string  Notes       { get; set; } = string.Empty;

        // Display helper
        public string  CustomerName { get; set; } = string.Empty;
    }
}
