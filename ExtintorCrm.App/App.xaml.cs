using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using ExtintorCrm.App.Infrastructure;
using ExtintorCrm.App.Infrastructure.Backup;
using ExtintorCrm.App.Infrastructure.Logging;
using ExtintorCrm.App.Infrastructure.Settings;
using ExtintorCrm.App.Infrastructure.WebView;
using ExtintorCrm.App.Presentation;
using Microsoft.EntityFrameworkCore;

namespace ExtintorCrm.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private const int MainWindowRenderTimeoutMs = 6000;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        RegisterGlobalExceptionHandlers();
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var settingsService = new AppSettingsService();
        var settings = settingsService.Load();
        AppThemeManager.ApplyTheme(settings.Theme);
        AppThemeManager.ApplyChromeCustomization(
            settings.UiBorderColorHex,
            settings.UiTitleBarColorHex,
            settings.UiVanillaColorHex,
            settings.UiVanillaIntensityPercent);

        var splash = new SplashScreenWindow();
        splash.SetStatus("Inicializando...");
        splash.Show();
        splash.Activate();

        try
        {
            splash.SetStatus("Inicializando armazenamento...");
            await Task.Run(AppDataPaths.EnsureInitialized);
            WebView2Bootstrapper.PreWarm();

            splash.SetStatus("Verificando banco de dados...");
            var hasPendingMigrations = await Task.Run(() =>
            {
                using var db = new AppDbContext();
                return db.Database.GetPendingMigrations().Any();
            });

            if (hasPendingMigrations)
            {
                splash.SetStatus("Criando backup de seguranca...");
                await Task.Run(TryCreatePreMigrationBackup);
            }

            splash.SetStatus("Aplicando migracoes...");
            await Task.Run(() =>
            {
                using var db = new AppDbContext();
                db.Database.Migrate();
            });

            AppLogger.Info("Inicializacao do banco concluida com sucesso.");

            splash.SetStatus("Preparando interface...");
            var mainWindow = new MainWindow
            {
                Opacity = 0,
                ShowInTaskbar = false,
                ShowActivated = false
            };

            var firstRenderTask = WaitForFirstContentRenderedAsync(mainWindow);
            MainWindow = mainWindow;
            mainWindow.Show();

            try
            {
                await mainWindow.WarmupVisualsAsync();
            }
            catch (Exception warmupEx)
            {
                AppLogger.Error("Falha ao aquecer componentes visuais da interface.", warmupEx);
            }

            splash.SetStatus("Finalizando abertura...");
            await WaitForMainWindowPresentationAsync(mainWindow, firstRenderTask);

            // Keep splash on top while the main window settles to avoid black frames.
            mainWindow.Opacity = 1;
            mainWindow.ShowInTaskbar = true;
            mainWindow.Activate();
            await mainWindow.PrepareForRevealAsync();
            await mainWindow.PrewarmRockerControlsAsync();

            var splashFadeTask = splash.FadeOutAndCloseAsync(620);
            var handoffTask = mainWindow.PlayStartupHandoffAsync(620);
            await Task.WhenAll(splashFadeTask, handoffTask);
            await mainWindow.HideStartupOverlayAsync();
            ShutdownMode = ShutdownMode.OnMainWindowClose;
        }
        catch (Exception ex)
        {
            AppLogger.Error("Falha ao inicializar o banco de dados.", ex);

            if (splash.IsVisible)
            {
                splash.Close();
            }

            var dbPath = "(nao definido)";
            try
            {
                dbPath = AppDbContext.GetDatabasePath();
            }
            catch
            {
                // Ignore DB path read errors during startup failure.
            }

            DialogService.Error(
                "Erro de inicializacao",
                $"Erro ao inicializar o banco de dados: {ex.Message}{Environment.NewLine}{Environment.NewLine}Caminho do banco: {dbPath}",
                null);

            Shutdown();
            return;
        }
        finally
        {
            if (splash.IsVisible)
            {
                splash.Close();
            }
        }
    }

    private static Task WaitForFirstContentRenderedAsync(Window mainWindow)
    {
        var readyTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler? handler = null;
        handler = (_, _) =>
        {
            if (handler != null)
            {
                mainWindow.ContentRendered -= handler;
            }

            readyTcs.TrySetResult(true);
        };

        mainWindow.ContentRendered += handler;
        return readyTcs.Task;
    }

    private static async Task WaitForMainWindowPresentationAsync(Window mainWindow, Task firstRenderTask)
    {
        await Task.WhenAny(firstRenderTask, Task.Delay(MainWindowRenderTimeoutMs));
        await mainWindow.Dispatcher.InvokeAsync(mainWindow.UpdateLayout, DispatcherPriority.Render);
        await mainWindow.Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.ApplicationIdle);
    }

    private static void RegisterGlobalExceptionHandlers()
    {
        Current.DispatcherUnhandledException += (_, args) =>
        {
            AppLogger.Error("Excecao nao tratada na UI thread.", args.Exception);
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            AppLogger.Error("Excecao nao tratada no dominio da aplicacao.", ex);
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            AppLogger.Error("Excecao de Task nao observada.", args.Exception);
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
            AppLogger.Info("Backup pre-migracao criado com sucesso.");
        }
        catch (Exception ex)
        {
            AppLogger.Error("Falha ao criar backup pre-migracao.", ex);
            // If pre-migration backup fails, keep startup going.
            // Manual/automatic backup flow remains available to users.
        }
    }
}
