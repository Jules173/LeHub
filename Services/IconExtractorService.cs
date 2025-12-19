using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LeHub.Services;

public class IconExtractorService
{
    private static IconExtractorService? _instance;
    public static IconExtractorService Instance => _instance ??= new IconExtractorService();

    private readonly string _cacheFolder;
    private readonly BitmapImage _defaultIcon;

    private IconExtractorService()
    {
        _cacheFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LeHub", "cache", "icons");

        Directory.CreateDirectory(_cacheFolder);

        _defaultIcon = CreateDefaultIcon();
    }

    private static BitmapImage CreateDefaultIcon()
    {
        // Create a simple default icon (gray app icon)
        var bitmap = new System.Drawing.Bitmap(48, 48);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(System.Drawing.Color.FromArgb(80, 80, 100));
            using var brush = new SolidBrush(System.Drawing.Color.FromArgb(150, 150, 170));
            g.FillRectangle(brush, 8, 8, 32, 32);
        }

        return BitmapToImageSource(bitmap);
    }

    public ImageSource GetIcon(string exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath))
            return _defaultIcon;

        try
        {
            var cacheFile = GetCacheFilePath(exePath);

            // Check cache first
            if (File.Exists(cacheFile))
            {
                return LoadFromCache(cacheFile);
            }

            // Extract icon from exe
            if (File.Exists(exePath))
            {
                var icon = ExtractIconFromFile(exePath);
                if (icon != null)
                {
                    SaveToCache(icon, cacheFile);
                    return BitmapToImageSource(icon);
                }
            }
        }
        catch
        {
            // Ignore errors, return default
        }

        return _defaultIcon;
    }

    private string GetCacheFilePath(string exePath)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(exePath.ToLowerInvariant()));
        var hashString = Convert.ToHexString(hash)[..16];
        return Path.Combine(_cacheFolder, $"{hashString}.png");
    }

    private static Bitmap? ExtractIconFromFile(string filePath)
    {
        try
        {
            using var icon = Icon.ExtractAssociatedIcon(filePath);
            if (icon != null)
            {
                return icon.ToBitmap();
            }
        }
        catch
        {
            // Ignore
        }

        return null;
    }

    private static void SaveToCache(Bitmap bitmap, string cachePath)
    {
        try
        {
            bitmap.Save(cachePath, ImageFormat.Png);
        }
        catch
        {
            // Ignore cache write errors
        }
    }

    private static BitmapImage LoadFromCache(string cachePath)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(cachePath, UriKind.Absolute);
        image.EndInit();
        image.Freeze();
        return image;
    }

    private static BitmapImage BitmapToImageSource(Bitmap bitmap)
    {
        using var memory = new MemoryStream();
        bitmap.Save(memory, ImageFormat.Png);
        memory.Position = 0;

        var bitmapImage = new BitmapImage();
        bitmapImage.BeginInit();
        bitmapImage.StreamSource = memory;
        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
        bitmapImage.EndInit();
        bitmapImage.Freeze();

        return bitmapImage;
    }

    public void ClearCache()
    {
        try
        {
            foreach (var file in Directory.GetFiles(_cacheFolder, "*.png"))
            {
                File.Delete(file);
            }
        }
        catch
        {
            // Ignore
        }
    }

    public void InvalidateCache(string exePath)
    {
        try
        {
            var cacheFile = GetCacheFilePath(exePath);
            if (File.Exists(cacheFile))
            {
                File.Delete(cacheFile);
            }
        }
        catch
        {
            // Ignore
        }
    }
}
