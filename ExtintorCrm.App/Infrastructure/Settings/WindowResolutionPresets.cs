using System;
using System.Collections.Generic;

namespace ExtintorCrm.App.Infrastructure.Settings
{
    public static class WindowResolutionPresets
    {
        public const string Auto = "Auto";
        public const string Resolution1280x720 = "1280x720";
        public const string Resolution1366x768 = "1366x768";
        public const string Resolution1600x900 = "1600x900";
        public const string Resolution1920x1080 = "1920x1080";

        public static IReadOnlyList<string> All { get; } =
        [
            Auto,
            Resolution1280x720,
            Resolution1366x768,
            Resolution1600x900,
            Resolution1920x1080
        ];

        public static string Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return Auto;
            }

            var candidate = value.Trim();
            if (string.Equals(candidate, Resolution1280x720, StringComparison.OrdinalIgnoreCase))
            {
                return Resolution1280x720;
            }

            if (string.Equals(candidate, Resolution1366x768, StringComparison.OrdinalIgnoreCase))
            {
                return Resolution1366x768;
            }

            if (string.Equals(candidate, Resolution1600x900, StringComparison.OrdinalIgnoreCase))
            {
                return Resolution1600x900;
            }

            if (string.Equals(candidate, Resolution1920x1080, StringComparison.OrdinalIgnoreCase))
            {
                return Resolution1920x1080;
            }

            return Auto;
        }

        public static bool TryGetSize(string? value, out double width, out double height)
        {
            switch (Normalize(value))
            {
                case Resolution1280x720:
                    width = 1280;
                    height = 720;
                    return true;
                case Resolution1366x768:
                    width = 1366;
                    height = 768;
                    return true;
                case Resolution1600x900:
                    width = 1600;
                    height = 900;
                    return true;
                case Resolution1920x1080:
                    width = 1920;
                    height = 1080;
                    return true;
                default:
                    width = 0;
                    height = 0;
                    return false;
            }
        }
    }
}
