using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ExtintorCrm.App.Infrastructure.Logging;
using ExtintorCrm.App.Infrastructure.Settings;
using ExtintorCrm.App.Infrastructure.WebView;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using MediaColor = System.Windows.Media.Color;
using WpfUserControl = System.Windows.Controls.UserControl;

namespace ExtintorCrm.App.Presentation.Controls;

public partial class ThemeRockerSwitchControl : WpfUserControl
{
    private bool _initAttempted;
    private bool _webReady;
    private bool _webPainted;
    private bool _paintReadyRequested;
    private bool _updatingFromWeb;
    private bool _usingFallback;
    private bool _themeObserverAttached;
    private bool _hasPresentedWeb;
    private bool _isCrossfading;
    private double _visibleRatio = 1d;
    private ScrollViewer? _parentScrollViewer;
    private FrameworkElement? _clipHost;
    private WebView2? _rockerWebView;
    private readonly TaskCompletionSource<bool> _readyTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public static readonly DependencyProperty IsCheckedProperty = DependencyProperty.Register(
        nameof(IsChecked),
        typeof(bool),
        typeof(ThemeRockerSwitchControl),
        new FrameworkPropertyMetadata(
            false,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnIsCheckedChanged));

    public static readonly DependencyProperty LeftLabelProperty = DependencyProperty.Register(
        nameof(LeftLabel),
        typeof(string),
        typeof(ThemeRockerSwitchControl),
        new PropertyMetadata("On", OnLabelsChanged));

    public static readonly DependencyProperty RightLabelProperty = DependencyProperty.Register(
        nameof(RightLabel),
        typeof(string),
        typeof(ThemeRockerSwitchControl),
        new PropertyMetadata("Off", OnLabelsChanged));

