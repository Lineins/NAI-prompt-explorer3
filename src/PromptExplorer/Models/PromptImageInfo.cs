using System;

namespace PromptExplorer.Models
{
    public class PromptImageInfo
    {
        public PromptImageInfo(string filePath, string prompt, DateTime lastWriteTimeUtc)
        {
            FilePath = filePath;
            FileName = System.IO.Path.GetFileName(filePath);
            Prompt = prompt;
            LastWriteTimeUtc = lastWriteTimeUtc;
        }

        public string FilePath { get; }

        public string FileName { get; }

        public string Prompt { get; }

        public DateTime LastWriteTimeUtc { get; }
    }
}
