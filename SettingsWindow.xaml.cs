using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using System.Text.Json;

namespace DeskOp
{
    public partial class SettingsWindow : Window
    {
        private readonly string settingsPath = "theme-settings.json";
        private ThemeSettings currentSettings = new();
        public Action<Brush>? OnBackgroundThemeSelected;
        public Action<Brush>? OnSelectedColorChanged;
        public Action<Brush>? OnSelectedButtonColorSelected;
        public Action<string>? OnPluginImported;
        public Action<string>? OnThemeModeChanged;

        public SettingsWindow()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void SelectedColorPicker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e)
        {
            if (e.NewValue.HasValue)
            {
                var newBrush = new SolidColorBrush(e.NewValue.Value);
                currentSettings.SelectedColorHex = e.NewValue.Value.ToString();
                SaveSettings();

                OnSelectedColorChanged?.Invoke(newBrush);
                OnSelectedButtonColorSelected?.Invoke(newBrush);
            }
        }

        private void ThemeMode_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Tag is string themeTag)
            {
                currentSettings.Mode = themeTag; // ← update mode

                var backgroundColor = themeTag == "light"
                    ? Colors.White
                    : (Color)ColorConverter.ConvertFromString("#292B2F")!;

                currentSettings.DefaultColorHex = backgroundColor.ToString(); // ← update default
                SaveSettings(); // ← write to disk

                OnThemeModeChanged?.Invoke(themeTag);
                OnBackgroundThemeSelected?.Invoke(new SolidColorBrush(backgroundColor));
            }
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;

                foreach (var file in files)
                {
                    var fileName = System.IO.Path.GetFileName(file);
                    var item = new TextBlock
                    {
                        Text = $"✔ {fileName}",
                        Foreground = Brushes.LightGreen,
                        Margin = new Thickness(2)
                    };

                    PluginList.Children.Add(item);
                    OnPluginImported?.Invoke(file);
                }
            }
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
        private void LoadSettings()
        {
            try
            {
                if (File.Exists(settingsPath))
                {
                    string json = File.ReadAllText(settingsPath);
                    currentSettings = JsonSerializer.Deserialize<ThemeSettings>(json)!;

                    var defaultColor = (Color)ColorConverter.ConvertFromString(currentSettings.DefaultColorHex)!;
                    var selectedColor = (Color)ColorConverter.ConvertFromString(currentSettings.SelectedColorHex)!;

                    OnBackgroundThemeSelected?.Invoke(new SolidColorBrush(defaultColor));
                    OnSelectedButtonColorSelected?.Invoke(new SolidColorBrush(selectedColor));
                    OnThemeModeChanged?.Invoke(currentSettings.Mode);
                    if (currentSettings.Mode == "light")
                        LightModeRadio.IsChecked = true;
                    else
                        DarkModeRadio.IsChecked = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load theme settings: {ex.Message}", "DeskOp", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void SaveSettings()
        {
            try
            {
                string json = JsonSerializer.Serialize(currentSettings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(settingsPath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save theme settings: {ex.Message}", "DeskOp", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
    public class ThemeSettings
    {
        public string DefaultColorHex { get; set; } = "#FF292B2F"; // Opaque dark
        public string SelectedColorHex { get; set; } = "#FF2ECC71";
        public string Mode { get; set; } = "dark";
    }
}
