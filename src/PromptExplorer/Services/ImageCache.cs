using System.Collections.Concurrent;
using System.IO;
using System.Windows.Media.Imaging;

namespace PromptExplorer.Services
{
    public class ImageCache
    {
        private readonly ConcurrentDictionary<string, BitmapSource> _cache = new();

        public BitmapSource? GetThumbnail(string filePath, int targetSize)
        {
            var key = BuildKey(filePath, targetSize);
            if (_cache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var bitmap = LoadThumbnail(filePath, targetSize);
            if (bitmap != null)
            {
                _cache[key] = bitmap;
            }

            return bitmap;
        }

        public void Invalidate(string filePath)
        {
            var prefix = filePath + "|";
            foreach (var key in _cache.Keys)
            {
                if (key.StartsWith(prefix))
                {
                    _cache.TryRemove(key, out _);
                }
            }
        }

        public void Clear()
        {
            _cache.Clear();
        }

        private static string BuildKey(string filePath, int targetSize) => $"{filePath}|{targetSize}";

        private static BitmapSource? LoadThumbnail(string filePath, int targetSize)
        {
            try
            {
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var memory = new MemoryStream();
                stream.CopyTo(memory);
                memory.Position = 0;

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                if (targetSize > 0)
                {
                    bitmap.DecodePixelWidth = targetSize;
                    bitmap.DecodePixelHeight = targetSize;
                }
                bitmap.StreamSource = memory;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                return null;
            }
        }
    }
}
