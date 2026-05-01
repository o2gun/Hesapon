using Microsoft.Data.Sqlite;
using System;
using System.IO;

namespace ConstruxERP.Repositories
{
    public static class DatabaseContext
    {
        private static string _connectionString = string.Empty;

        public static void Initialize()
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ConstruxERP");
            Directory.CreateDirectory(folder);

            string dbPath = Path.Combine(folder, "construx.db");
            _connectionString = $"Data Source={dbPath};";

            RunMigrations();
        }

        public static SqliteConnection GetConnection()
        {
            if (string.IsNullOrEmpty(_connectionString))
                throw new InvalidOperationException(
                    "DatabaseContext.Initialize() must be called before GetConnection().");
            var conn = new SqliteConnection(_connectionString);
            conn.Open();
            return conn;
        }

        private static void RunMigrations()
        {
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();

            // ── Core schema (CREATE IF NOT EXISTS) ───────────────────────────
            cmd.CommandText = @"
                PRAGMA journal_mode=WAL;
                PRAGMA foreign_keys=ON;

                CREATE TABLE IF NOT EXISTS customers (
                    id              INTEGER PRIMARY KEY AUTOINCREMENT,
                    name            TEXT    NOT NULL,
                    phone           TEXT    NOT NULL DEFAULT '',
                    email           TEXT    NOT NULL DEFAULT '',
                    address         TEXT    NOT NULL DEFAULT '',
                    billing_address TEXT    NOT NULL DEFAULT '',
                    total_debt      REAL    NOT NULL DEFAULT 0,
                    created_at      TEXT    NOT NULL DEFAULT (datetime('now','localtime'))
                );

                CREATE TABLE IF NOT EXISTS suppliers (
                    id              INTEGER PRIMARY KEY AUTOINCREMENT,
                    name            TEXT    NOT NULL,
                    phone           TEXT    NOT NULL DEFAULT '',
                    email           TEXT    NOT NULL DEFAULT '',
                    address         TEXT    NOT NULL DEFAULT '',
                    billing_address TEXT    NOT NULL DEFAULT '',
                    total_debt      REAL    NOT NULL DEFAULT 0,
                    created_at      TEXT    NOT NULL DEFAULT (datetime('now','localtime'))
                );

                CREATE TABLE IF NOT EXISTS purchases (
                    id             INTEGER PRIMARY KEY AUTOINCREMENT,
                    supplier_id    INTEGER NOT NULL REFERENCES suppliers(id),
                    product_id     INTEGER NOT NULL REFERENCES products(id),
                    qty            REAL    NOT NULL,
                    unit_price     REAL    NOT NULL,
                    total_price    REAL    NOT NULL,
                    amount_paid    REAL    NOT NULL DEFAULT 0,
                    remaining_debt REAL    NOT NULL DEFAULT 0,
                    note           TEXT    NOT NULL DEFAULT '',
                    purchase_date  TEXT    NOT NULL DEFAULT (datetime('now','localtime'))
                );

                CREATE TABLE IF NOT EXISTS supplier_payments (
                    id           INTEGER PRIMARY KEY AUTOINCREMENT,
                    supplier_id  INTEGER NOT NULL REFERENCES suppliers(id),
                    purchase_id  INTEGER REFERENCES purchases(id),
                    amount       REAL    NOT NULL,
                    paid_at      TEXT    NOT NULL DEFAULT (datetime('now','localtime')),
                    notes        TEXT    NOT NULL DEFAULT ''
                );

                CREATE TABLE IF NOT EXISTS products (
                    id             INTEGER PRIMARY KEY AUTOINCREMENT,
                    name           TEXT    NOT NULL,
                    category       TEXT    NOT NULL DEFAULT '',
                    unit           TEXT    NOT NULL DEFAULT 'piece',
                    purchase_price REAL    NOT NULL DEFAULT 0,
                    sale_price     REAL    NOT NULL DEFAULT 0,
                    stock_qty      REAL    NOT NULL DEFAULT 0,
                    min_stock      REAL    NOT NULL DEFAULT 0,
                    supplier_name  TEXT    NOT NULL DEFAULT '',
                    sku            TEXT    NOT NULL DEFAULT '',
                    notes          TEXT    NOT NULL DEFAULT '',
                    created_at     TEXT    NOT NULL DEFAULT (datetime('now','localtime')),
                    updated_at     TEXT    NOT NULL DEFAULT (datetime('now','localtime'))
                );

                CREATE TABLE IF NOT EXISTS sales (
                    id             INTEGER PRIMARY KEY AUTOINCREMENT,
                    customer_id    INTEGER NOT NULL REFERENCES customers(id),
                    product_id     INTEGER NOT NULL REFERENCES products(id),
                    qty            REAL    NOT NULL,
                    unit_price     REAL    NOT NULL,
                    total_price    REAL    NOT NULL,
                    amount_paid    REAL    NOT NULL DEFAULT 0,
                    remaining_debt REAL    NOT NULL DEFAULT 0,
                    payment_type   TEXT    NOT NULL DEFAULT 'cash',
                    note           TEXT    NOT NULL DEFAULT '',
                    sale_date      TEXT    NOT NULL DEFAULT (datetime('now','localtime'))
                );

                CREATE TABLE IF NOT EXISTS stock_movements (
                    id          INTEGER PRIMARY KEY AUTOINCREMENT,
                    product_id  INTEGER NOT NULL REFERENCES products(id),
                    qty_change  REAL    NOT NULL,
                    reason      TEXT    NOT NULL DEFAULT '',
                    reference   INTEGER,
                    moved_at    TEXT    NOT NULL DEFAULT (datetime('now','localtime'))
                );

                CREATE TABLE IF NOT EXISTS debt_payments (
                    id           INTEGER PRIMARY KEY AUTOINCREMENT,
                    customer_id  INTEGER NOT NULL REFERENCES customers(id),
                    sale_id      INTEGER REFERENCES sales(id),
                    amount       REAL    NOT NULL,
                    paid_at      TEXT    NOT NULL DEFAULT (datetime('now','localtime')),
                    notes        TEXT    NOT NULL DEFAULT ''
                );
            ";
            cmd.ExecuteNonQuery();

            // ── Incremental ALTER TABLE migrations (safe to run multiple times) ─
            AddColumnIfMissing(conn, "customers", "billing_address", "TEXT NOT NULL DEFAULT ''");
            AddColumnIfMissing(conn, "sales", "note", "TEXT NOT NULL DEFAULT ''");
            AddColumnIfMissing(conn, "suppliers", "billing_address", "TEXT NOT NULL DEFAULT ''");
            AddColumnIfMissing(conn, "suppliers", "contact_name", "TEXT NOT NULL DEFAULT ''");
        }

        /// <summary>
        /// Adds a column to an existing table only if it doesn't already exist.
        /// SQLite doesn't support IF NOT EXISTS in ALTER TABLE, so we check pragma first.
        /// </summary>
        private static void AddColumnIfMissing(
            SqliteConnection conn, string table, string column, string definition)
        {
            using var check = conn.CreateCommand();
            check.CommandText = $"PRAGMA table_info({table})";
            using var r = check.ExecuteReader();
            while (r.Read())
            {
                if (r.GetString(1).Equals(column, StringComparison.OrdinalIgnoreCase))
                    return; // already exists
            }

            using var alter = conn.CreateCommand();
            alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition}";
            alter.ExecuteNonQuery();
        }
    }
}
