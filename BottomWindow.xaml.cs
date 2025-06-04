using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Linq;

namespace DeskOp
{
    public partial class BottomWindow : Window
    {
        private Rect? _lastSnapRect = null;
        private bool _wasSnapped = false;
        private Point _dragOffset;
        private bool _isDragging = false;
        private SnapHintOverlay? _overlay;
        private Dictionary<string, List<string>> _filters = new();
        private string _currentCategory = "None";
        private Brush _defaultBrush = new SolidColorBrush(Color.FromRgb(41, 43, 47));
        private Brush _highlightBrush = new SolidColorBrush(Color.FromRgb(46, 204, 113));

        public BottomWindow()
        {
            InitializeComponent();
            LoadFilters();
            this.Opacity = 0;
            this.Visibility = Visibility.Collapsed;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.Topmost = true;
            this.SizeToContent = SizeToContent.Manual;

            Rect initial = GetSnapRect(SnapZone.CenterBottom);
            this.Left = initial.Left;
            this.Top = initial.Top;
            this.Width = initial.Width;
            this.Height = initial.Height;

            _overlay = new SnapHintOverlay();
            _overlay.Owner = null;
            _overlay.Show();

            LoadIcons("None");

            var hwnd = new WindowInteropHelper(this).Handle;

            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            exStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);

            SetWindowPos(hwnd, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }

        public void SetTheme(Brush defaultBg, Brush highlightBg)
        {
            _defaultBrush = defaultBg;
            _highlightBrush = highlightBg;
        }

        private string? GetUrlFromInternetShortcut(string path)
        {
            try
            {
                var lines = File.ReadAllLines(path);
                foreach (var line in lines)
                {
                    if (line.StartsWith("URL=", StringComparison.OrdinalIgnoreCase))
                        return line.Substring(4).Trim();
                }
            }
            catch { }

            return null;
        }

        private (string? TargetPath, string? Arguments, string? WorkingDir, string? Description) GetShortcutDetails(string shortcutPath)
        {
            try
            {
                dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!);
                dynamic shortcut = shell.CreateShortcut(shortcutPath);

                return (
                    shortcut.TargetPath as string,
                    shortcut.Arguments as string,
                    shortcut.WorkingDirectory as string,
                    shortcut.Description as string
                );
            }
            catch
            {
                return (null, null, null, null);
            }
        }

        private bool ShouldIncludeAny(List<string> candidates, string category)
        {
            if (category == "All") return true;
            if (category == "None") return false;

            // Explicit match for requested category
            if (_filters.TryGetValue(category, out var keywords))
            {
                foreach (var keyword in keywords)
                {
                    string kw = keyword.ToLower();
                    if (candidates.Any(text => text.Contains(kw)))
                        return true;
                }
            }

            // Special fallback: Productivity = NOT Docs and NOT Games
            if (category == "Productivity")
            {
                bool isInDocs = MatchesCategory(candidates, "Docs");
                bool isInGames = MatchesCategory(candidates, "Games");

                return !isInDocs && !isInGames;
            }

            return false;
        }

        private bool MatchesCategory(List<string> candidates, string category)
        {
            if (_filters.TryGetValue(category, out var keywords))
            {
                foreach (var keyword in keywords)
                {
                    string kw = keyword.ToLower();
                    if (candidates.Any(text => text.Contains(kw)))
                        return true;
                }
            }
            return false;
        }

        public bool LoadIcons(string category)
        {
            _currentCategory = category;
            IconPanel.Children.Clear();

            string userDesktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string publicDesktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);

            var files = new List<string>();
            files.AddRange(Directory.GetFiles(userDesktop));
            files.AddRange(Directory.GetFiles(publicDesktop));

            files = files.FindAll(f =>
            {
                var attr = File.GetAttributes(f);
                return !attr.HasFlag(FileAttributes.Hidden) && !attr.HasFlag(FileAttributes.System);
            });

