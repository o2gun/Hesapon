using ConstruxERP.Repositories;
using System;
using System.Collections.Generic;

namespace ConstruxERP.Services
{
    public class ReportSummary
    {
        public decimal TotalSales { get; set; }
        public decimal TotalPurchases { get; set; }
        public decimal TotalCollections { get; set; }
        public decimal TotalSupplierPayments { get; set; }
        public decimal NetCashFlow => TotalCollections - TotalSupplierPayments;
    }

    public class TopProductItem
    {
        public string ProductName { get; set; } = string.Empty;
        public decimal TotalQty { get; set; }
        public decimal TotalRevenue { get; set; }
    }

    public class ReportService
    {
        public ReportSummary GetSummary(DateTime start, DateTime end)
        {
            var summary = new ReportSummary();
            using var conn = DatabaseContext.GetConnection();

            string sDate = start.ToString("yyyy-MM-dd 00:00:00");
            string eDate = end.ToString("yyyy-MM-dd 23:59:59");

            // 1. Toplam Satışlar
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT COALESCE(SUM(total_price), 0) FROM sales WHERE sale_date BETWEEN @s AND @e";
                cmd.Parameters.AddWithValue("@s", sDate);
                cmd.Parameters.AddWithValue("@e", eDate);
                summary.TotalSales = Convert.ToDecimal(cmd.ExecuteScalar()); // Güvenli Cast
            }

            // 2. Toplam Alımlar
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT COALESCE(SUM(total_price), 0) FROM purchases WHERE purchase_date BETWEEN @s AND @e";
                cmd.Parameters.AddWithValue("@s", sDate);
                cmd.Parameters.AddWithValue("@e", eDate);
                summary.TotalPurchases = Convert.ToDecimal(cmd.ExecuteScalar());
            }

            // 3. Müşterilerden Tahsilatlar (Giren Para)
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT COALESCE(SUM(amount), 0) FROM debt_payments WHERE paid_at BETWEEN @s AND @e";
                cmd.Parameters.AddWithValue("@s", sDate);
                cmd.Parameters.AddWithValue("@e", eDate);
                summary.TotalCollections = Convert.ToDecimal(cmd.ExecuteScalar());
            }

            // 4. Tedarikçilere Ödemeler (Çıkan Para)
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT COALESCE(SUM(amount), 0) FROM supplier_payments WHERE paid_at BETWEEN @s AND @e";
                cmd.Parameters.AddWithValue("@s", sDate);
                cmd.Parameters.AddWithValue("@e", eDate);
                summary.TotalSupplierPayments = Convert.ToDecimal(cmd.ExecuteScalar());
            }

            return summary;
        }

        public List<TopProductItem> GetTopSellingProducts(DateTime start, DateTime end, int limit = 10)
        {
            var list = new List<TopProductItem>();
            using var conn = DatabaseContext.GetConnection();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                SELECT p.name, SUM(s.qty) as total_qty, SUM(s.total_price) as total_rev
                FROM sales s
                JOIN products p ON p.id = s.product_id
                WHERE s.sale_date BETWEEN @s AND @e
                GROUP BY p.id
                ORDER BY total_qty DESC 
                LIMIT @limit";

            cmd.Parameters.AddWithValue("@s", start.ToString("yyyy-MM-dd 00:00:00"));
            cmd.Parameters.AddWithValue("@e", end.ToString("yyyy-MM-dd 23:59:59"));
            cmd.Parameters.AddWithValue("@limit", limit);

            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new TopProductItem
                {
                    ProductName = r.GetString(0),
                    TotalQty = r.GetDecimal(1),
                    TotalRevenue = r.GetDecimal(2)
                });
            }
            return list;
        }
    }
}