namespace ConstruxERP.Models
{
    /// <summary>
    /// Represents a stock-keeping product in the inventory.
    /// </summary>
    public class Product
    {
        public int    Id            { get; set; }
        public string Name          { get; set; } = string.Empty;
        public string Category      { get; set; } = string.Empty;
        /// <summary>Unit of measure: piece, kg, ton, bag, sheet, length, roll, sack, meter …</summary>
        public string Unit          { get; set; } = "piece";
        public decimal PurchasePrice { get; set; }
        public decimal SalePrice     { get; set; }
        public decimal StockQty      { get; set; }
        public decimal MinStock      { get; set; }
        public string SupplierName  { get; set; } = string.Empty;
        public string Sku           { get; set; } = string.Empty;
        public string Notes         { get; set; } = string.Empty;
        public string CreatedAt     { get; set; } = string.Empty;
        public string UpdatedAt     { get; set; } = string.Empty;

        /// <summary>True when current stock has fallen below the configured minimum.</summary>
        public bool IsLowStock => StockQty < MinStock;

        public override string ToString() => $"{Name} ({Unit})";
    }
}
