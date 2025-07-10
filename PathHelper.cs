using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;

namespace DeskOp
{
    public static class PathHelper
    {
        public static readonly string AppDataFolder =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DeskOp");

        public static string GetSettingsPath() =>
            Path.Combine(AppDataFolder, "theme-settings.json");

        public static string GetFiltersPath() =>
            Path.Combine(AppDataFolder, "filters.json");

        public static void EnsureAppDataFolderAndDefaults()
        {
            if (!Directory.Exists(AppDataFolder))
                Directory.CreateDirectory(AppDataFolder);

            string settingsPath = GetSettingsPath();
            if (!File.Exists(settingsPath))
            {
                var defaultSettings = new ThemeSettings
                {
                    DefaultColorHex = "#FFFFFFFF",
                    SelectedColorHex = "#FF68DBFF",
                    Mode = "light",
                    SnapZone = "Right",
                    LastFilter = "All",
                    MainWindowLeft = 666,
                    MainWindowTop = 23
                };

                string json = JsonSerializer.Serialize(defaultSettings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(settingsPath, json);
            }

            string filtersPath = GetFiltersPath();
            if (!File.Exists(filtersPath))
            {
                var defaultFilters = new Dictionary<string, List<string>>
                {
                    { "Games", new List<string> { "steam", "epic", "battle.net", "riot", "games", "launcher", "gog" } },
                    { "Productivity", new List<string> { "word", "excel", "notion", "onenote", "vscode", "teams" } },
                    { "Docs", new List<string> { "pdf", "docx", "txt", "markdown", "html" } }
                };

                string json = JsonSerializer.Serialize(defaultFilters, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filtersPath, json);
            }
        }
    }

    public class ThemeSettings
    {
        public string DefaultColorHex { get; set; } = "#292B2F";
        public string SelectedColorHex { get; set; } = "#2ECC71";
        public string Mode { get; set; } = "dark";
        public string SnapZone { get; set; } = "Right";
        public string LastFilter { get; set; } = "None";
        public double MainWindowLeft { get; set; } = 100;
        public double MainWindowTop { get; set; } = 100;
    }
}
