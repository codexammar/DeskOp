using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DeskOp
{
    public partial class SettingsWindow : Window
    {
        // This will be set by MainWindow before opening
        public Action<Brush>? OnThemeSelected;

        public SettingsWindow()
        {
            InitializeComponent();
        }

        private void ThemeButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string colorCode)
            {
                try
                {
                    var brush = (Brush)new BrushConverter().ConvertFromString(colorCode)!;
                    OnThemeSelected?.Invoke(brush);
                }
                catch
                {
                    MessageBox.Show("Invalid color format.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
