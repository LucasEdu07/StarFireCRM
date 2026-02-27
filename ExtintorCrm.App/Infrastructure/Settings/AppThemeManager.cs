using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;

namespace ExtintorCrm.App.Infrastructure.Settings
{
    public static class AppThemeManager
    {
        public const string LightTheme = "Light";
        public const string DarkTheme = "Dark";

        private const string ThemeRootPath = "Presentation/Theme/";
        private const string LightThemePath = ThemeRootPath + "Theme.Light.xaml";
        private const string DarkThemePath = ThemeRootPath + "Theme.Dark.xaml";

        private static readonly string[] BorderCustomizationKeys =
        [
            "BorderColor",
            "ControlBorder",
            "BorderSubtle",
            "BorderStrong"
        ];

        private static readonly string[] TitleBarCustomizationKeys =
        [
            "TitleBarBackground",
            "TitleBarBorder",
            "TitleBarButtonBg",
            "TitleBarButtonHoverBg"
        ];

        private static readonly string[] VanillaCustomizationKeys =
        [
            "PrimaryRed",
            "PrimaryRedHover",
            "AccentPrimary",
            "AccentHover",
            "AccentPressed",
            "ButtonPrimaryBg",
            "ButtonPrimaryFg",
            "ActionAccentBg",
            "ActionAccentHover",
            "ActionAccentBorder",
            "AccentBlueBorder",
            "AccentBlueFg",
            "AccentBlueBg",
            "DropDownActiveText",
            "RowSelectedBorder",
            "RockerOnFace",
            "SfRed",
            "SfRedHover",
            "Info"
        ];

        public static string CurrentTheme { get; private set; } = LightTheme;
        public static event EventHandler? ThemeResourcesChanged;

        public static string NormalizeTheme(string? theme)
        {
            return string.Equals(theme, DarkTheme, StringComparison.OrdinalIgnoreCase)
                ? DarkTheme
                : LightTheme;
        }

        public static int NormalizeVanillaIntensityPercent(int value)
        {
            if (value <= 0)
            {
                return 100;
            }

            return Math.Clamp(value, 50, 150);
        }

        public static string NormalizeOptionalHexColor(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var candidate = value.Trim();
            if (!candidate.StartsWith("#", StringComparison.Ordinal))
            {
                candidate = "#" + candidate;
            }

            if (candidate.Length == 4)
            {
                candidate = $"#{candidate[1]}{candidate[1]}{candidate[2]}{candidate[2]}{candidate[3]}{candidate[3]}";
            }

            if (candidate.Length != 7)
            {
                return string.Empty;
            }

            for (var i = 1; i < candidate.Length; i++)
            {
                if (!Uri.IsHexDigit(candidate[i]))
                {
                    return string.Empty;
                }
            }

            return candidate.ToUpperInvariant();
        }