            int added = 0;
            foreach (var path in files)
            {
                string name = Path.GetFileNameWithoutExtension(path).ToLower();
                string ext = Path.GetExtension(path).TrimStart('.').ToLower();
                List<string> searchTerms = new() { name, ext };

                if (path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                {
                    var (target, args, startIn, comment) = GetShortcutDetails(path);
                    if (!string.IsNullOrWhiteSpace(target)) searchTerms.Add(target.ToLower());
                    if (!string.IsNullOrWhiteSpace(args)) searchTerms.Add(args.ToLower());
                    if (!string.IsNullOrWhiteSpace(startIn)) searchTerms.Add(startIn.ToLower());
                    if (!string.IsNullOrWhiteSpace(comment)) searchTerms.Add(comment.ToLower());
                }
                else if (path.EndsWith(".url", StringComparison.OrdinalIgnoreCase))
                {
                    var url = GetUrlFromInternetShortcut(path);
                    if (!string.IsNullOrWhiteSpace(url)) searchTerms.Add(url.ToLower());
                }

                if (ShouldIncludeAny(searchTerms, category))
                {
                    var icon = new Button
                    {
                        Content = name,
                        Background = _defaultBrush,
                        Foreground = Brushes.White,
                        Margin = new Thickness(6),
                        Padding = new Thickness(12, 6, 12, 6),
                        Cursor = Cursors.Hand,
                        BorderThickness = new Thickness(0),
                        FontSize = 14
                    };

                    icon.Click += (s, e) => Process.Start("explorer.exe", path);
                    IconPanel.Children.Add(icon);
                    added++;
                }
            }

            if (added > 0 && !_wasSnapped)
            {
                ResizeToFit();
                return true;
            }

            return added > 0;
        }

        private bool ShouldInclude(string name, string category)
        {
            if (category == "All") return true;
            if (category == "None") return false;

            if (_filters.TryGetValue(category, out var keywords))
            {
                foreach (var word in keywords)
                    if (name.Contains(word.ToLower()))
                        return true;
            }
            return false;
        }

