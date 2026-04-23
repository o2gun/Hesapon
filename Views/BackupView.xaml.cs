using ConstruxERP.Services;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ConstruxERP.Views
{
    public partial class BackupView : UserControl
    {
        private readonly BackupService _service = new();

        public BackupView() => InitializeComponent();

        private void UserControl_Loaded(object sender, RoutedEventArgs e) => LoadBackups();

        private void LoadBackups()
        {
            var files = _service.GetBackupFiles()
                .OrderByDescending(f => f)
                .Select(f => new
                {
                    FileName = Path.GetFileName(f),
                    FileSize = $"{new FileInfo(f).Length / 1024.0:F1} KB",
                    FullPath = f
                })
                .ToList();

            TxtNone.Visibility   = files.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            BackupList.ItemsSource = files;
        }

        private void BtnCreateBackup_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string path = _service.CreateBackup();
                MessageBox.Show($"Backup created:\n{Path.GetFileName(path)}",
                    "Backup Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadBackups();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Backup failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnRestore_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string path)
            {
                var result = MessageBox.Show(
                    $"Restore database from:\n{Path.GetFileName(path)}\n\n" +
                    "The current database will be archived before restore. Continue?",
                    "Confirm Restore", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes) return;

                try
                {
                    _service.RestoreBackup(path);
                    MessageBox.Show("Database restored. Please restart the application.",
                        "Restore Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Restore failed: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnDeleteBackup_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string path)
            {
                var result = MessageBox.Show(
                    $"Delete backup:\n{Path.GetFileName(path)}?",
                    "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes) return;

                try { File.Delete(path); LoadBackups(); }
                catch (Exception ex)
                {
                    MessageBox.Show($"Delete failed: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
