using System;
using System.IO;
using System.Linq;
using System.Text;
using MetadataExtractor;
using MetadataExtractor.Formats.Png;
using PromptExplorer.Models;

namespace PromptExplorer.Services
{
    public class PromptExtractor
    {
        public PromptImageInfo? LoadPromptInfo(string filePath)
        {
            try
            {
                var prompt = ExtractPromptText(filePath);
                var lastWrite = File.GetLastWriteTimeUtc(filePath);
                return new PromptImageInfo(filePath, prompt, lastWrite);
            }
            catch
            {
                return null;
            }
        }

        private static string ExtractPromptText(string filePath)
        {
            var directories = ImageMetadataReader.ReadMetadata(filePath);
            var textDirectories = directories.OfType<PngTextDirectory>().ToList();
            if (textDirectories.Count == 0)
            {
                return string.Empty;
            }

            var exactPrompt = new StringBuilder();
            var additional = new StringBuilder();

            foreach (var directory in textDirectories)
            {
                foreach (var entry in directory.TextEntries)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.Text))
                    {
                        continue;
                    }

                    if (entry.Keyword.Equals("prompt", StringComparison.OrdinalIgnoreCase))
                    {
                        if (exactPrompt.Length > 0)
                        {
                            exactPrompt.AppendLine();
                        }

                        exactPrompt.Append(entry.Text.Trim());
                    }
                    else if (entry.Keyword.Equals("parameters", StringComparison.OrdinalIgnoreCase) ||
                             entry.Keyword.Equals("comment", StringComparison.OrdinalIgnoreCase))
                    {
                        if (additional.Length > 0)
                        {
                            additional.AppendLine();
                        }

                        additional.Append(entry.Text.Trim());
                    }
                    else
                    {
                        if (additional.Length > 0)
                        {
                            additional.AppendLine();
                        }

                        additional.Append(entry.Keyword);
                        additional.Append(':');
                        additional.Append(' ');
                        additional.Append(entry.Text.Trim());
                    }
                }
            }

            if (exactPrompt.Length > 0)
            {
                if (additional.Length > 0)
                {
                    exactPrompt.AppendLine();
                    exactPrompt.AppendLine();
                    exactPrompt.Append(additional);
                }

                return exactPrompt.ToString();
            }

            return additional.ToString();
        }
    }
}