        private void LoadFilters()
        {
            try
            {
                string path = "filters.json";
                string json = File.ReadAllText(path);
                _filters = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json)!;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load filters.json\n\n{ex.Message}", "DeskOp Error", MessageBoxButton.OK, MessageBoxImage.Error);
                _filters = new();
            }
        }

        private void ResizeToFit()
        {
            Dispatcher.InvokeAsync(() =>
            {
                IconPanel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Size desired = IconPanel.DesiredSize;

                double padding = 40;
                double width = Math.Min(desired.Width + padding, SystemParameters.PrimaryScreenWidth - 100);
                double height = Math.Min(desired.Height + padding, SystemParameters.PrimaryScreenHeight - 100);

                AnimateTo(new Rect(this.Left, this.Top, width, height));
            }, DispatcherPriority.Loaded);
        }

        // New Fade Helpers
        public void ShowWithFade()
        {
            if (_lastSnapRect.HasValue)
            {
                this.Left = _lastSnapRect.Value.Left;
                this.Top = _lastSnapRect.Value.Top;
                this.Width = _lastSnapRect.Value.Width;
                this.Height = _lastSnapRect.Value.Height;
            }

            this.Visibility = Visibility.Visible;

            var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
            this.BeginAnimation(Window.OpacityProperty, fade);
        }

        public void HideWithFade()
        {
            if (_wasSnapped)
                _lastSnapRect = new Rect(this.Left, this.Top, this.Width, this.Height);

            var fade = new DoubleAnimation(this.Opacity, 0, TimeSpan.FromMilliseconds(200));
            fade.Completed += (s, e) => this.Visibility = Visibility.Collapsed;
            this.BeginAnimation(Window.OpacityProperty, fade);
        }

        // 🧲 Snap logic
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                _dragOffset = e.GetPosition(this);
                _isDragging = true;
                this.CaptureMouse();
            }
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging && e.LeftButton == MouseButtonState.Pressed)
            {
                var screenPos = PointToScreen(e.GetPosition(this));
                this.Left = screenPos.X - _dragOffset.X;
                this.Top = screenPos.Y - _dragOffset.Y;
                ShowSnapHintForCurrentPosition();
            }
        }

        private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                this.ReleaseMouseCapture();
                _overlay?.HideHint();
                SnapToNearestZone();
            }
        }

        private enum SnapZone { Left, Right, HorizontalStrip, Square, CenterBottom }

        private void ShowSnapHintForCurrentPosition()
        {
            SnapZone zone = DetermineBestSnapZone();
            Rect previewRect = GetSnapRect(zone);
            _overlay?.ShowHint(previewRect);
        }

        private void SnapToNearestZone()
        {
            SnapZone zone = DetermineBestSnapZone();
            Rect rect = GetSnapRect(zone);
            _lastSnapRect = rect;
            _wasSnapped = true;
            AnimateTo(rect);
        }

        private SnapZone DetermineBestSnapZone()
        {
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;

            double centerX = screenWidth / 2;
            double centerY = screenHeight / 2;
            double left = this.Left + this.ActualWidth / 2;
            double top = this.Top + this.ActualHeight / 2;

            double Dist(double x1, double y1, double x2, double y2) =>
                Math.Sqrt(Math.Pow(x1 - x2, 2) + Math.Pow(y1 - y2, 2));

            var distances = new (SnapZone, double)[]
            {
                (SnapZone.Left, Dist(left, top, screenWidth * 0.15, centerY)),
                (SnapZone.Right, Dist(left, top, screenWidth * 0.85, centerY)),
                (SnapZone.HorizontalStrip, Dist(left, top, centerX, screenHeight - 60)),
                (SnapZone.Square, Dist(left, top, centerX, centerY)),
                (SnapZone.CenterBottom, Dist(left, top, centerX, screenHeight * 0.85))
            };

            Array.Sort(distances, (a, b) => a.Item2.CompareTo(b.Item2));
            return distances[0].Item1;
        }

        private Rect GetSnapRect(SnapZone zone)
        {
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;
            double padding = 40;

            return zone switch
            {
                SnapZone.Left => new Rect(padding, screenHeight * 0.25, 300, 400),
                SnapZone.Right => new Rect(screenWidth - 300 - padding, screenHeight * 0.25, 300, 400),
                SnapZone.HorizontalStrip => new Rect((screenWidth - 600) / 2, screenHeight - 200 - padding, 600, 200),
                SnapZone.Square => new Rect((screenWidth - 400) / 2, (screenHeight - 400) / 2, 400, 400),
                SnapZone.CenterBottom => new Rect((screenWidth - 600) / 2, screenHeight - 180 - padding, 600, 180),
                _ => Rect.Empty
            };
        }

        public void ApplyFilter(string category)
        {
            LoadIcons(category);
        }

        public void ApplyTheme(Brush defaultBg, Brush highlightBg, string mode)
        {
            _defaultBrush = defaultBg;
            _highlightBrush = highlightBg;

            bool isLight = mode == "light";
            var fg = isLight ? Brushes.Black : Brushes.White;
            var bg = isLight ? Brushes.White : new SolidColorBrush(Color.FromArgb(200, 30, 30, 30));

            this.Background = Brushes.Transparent;
            RootBorder.Background = bg;

            foreach (var child in IconPanel.Children)
            {
                if (child is Button btn)
                {
                    btn.Background = _defaultBrush;
                    btn.Foreground = fg;
                }
            }
        }

        private void AnimateTo(Rect target)
        {
            double startLeft = this.Left;
            double startTop = this.Top;
            double startWidth = this.Width;
            double startHeight = this.Height;

            double dLeft = target.Left - startLeft;
            double dTop = target.Top - startTop;
            double dWidth = target.Width - startWidth;
            double dHeight = target.Height - startHeight;

            int durationMs = 150;
            int frames = 20;
            int interval = durationMs / frames;
            int currentFrame = 0;

            DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(interval) };

            timer.Tick += (s, e) =>
            {
                currentFrame++;
                double progress = (double)currentFrame / frames;
                double eased = EaseOutQuad(progress);

                this.Left = startLeft + dLeft * eased;
                this.Top = startTop + dTop * eased;
                this.Width = startWidth + dWidth * eased;
                this.Height = startHeight + dHeight * eased;

                if (currentFrame >= frames)
                {
                    timer.Stop();
                    this.Left = target.Left;
                    this.Top = target.Top;
                    this.Width = target.Width;
                    this.Height = target.Height;
                }
            };

            timer.Start();
        }

        private double EaseOutQuad(double t) => t * (2 - t);

        private const int SWP_NOMOVE = 0x0002;
        private const int SWP_NOSIZE = 0x0001;
        private const int SWP_NOACTIVATE = 0x0010;
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_NOACTIVATE = 0x08000000;

        private static readonly IntPtr HWND_BOTTOM = new(1);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    }
}
