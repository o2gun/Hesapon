using System;
using System.IO;

namespace ConstruxERP.Services
{
    /// <summary>
    /// Handles automatic and manual SQLite database backups.
    /// Backups are stored in %AppData%\ConstruxERP\backups\.
    /// </summary>
    public class BackupService
    {
        private static readonly string _dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ConstruxERP", "construx.db");

        private static readonly string _backupDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ConstruxERP", "backups");

        public BackupService()
        {
            Directory.CreateDirectory(_backupDir);
        }

        // ─── Manual backup ────────────────────────────────────────────────────

        /// <summary>
        /// Creates a timestamped copy of the database file.
        /// Returns the full path to the backup file.
        /// </summary>
        public string CreateBackup()
        {
            if (!File.Exists(_dbPath))
                throw new FileNotFoundException("Database file not found.", _dbPath);

            string fileName  = $"backup_{DateTime.Now:yyyy_MM_dd_HHmmss}.db";
            string destPath  = Path.Combine(_backupDir, fileName);

            File.Copy(_dbPath, destPath, overwrite: true);
            return destPath;
        }

        // ─── Restore ──────────────────────────────────────────────────────────

        /// <summary>
        /// Restores the database from the given backup file.
        /// The current live database is archived before overwriting.
        /// </summary>
        public void RestoreBackup(string backupFilePath)
        {
            if (!File.Exists(backupFilePath))
                throw new FileNotFoundException("Backup file not found.", backupFilePath);

            // Archive the current DB first
            if (File.Exists(_dbPath))
            {
                string archive = Path.Combine(_backupDir,
                    $"pre_restore_{DateTime.Now:yyyy_MM_dd_HHmmss}.db");
                File.Copy(_dbPath, archive, overwrite: true);
            }

            File.Copy(backupFilePath, _dbPath, overwrite: true);
        }

        // ─── List backups ─────────────────────────────────────────────────────

        public string[] GetBackupFiles()
        {
            return Directory.GetFiles(_backupDir, "backup_*.db");
        }

        // ─── Auto-backup (call once per day on startup) ───────────────────────

        /// <summary>
        /// Creates a backup only if no backup exists for today.
        /// Call this from App.xaml.cs after DatabaseContext.Initialize().
        /// </summary>
        public void AutoBackupIfNeeded()
        {
            string todayTag = DateTime.Now.ToString("yyyy_MM_dd");
            string pattern  = $"backup_{todayTag}_*.db";

            string[] existing = Directory.GetFiles(_backupDir, pattern);
            if (existing.Length == 0)
            {
                try { CreateBackup(); }
                catch { /* Don't crash the app if the auto-backup fails */ }
            }
        }

        // ─── Prune old backups (keep last N) ─────────────────────────────────

        public void PruneOldBackups(int keepCount = 30)
        {
            var files = Directory.GetFiles(_backupDir, "backup_*.db");
            Array.Sort(files);                      // oldest first (date in name)
            int toDelete = files.Length - keepCount;
            for (int i = 0; i < toDelete; i++)
                File.Delete(files[i]);
        }
    }
}
