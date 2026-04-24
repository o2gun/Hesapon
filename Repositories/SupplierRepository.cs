using ConstruxERP.Models;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;

namespace ConstruxERP.Repositories
{
    public class SupplierRepository
    {
        public List<Supplier> GetAll()
        {
            var list = new List<Supplier>();
            using var conn = DatabaseContext.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, name, phone, email, address, billing_address, total_debt, created_at FROM suppliers ORDER BY name";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new Supplier
                {
                    Id = r.GetInt32(0),
                    Name = r.GetString(1),
                    Phone = r.GetString(2),
                    Email = r.GetString(3),
                    Address = r.GetString(4),
                    BillingAddress = r.GetString(5),
                    TotalDebt = r.GetDecimal(6),
                    CreatedAt = r.GetString(7)
                });
            }
            return list;
        }

        public int Insert(Supplier s)
        {
            using var conn = DatabaseContext.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO suppliers (name, phone, email, address, billing_address)
                VALUES (@n, @p, @e, @a, @ba);
                SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@n", s.Name);
            cmd.Parameters.AddWithValue("@p", s.Phone ?? "");
            cmd.Parameters.AddWithValue("@e", s.Email ?? "");
            cmd.Parameters.AddWithValue("@a", s.Address ?? "");
            cmd.Parameters.AddWithValue("@ba", s.BillingAddress ?? "");
            return Convert.ToInt32(cmd.ExecuteScalar());
        }
    }
}