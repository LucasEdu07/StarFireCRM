using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using ExtintorCrm.App.Infrastructure;
using ExtintorCrm.App.Infrastructure.Backup;
using ExtintorCrm.App.Infrastructure.Import;
using ExtintorCrm.App.Infrastructure.Logging;
using ExtintorCrm.App.Infrastructure.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;

namespace ExtintorCrm.App.Presentation
{
    public partial class ClientesViewModel
    {
        private void ApplyWindowMode()
        {
            if (IsFullscreen)
            {
                MainWindowLeft = double.NaN;
                MainWindowTop = double.NaN;
            }
            else
            {
                var workArea = SystemParameters.WorkArea;
                var preferredWidth = DefaultWindowWidth;
                var preferredHeight = DefaultWindowHeight;
                var minimumWidth = 980d;
                var minimumHeight = 680d;

                if (WindowResolutionPresets.TryGetSize(SelectedWindowResolutionPreset, out var resolutionWidthPx, out var resolutionHeightPx))
                {
                    // Presets are stored in physical pixels; convert to WPF DIP so DPI scaling behaves as expected.
                    var (dpiScaleX, dpiScaleY) = GetCurrentDpiScale();
                    preferredWidth = resolutionWidthPx / dpiScaleX;
                    preferredHeight = resolutionHeightPx / dpiScaleY;

                    // Allow compact presets to actually resize the window on high-DPI screens.
                    minimumWidth = 840d;
                    minimumHeight = 560d;
                }

                MainWindowWidth = FitWindowDimension(preferredWidth, workArea.Width, minimumSize: minimumWidth, safePadding: 26);
                MainWindowHeight = FitWindowDimension(preferredHeight, workArea.Height, minimumSize: minimumHeight, safePadding: 28);
                MainWindowLeft = workArea.Left + Math.Max(0, (workArea.Width - MainWindowWidth) / 2);
                MainWindowTop = workArea.Top + Math.Max(0, (workArea.Height - MainWindowHeight) / 2);
            }

            OnPropertyChanged(nameof(MainWindowState));
            OnPropertyChanged(nameof(MainWindowStyle));
            OnPropertyChanged(nameof(MainResizeMode));
            OnPropertyChanged(nameof(MainTopmost));
        }

        private static (double DpiScaleX, double DpiScaleY) GetCurrentDpiScale()
        {
            var window = Application.Current?.MainWindow;
            if (window is null)
            {
                return (1d, 1d);
            }

            var dpi = VisualTreeHelper.GetDpi(window);
            var dpiScaleX = dpi.DpiScaleX <= 0 ? 1d : dpi.DpiScaleX;
            var dpiScaleY = dpi.DpiScaleY <= 0 ? 1d : dpi.DpiScaleY;
            return (dpiScaleX, dpiScaleY);
        }

        private static double FitWindowDimension(double preferredSize, double availableSize, double minimumSize, double safePadding)
        {
            var usableSize = Math.Max(0, availableSize - safePadding);
            if (usableSize <= 0)
            {
                return availableSize;
            }

            var clampedMinimum = Math.Min(minimumSize, usableSize);
            var clampedPreferred = Math.Min(preferredSize, usableSize);
            return Math.Max(clampedPreferred, clampedMinimum);
        }

        private void SaveAppSettings(string theme)
        {
            var current = _appSettingsService.Load();
            current.Theme = theme;
            current.Fullscreen = IsFullscreen;
            current.WindowResolutionPreset = SelectedWindowResolutionPreset;
            current.BackupEnabled = BackupAutomatico;
            current.BackupFolder = BackupFolder;
            current.BackupIntervalHours = BackupIntervalHours;
            current.BackupRetentionCount = BackupRetentionCount;
            current.LastAutoBackupUtc = _lastAutoBackupUtc;
            current.NotificationShowExtintores = NotificationShowExtintores;
            current.NotificationShowAlvaras = NotificationShowAlvaras;
            current.NotificationShowPagamentos = NotificationShowPagamentos;
            current.NotificationIncludeOverdue = NotificationIncludeOverdue;
            current.NotificationDaysWindow = NotificationDaysWindow;
            current.NotificationMaxItems = NotificationMaxItems;
            current.UiBorderColorHex = UiBorderColorHex;
            current.UiTitleBarColorHex = UiTitleBarColorHex;
            current.UiVanillaColorHex = UiVanillaColorHex;
            current.UiVanillaIntensityPercent = UiVanillaIntensityPercent;
            current.ExportPreferredEntity = _exportPreferredEntity;
            current.ExportPreferExcel = _exportPreferExcel;
            current.ExportClienteSelectedFields = string.Join(';', _preferredClienteExportFields.OrderBy(x => x));
            current.ExportPagamentoSelectedFields = string.Join(';', _preferredPagamentoExportFields.OrderBy(x => x));
            _appSettingsService.Save(current);
            OnPropertyChanged(nameof(LastBackupLabel));
        }

