using System;
using System.IO;
using System.Threading.Tasks;
using ExtintorCrm.App.Infrastructure.Logging;
using Microsoft.Web.WebView2.Core;

namespace ExtintorCrm.App.Infrastructure.WebView;

public static class WebView2Bootstrapper
{
    private static readonly object SyncRoot = new();
    private static Task<CoreWebView2Environment?>? _environmentTask;

    public static void PreWarm()
    {
        _ = GetEnvironmentAsync();
    }

    public static Task<CoreWebView2Environment?> GetEnvironmentAsync()
    {
        lock (SyncRoot)
        {
            _environmentTask ??= CreateEnvironmentAsync();
            return _environmentTask;
        }
    }

    private static async Task<CoreWebView2Environment?> CreateEnvironmentAsync()
    {
        try
        {
            AppDataPaths.EnsureInitialized();
            var userDataDir = Path.Combine(AppDataPaths.DataDirectory, "webview2");
            Directory.CreateDirectory(userDataDir);
            return await CoreWebView2Environment.CreateAsync(null, userDataDir);
        }
        catch (Exception ex)
        {
            AppLogger.Error("Falha ao pré-inicializar WebView2.", ex);
            return null;
        }
    }
}
