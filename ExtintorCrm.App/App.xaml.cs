using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using ExtintorCrm.App.Infrastructure;
using ExtintorCrm.App.Infrastructure.Backup;
using ExtintorCrm.App.Infrastructure.Logging;
using ExtintorCrm.App.Infrastructure.Settings;
using ExtintorCrm.App.Presentation;
using Microsoft.EntityFrameworkCore;

namespace ExtintorCrm.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        RegisterGlobalExceptionHandlers();

        try
        {
            AppDataPaths.EnsureInitialized();

            using var db = new AppDbContext();
            var hasPendingMigrations = db.Database.GetPendingMigrations().Any();
            if (hasPendingMigrations)
            {
                TryCreatePreMigrationBackup();
            }

            db.Database.Migrate();
            AppLogger.Info("Inicialização do banco concluída com sucesso.");
        }
        catch (Exception ex)
        {
            AppLogger.Error("Falha ao inicializar o banco de dados.", ex);

            var dbPath = "(não definido)";
            try
            {
                dbPath = AppDbContext.GetDatabasePath();
            }
            catch
            {
                // Ignora erro ao consultar caminho de banco durante falha de startup.
            }

            DialogService.Error(
                "Erro de inicialização",
                $"Erro ao inicializar o banco de dados: {ex.Message}{Environment.NewLine}{Environment.NewLine}Caminho do banco: {dbPath}",
                null);

            Shutdown();
            return;
        }

        var settingsService = new AppSettingsService();
        var settings = settingsService.Load();
        AppThemeManager.ApplyTheme(settings.Theme);
    }

    private static void RegisterGlobalExceptionHandlers()
    {
        Current.DispatcherUnhandledException += (_, args) =>
        {
            AppLogger.Error("Exceção não tratada na UI thread.", args.Exception);
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            AppLogger.Error("Exceção não tratada no domínio da aplicação.", ex);
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            AppLogger.Error("Exceção de Task não observada.", args.Exception);
            args.SetObserved();
        };
    }

    private static void TryCreatePreMigrationBackup()
    {
        try
        {
            var backupFolder = Path.Combine(AppDataPaths.DefaultBackupDirectory, "pre-migration");
            var backupService = new BackupService();
            backupService.CreateBackupAsync(backupFolder).GetAwaiter().GetResult();
            AppLogger.Info("Backup pré-migração criado com sucesso.");
        }
        catch (Exception ex)
        {
            AppLogger.Error("Falha ao criar backup pré-migração.", ex);
            // Se o backup pré-migração falhar, não bloqueia a inicialização.
            // O fluxo normal de backup manual/automático continua disponível.
        }
    }
}
