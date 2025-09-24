using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PromptExplorer.Models;
using PromptExplorer.Services;
using PromptExplorer.Settings;

namespace PromptExplorer.ViewModels
{
    public class MainViewModel : ObservableObject, IDisposable
    {
        private readonly SettingsService _settingsService;
        private readonly PromptExtractor _promptExtractor;
        private readonly ImageCache _imageCache;
        private readonly List<ImageItemViewModel> _allImages = new();
        private readonly ObservableCollection<string> _availableFolders = new();
        private readonly ReadOnlyObservableCollection<string> _readOnlyFolders;
        private readonly SemaphoreSlim _settingsLock = new(1, 1);
        private readonly System.Windows.Threading.Dispatcher _dispatcher;
        private AppSettings _settings = new();
        private string _searchText = string.Empty;
        private SearchMode _searchMode = SearchMode.ExactSequence;
        private string? _selectedFolder;
        private ImageItemViewModel? _selectedImage;
        private double _thumbnailSize = 192;
        private CancellationTokenSource? _loadCancellation;
        private FileSystemWatcher? _watcher;

        public MainViewModel(SettingsService settingsService, PromptExtractor promptExtractor, ImageCache imageCache)
        {
            _settingsService = settingsService;
            _promptExtractor = promptExtractor;
            _imageCache = imageCache;
            _dispatcher = Application.Current.Dispatcher;

            FilteredImages = new ObservableCollection<ImageItemViewModel>();
            FilteredImages.CollectionChanged += OnFilteredImagesChanged;

            _readOnlyFolders = new ReadOnlyObservableCollection<string>(_availableFolders);

            SearchCommand = new RelayCommand(ApplySearch);
            ResetCommand = new RelayCommand(ResetSearch);
            ToggleSearchModeCommand = new RelayCommand(ToggleSearchMode);
            BrowseFolderCommand = new RelayCommand(BrowseFolder);
            AddPresetCommand = new RelayCommand(AddPreset, () => Directory.Exists(SelectedFolder ?? string.Empty));
            SetDefaultFolderCommand = new RelayCommand(SetDefaultFolder, () => Directory.Exists(SelectedFolder ?? string.Empty));
            ExportPromptCommand = new RelayCommand(ExportPrompt, () => SelectedImage != null);
        }

        public ObservableCollection<ImageItemViewModel> FilteredImages { get; }

