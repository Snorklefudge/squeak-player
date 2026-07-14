using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SqueakPlayer
{
    /// <summary>
    /// Small JSON-backed store in %AppData%\Squeak\settings.json.
    /// Remembers volume/mute, always-on-top, and per-file playback positions.
    /// </summary>
    public class Settings
    {
        public int Volume { get; set; } = 100;
        public bool Muted { get; set; }
        public bool AlwaysOnTop { get; set; }
        public bool AutoplayNext { get; set; } = true;
        public string Language { get; set; } = "auto";   // "auto" | "en" | "pl"

        // file path -> last playback position (ms)
        public Dictionary<string, long> Positions { get; set; } = new();

        private const int MaxRememberedFiles = 50;

        private static string Dir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Squeak");
        private static string FilePath => Path.Combine(Dir, "settings.json");

        public static Settings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings();
            }
            catch { /* corrupt / unreadable → fall back to defaults */ }
            return new Settings();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllText(FilePath,
                    JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { /* best-effort persistence */ }
        }

        /// <summary>Store where we stopped, unless we're at the very start or basically finished.</summary>
        public void RememberPosition(string? path, long timeMs, long lengthMs)
        {
            if (string.IsNullOrEmpty(path)) return;

            if (lengthMs > 0 && (timeMs < 5000 || timeMs > lengthMs - 10000))
            {
                Positions.Remove(path);
                return;
            }

            Positions[path] = timeMs;

            // Trim oldest entries (Dictionary preserves insertion order).
            while (Positions.Count > MaxRememberedFiles)
                Positions.Remove(Positions.Keys.First());
        }
    }
}
