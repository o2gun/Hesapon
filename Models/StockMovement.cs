namespace ConstruxERP.Models
{
    /// <summary>
    /// Records every change to a product's stock quantity (purchase in or sale out).
    /// </summary>
    public class StockMovement
    {
        public int     Id          { get; set; }
        public int     ProductId   { get; set; }
        /// <summary>Positive = stock received, Negative = sold.</summary>
        public decimal QtyChange   { get; set; }
        /// <summary>sale | purchase | adjustment</summary>
        public string  Reason      { get; set; } = string.Empty;
        /// <summary>FK to the related sale or purchase id.</summary>
        public int?    Reference   { get; set; }
        public string  MovedAt     { get; set; } = string.Empty;

        // Display helper
        public string  ProductName { get; set; } = string.Empty;
    }
}
