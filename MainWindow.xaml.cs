using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;

namespace DeskOp
{
    public partial class MainWindow : Window
    {
        private Brush _defaultBrush = (Brush)new BrushConverter().ConvertFrom("#292B2F")!;
        private Brush _selectedBrush = (Brush)new BrushConverter().ConvertFrom("#2ECC71")!;
        private Button? _selectedButton;
        private double _hoverDimOpacity = 0.7;
        private double _fullOpacity = 1.0;
        private TimeSpan _fadeDuration = TimeSpan.FromMilliseconds(120);
        private string _currentThemeMode = "dark";
        private string _currentFilter = "None";

        private BottomWindow? _bottomWindow;

        public MainWindow()
        {
            if (File.Exists("theme-settings.json"))
            {
                try
                {
                    var json = File.ReadAllText("theme-settings.json");
                    var settings = JsonSerializer.Deserialize<ThemeSettings>(json);

                    if (settings is not null)
                    {
                        _defaultBrush = (Brush)new BrushConverter().ConvertFromString(settings.DefaultColorHex)!;
                        _selectedBrush = (Brush)new BrushConverter().ConvertFromString(settings.SelectedColorHex)!;
                        _currentThemeMode = settings.Mode;
                        _currentFilter = settings.LastFilter;

                        // Position the MainWindow
                        this.Left = settings.MainWindowLeft;
                        this.Top = settings.MainWindowTop;
                    }
                }
                catch
                {
                    // Fallback
                }
            }
            InitializeComponent();
            HighlightFilterButton(_currentFilter);
            if (_currentFilter != "None")
            {
                _bottomWindow = new BottomWindow();
                _bottomWindow.SetTheme(_defaultBrush, _selectedBrush);
                _bottomWindow.ApplyTheme(_defaultBrush, _selectedBrush, _currentThemeMode);
                _bottomWindow.ApplyFilter(_currentFilter);
                SyncThemeToBottomWindow();
                _bottomWindow.ShowWithFade();
            }
            ApplyThemeMode(_currentThemeMode);
            MouseLeftButtonUp += Window_MouseLeftButtonUp;
        }

        private void HighlightFilterButton(string tag)
        {
            foreach (var child in LogicalTreeHelper.GetChildren(this))
            {
                if (child is Border border)
                {
                    foreach (var wrap in LogicalTreeHelper.GetChildren(border))
                    {
                        if (wrap is WrapPanel panel)
                        {
                            foreach (var element in panel.Children)
                            {
                                if (element is Button btn && (btn.Tag?.ToString() ?? "") == tag)
                                {
                                    _selectedButton = btn;
                                    btn.Background = _selectedBrush;
                                    return;
                                }
                            }
                        }
                    }
                }
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;

            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            exStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);

            bool wallpaperEngineRunning = IsWallpaperEngineRunning();
            if (!wallpaperEngineRunning)
            {
                IntPtr progman = FindWindow("Progman", null);
                if (progman != IntPtr.Zero)
                    SetParent(hwnd, progman);
            }

            SetWindowPos(hwnd, HWND_BOTTOM, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }

        private bool IsWallpaperEngineRunning()
        {
            foreach (var p in Process.GetProcesses())
            {
                string name = p.ProcessName.ToLower();
                if (name.Contains("wallpaper32") || name.Contains("wallpaper64"))
                    return true;
            }
            return false;
        }

        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow();
            settingsWindow.Owner = this;
            settingsWindow.OnBackgroundThemeSelected = ApplyButtonBackground;
            settingsWindow.OnSelectedButtonColorSelected = ApplySelectedHighlight;
            settingsWindow.OnThemeModeChanged = ApplyThemeMode;
            settingsWindow.ShowDialog();
        }

        private void ApplyButtonBackground(Brush brush)
        {
            _defaultBrush = brush;

            foreach (var child in LogicalTreeHelper.GetChildren(this))
            {
                if (child is Border border)
                {
                    foreach (var wrap in LogicalTreeHelper.GetChildren(border))
                    {
                        if (wrap is WrapPanel panel)
                        {
                            foreach (var element in panel.Children)
                            {
                                if (element is Button btn && btn != _selectedButton)
                                    btn.Background = _defaultBrush;
                            }
                        }
                    }
                }
            }

            if (_selectedButton is not null)
                _selectedButton.Background = _selectedBrush;

            SyncThemeToBottomWindow();
        }

        private void ApplySelectedHighlight(Brush brush)
        {
            _selectedBrush = brush;

            if (_selectedButton is not null)
            {
                var animatedBrush = new SolidColorBrush(((SolidColorBrush)_selectedButton.Background).Color);
                _selectedButton.Background = animatedBrush;

                var animation = new ColorAnimation
                {
                    To = ((SolidColorBrush)_selectedBrush).Color,
                    Duration = TimeSpan.FromMilliseconds(200),
                    EasingFunction = new QuadraticEase()
                };

                animatedBrush.BeginAnimation(SolidColorBrush.ColorProperty, animation);
            }

            SyncThemeToBottomWindow();
        }