        public static bool TryParseOptionalHexColor(string? value, out Color color)
        {
            var normalized = NormalizeOptionalHexColor(value);
            if (string.IsNullOrEmpty(normalized))
            {
                color = default;
                return false;
            }

            color = Color.FromRgb(
                byte.Parse(normalized.Substring(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                byte.Parse(normalized.Substring(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                byte.Parse(normalized.Substring(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
            return true;
        }

        public static void ApplyTheme(string? theme)
        {
            if (Application.Current == null)
            {
                return;
            }

            var normalized = NormalizeTheme(theme);
            var source = normalized == DarkTheme ? DarkThemePath : LightThemePath;

            var dictionaries = Application.Current.Resources.MergedDictionaries;
            var existing = dictionaries.FirstOrDefault(d =>
                d.Source != null &&
                (d.Source.OriginalString.Contains(LightThemePath, StringComparison.OrdinalIgnoreCase) ||
                 d.Source.OriginalString.Contains(DarkThemePath, StringComparison.OrdinalIgnoreCase)));

            if (existing != null)
            {
                if (string.Equals(existing.Source?.OriginalString, source, StringComparison.OrdinalIgnoreCase))
                {
                    CurrentTheme = normalized;
                    return;
                }

                var index = dictionaries.IndexOf(existing);
                dictionaries.RemoveAt(index);
                dictionaries.Insert(index, new ResourceDictionary { Source = new Uri(source, UriKind.Relative) });
            }
            else
            {
                dictionaries.Insert(0, new ResourceDictionary { Source = new Uri(source, UriKind.Relative) });
            }

            CurrentTheme = normalized;
            ThemeResourcesChanged?.Invoke(null, EventArgs.Empty);
        }

        public static void ApplyChromeCustomization(string? borderHex, string? titleBarHex, string? vanillaHex = null, int vanillaIntensityPercent = 100)
        {
            if (Application.Current == null)
            {
                return;
            }

            var resources = Application.Current.Resources;
            var isDark = string.Equals(CurrentTheme, DarkTheme, StringComparison.OrdinalIgnoreCase);

            if (TryParseOptionalHexColor(borderHex, out var borderColor))
            {
                var controlBorder = isDark ? Lighten(borderColor, 0.08) : Darken(borderColor, 0.04);
                var borderSubtle = isDark ? Darken(borderColor, 0.2) : Lighten(borderColor, 0.18);
                var borderStrong = isDark ? Lighten(borderColor, 0.18) : Darken(borderColor, 0.1);

                SetBrush(resources, "BorderColor", new SolidColorBrush(borderColor));
                SetBrush(resources, "ControlBorder", new SolidColorBrush(controlBorder));
                SetBrush(resources, "BorderSubtle", new SolidColorBrush(borderSubtle));
                SetBrush(resources, "BorderStrong", new SolidColorBrush(borderStrong));
            }
            else
            {
                RemoveOverrides(resources, BorderCustomizationKeys);
            }

            if (TryParseOptionalHexColor(titleBarHex, out var titleBarColor))
            {
                Brush titleBarBackground;
                Brush titleBarButtonBg;
                Brush titleBarButtonHoverBg;
                Color titleBarBorder;

                if (isDark)
                {
                    titleBarBackground = CreateLinearGradient(
                        Lighten(titleBarColor, 0.14),
                        Darken(titleBarColor, 0.18),
                        vertical: false);
                    titleBarButtonBg = CreateLinearGradient(
                        Lighten(titleBarColor, 0.06),
                        Darken(titleBarColor, 0.1),
                        vertical: true);
                    titleBarButtonHoverBg = CreateLinearGradient(
                        Lighten(titleBarColor, 0.2),
                        Darken(titleBarColor, 0.02),
                        vertical: true);
                    titleBarBorder = Lighten(titleBarColor, 0.08);
                }
                else
                {
                    titleBarBackground = CreateLinearGradient(
                        Lighten(titleBarColor, 0.45),
                        Lighten(titleBarColor, 0.28),
                        vertical: false);
                    titleBarButtonBg = CreateLinearGradient(
                        Lighten(titleBarColor, 0.58),
                        Lighten(titleBarColor, 0.44),
                        vertical: true);
                    titleBarButtonHoverBg = CreateLinearGradient(
                        Lighten(titleBarColor, 0.64),
                        Lighten(titleBarColor, 0.5),
                        vertical: true);
                    titleBarBorder = Darken(titleBarColor, 0.14);
                }

                SetBrush(resources, "TitleBarBackground", titleBarBackground);
                SetBrush(resources, "TitleBarButtonBg", titleBarButtonBg);
                SetBrush(resources, "TitleBarButtonHoverBg", titleBarButtonHoverBg);
                SetBrush(resources, "TitleBarBorder", new SolidColorBrush(titleBarBorder));
            }
            else
            {
                RemoveOverrides(resources, TitleBarCustomizationKeys);
            }

            if (TryParseOptionalHexColor(vanillaHex, out var vanillaColor))
            {
                ApplyVanillaCustomization(resources, vanillaColor, isDark, vanillaIntensityPercent);
            }
            else
            {
                RemoveOverrides(resources, VanillaCustomizationKeys);
            }

            ThemeResourcesChanged?.Invoke(null, EventArgs.Empty);
        }

        private static void ApplyVanillaCustomization(ResourceDictionary resources, Color sourceColor, bool isDark, int intensityPercent)
        {
            var baseColor = TuneVanillaBase(sourceColor, isDark);
            var intensity = NormalizeVanillaIntensityPercent(intensityPercent) / 100d;

            double Scale(double amount) => Math.Clamp(amount * intensity, 0, 0.95);

            var primary = isDark ? Lighten(baseColor, Scale(0.14)) : Darken(baseColor, Scale(0.02));
            var primaryHover = isDark ? Darken(primary, Scale(0.12)) : Darken(primary, Scale(0.18));

            var accentPrimary = isDark ? Lighten(baseColor, Scale(0.18)) : Darken(baseColor, Scale(0.01));
            var accentHover = isDark ? Darken(accentPrimary, Scale(0.12)) : Darken(accentPrimary, Scale(0.15));
            var accentPressed = isDark ? Darken(accentPrimary, Scale(0.24)) : Darken(accentPrimary, Scale(0.28));

            var buttonPrimaryBg = isDark ? Lighten(baseColor, Scale(0.1)) : Darken(baseColor, Scale(0.04));
            var buttonPrimaryFg = ReadableTextOn(buttonPrimaryBg);

            var actionAccentBg = isDark ? Lighten(baseColor, Scale(0.12)) : Darken(baseColor, Scale(0.06));
            var actionAccentHover = isDark ? Darken(actionAccentBg, Scale(0.16)) : Darken(actionAccentBg, Scale(0.16));
            var actionAccentBorder = isDark ? Lighten(actionAccentBg, Scale(0.14)) : Lighten(actionAccentBg, Scale(0.08));

            var accentBlueBorder = isDark ? Lighten(baseColor, Scale(0.26)) : Darken(baseColor, Scale(0.14));
            var accentBlueBg = isDark ? Darken(baseColor, Scale(0.62)) : Lighten(baseColor, Scale(0.82));
            var accentBlueFg = ReadableTextOn(accentBlueBg);

            SetBrush(resources, "PrimaryRed", new SolidColorBrush(primary));
            SetBrush(resources, "PrimaryRedHover", new SolidColorBrush(primaryHover));
            SetBrush(resources, "AccentPrimary", new SolidColorBrush(accentPrimary));
            SetBrush(resources, "AccentHover", new SolidColorBrush(accentHover));
            SetBrush(resources, "AccentPressed", new SolidColorBrush(accentPressed));
            SetBrush(resources, "ButtonPrimaryBg", new SolidColorBrush(buttonPrimaryBg));
            SetBrush(resources, "ButtonPrimaryFg", new SolidColorBrush(buttonPrimaryFg));
            SetBrush(resources, "ActionAccentBg", new SolidColorBrush(actionAccentBg));
            SetBrush(resources, "ActionAccentHover", new SolidColorBrush(actionAccentHover));
            SetBrush(resources, "ActionAccentBorder", new SolidColorBrush(actionAccentBorder));
            SetBrush(resources, "AccentBlueBorder", new SolidColorBrush(accentBlueBorder));
            SetBrush(resources, "AccentBlueBg", new SolidColorBrush(accentBlueBg));
            SetBrush(resources, "AccentBlueFg", new SolidColorBrush(accentBlueFg));
            SetBrush(resources, "DropDownActiveText", new SolidColorBrush(accentBlueBorder));
            SetBrush(resources, "RowSelectedBorder", new SolidColorBrush(accentBlueBorder));
            SetBrush(resources, "RockerOnFace", new SolidColorBrush(actionAccentBg));
            SetBrush(resources, "SfRed", new SolidColorBrush(buttonPrimaryBg));
            SetBrush(resources, "SfRedHover", new SolidColorBrush(primaryHover));
            SetBrush(resources, "Info", new SolidColorBrush(actionAccentBg));
        }

        private static Color TuneVanillaBase(Color color, bool isDark)
        {
            var luminance = RelativeLuminance(color);
            if (isDark)
            {
                if (luminance < 0.2)
                {
                    return Lighten(color, 0.24);
                }

                if (luminance > 0.82)
                {
                    return Darken(color, 0.28);
                }

                return color;
            }

            if (luminance < 0.12)
            {
                return Lighten(color, 0.26);
            }

            if (luminance > 0.84)
            {
                return Darken(color, 0.34);
            }

            return color;
        }

        private static Color ReadableTextOn(Color background)
        {
            return RelativeLuminance(background) >= 0.58
                ? Color.FromRgb(22, 34, 52)
                : Colors.White;
        }

        private static double RelativeLuminance(Color color)
        {
            static double Linear(double channel)
            {
                var c = channel / 255d;
                return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
            }

            var r = Linear(color.R);
            var g = Linear(color.G);
            var b = Linear(color.B);
            return (0.2126 * r) + (0.7152 * g) + (0.0722 * b);
        }

        private static void RemoveOverrides(ResourceDictionary resources, string[] keys)
        {
            foreach (var key in keys)
            {
                if (resources.Contains(key))
                {
                    resources.Remove(key);
                }
            }
        }

        private static void SetBrush(ResourceDictionary resources, string key, Brush brush)
        {
            if (brush.CanFreeze)
            {
                brush.Freeze();
            }

            resources[key] = brush;
        }

        private static LinearGradientBrush CreateLinearGradient(Color start, Color end, bool vertical)
        {
            var brush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = vertical ? new Point(0, 1) : new Point(1, 1)
            };
            brush.GradientStops.Add(new GradientStop(start, 0));
            brush.GradientStops.Add(new GradientStop(end, 1));
            return brush;
        }

        private static Color Lighten(Color color, double amount)
        {
            return Blend(color, Colors.White, amount);
        }

        private static Color Darken(Color color, double amount)
        {
            return Blend(color, Colors.Black, amount);
        }

        private static Color Blend(Color source, Color target, double amount)
        {
            var t = Math.Clamp(amount, 0, 1);
            return Color.FromArgb(
                (byte)Math.Round(source.A + ((target.A - source.A) * t)),
                (byte)Math.Round(source.R + ((target.R - source.R) * t)),
                (byte)Math.Round(source.G + ((target.G - source.G) * t)),
                (byte)Math.Round(source.B + ((target.B - source.B) * t)));
        }
    }
}
