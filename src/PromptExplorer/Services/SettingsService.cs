using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using PromptExplorer.Settings;

namespace PromptExplorer.Services
{
    public class SettingsService
    {
        private readonly string _settingsPath;

        public SettingsService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var folder = Path.Combine(appData, "NAIPromptExplorer");
            Directory.CreateDirectory(folder);
            _settingsPath = Path.Combine(folder, "settings.json");
        }

        public async Task<AppSettings> LoadAsync()
        {
            AppSettings settings;
            if (File.Exists(_settingsPath))
            {
                try
                {
                    await using var stream = File.OpenRead(_settingsPath);
                    settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new AppSettings();
                }
                catch
                {
                    settings = new AppSettings();
                }
            }
            else
            {
                settings = new AppSettings();
            }

            EnsureDefaults(settings);
            return settings;
        }

        public async Task SaveAsync(AppSettings settings)
        {
            EnsureDefaults(settings);
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            await using var stream = File.Open(_settingsPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await JsonSerializer.SerializeAsync(stream, settings, options);
        }

        public string SettingsPath => _settingsPath;

        private static void EnsureDefaults(AppSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.DefaultFolder))
            {
                settings.DefaultFolder = @"C:\\Users\\kuron\\Downloads\\NAIv4.5画風";
            }

            if (settings.PresetFolders == null)
            {
                settings.PresetFolders = new List<string>();
            }

            if (!settings.PresetFolders.Contains(settings.DefaultFolder, StringComparer.OrdinalIgnoreCase))
            {
                settings.PresetFolders.Add(settings.DefaultFolder);
            }

            settings.PresetFolders = settings.PresetFolders
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (string.IsNullOrWhiteSpace(settings.LastUsedFolder))
            {
                settings.LastUsedFolder = settings.DefaultFolder;
            }
        }
    }
}
