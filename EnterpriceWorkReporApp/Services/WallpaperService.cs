using System;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;

namespace EnterpriseWorkReport.Services
{
    public static class WallpaperService
    {
        public static string WallpaperFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wallpaper");

        private static string _currentWallpaper;
        public static string CurrentWallpaper => _currentWallpaper;

        public static event EventHandler<string> WallpaperChanged;

        static WallpaperService()
        {
            EnsureWallpaperFolder();
        }

        public static void EnsureWallpaperFolder()
        {
            if (!Directory.Exists(WallpaperFolder))
                Directory.CreateDirectory(WallpaperFolder);
        }

        public static string GetNextWallpaper()
        {
            EnsureWallpaperFolder();
            
            var files = Directory.GetFiles(WallpaperFolder, "*.*");
            if (files.Length == 0) return null;

            var validExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };
            var images = Array.FindAll(files, f => 
                validExtensions.Contains(Path.GetExtension(f).ToLower()));

            if (images.Length == 0) return null;

            var random = new Random();
            int currentIndex = -1;

            if (!string.IsNullOrEmpty(_currentWallpaper))
            {
                currentIndex = Array.IndexOf(images, _currentWallpaper);
            }

            int nextIndex = (currentIndex + 1) % images.Length;
            _currentWallpaper = images[nextIndex];
            
            WallpaperChanged?.Invoke(null, _currentWallpaper);
            return _currentWallpaper;
        }

        public static string GetRandomWallpaper()
        {
            EnsureWallpaperFolder();
            
            var files = Directory.GetFiles(WallpaperFolder, "*.*");
            if (files.Length == 0) return null;

            var validExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };
            var images = Array.FindAll(files, f => 
                validExtensions.Contains(Path.GetExtension(f).ToLower()));

            if (images.Length == 0) return null;

            var random = new Random();
            _currentWallpaper = images[random.Next(images.Length)];
            
            WallpaperChanged?.Invoke(null, _currentWallpaper);
            return _currentWallpaper;
        }

        public static string GetFirstWallpaper()
        {
            EnsureWallpaperFolder();
            
            var files = Directory.GetFiles(WallpaperFolder, "*.*");
            if (files.Length == 0) return null;

            var validExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };
            var images = Array.FindAll(files, f => 
                validExtensions.Contains(Path.GetExtension(f).ToLower()));

            if (images.Length == 0) return null;

            _currentWallpaper = images[0];
            WallpaperChanged?.Invoke(null, _currentWallpaper);
            return _currentWallpaper;
        }

        public static BitmapImage LoadWallpaperImage(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return null;

            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(filePath, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        public static int GetWallpaperCount()
        {
            EnsureWallpaperFolder();
            var files = Directory.GetFiles(WallpaperFolder, "*.*");
            var validExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };
            return Array.FindAll(files, f => 
                validExtensions.Contains(Path.GetExtension(f).ToLower())).Length;
        }

        public static string[] GetAvailableWallpapers()
        {
            EnsureWallpaperFolder();
            var files = Directory.GetFiles(WallpaperFolder, "*.*");
            var validExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };
            return Array.FindAll(files, f => 
                validExtensions.Contains(Path.GetExtension(f).ToLower()));
        }
    }
}