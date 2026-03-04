using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ExtintorCrm.App.Presentation;

internal static class FileIconHelper
{
    private const uint FileAttributeNormal = 0x00000080;
    private const uint ShgfiIcon = 0x000000100;
    private const uint ShgfiSmallIcon = 0x000000001;
    private const uint ShgfiUseFileAttributes = 0x000000010;

    private static readonly ConcurrentDictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static ImageSource? GetSmallIcon(string? fileName)
    {
        var extension = NormalizeExtension(fileName);
        return Cache.GetOrAdd(extension, CreateIconForExtension);
    }

    private static string NormalizeExtension(string? fileName)
    {
        var extension = Path.GetExtension(fileName ?? string.Empty);
        return string.IsNullOrWhiteSpace(extension)
            ? ".file"
            : extension.ToLowerInvariant();
    }

    private static ImageSource? CreateIconForExtension(string extension)
    {
        var fakePath = "dummy" + extension;
        var cbFileInfo = (uint)Marshal.SizeOf<Shfileinfo>();
        var flags = ShgfiIcon | ShgfiSmallIcon | ShgfiUseFileAttributes;

        _ = ShGetFileInfo(fakePath, FileAttributeNormal, out var shinfo, cbFileInfo, flags);
        if (shinfo.hIcon == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var icon = Imaging.CreateBitmapSourceFromHIcon(
                shinfo.hIcon,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            icon.Freeze();
            return icon;
        }
        catch
        {
            return null;
        }
        finally
        {
            _ = DestroyIcon(shinfo.hIcon);
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr ShGetFileInfo(
        string pszPath,
        uint dwFileAttributes,
        out Shfileinfo psfi,
        uint cbFileInfo,
        uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Shfileinfo
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }
}
