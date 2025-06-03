using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DeskOp
{
    public partial class SettingsWindow : Window
    {
        public Action<Brush>? OnBackgroundThemeSelected;
        public Action<Brush>? OnSelectedButtonColorSelected;
        public Action<string>? OnPluginImported;
        public Action<string>? OnThemeModeChanged;

        public SettingsWindow()
        {
            InitializeComponent();
        }

        private void SelectColor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string colorCode)
            {
                try
                {
                    var brush = (Brush)new BrushConverter().ConvertFromString(colorCode)!;
                    OnSelectedButtonColorSelected?.Invoke(brush);
                }
                catch
                {
                    MessageBox.Show("Invalid color format.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ThemeMode_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Tag is string themeTag)
            {
                OnThemeModeChanged?.Invoke(themeTag);

                var backgroundColor = themeTag == "light" ? Brushes.White : (Brush)new BrushConverter().ConvertFromString("#292B2F")!;
                OnBackgroundThemeSelected?.Invoke(backgroundColor);
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

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
