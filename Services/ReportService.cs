using ConstruxERP.Models;
using ConstruxERP.Repositories;
using Microsoft.Data.Sqlite;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.IO;

namespace ConstruxERP.Services
{
    public class ReportData
    {
        public decimal TotalRevenue      { get; set; }
        public decimal TotalPaid         { get; set; }
        public decimal TotalDebt         { get; set; }
        public int     TransactionCount  { get; set; }
        public List<(string Product, decimal Revenue, decimal Qty)> TopProducts { get; set; } = new();
        public List<(string Month, decimal Revenue)> MonthlyRevenue { get; set; } = new();
        public List<(string Category, decimal Revenue)> ByCategory  { get; set; } = new();
    }

    public class ReportService
    {
        private readonly SaleRepository _saleRepo = new();

        // ─── Report Data ──────────────────────────────────────────────────────

        public ReportData GetReport(DateTime from, DateTime to)
        {
            var sales = _saleRepo.GetByDateRange(from, to);
            var data  = new ReportData { TransactionCount = sales.Count };

            foreach (var s in sales)
            {
                data.TotalRevenue += s.TotalPrice;
                data.TotalPaid    += s.AmountPaid;
                data.TotalDebt    += s.RemainingDebt;
            }

            data.TopProducts   = GetTopProducts(from, to);
            data.MonthlyRevenue = GetMonthlyRevenue(from.Year);
            data.ByCategory    = GetRevenueByCategory(from, to);
            return data;
        }

        public ReportData GetDailyReport()  => GetReport(DateTime.Today, DateTime.Today);
        public ReportData GetMonthlyReport()
        {
            var now = DateTime.Now;
            return GetReport(new DateTime(now.Year, now.Month, 1), DateTime.Today);
        }

        // ─── Aggregated queries ───────────────────────────────────────────────

        private List<(string, decimal, decimal)> GetTopProducts(DateTime from, DateTime to)
        {
            var list = new List<(string, decimal, decimal)>();
            using var conn = DatabaseContext.GetConnection();
            using var cmd  = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT p.name, SUM(s.total_price) AS rev, SUM(s.qty) AS qty
                FROM sales s JOIN products p ON p.id = s.product_id
                WHERE s.sale_date BETWEEN @from AND @to
                GROUP BY p.id ORDER BY rev DESC LIMIT 5";
            cmd.Parameters.AddWithValue("@from", from.ToString("yyyy-MM-dd 00:00:00"));
            cmd.Parameters.AddWithValue("@to",   to.ToString("yyyy-MM-dd 23:59:59"));
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add((r.GetString(0), r.GetDecimal(1), r.GetDecimal(2)));
            return list;
        }

        private List<(string, decimal)> GetMonthlyRevenue(int year)
        {
            var list = new List<(string, decimal)>();
            using var conn = DatabaseContext.GetConnection();
            using var cmd  = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT strftime('%m', sale_date) AS mo, COALESCE(SUM(total_price),0)
                FROM sales
                WHERE strftime('%Y', sale_date) = @year
                GROUP BY mo ORDER BY mo";
            cmd.Parameters.AddWithValue("@year", year.ToString());
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                int month = int.Parse(r.GetString(0));
                list.Add((new DateTime(year, month, 1).ToString("MMM"), r.GetDecimal(1)));
            }
            return list;
        }

        private List<(string, decimal)> GetRevenueByCategory(DateTime from, DateTime to)
        {
            var list = new List<(string, decimal)>();
            using var conn = DatabaseContext.GetConnection();
            using var cmd  = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT p.category, SUM(s.total_price)
                FROM sales s JOIN products p ON p.id = s.product_id
                WHERE s.sale_date BETWEEN @from AND @to
                GROUP BY p.category ORDER BY 2 DESC";
            cmd.Parameters.AddWithValue("@from", from.ToString("yyyy-MM-dd 00:00:00"));
            cmd.Parameters.AddWithValue("@to",   to.ToString("yyyy-MM-dd 23:59:59"));
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add((r.GetString(0), r.GetDecimal(1)));
            return list;
        }

        // ─── Excel Export ─────────────────────────────────────────────────────

        public void ExportSalesToExcel(IEnumerable<Sale> sales, string filePath)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using var pkg  = new ExcelPackage();
            var ws = pkg.Workbook.Worksheets.Add("Sales");

            // Header row
            string[] headers = {
                "Date", "Customer", "Product", "Unit", "Qty",
                "Unit Price", "Total Price", "Amount Paid", "Remaining Debt", "Payment Type"
            };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cells[1, i + 1].Value = headers[i];
                ws.Cells[1, i + 1].Style.Font.Bold = true;
                ws.Cells[1, i + 1].Style.Fill.PatternType =
                    OfficeOpenXml.Style.ExcelFillStyle.Solid;
                ws.Cells[1, i + 1].Style.Fill.BackgroundColor.SetColor(
                    System.Drawing.Color.FromArgb(37, 99, 235));
                ws.Cells[1, i + 1].Style.Font.Color.SetColor(System.Drawing.Color.White);
            }

            int row = 2;
            foreach (var s in sales)
            {
                ws.Cells[row, 1].Value  = s.SaleDate;
                ws.Cells[row, 2].Value  = s.CustomerName;
                ws.Cells[row, 3].Value  = s.ProductName;
                ws.Cells[row, 4].Value  = s.ProductUnit;
                ws.Cells[row, 5].Value  = (double)s.Qty;
                ws.Cells[row, 6].Value  = (double)s.UnitPrice;
                ws.Cells[row, 7].Value  = (double)s.TotalPrice;
                ws.Cells[row, 8].Value  = (double)s.AmountPaid;
                ws.Cells[row, 9].Value  = (double)s.RemainingDebt;
                ws.Cells[row, 10].Value = s.PaymentType;

                // Red background for rows with debt
                if (s.RemainingDebt > 0)
                    ws.Cells[row, 1, row, 10].Style.Fill.PatternType =
                        OfficeOpenXml.Style.ExcelFillStyle.Solid;

                row++;
            }

            ws.Cells.AutoFitColumns();
            pkg.SaveAs(new FileInfo(filePath));
        }

        // ─── CSV Export ───────────────────────────────────────────────────────

        public void ExportSalesToCsv(IEnumerable<Sale> sales, string filePath)
        {
            using var writer = new StreamWriter(filePath);
            writer.WriteLine(
                "Date,Customer,Product,Unit,Qty,UnitPrice,TotalPrice,AmountPaid,RemainingDebt,PaymentType");
            foreach (var s in sales)
                writer.WriteLine(
                    $"{s.SaleDate},{CsvEscape(s.CustomerName)},{CsvEscape(s.ProductName)}," +
                    $"{s.ProductUnit},{s.Qty},{s.UnitPrice},{s.TotalPrice}," +
                    $"{s.AmountPaid},{s.RemainingDebt},{s.PaymentType}");
        }

        private static string CsvEscape(string v) =>
            v.Contains(',') || v.Contains('"') ? $"\"{v.Replace("\"", "\"\"")}\"" : v;
    }
}