        private static void ApplyPreferredExportFields(string raw, HashSet<string> destination)
        {
            destination.Clear();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return;
            }

            foreach (var field in raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                destination.Add(field);
            }
        }

        private void StartBackupScheduler()
        {
            if (_backupTimer == null)
            {
                _backupTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
                _backupTimer.Tick += async (_, _) => await CheckAutoBackupAsync();
            }

            if (!_backupTimer.IsEnabled)
            {
                _backupTimer.Start();
            }
        }

        private async Task CheckAutoBackupAsync()
        {
            if (!BackupAutomatico || IsBackupRunning || IsImporting)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(BackupFolder))
            {
                return;
            }

            var interval = TimeSpan.FromHours(Math.Max(1, BackupIntervalHours));
            var last = _lastAutoBackupUtc ?? DateTime.MinValue;
            if ((DateTime.UtcNow - last) < interval)
            {
                return;
            }

            await RunBackupAsync(true);
        }

        private void SelectBackupFolder()
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Selecione a pasta para salvar os backups",
                InitialDirectory = string.IsNullOrWhiteSpace(BackupFolder)
                    ? AppSettingsService.GetDefaultBackupFolder()
                    : BackupFolder
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            BackupFolder = dialog.FolderName;
        }

        private async Task RunBackupAsync(bool automatic)
        {
            if (IsBackupRunning)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(BackupFolder))
            {
                await ShowToastAsync("Defina a pasta de backup nas Configuracoes.", "Error");
                return;
            }

            try
            {
                IsBackupRunning = true;
                var backupService = new BackupService();
                var operation = await backupService.TryCreateBackupAsync(BackupFolder);
                if (!operation.IsSuccess)
                {
                    await ShowOperationResultAsync(operation, showDialogOnFailure: !automatic);
                    return;
                }

                CleanupOldBackups();
                _lastAutoBackupUtc = DateTime.UtcNow;
                var theme = IsDarkMode ? AppThemeManager.DarkTheme : AppThemeManager.LightTheme;
                SaveAppSettings(theme);

                if (automatic)
                {
                    await ShowToastAsync("Backup automatico executado com sucesso.", "Info");
                }
                else
                {
                    await ShowOperationResultAsync(operation, showDialogOnFailure: false);
                }
            }
            catch (Exception ex)
            {
                await LogAndToastErrorAsync("Falha na execução de backup.", "Falha no backup", ex);
            }
            finally
            {
                IsBackupRunning = false;
            }
        }

        private void CleanupOldBackups()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(BackupFolder) || !System.IO.Directory.Exists(BackupFolder))
                {
                    return;
                }

                var files = System.IO.Directory.GetFiles(BackupFolder, "star-fire-backup-*.zip")
                    .OrderByDescending(System.IO.File.GetCreationTimeUtc)
                    .ToList();

                var keep = Math.Max(1, BackupRetentionCount);
                foreach (var file in files.Skip(keep))
                {
                    System.IO.File.Delete(file);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("Falha na limpeza de backups antigos.", ex);
                // Mantém fluxo principal mesmo se limpeza falhar.
            }
        }

        private async Task RestoreBackupAsync()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Backup Star Fire CRM (*.zip)|*.zip",
                CheckFileExists = true,
                Multiselect = false,
                InitialDirectory = string.IsNullOrWhiteSpace(BackupFolder)
                    ? AppSettingsService.GetDefaultBackupFolder()
                    : BackupFolder
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            var confirmed = DialogService.Confirm(
                "Restaurar backup",
                "Esta ação substituirá banco e configurações atuais. Deseja continuar?",
                Application.Current?.MainWindow);

            if (!confirmed)
            {
                return;
            }

            try
            {
                IsBackupRunning = true;
                var backupService = new BackupService();
                var operation = await backupService.TryRestoreBackupAsync(dialog.FileName);
                if (!operation.IsSuccess)
                {
                    await ShowOperationResultAsync(operation);
                    return;
                }

                await LoadAsync(reloadUiSettings: true);
                await ShowOperationResultAsync(operation, showDialogOnFailure: false);
            }
            catch (Exception ex)
            {
                await LogAndToastErrorAsync("Falha ao restaurar backup.", "Falha ao restaurar backup", ex);
            }
            finally
            {
                IsBackupRunning = false;
            }
        }

        private async Task RecreateClientesAsync()
        {
            var confirmed = DialogService.ConfirmWithText(
                "Recriar base de clientes",
                "Esta ação vai apagar todos os clientes e também os pagamentos vinculados.",
                "RECRIAR CLIENTES",
                Application.Current?.MainWindow);

            if (!confirmed)
            {
                return;
            }

            IsImporting = true;
            string? backupFile = null;

            try
            {
                try
                {
                    var backupFolder = string.IsNullOrWhiteSpace(BackupFolder)
                        ? AppSettingsService.GetDefaultBackupFolder()
                        : BackupFolder;
                    var backupService = new BackupService();
                    backupFile = await backupService.CreateBackupAsync(Path.Combine(backupFolder, "pre-recreate-clientes"));
                }
                catch (Exception ex)
                {
                    AppLogger.Error("Falha ao criar backup pré-recriação de clientes.", ex);
                    // Continua com a recriação mesmo se backup prévio falhar.
                }

                await using (var db = new AppDbContext())
                {
                    await db.Pagamentos.ExecuteDeleteAsync();
                    await db.Clientes.ExecuteDeleteAsync();
                }

                _allClientes.Clear();
                _allPagamentos.Clear();
                Clientes.Clear();
                Pagamentos.Clear();
                SelectedCliente = null;
                SelectedPagamento = null;

                await LoadAsync();

                var message = backupFile == null
                    ? "Base de clientes recriada com sucesso."
                    : $"Base de clientes recriada com sucesso. Backup salvo em: {backupFile}";
                await ShowToastAsync(message, "Success");
            }
            catch (Exception ex)
            {
                await LogAndToastErrorAsync("Falha ao recriar base de clientes.", "Falha ao recriar base de clientes", ex);
            }
            finally
            {
                IsImporting = false;
            }
        }

        private async Task RecreatePagamentosAsync()
        {
            var confirmed = DialogService.ConfirmWithText(
                "Recriar base de pagamentos",
                "Esta ação vai apagar todos os pagamentos cadastrados, mantendo os clientes.",
                "RECRIAR PAGAMENTOS",
                Application.Current?.MainWindow);

            if (!confirmed)
            {
                return;
            }

            IsImporting = true;
            string? backupFile = null;

            try
            {
                try
                {
                    var backupFolder = string.IsNullOrWhiteSpace(BackupFolder)
                        ? AppSettingsService.GetDefaultBackupFolder()
                        : BackupFolder;
                    var backupService = new BackupService();
                    backupFile = await backupService.CreateBackupAsync(Path.Combine(backupFolder, "pre-recreate-pagamentos"));
                }
                catch (Exception ex)
                {
                    AppLogger.Error("Falha ao criar backup pré-recriação de pagamentos.", ex);
                    // Continua com a recriação mesmo se backup prévio falhar.
                }

                await using (var db = new AppDbContext())
                {
                    await db.Pagamentos.ExecuteDeleteAsync();
                }

                _allPagamentos.Clear();
                Pagamentos.Clear();
                SelectedPagamento = null;

                await LoadPagamentosAsync();
                RefreshDashboardExecutiveData();

                var message = backupFile == null
                    ? "Base de pagamentos recriada com sucesso."
                    : $"Base de pagamentos recriada com sucesso. Backup salvo em: {backupFile}";
                await ShowToastAsync(message, "Success");
            }
            catch (Exception ex)
            {
                await LogAndToastErrorAsync("Falha ao recriar base de pagamentos.", "Falha ao recriar base de pagamentos", ex);
            }
            finally
            {
                IsImporting = false;
            }
        }

        private async Task ImportPagamentosAsync()
        {
            var docsPagamentoFolder = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "docs", "ArquivoImportacaoPagamentos");
            var initialDirectory = Directory.Exists(docsPagamentoFolder)
                ? Path.GetFullPath(docsPagamentoFolder)
                : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            var dialog = new OpenFileDialog
            {
                Filter = "Excel OpenXML (*.xlsx;*.xlsm;*.xltx;*.xltm)|*.xlsx;*.xlsm;*.xltx;*.xltm",
                CheckFileExists = true,
                Multiselect = false,
                InitialDirectory = initialDirectory
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                IsImporting = true;
                var importer = new PagamentoExcelImporter();
                var result = await importer.ImportAsync(dialog.FileName);

                await LoadPagamentosAsync();

                var missingClientCount = result.SkippedReasons.Count(reason =>
                    !string.IsNullOrWhiteSpace(reason) &&
                    reason.Contains("cliente", StringComparison.OrdinalIgnoreCase) &&
                    reason.Contains("cpf", StringComparison.OrdinalIgnoreCase));

                var operation = result.ToOperationResult("pagamentos");
                await ShowOperationResultAsync(operation);

                if (missingClientCount > 0)
                {
                    var owner = Application.Current?.MainWindow;
                    DialogService.Info(
                        "Cliente não encontrado",
                        $"{missingClientCount} pagamento(s) não foram computados porque o cliente (CPF/CNPJ) não existe no sistema.\n\nCadastre ou importe os clientes primeiro e depois reimporte os pagamentos.",
                        owner);
                }
            }
            catch (Exception ex)
            {
                await LogAndToastErrorAsync("Falha ao importar pagamentos.", "Falha ao importar pagamentos", ex);
            }
            finally
            {
                IsImporting = false;
            }
        }
    }
}
