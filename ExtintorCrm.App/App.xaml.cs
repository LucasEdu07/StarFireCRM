using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
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
    private const int WindowCornerRadiusPx = 16;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        RegisterRoundedWindowHandlers();
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

    private static void RegisterRoundedWindowHandlers()
    {
        EventManager.RegisterClassHandler(
            typeof(Window),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnWindowLoaded));
    }

    private static void OnWindowLoaded(object sender, RoutedEventArgs _)
    {
        if (sender is not Window window)
        {
            return;
        }

        if (window.WindowStyle != WindowStyle.None)
        {
            return;
        }

        window.SourceInitialized -= OnWindowSourceInitialized;
        window.SourceInitialized += OnWindowSourceInitialized;
        window.SizeChanged -= OnWindowSizeChanged;
        window.SizeChanged += OnWindowSizeChanged;
        window.StateChanged -= OnWindowStateChanged;
        window.StateChanged += OnWindowStateChanged;

        ApplyRoundedWindowRegion(window);
    }

    private static void OnWindowSourceInitialized(object? sender, EventArgs _)
    {
        if (sender is Window window)
        {
            ApplyRoundedWindowRegion(window);
        }
    }

    private static void OnWindowSizeChanged(object sender, SizeChangedEventArgs _)
    {
        if (sender is Window window)
        {
            ApplyRoundedWindowRegion(window);
        }
    }

    private static void OnWindowStateChanged(object? sender, EventArgs _)
    {
        if (sender is Window window)
        {
            ApplyRoundedWindowRegion(window);
        }
    }

    private static void ApplyRoundedWindowRegion(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        if (window.WindowState == WindowState.Maximized)
        {
            SetWindowRgn(hwnd, IntPtr.Zero, true);
            return;
        }

        if (window.ActualWidth <= 0 || window.ActualHeight <= 0)
        {
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(window);
        var widthPx = (int)Math.Ceiling(window.ActualWidth * dpi.DpiScaleX);
        var heightPx = (int)Math.Ceiling(window.ActualHeight * dpi.DpiScaleY);
        var radiusPx = (int)Math.Ceiling(WindowCornerRadiusPx * Math.Max(dpi.DpiScaleX, dpi.DpiScaleY));

        var region = CreateRoundRectRgn(0, 0, widthPx + 1, heightPx + 1, radiusPx * 2, radiusPx * 2);
        if (region == IntPtr.Zero)
        {
            return;
        }

        if (SetWindowRgn(hwnd, region, true) == 0)
        {
            DeleteObject(region);
        }
    }

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateRoundRectRgn(
        int nLeftRect,
        int nTopRect,
        int nRightRect,
        int nBottomRect,
        int nWidthEllipse,
        int nHeightEllipse);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr hObject);

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