        public ReadOnlyObservableCollection<string> AvailableFolders => _readOnlyFolders;

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    // Optional: Live search can be enabled here if desired.
                }
            }
        }

        public SearchMode SearchMode
        {
            get => _searchMode;
            private set => SetProperty(ref _searchMode, value);
        }

        public string? SelectedFolder
        {
            get => _selectedFolder;
            set
            {
                if (SetProperty(ref _selectedFolder, value))
                {
                    AddPresetCommand.NotifyCanExecuteChanged();
                    SetDefaultFolderCommand.NotifyCanExecuteChanged();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        _settings.LastUsedFolder = value;
                        _ = SaveSettingsAsync();
                        _ = LoadFolderAsync(value);
                    }
                }
            }
        }

        public string DefaultFolder
        {
            get => _settings.DefaultFolder;
            private set
            {
                if (_settings.DefaultFolder != value)
                {
                    _settings.DefaultFolder = value;
                    OnPropertyChanged();
                }
            }
        }

        public ImageItemViewModel? SelectedImage
        {
            get => _selectedImage;
            set
            {
                if (SetProperty(ref _selectedImage, value))
                {
                    ExportPromptCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public double ThumbnailSize
        {
            get => _thumbnailSize;
            set
            {
                var clamped = Math.Max(64, Math.Min(512, value));
                if (SetProperty(ref _thumbnailSize, clamped))
                {
                    foreach (var item in _allImages)
                    {
                        item.RefreshThumbnail();
                    }
                }
            }
        }

        public int HitCount => FilteredImages.Count;

        public IRelayCommand SearchCommand { get; }

        public IRelayCommand ResetCommand { get; }

        public IRelayCommand ToggleSearchModeCommand { get; }

        public IRelayCommand BrowseFolderCommand { get; }

        public IRelayCommand AddPresetCommand { get; }

        public IRelayCommand SetDefaultFolderCommand { get; }

        public IRelayCommand ExportPromptCommand { get; }

        public async Task InitializeAsync()
        {
            _settings = await _settingsService.LoadAsync();
            DefaultFolder = _settings.DefaultFolder;

            _availableFolders.Clear();
            foreach (var folder in _settings.PresetFolders.Where(Directory.Exists))
            {
                _availableFolders.Add(folder);
            }

            if (!_availableFolders.Any())
            {
                _availableFolders.Add(_settings.DefaultFolder);
            }

            SelectedFolder = Directory.Exists(_settings.LastUsedFolder ?? string.Empty)
                ? _settings.LastUsedFolder
                : _settings.DefaultFolder;
        }

        public void AdjustThumbnailSize(double delta)
        {
            ThumbnailSize = ThumbnailSize + delta;
        }

        private void ToggleSearchMode()
        {
            SearchMode = SearchMode == SearchMode.ExactSequence ? SearchMode.AndTags : SearchMode.ExactSequence;
            ApplySearch();
        }

        private void ResetSearch()
        {
            SearchText = string.Empty;
            ApplySearch();
        }

        private void ApplySearch()
        {
            IEnumerable<ImageItemViewModel> query = _allImages;
            var text = SearchText?.Trim();
            if (!string.IsNullOrWhiteSpace(text))
            {
                if (SearchMode == SearchMode.ExactSequence)
                {
                    query = query.Where(img => !string.IsNullOrEmpty(img.Prompt) &&
                                               img.Prompt.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0);
                }
                else
                {
                    var tokens = text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                     .Select(t => t.Trim())
                                     .Where(t => !string.IsNullOrWhiteSpace(t))
                                     .ToList();
                    if (tokens.Count > 0)
                    {
                        query = query.Where(img => tokens.All(token => !string.IsNullOrEmpty(img.Prompt) &&
                                                                    img.Prompt.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0));
                    }
                }
            }

            var results = query.ToList();

            FilteredImages.Clear();
            foreach (var item in results)
            {
                FilteredImages.Add(item);
            }
        }

        private async Task LoadFolderAsync(string folder)
        {
            _loadCancellation?.Cancel();
            _loadCancellation = new CancellationTokenSource();
            var token = _loadCancellation.Token;

            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                _dispatcher.Invoke(() =>
                {
                    _imageCache.Clear();
                    _allImages.Clear();
                    FilteredImages.Clear();
                    _watcher?.Dispose();
                    _watcher = null;
                });
                return;
            }

            try
            {
                var infos = await Task.Run(() => LoadPromptInfos(folder, token), token);

                await _dispatcher.InvokeAsync(() =>
                {
                    _imageCache.Clear();
                    _allImages.Clear();
                    foreach (var info in infos)
                    {
                        _allImages.Add(new ImageItemViewModel(info, _imageCache, () => (int)Math.Round(ThumbnailSize)));
                    }

                    SortImages();
                    ApplySearch();
                    SetupWatcher(folder);
                }, System.Windows.Threading.DispatcherPriority.Background, token);
            }
            catch (OperationCanceledException)
            {
                // Swallow cancellation
            }
            catch (Exception)
            {
                // Ignore unexpected errors during load to keep UI responsive.
            }
        }

        private List<PromptImageInfo> LoadPromptInfos(string folder, CancellationToken token)
        {
            var result = new List<PromptImageInfo>();
            var files = Directory.EnumerateFiles(folder, "*.png", SearchOption.TopDirectoryOnly);

            Parallel.ForEach(files, new ParallelOptions
            {
                CancellationToken = token,
                MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1)
            }, file =>
            {
                var info = _promptExtractor.LoadPromptInfo(file);
                if (info != null)
                {
                    lock (result)
                    {
                        result.Add(info);
                    }
                }
            });

            result.Sort((a, b) => string.Compare(a.FileName, b.FileName, StringComparison.OrdinalIgnoreCase));
            return result;
        }

        private void SortImages()
        {
            _allImages.Sort((a, b) => string.Compare(a.FileName, b.FileName, StringComparison.OrdinalIgnoreCase));
        }

        private void BrowseFolder()
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "表示したいPNGフォルダを選択してください",
                ShowNewFolderButton = false
            };

            if (!string.IsNullOrWhiteSpace(SelectedFolder) && Directory.Exists(SelectedFolder))
            {
                dialog.SelectedPath = SelectedFolder;
            }
            else if (Directory.Exists(DefaultFolder))
            {
                dialog.SelectedPath = DefaultFolder;
            }

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                SelectedFolder = dialog.SelectedPath;
                if (!ContainsFolder(dialog.SelectedPath))
                {
                    _availableFolders.Add(dialog.SelectedPath);
                    _ = SaveSettingsAsync();
                }
            }
        }

        private void AddPreset()
        {
            if (string.IsNullOrWhiteSpace(SelectedFolder) || !Directory.Exists(SelectedFolder))
            {
                return;
            }

            if (!ContainsFolder(SelectedFolder))
            {
                _availableFolders.Add(SelectedFolder);
            }

            if (!_settings.PresetFolders.Contains(SelectedFolder, StringComparer.OrdinalIgnoreCase))
            {
                _settings.PresetFolders.Add(SelectedFolder);
                _ = SaveSettingsAsync();
            }
        }

        private void SetDefaultFolder()
        {
            if (string.IsNullOrWhiteSpace(SelectedFolder) || !Directory.Exists(SelectedFolder))
            {
                return;
            }

            DefaultFolder = SelectedFolder;
            if (!ContainsFolder(SelectedFolder))
            {
                _availableFolders.Add(SelectedFolder);
            }

            if (!_settings.PresetFolders.Contains(SelectedFolder, StringComparer.OrdinalIgnoreCase))
            {
                _settings.PresetFolders.Add(SelectedFolder);
            }

            _ = SaveSettingsAsync();
        }

        private void ExportPrompt()
        {
            if (SelectedImage == null)
            {
                return;
            }

            try
            {
                var targetPath = Path.ChangeExtension(SelectedImage.FilePath, ".txt");
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                File.WriteAllText(targetPath, SelectedImage.Prompt ?? string.Empty, System.Text.Encoding.UTF8);
            }
            catch
            {
                // Intentionally ignore IO exceptions. In a real application logging would be added.
            }
        }

        private void SetupWatcher(string folder)
        {
            _watcher?.Dispose();
            _watcher = new FileSystemWatcher(folder, "*.png")
            {
                IncludeSubdirectories = false,
                EnableRaisingEvents = true
            };

            _watcher.Created += (_, e) => HandleFileChangeAsync(e.FullPath);
            _watcher.Changed += (_, e) => HandleFileChangeAsync(e.FullPath);
            _watcher.Renamed += (_, e) => HandleFileRenameAsync(e.OldFullPath, e.FullPath);
            _watcher.Deleted += (_, e) => HandleFileDeleted(e.FullPath);
        }

        private void HandleFileChangeAsync(string path)
        {
            if (!IsPng(path))
            {
                return;
            }

            _ = Task.Run(async () =>
            {
                await Task.Delay(200);
                var info = _promptExtractor.LoadPromptInfo(path);
                if (info == null)
                {
                    return;
                }

                await _dispatcher.InvokeAsync(() =>
                {
                    var existing = _allImages.FirstOrDefault(i => string.Equals(i.FilePath, path, StringComparison.OrdinalIgnoreCase));
                    if (existing != null)
                    {
                        existing.Update(info);
                    }
                    else
                    {
                        var vm = new ImageItemViewModel(info, _imageCache, () => (int)Math.Round(ThumbnailSize));
                        _allImages.Add(vm);
                        SortImages();
                    }

                    ApplySearch();
                });
            });
        }

        private void HandleFileRenameAsync(string oldPath, string newPath)
        {
            HandleFileDeleted(oldPath);
            HandleFileChangeAsync(newPath);
        }

        private void HandleFileDeleted(string path)
        {
            if (!IsPng(path))
            {
                return;
            }

            _dispatcher.Invoke(() =>
            {
                var existing = _allImages.FirstOrDefault(i => string.Equals(i.FilePath, path, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    _allImages.Remove(existing);
                    FilteredImages.Remove(existing);
                    _imageCache.Invalidate(path);
                    ApplySearch();
                }
            });
        }

        private static bool IsPng(string path) => string.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase);

        private void OnFilteredImagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(HitCount));
        }

        private bool ContainsFolder(string path)
        {
            return _availableFolders.Any(existing => string.Equals(existing, path, StringComparer.OrdinalIgnoreCase));
        }

        private async Task SaveSettingsAsync()
        {
            await _settingsLock.WaitAsync();
            try
            {
                _settings.PresetFolders = _availableFolders.ToList();
                await _settingsService.SaveAsync(_settings);
            }
            finally
            {
                _settingsLock.Release();
            }
        }

        public void Dispose()
        {
            _loadCancellation?.Cancel();
            _watcher?.Dispose();
            FilteredImages.CollectionChanged -= OnFilteredImagesChanged;
            _settingsLock.Dispose();
        }
    }
}
