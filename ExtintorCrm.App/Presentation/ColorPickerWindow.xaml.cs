using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ExtintorCrm.App.Infrastructure.Settings;

namespace ExtintorCrm.App.Presentation;

public partial class ColorPickerWindow : Window
{
    private const double MaxHue = 360d;
    private bool _isSvDragging;
    private bool _isHueDragging;
    private double _hue;
    private double _saturation;
    private double _value;

    public ColorPickerWindow(string title, string? initialHex, Window? owner)
    {
        InitializeComponent();
        Owner = owner;
        WindowTitleText.Text = string.IsNullOrWhiteSpace(title) ? "Selecionar cor" : title.Trim();
        Initialize(initialHex);
    }

    public string SelectedHexColor { get; private set; } = string.Empty;

    private void Initialize(string? initialHex)
    {
        var defaultColor = Color.FromRgb(45, 102, 196);
        if (Application.Current?.TryFindResource("AccentBlueBorder") is SolidColorBrush accentBrush)
        {
            defaultColor = accentBrush.Color;
        }

        var initialColor = AppThemeManager.TryParseOptionalHexColor(initialHex, out var parsedColor)
            ? parsedColor
            : defaultColor;

        SetColor(initialColor);
    }

    private void SaturationValueArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isSvDragging = true;
        SaturationValueArea.CaptureMouse();
        UpdateSaturationValueFromPoint(e.GetPosition(SaturationValueArea));
    }

    private void SaturationValueArea_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isSvDragging)
        {
            return;
        }

        UpdateSaturationValueFromPoint(e.GetPosition(SaturationValueArea));
    }

    private void SaturationValueArea_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isSvDragging)
        {
            return;
        }

        _isSvDragging = false;
        SaturationValueArea.ReleaseMouseCapture();
        UpdateSaturationValueFromPoint(e.GetPosition(SaturationValueArea));
    }

    private void HueArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isHueDragging = true;
        HueArea.CaptureMouse();
        UpdateHueFromPoint(e.GetPosition(HueArea));
    }

    private void HueArea_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isHueDragging)
        {
            return;
        }

        UpdateHueFromPoint(e.GetPosition(HueArea));
    }

    private void HueArea_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isHueDragging)
        {
            return;
        }

        _isHueDragging = false;
        HueArea.ReleaseMouseCapture();
        UpdateHueFromPoint(e.GetPosition(HueArea));
    }

    private void SaturationValueArea_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateSelectorPositions();
    }

    private void HueArea_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateSelectorPositions();
    }

    private void UpdateSaturationValueFromPoint(Point point)
    {
        var width = Math.Max(1, SaturationValueArea.ActualWidth);
        var height = Math.Max(1, SaturationValueArea.ActualHeight);

        var x = Math.Clamp(point.X, 0, width);
        var y = Math.Clamp(point.Y, 0, height);

        _saturation = x / width;
        _value = 1 - (y / height);
        UpdateFromHsv();
    }

    private void UpdateHueFromPoint(Point point)
    {
        var height = Math.Max(1, HueArea.ActualHeight);
        var y = Math.Clamp(point.Y, 0, height);
        _hue = Math.Clamp((y / height) * MaxHue, 0, MaxHue);
        UpdateFromHsv();
    }

    private void SetColor(Color color)
    {
        ToHsv(color, out _hue, out _saturation, out _value);
        UpdateFromHsv();
    }

    private void UpdateFromHsv()
    {
        var color = FromHsv(_hue, _saturation, _value);

        var hueBaseColor = FromHsv(_hue, 1, 1);
        SaturationValueHueLayer.Fill = new SolidColorBrush(hueBaseColor);
        SelectedHexColor = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        PreviewColorBorder.Background = new SolidColorBrush(color);
        HexValueText.Text = SelectedHexColor;
        UpdateSelectorPositions();
    }

    private void UpdateSelectorPositions()
    {
        var svWidth = SaturationValueArea.ActualWidth;
        var svHeight = SaturationValueArea.ActualHeight;
        if (svWidth > 0 && svHeight > 0)
        {
            var selectorX = (_saturation * svWidth) - (SaturationValueSelector.Width / 2);
            var selectorY = ((1 - _value) * svHeight) - (SaturationValueSelector.Height / 2);
            Canvas.SetLeft(SaturationValueSelector, Math.Clamp(selectorX, -SaturationValueSelector.Width / 2, svWidth - (SaturationValueSelector.Width / 2)));
            Canvas.SetTop(SaturationValueSelector, Math.Clamp(selectorY, -SaturationValueSelector.Height / 2, svHeight - (SaturationValueSelector.Height / 2)));
        }

        var hueHeight = HueArea.ActualHeight;
        if (hueHeight <= 0)
        {
            return;
        }

        var hueY = ((_hue / MaxHue) * hueHeight) - (HueSelector.Height / 2);
        Canvas.SetTop(HueSelector, Math.Clamp(hueY, -HueSelector.Height / 2, hueHeight - (HueSelector.Height / 2)));
        Canvas.SetLeft(HueSelector, 0);
    }

    private static Color FromHsv(double hue, double saturation, double value)
    {
        var h = ((hue % MaxHue) + MaxHue) % MaxHue;
        var s = Math.Clamp(saturation, 0, 1);
        var v = Math.Clamp(value, 0, 1);

        if (s <= 0)
        {
            var gray = (byte)Math.Round(v * 255);
            return Color.FromRgb(gray, gray, gray);
        }

        var sector = h / 60d;
        var i = (int)Math.Floor(sector);
        var f = sector - i;

        var p = v * (1d - s);
        var q = v * (1d - (s * f));
        var t = v * (1d - (s * (1d - f)));

        (double r, double g, double b) = i switch
        {
            0 => (v, t, p),
            1 => (q, v, p),
            2 => (p, v, t),
            3 => (p, q, v),
            4 => (t, p, v),
            _ => (v, p, q)
        };

        return Color.FromRgb(
            (byte)Math.Round(r * 255),
            (byte)Math.Round(g * 255),
            (byte)Math.Round(b * 255));
    }

    private static void ToHsv(Color color, out double hue, out double saturation, out double value)
    {
        var r = color.R / 255d;
        var g = color.G / 255d;
        var b = color.B / 255d;

        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;

        value = max;
        saturation = max <= 0 ? 0 : delta / max;

        if (delta <= 0)
        {
            hue = 0;
            return;
        }

        hue = max switch
        {
            var m when Math.Abs(m - r) < double.Epsilon => 60 * (((g - b) / delta) % 6),
            var m when Math.Abs(m - g) < double.Epsilon => 60 * (((b - r) / delta) + 2),
            _ => 60 * (((r - g) / delta) + 4)
        };

        if (hue < 0)
        {
            hue += MaxHue;
        }
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }
}