    public ThemeRockerSwitchControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        IsEnabledChanged += OnIsEnabledChanged;
        LayoutUpdated += OnLayoutUpdated;
    }

    public bool IsChecked
    {
        get => (bool)GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }

    public Task ReadyTask => _readyTcs.Task;

    public string LeftLabel
    {
        get => (string)GetValue(LeftLabelProperty);
        set => SetValue(LeftLabelProperty, value);
    }

    public string RightLabel
    {
        get => (string)GetValue(RightLabelProperty);
        set => SetValue(RightLabelProperty, value);
    }

    private static void OnIsCheckedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (ThemeRockerSwitchControl)d;
        if (control._updatingFromWeb)
        {
            return;
        }

        _ = control.SyncStateToWebAsync();
    }

    private static void OnLabelsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (ThemeRockerSwitchControl)d;
        _ = control.SyncStateToWebAsync();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        AttachThemeObserver();
        AttachScrollObserver();
        await PrepareForDisplayInternalAsync();
        UpdateRenderModeFromViewport();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        DetachThemeObserver();
        DetachScrollObserver();
    }

    public void PrepareForDisplay()
    {
        if (DesignerProperties.GetIsInDesignMode(this))
        {
            return;
        }

        _ = PrepareForDisplayInternalAsync();
    }

    private async Task PrepareForDisplayInternalAsync()
    {
        if (DesignerProperties.GetIsInDesignMode(this))
        {
            return;
        }

        if (_webReady)
        {
            await SyncStateToWebAsync();
            if (!_webPainted)
            {
                _ = RequestPaintReadyAsync();
            }

            UpdateRenderModeFromViewport();
            return;
        }

        await EnsureWebViewReadyAsync();
        await SyncStateToWebAsync();
        UpdateRenderModeFromViewport();
    }

    private void OnIsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        _ = SyncStateToWebAsync();
    }

    private async Task EnsureWebViewReadyAsync()
    {
        if (_initAttempted || _usingFallback)
        {
            return;
        }

        _initAttempted = true;

        try
        {
            var environment = await WebView2Bootstrapper.GetEnvironmentAsync();
            if (environment == null)
            {
                ShowFallback();
                return;
            }

            var webView = EnsureWebViewHost();
            await webView.EnsureCoreWebView2Async(environment);
            webView.DefaultBackgroundColor = System.Drawing.Color.Transparent;

            var core = webView.CoreWebView2;
            if (core == null)
            {
                ShowFallback();
                return;
            }

            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.AreBrowserAcceleratorKeysEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.IsZoomControlEnabled = false;

            core.WebMessageReceived += CoreWebView2_WebMessageReceived;
            webView.NavigationCompleted += RockerWebView_NavigationCompleted;
            webView.NavigateToString(HtmlTemplate);
        }
        catch (Exception ex)
        {
            AppLogger.Error("Falha ao inicializar switch WebView2. Usando fallback nativo.", ex);
            ShowFallback();
        }
    }

    private async void RockerWebView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess)
        {
            ShowFallback();
            return;
        }

        _webReady = true;
        _webPainted = false;
        _paintReadyRequested = false;
        _hasPresentedWeb = false;
        _isCrossfading = false;
        await SyncStateToWebAsync();
        await RequestPaintReadyAsync();
        UpdateRenderModeFromViewport();
    }

    private void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        string payload;
        try
        {
            payload = e.TryGetWebMessageAsString();
        }
        catch
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(payload))
        {
            return;
        }

        try
        {
            using var json = JsonDocument.Parse(payload);
            var root = json.RootElement;
            if (!root.TryGetProperty("type", out var typeElement))
            {
                return;
            }

            var type = typeElement.GetString();
            if (string.Equals(type, "ready", StringComparison.OrdinalIgnoreCase))
            {
                _ = SyncStateToWebAsync();
                _ = RequestPaintReadyAsync();
                return;
            }

            if (string.Equals(type, "painted", StringComparison.OrdinalIgnoreCase))
            {
                _webPainted = true;
                _paintReadyRequested = false;
                UpdateRenderModeFromViewport();
                _readyTcs.TrySetResult(true);
                return;
            }

            if (!string.Equals(type, "toggle", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!root.TryGetProperty("value", out var valueElement))
            {
                return;
            }

            var value = valueElement.GetBoolean();
            if (value == IsChecked)
            {
                return;
            }

            _updatingFromWeb = true;
            try
            {
                SetCurrentValue(IsCheckedProperty, value);
            }
            finally
            {
                _updatingFromWeb = false;
            }

            _ = SyncStateToWebAsync();
        }
        catch (Exception ex)
        {
            AppLogger.Error("Falha ao processar mensagem do switch WebView2.", ex);
        }
    }

    private async Task SyncStateToWebAsync()
    {
        if (_usingFallback)
        {
            return;
        }

        if (!_webReady || _rockerWebView?.CoreWebView2 == null)
        {
            return;
        }

        var isCheckedLiteral = IsChecked ? "true" : "false";
        var isEnabledLiteral = IsEnabled ? "true" : "false";
        var labelsJson = JsonSerializer.Serialize(new
        {
            left = NormalizeLabel(LeftLabel, "On"),
            right = NormalizeLabel(RightLabel, "Off")
        });

        try
        {
            var paletteJson = JsonSerializer.Serialize(BuildPalette());
            await _rockerWebView.ExecuteScriptAsync($"window.setRockerPalette({paletteJson});");
            await _rockerWebView.ExecuteScriptAsync($"window.setRockerLabels({labelsJson});");
            await _rockerWebView.ExecuteScriptAsync($"window.setRockerState({isCheckedLiteral}, {isEnabledLiteral});");
        }
        catch (Exception ex)
        {
            AppLogger.Error("Falha ao sincronizar estado do switch WebView2.", ex);
        }
    }

    private async Task RequestPaintReadyAsync()
    {
        if (_usingFallback || !_webReady || _webPainted || _paintReadyRequested || _rockerWebView?.CoreWebView2 == null)
        {
            return;
        }

        _paintReadyRequested = true;

        try
        {
            await _rockerWebView.ExecuteScriptAsync("window.notifyPaintReady && window.notifyPaintReady();");
        }
        catch (Exception ex)
        {
            _paintReadyRequested = false;
            AppLogger.Error("Falha ao solicitar sinal de paint-ready do switch WebView2.", ex);
            UpdateRenderModeFromViewport();
            _readyTcs.TrySetResult(true);
        }
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        UpdateRenderModeFromViewport();
    }

    private void OnThemeResourcesChanged(object? sender, EventArgs e)
    {
        _ = Dispatcher.InvokeAsync(async () =>
        {
            await SyncStateToWebAsync();
            UpdateRenderModeFromViewport();
        }, DispatcherPriority.Send);
    }

    private void OnParentScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        UpdateRenderModeFromViewport();
    }

    private void OnParentScrollSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateRenderModeFromViewport();
    }

    private void AttachScrollObserver()
    {
        if (_parentScrollViewer != null)
        {
            return;
        }

        _parentScrollViewer = FindAncestor<ScrollViewer>(this);
        _clipHost = FindAncestor<ScrollContentPresenter>(this) as FrameworkElement
                    ?? _parentScrollViewer as FrameworkElement;
        if (_parentScrollViewer == null)
        {
            return;
        }

        _parentScrollViewer.ScrollChanged += OnParentScrollChanged;
        _parentScrollViewer.SizeChanged += OnParentScrollSizeChanged;
    }

    private void DetachScrollObserver()
    {
        if (_parentScrollViewer == null)
        {
            return;
        }

        _parentScrollViewer.ScrollChanged -= OnParentScrollChanged;
        _parentScrollViewer.SizeChanged -= OnParentScrollSizeChanged;
        _parentScrollViewer = null;
        _clipHost = null;
    }

    private void AttachThemeObserver()
    {
        if (_themeObserverAttached)
        {
            return;
        }

        AppThemeManager.ThemeResourcesChanged += OnThemeResourcesChanged;
        _themeObserverAttached = true;
    }

    private void DetachThemeObserver()
    {
        if (!_themeObserverAttached)
        {
            return;
        }

        AppThemeManager.ThemeResourcesChanged -= OnThemeResourcesChanged;
        _themeObserverAttached = false;
    }

    private void UpdateRenderModeFromViewport()
    {
        if (_parentScrollViewer == null)
        {
            AttachScrollObserver();
        }

        if (_usingFallback)
        {
            ApplyRenderMode();
            return;
        }

        _visibleRatio = GetVisibleRatioInScrollViewer();

        ApplyRenderMode();
    }

    private double GetVisibleRatioInScrollViewer()
    {
        if (_clipHost == null || !_clipHost.IsVisible || ActualHeight <= 0 || ActualWidth <= 0)
        {
            return 1d;
        }

        try
        {
            var bounds = TransformToAncestor(_clipHost).TransformBounds(new Rect(0, 0, ActualWidth, ActualHeight));
            var visibleTop = Math.Max(0, bounds.Top);
            var visibleBottom = Math.Min(_clipHost.ActualHeight, bounds.Bottom);
            var visibleHeight = Math.Max(0, visibleBottom - visibleTop);
            var ratio = visibleHeight / Math.Max(1, bounds.Height);

            return Math.Clamp(ratio, 0d, 1d);
        }
        catch
        {
            return 1d;
        }
    }

    private void ApplyRenderMode()
    {
        if (_usingFallback)
        {
            RockerHost.Visibility = Visibility.Collapsed;
            FallbackToggle.Visibility = Visibility.Visible;
            _hasPresentedWeb = false;
            _isCrossfading = false;
            return;
        }

        var hiddenByScroll = _visibleRatio < 0.995;
        if (hiddenByScroll)
        {
            StopCrossfadeAnimations();
            RockerHost.Visibility = Visibility.Collapsed;
            FallbackToggle.Visibility = Visibility.Collapsed;
            return;
        }

        if (!_webPainted)
        {
            ShowPendingVisual();
            _ = RequestPaintReadyAsync();
            return;
        }

        if (!_hasPresentedWeb && FallbackToggle.Visibility == Visibility.Visible)
        {
            BeginWebRevealTransition();
            return;
        }

        ShowWebHost();
    }

    private void ShowWebHost()
    {
        StopCrossfadeAnimations();
        RockerHost.Visibility = Visibility.Visible;
        RockerHost.Opacity = 1;
        FallbackToggle.Visibility = Visibility.Collapsed;
        FallbackToggle.Opacity = 1;
        _hasPresentedWeb = true;
        _isCrossfading = false;
    }

    private void ShowFallbackVisual()
    {
        StopCrossfadeAnimations();
        RockerHost.Visibility = Visibility.Collapsed;
        RockerHost.Opacity = 0;
        FallbackToggle.Visibility = Visibility.Visible;
        FallbackToggle.Opacity = 1;
    }

    private void ShowPendingVisual()
    {
        StopCrossfadeAnimations();
        RockerHost.Visibility = Visibility.Collapsed;
        RockerHost.Opacity = 0;
        FallbackToggle.Visibility = Visibility.Collapsed;
        FallbackToggle.Opacity = 1;
    }

    private void BeginWebRevealTransition()
    {
        if (_isCrossfading)
        {
            return;
        }

        _isCrossfading = true;
        RockerHost.Visibility = Visibility.Visible;
        RockerHost.Opacity = 0;
        FallbackToggle.Visibility = Visibility.Visible;
        FallbackToggle.Opacity = 1;

        var duration = TimeSpan.FromMilliseconds(120);
        var webFadeIn = new DoubleAnimation(0, 1, duration) { FillBehavior = FillBehavior.Stop };
        var fallbackFadeOut = new DoubleAnimation(1, 0, duration) { FillBehavior = FillBehavior.Stop };

        webFadeIn.Completed += (_, _) =>
        {
            _isCrossfading = false;
            if (_visibleRatio < 0.995 || !_webPainted || _usingFallback)
            {
                ApplyRenderMode();
                return;
            }

            RockerHost.Opacity = 1;
            FallbackToggle.Opacity = 1;
            FallbackToggle.Visibility = Visibility.Collapsed;
            _hasPresentedWeb = true;
        };

        RockerHost.BeginAnimation(OpacityProperty, webFadeIn);
        FallbackToggle.BeginAnimation(OpacityProperty, fallbackFadeOut);
    }

    private void StopCrossfadeAnimations()
    {
        RockerHost.BeginAnimation(OpacityProperty, null);
        FallbackToggle.BeginAnimation(OpacityProperty, null);
    }

    private Dictionary<string, string> BuildPalette()
    {
        return new Dictionary<string, string>
        {
            ["rocker-text"] = ResolveBrushColor("TextMuted", "#888888"),
            ["rocker-border"] = ResolveBrushColor("BorderColor", "#E6E6E6"),
            ["rocker-base"] = ResolveBrushColor("SurfaceMuted", "#979797"),
            ["switch-inactive-bg"] = ResolveBrushColor("ControlBackground", "#DDDDDD"),
            ["switch-inactive-text"] = ResolveBrushColor("TextSecondary", "#888888"),
            ["switch-edge"] = ResolveBrushColor("ControlBorder", "#C8C8C8"),
            ["switch-on-bg"] = ResolveBrushColor("ActionAccentBg", "#2D66C4"),
            ["switch-off-bg"] = ResolveBrushColor("PrimaryRed", "#C94848"),
            ["switch-on-text"] = ResolveBrushColor("ButtonPrimaryFg", "#FFFFFF"),
            ["focus-off-text"] = ResolveBrushColor("TextPrimary", "#333333"),
            ["focus-on-text"] = ResolveBrushColor("ButtonPrimaryFg", "#FFFFFF")
        };
    }

    private static string NormalizeLabel(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var normalized = value.Trim();
        return normalized.Length > 8 ? normalized[..8] : normalized;
    }

    private string ResolveBrushColor(string key, string fallback)
    {
        if (TryFindResource(key) is SolidColorBrush brush)
        {
            return ToCssColor(brush.Color);
        }

        return fallback;
    }

    private static string ToCssColor(MediaColor color)
    {
        var alpha = (color.A / 255d).ToString("0.###", CultureInfo.InvariantCulture);
        return $"rgba({color.R},{color.G},{color.B},{alpha})";
    }

    private void ShowFallback()
    {
        if (_usingFallback)
        {
            return;
        }

        _usingFallback = true;
        _readyTcs.TrySetResult(true);
        ApplyRenderMode();
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T target)
            {
                return target;
            }

        current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private WebView2 EnsureWebViewHost()
    {
        if (_rockerWebView != null)
        {
            return _rockerWebView;
        }

        _rockerWebView = new WebView2
        {
            Focusable = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        RockerWebHostContainer.Children.Clear();
        RockerWebHostContainer.Children.Add(_rockerWebView);
        return _rockerWebView;
    }

    private const string HtmlTemplate = """
<!DOCTYPE html>
<html>
<head>
  <meta charset="utf-8" />
  <style>
    :root {
      --rocker-text: #888;
      --rocker-border: #eee;
      --rocker-base: #999;
      --switch-inactive-bg: #ddd;
      --switch-inactive-text: #888;
      --switch-edge: #ccc;
      --switch-on-bg: #0084d0;
      --switch-off-bg: #bd5757;
      --switch-on-text: #fff;
      --focus-off-text: #333;
      --focus-on-text: #fff;
    }
    html {
      box-sizing: border-box;
      font-family: Arial, sans-serif;
      font-size: 100%;
      width: 100%;
      height: 100%;
      background: transparent;
    }
    *, *:before, *:after {
      box-sizing: inherit;
      margin: 0;
      padding: 0;
    }
    body {
      width: 100%;
      height: 100%;
      overflow: hidden;
      background: transparent;
      user-select: none;
    }
    .mid {
      display: flex;
      align-items: center;
      justify-content: center;
      width: 100%;
      height: 100%;
    }
    .rocker {
      display: inline-block;
      position: relative;
      font-size: 2em;
      font-weight: bold;
      text-align: center;
      text-transform: uppercase;
      color: var(--rocker-text);
      width: 7em;
      height: 4em;
      overflow: hidden;
      border-bottom: 0.5em solid var(--rocker-border);
    }
    .rocker-small {
      font-size: 0.75em;
      margin: 0;
    }
    .rocker::before {
      content: "";
      position: absolute;
      top: 0.5em;
      left: 0;
      right: 0;
      bottom: 0;
      background-color: var(--rocker-base);
      border: 0.5em solid var(--rocker-border);
      border-bottom: 0;
    }
    .rocker input {
      opacity: 0;
      width: 0;
      height: 0;
      position: absolute;
    }
    .switch-left,
    .switch-right {
      cursor: pointer;
      position: absolute;
      display: flex;
      align-items: center;
      justify-content: center;
      height: 2.5em;
      width: 3em;
      font-family: "Segoe UI Symbol", "Segoe UI Emoji", Arial, sans-serif;
      transition: 0.2s;
    }
    .switch-left {
      height: 2.4em;
      width: 2.75em;
      left: 0.85em;
      bottom: 0.4em;
      background-color: var(--switch-inactive-bg);
      color: var(--switch-inactive-text);
      transform: rotate(15deg) skewX(15deg);
    }
    .switch-right {
      right: 0.5em;
      bottom: 0;
      background-color: var(--switch-off-bg);
      color: var(--switch-on-text);
    }
    .switch-left::before,
    .switch-right::before {
      content: "";
      position: absolute;
      width: 0.4em;
      height: 2.45em;
      bottom: -0.45em;
      background-color: var(--switch-edge);
      transform: skewY(-65deg);
    }
    .switch-left::before {
      left: -0.4em;
    }
    .switch-right::before {
      right: -0.375em;
      background-color: transparent;
      transform: skewY(65deg);
    }
    input:checked + .switch-left {
      background-color: var(--switch-on-bg);
      color: var(--switch-on-text);
      bottom: 0;
      left: 0.5em;
      height: 2.5em;
      width: 3em;
      transform: rotate(0deg) skewX(0deg);
    }
    input:checked + .switch-left::before {
      background-color: transparent;
      width: 3.0833em;
    }
    input:checked + .switch-left + .switch-right {
      background-color: var(--switch-inactive-bg);
      color: var(--switch-inactive-text);
      bottom: 0.4em;
      right: 0.8em;
      height: 2.4em;
      width: 2.75em;
      transform: rotate(-15deg) skewX(-15deg);
    }
    input:checked + .switch-left + .switch-right::before {
      background-color: var(--switch-edge);
    }
    input:focus + .switch-left {
      color: var(--focus-off-text);
    }
    input:checked:focus + .switch-left {
      color: var(--focus-on-text);
    }
    input:focus + .switch-left + .switch-right {
      color: var(--focus-on-text);
    }
    input:checked:focus + .switch-left + .switch-right {
      color: var(--focus-off-text);
    }
    input:disabled + .switch-left,
    input:disabled + .switch-left + .switch-right {
      cursor: not-allowed;
      opacity: 0.65;
    }
  </style>
</head>
<body>
  <div class="mid">
    <label class="rocker rocker-small">
      <input id="rocker-input" type="checkbox" checked>
      <span id="rocker-left" class="switch-left">On</span>
      <span id="rocker-right" class="switch-right">Off</span>
    </label>
  </div>

  <script>
    (() => {
      const input = document.getElementById('rocker-input');
      const leftLabel = document.getElementById('rocker-left');
      const rightLabel = document.getElementById('rocker-right');
      const root = document.documentElement;

      const setRockerPalette = (palette) => {
        if (!palette) {
          return;
        }

        Object.keys(palette).forEach((key) => {
          root.style.setProperty(`--${key}`, palette[key]);
        });
      };

      const setRockerState = (checked, enabled) => {
        input.checked = !!checked;
        input.disabled = !enabled;
      };

      const setRockerLabels = (labels) => {
        if (!labels) {
          return;
        }

        if (typeof labels.left === 'string' && labels.left.length > 0) {
          leftLabel.textContent = labels.left;
        }

        if (typeof labels.right === 'string' && labels.right.length > 0) {
          rightLabel.textContent = labels.right;
        }
      };

      window.setRockerPalette = setRockerPalette;
      window.setRockerLabels = setRockerLabels;
      window.setRockerState = setRockerState;
      window.notifyPaintReady = () => {
        requestAnimationFrame(() => {
          requestAnimationFrame(() => {
            if (window.chrome && window.chrome.webview) {
              window.chrome.webview.postMessage(JSON.stringify({ type: 'painted' }));
            }
          });
        });
      };

      input.addEventListener('change', () => {
        if (!window.chrome || !window.chrome.webview) {
          return;
        }

        window.chrome.webview.postMessage(JSON.stringify({
          type: 'toggle',
          value: input.checked
        }));
      });

      if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage(JSON.stringify({ type: 'ready' }));
      }
    })();
  </script>
</body>
</html>
""";
}
