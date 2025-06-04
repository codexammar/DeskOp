using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;

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

        public MainWindow()
        {
            InitializeComponent();
            MouseLeftButtonUp += Window_MouseLeftButtonUp;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;

            // Set tool window and prevent activation
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            exStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);

            // Detect Wallpaper Engine
            bool wallpaperEngineRunning = IsWallpaperEngineRunning();

            if (!wallpaperEngineRunning)
            {
                // Set parent to Progman to dock below icons
                IntPtr progman = FindWindow("Progman", null);
                if (progman != IntPtr.Zero)
                {
                    SetParent(hwnd, progman);
                }
            }

            // Push to bottom
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
        }

        private void ApplyThemeMode(string mode)
        {
            _currentThemeMode = mode;
            bool isLight = mode == "light";

            var bg = isLight ? Brushes.White : (Brush)new BrushConverter().ConvertFrom("#CC1E1E1E")!;
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
        }

        private void ModeButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button clickedButton)
            {
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