        private void ApplyThemeMode(string mode)
        {
            _currentThemeMode = mode;
            bool isLight = mode == "light";

            var bg = isLight ? Brushes.White : (Brush)new BrushConverter().ConvertFrom("#FF292B2F")!;
            var fg = isLight ? Brushes.Black : Brushes.White;

            if (Content is Border root)
            {
                root.Background = bg;

                if (root.Child is WrapPanel wrap)
                {
                    foreach (var child in wrap.Children)
                    {
                        if (child is Button btn)
                        {
                            btn.Foreground = fg;
                            if (btn != _selectedButton)
                                btn.Background = _defaultBrush;
                        }
                    }
                }
            }

            SyncThemeToBottomWindow();
        }

        private void SyncThemeToBottomWindow()
        {
            _bottomWindow?.ApplyTheme(_defaultBrush, _selectedBrush, _currentThemeMode);
        }

        private void ModeButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button clickedButton)
            {
                // Reset previous selected button
                if (_selectedButton is not null)
                    _selectedButton.Background = _defaultBrush;

                _selectedButton = clickedButton;
                var startColor = ((SolidColorBrush)_selectedButton.Background).Color;
                var targetColor = ((SolidColorBrush)_selectedBrush).Color;

                var animatedBrush = new SolidColorBrush(startColor);
                _selectedButton.Background = animatedBrush;

                var animation = new ColorAnimation
                {
                    To = targetColor,
                    Duration = TimeSpan.FromMilliseconds(200),
                    EasingFunction = new QuadraticEase()
                };
                animatedBrush.BeginAnimation(SolidColorBrush.ColorProperty, animation);

                _currentFilter = clickedButton.Tag?.ToString() ?? "None";

                if (_currentFilter == "None")
                {
                    _bottomWindow?.HideWithFade();
                }
                else
                {
                    // Create BottomWindow if needed
                    if (_bottomWindow is null)
                    {
                        _bottomWindow = new BottomWindow();
                        _bottomWindow.SetTheme(_defaultBrush, _selectedBrush);
                        _bottomWindow.ApplyTheme(_defaultBrush, _selectedBrush, _currentThemeMode);
                    }

                    _bottomWindow.ApplyFilter(_currentFilter);
                    bool hasIcons = _bottomWindow.IconPanel.Children.Count > 0;
                    SyncThemeToBottomWindow();

                    if (hasIcons)
                    {
                        _bottomWindow.ShowWithFade();
                    }
                    else
                    {
                        _bottomWindow.HideWithFade();
                    }
                }
            }
            SaveLastSettings(); // 🆕
        }

        private void SaveLastSettings()
        {
            try
            {
                string path = "theme-settings.json";
                ThemeSettings settings;

                if (File.Exists(path))
                {
                    string existing = File.ReadAllText(path);
                    settings = JsonSerializer.Deserialize<ThemeSettings>(existing) ?? new ThemeSettings();
                }
                else settings = new ThemeSettings();

                settings.DefaultColorHex = ((SolidColorBrush)_defaultBrush).Color.ToString();
                settings.SelectedColorHex = ((SolidColorBrush)_selectedBrush).Color.ToString();
                settings.Mode = _currentThemeMode;
                settings.LastFilter = _currentFilter;
                settings.MainWindowLeft = this.Left;
                settings.MainWindowTop = this.Top;

                File.WriteAllText(path, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch
            {
                MessageBox.Show("Failed to save filter & window position.", "DeskOp", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void SaveLastFilter(string filter)
        {
            try
            {
                string path = "theme-settings.json";
                Dictionary<string, object> settings;

                if (File.Exists(path))
                {
                    string existing = File.ReadAllText(path);
                    settings = JsonSerializer.Deserialize<Dictionary<string, object>>(existing) ?? new();
                }
                else settings = new();

                settings["LastFilter"] = filter;
                File.WriteAllText(path, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch
            {
                // Optional: log or ignore
            }
        }

        private void ModeButton_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is Button button)
                AnimateOpacity(button, _hoverDimOpacity);
        }

        private void ModeButton_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is Button button)
                AnimateOpacity(button, _fullOpacity);
        }

        private void AnimateOpacity(UIElement element, double toOpacity)
        {
            var animation = new DoubleAnimation
            {
                To = toOpacity,
                Duration = new Duration(_fadeDuration),
                EasingFunction = new QuadraticEase()
            };
            element.BeginAnimation(UIElement.OpacityProperty, animation);
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            SnapToHorizontalCenterIfClose();
        }

        private void SnapToHorizontalCenterIfClose()
        {
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double windowWidth = ActualWidth;
            double currentLeft = Left;
            double centerX = (screenWidth - windowWidth) / 2;

            if (Math.Abs(currentLeft - centerX) <= 50)
                Left = centerX;
        }

        // Win32 API Interop
        private const int SWP_NOMOVE = 0x0002;
        private const int SWP_NOSIZE = 0x0001;
        private const int SWP_NOACTIVATE = 0x0010;
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_NOACTIVATE = 0x08000000;

        private static readonly IntPtr HWND_BOTTOM = new(1);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

        [DllImport("user32.dll")]
        private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    }
}
