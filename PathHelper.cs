using System;
using System.IO;

namespace DeskOp
{
    public static class PathHelper
    {
        public static string GetAppDataPath(string fileName)
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DeskOp"
            );
            Directory.CreateDirectory(folder); // Ensure folder exists
            return Path.Combine(folder, fileName);
        }

        public static void EnsureDefaults()
        {
            CopyDefaultIfMissing("filters.json");
            CopyDefaultIfMissing("theme-settings.json");
        }

        private static void CopyDefaultIfMissing(string fileName)
        {
            string targetPath = GetAppDataPath(fileName);
            if (!File.Exists(targetPath))
            {
                string sourcePath = Path.Combine(AppContext.BaseDirectory, fileName);
                if (File.Exists(sourcePath))
                {
                    File.Copy(sourcePath, targetPath);
                }
                else
                {
                    // 🧠 Fallback to hardcoded defaults
                    if (fileName == "filters.json")
                    {
                        string defaultFilters = """
                {
                    "Games": ["steam", "epic", "riot", "launcher"],
                    "Docs": ["pdf", "docx", "txt", "markdown", "html"],
                    "Work": ["visual", "code", "vs", "sql", "unity"]
                }
                """;
                        File.WriteAllText(targetPath, defaultFilters);
                    }
                    else if (fileName == "theme-settings.json")
                    {
                        string defaultTheme = """
                {
                    "DefaultColorHex": "#292B2F",
                    "SelectedColorHex": "#2ECC71",
                    "Mode": "dark",
                    "SnapZone": "BottomCenter",
                    "LastFilter": "None",
                    "MainWindowLeft": 100,
                    "MainWindowTop": 100
                }
                """;
                        File.WriteAllText(targetPath, defaultTheme);
                    }
                }
            }
        }
    }
}
