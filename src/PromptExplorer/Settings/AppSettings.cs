using System.Collections.Generic;

namespace PromptExplorer.Settings
{
    public class AppSettings
    {
        public string DefaultFolder { get; set; } = @"C:\\Users\\kuron\\Downloads\\NAIv4.5画風";

        public string? LastUsedFolder { get; set; }

        public List<string> PresetFolders { get; set; } = new();
    }
}
