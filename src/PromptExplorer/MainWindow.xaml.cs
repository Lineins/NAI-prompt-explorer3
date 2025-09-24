using System;
using System.Windows;
using System.Windows.Input;
using PromptExplorer.Services;
using PromptExplorer.ViewModels;

namespace PromptExplorer
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();

            var settingsService = new SettingsService();
            var promptExtractor = new PromptExtractor();
            var imageCache = new ImageCache();
            _viewModel = new MainViewModel(settingsService, promptExtractor, imageCache);
            DataContext = _viewModel;

            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;
            await _viewModel.InitializeAsync();
        }

        private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && _viewModel.SearchCommand.CanExecute(null))
            {
                _viewModel.SearchCommand.Execute(null);
                e.Handled = true;
            }
        }

        private void ThumbnailList_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                e.Handled = true;
                var delta = e.Delta > 0 ? 16 : -16;
                _viewModel.AdjustThumbnailSize(delta);
            }
        }

        private void MainWindow_OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _viewModel.Dispose();
        }
    }
}
