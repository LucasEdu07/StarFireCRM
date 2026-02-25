using System;

namespace ExtintorCrm.App.Infrastructure.Settings
{
    public sealed class AppSettings
    {
        public string Theme { get; set; } = AppThemeManager.LightTheme;
        public bool Fullscreen { get; set; }
        public bool BackupEnabled { get; set; }
        public string BackupFolder { get; set; } = string.Empty;
        public int BackupIntervalHours { get; set; } = 24;
        public int BackupRetentionCount { get; set; } = 10;
        public DateTime? LastAutoBackupUtc { get; set; }
        public string ExportPreferredEntity { get; set; } = "Clientes";
        public bool ExportPreferExcel { get; set; } = true;
        public string ExportClienteSelectedFields { get; set; } = string.Empty;
        public string ExportPagamentoSelectedFields { get; set; } = string.Empty;
        public bool NotificationShowExtintores { get; set; } = true;
        public bool NotificationShowAlvaras { get; set; } = true;
        public bool NotificationShowPagamentos { get; set; } = true;
        public bool NotificationIncludeOverdue { get; set; } = true;
        public int NotificationDaysWindow { get; set; } = 30;
        public int NotificationMaxItems { get; set; } = 10;
    }
}
