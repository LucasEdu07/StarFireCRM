using System;
using System.Linq;
using System.Windows;

namespace ExtintorCrm.App.Infrastructure.Settings
{
    public static class AppThemeManager
    {
        public const string LightTheme = "Light";
        public const string DarkTheme = "Dark";

        private const string ThemeRootPath = "Presentation/Theme/";
        private const string LightThemePath = ThemeRootPath + "Theme.Light.xaml";
        private const string DarkThemePath = ThemeRootPath + "Theme.Dark.xaml";

        public static string CurrentTheme { get; private set; } = LightTheme;

        public static string NormalizeTheme(string? theme)
        {
            return string.Equals(theme, DarkTheme, StringComparison.OrdinalIgnoreCase)
                ? DarkTheme
                : LightTheme;
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
        }
    }
}
