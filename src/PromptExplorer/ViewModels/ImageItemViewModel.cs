using System;
using CommunityToolkit.Mvvm.ComponentModel;
using PromptExplorer.Models;
using PromptExplorer.Services;
using System.Windows.Media.Imaging;

namespace PromptExplorer.ViewModels
{
    public class ImageItemViewModel : ObservableObject
    {
        private PromptImageInfo _info;
        private readonly ImageCache _imageCache;
        private readonly Func<int> _thumbnailSizeProvider;
        private BitmapSource? _thumbnail;

        public ImageItemViewModel(PromptImageInfo info, ImageCache imageCache, Func<int> thumbnailSizeProvider)
        {
            _info = info;
            _imageCache = imageCache;
            _thumbnailSizeProvider = thumbnailSizeProvider;
        }

        public string FilePath => _info.FilePath;

        public string FileName => _info.FileName;

        public string Prompt => _info.Prompt;

        public DateTime LastWriteTimeUtc => _info.LastWriteTimeUtc;

        public BitmapSource? Thumbnail
        {
            get
            {
                if (_thumbnail == null)
                {
                    var size = Math.Max(32, _thumbnailSizeProvider());
                    _thumbnail = _imageCache.GetThumbnail(FilePath, size);
                }

                return _thumbnail;
            }
        }

        public void RefreshThumbnail()
        {
            _thumbnail = null;
            OnPropertyChanged(nameof(Thumbnail));
        }

        public void Update(PromptImageInfo newInfo)
        {
            var previousPath = _info.FilePath;
            _info = newInfo;
            _imageCache.Invalidate(previousPath);

            _thumbnail = null;
            OnPropertyChanged(nameof(FilePath));
            OnPropertyChanged(nameof(FileName));
            OnPropertyChanged(nameof(Prompt));
            OnPropertyChanged(nameof(LastWriteTimeUtc));
            OnPropertyChanged(nameof(Thumbnail));
        }
    }
}
