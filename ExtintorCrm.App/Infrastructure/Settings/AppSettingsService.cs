using System;
using System.IO;
using System.Text.Json;
using ExtintorCrm.App.Infrastructure;

namespace ExtintorCrm.App.Infrastructure.Settings
{
    public sealed class AppSettingsService
    {
        public AppSettings Load()
        {
            try
            {
                var path = GetFilePath();
                if (!File.Exists(path))
                {
                    return Normalize(new AppSettings());
                }

                var raw = File.ReadAllText(path);
                var settings = JsonSerializer.Deserialize<AppSettings>(raw) ?? new AppSettings();
                return Normalize(settings);
            }
            catch
            {
                return Normalize(new AppSettings());
            }
        }

        public void Save(AppSettings settings)
        {
            var safe = Normalize(settings ?? new AppSettings());
            var path = GetFilePath();
            var json = JsonSerializer.Serialize(safe, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }

        public static string GetFilePath()
        {
            AppDataPaths.EnsureInitialized();
            return AppDataPaths.SettingsPath;
        }

        public static string GetDefaultBackupFolder()
        {
            AppDataPaths.EnsureInitialized();
            return AppDataPaths.DefaultBackupDirectory;
        }

        private static AppSettings Normalize(AppSettings settings)
        {
            settings.Theme = AppThemeManager.NormalizeTheme(settings.Theme);
            settings.BackupFolder = string.IsNullOrWhiteSpace(settings.BackupFolder)
                ? GetDefaultBackupFolder()
                : settings.BackupFolder.Trim();
            settings.BackupIntervalHours = settings.BackupIntervalHours <= 0 ? 24 : settings.BackupIntervalHours;
            settings.BackupRetentionCount = settings.BackupRetentionCount <= 0 ? 10 : settings.BackupRetentionCount;
            settings.ExportPreferredEntity = settings.ExportPreferredEntity == "Pagamentos" ? "Pagamentos" : "Clientes";
            settings.NotificationDaysWindow = settings.NotificationDaysWindow <= 0 ? 30 : Math.Clamp(settings.NotificationDaysWindow, 1, 365);
            settings.NotificationMaxItems = settings.NotificationMaxItems <= 0 ? 10 : Math.Clamp(settings.NotificationMaxItems, 1, 50);
            return settings;
        }
    }
}
