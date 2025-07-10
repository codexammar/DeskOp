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
using DrawingIcon = System.Drawing.Icon;
using System.Globalization;
using System.Windows.Media.Imaging;
using Microsoft.WindowsAPICodePack.Shell;

namespace DeskOp
{
    public partial class BottomWindow : Window
    {
        private Rect? _lastSnapRect = null;
        private bool _wasSnapped = false;
        private bool _isDragging = false;
        private SnapHintOverlay? _overlay;
        private Dictionary<string, List<string>> _filters = new();
        private string _currentCategory = "None";
        private System.Windows.Point _dragOffset;
        private System.Windows.Media.Brush _defaultBrush = new SolidColorBrush(Color.FromRgb(41, 43, 47));
        private System.Windows.Media.Brush _highlightBrush = new SolidColorBrush(Color.FromRgb(46, 204, 113));
        private SnapZone _currentSnapZone; // ✅ Empty for now
        // 📐 Tile size constants (used to prevent spacing/cutoff issues)
        private const double TILE_WIDTH = 112;
        private const double TILE_HEIGHT = 112;
        private const double TILE_SPACING = 6; // horizontal/vertical gap between columns/rows
        private const double TILE_TOTAL_WIDTH = TILE_WIDTH;  // ⚠️ Ignore Margin
        private const double TILE_TOTAL_HEIGHT = TILE_HEIGHT;
        private const double EXTRA_PADDING = 30;  // additional buffer to avoid cutoff
        private string _currentThemeMode = "dark"; // default value

        public BottomWindow()
        {
            _currentSnapZone = LoadSavedSnapZone();
            InitializeComponent();
            LoadFilters();
            this.Opacity = 0;
            this.Visibility = Visibility.Collapsed;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.Topmost = true;
            this.SizeToContent = SizeToContent.Manual;

            _overlay = new SnapHintOverlay();
            _overlay.Owner = null;
            _overlay.Show();

            bool hasIcons = LoadIcons(_currentCategory); // Use current filter category (should be "None" initially)
            int iconCount = IconPanel.Children.Count;

            // 🧠 Use saved snap zone for positioning
            Rect startupRect = GetDynamicSnapRect(_currentSnapZone, iconCount);
            this.Left = startupRect.Left;
            this.Top = startupRect.Top;
            this.Width = startupRect.Width;
            this.Height = startupRect.Height;

            // Apply layout direction based on zone
            ApplyOrientationForSnapZone();

            var hwnd = new WindowInteropHelper(this).Handle;

            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            exStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);

            SetWindowPos(hwnd, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }

        public void SetTheme(System.Windows.Media.Brush defaultBg, System.Windows.Media.Brush highlightBg)
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

        private SnapZone LoadSavedSnapZone()
        {
            try
            {
                string path = PathHelper.GetSettingsPath(); // ✅ Use AppData path
                if (!File.Exists(path))
                    return SnapZone.Right;

                string json = File.ReadAllText(path);
                var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("SnapZone", out var zoneProp))
                {
                    if (Enum.TryParse<SnapZone>(zoneProp.GetString(), ignoreCase: true, out var zone))
                        return zone;
                }
            }
            catch { }

            return SnapZone.Right; // fallback default
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

            var files = Directory.GetFiles(userDesktop)
                .Concat(Directory.GetFiles(publicDesktop))
                .Where(f => !File.GetAttributes(f).HasFlag(FileAttributes.Hidden | FileAttributes.System))
                .ToList();

            int added = 0;
            foreach (var path in files)
            {
                string name = Path.GetFileNameWithoutExtension(path).ToLower();
                string ext = Path.GetExtension(path).TrimStart('.').ToLower();
                List<string> searchTerms = new() { name, ext };

                string? iconPath = path;

                if (path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                {
                    var (target, args, startIn, comment) = GetShortcutDetails(path);
                    if (!string.IsNullOrWhiteSpace(target))
                    {
                        searchTerms.Add(target.ToLower());

                        // 🧠 Check if the target is a launcher like Update.exe and arguments hint at a real app
                        if (Path.GetFileName(target).Equals("Update.exe", StringComparison.OrdinalIgnoreCase) &&
                            !string.IsNullOrWhiteSpace(args) && args.Contains("processStart"))
                        {
                            var parts = args.Split(' ');
                            int idx = Array.FindIndex(parts, p => p.Equals("processStart", StringComparison.OrdinalIgnoreCase));
                            if (idx != -1 && idx + 1 < parts.Length)
                            {
                                var actualApp = parts[idx + 1];
                                var discordPath = Path.Combine(Path.GetDirectoryName(target)!, actualApp);
                                if (File.Exists(discordPath))
                                {
                                    iconPath = discordPath;
                                }
                            }
                        }
                        else
                        {
                            iconPath = target;
                        }
                    }
                    if (!string.IsNullOrWhiteSpace(args)) searchTerms.Add(args.ToLower());
                    if (!string.IsNullOrWhiteSpace(startIn)) searchTerms.Add(startIn.ToLower());
                    if (!string.IsNullOrWhiteSpace(comment)) searchTerms.Add(comment.ToLower());
                }
                else if (path.EndsWith(".url", StringComparison.OrdinalIgnoreCase))
                {
                    var url = GetUrlFromInternetShortcut(path);
                    if (!string.IsNullOrWhiteSpace(url))
                        searchTerms.Add(url.ToLower());

                    // 🧠 NEW: Try to extract IconFile from .url
                    string[] lines = File.ReadAllLines(path);
                    foreach (var line in lines)
                    {
                        if (line.StartsWith("IconFile=", StringComparison.OrdinalIgnoreCase))
                        {
                            string iconFile = line.Substring("IconFile=".Length).Trim();
                            if (File.Exists(iconFile))
                            {
                                iconPath = iconFile;
                            }
                        }
                    }
                }

                if (ShouldIncludeAny(searchTerms, category))
                {
                    const double TILE_WIDTH = 112;
                    const double TILE_HEIGHT = 108; // accommodate taller label

                    var tile = new Button
                    {
                        Width = TILE_WIDTH,
                        Height = TILE_HEIGHT,
                        Margin = new Thickness(3),
                        Padding = new Thickness(0),
                        Background = _defaultBrush,
                        Foreground = (_currentThemeMode == "light") ? Brushes.Black : Brushes.White,
                        Cursor = Cursors.Hand,
                        BorderThickness = new Thickness(0),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    var stack = new StackPanel
                    {
                        Orientation = Orientation.Vertical,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    var iconImage = new Image
                    {
                        Width = 48,
                        Height = 48,
                        Source = GetHighResIcon(iconPath) ?? GetIconImage(iconPath),
                        Margin = new Thickness(0, 0, 0, 4)
                    };

                    var label = new TextBlock
                    {
                        Text = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name),
                        TextAlignment = TextAlignment.Center,
                        TextWrapping = TextWrapping.Wrap,
                        FontSize = 10.5, // slightly smaller to prevent cutting
                        MaxWidth = 90,
                        MaxHeight = 32, // ensures 2-line max wrap
                        TextTrimming = TextTrimming.CharacterEllipsis
                    };

                    stack.Children.Add(iconImage);
                    stack.Children.Add(label);
                    tile.Content = stack;

                    tile.Click += (s, e) => Process.Start("explorer.exe", path);
                    IconPanel.Children.Add(tile);
                    added++;
                }
            }

            if (added > 0)
            {
                // 🔁 Unified grid layout
                RecalculateGridLayout();

                if (!_wasSnapped)
                {
                    ResizeToFit();
                }

                return true;
            }

            // 🧱 Constrain vertical size to prevent scrolling and force wrap
            double screenHeight = SystemParameters.PrimaryScreenHeight;
            double maxHeight = screenHeight * 0.8;

            IconPanel.MaxHeight = maxHeight;

            return added > 0;
        }

        private BitmapSource? GetHighResIcon(string filePath)
        {
            try
            {
                var shellFile = ShellFile.FromFilePath(filePath);
                var bitmap = shellFile.Thumbnail.ExtraLargeBitmapSource;

                if (bitmap != null)
                    bitmap.Freeze(); // Prevents UI threading issues

                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        private BitmapSource? GetIconImage(string? filePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                    return null;

                var icon = System.Drawing.Icon.ExtractAssociatedIcon(filePath);
                if (icon == null) return null;

                return Imaging.CreateBitmapSourceFromHIcon(
                    icon.Handle,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromWidthAndHeight(64, 64));
            }
            catch
            {
                return null;
            }
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
                PathHelper.EnsureAppDataFolderAndDefaults();
                string path = PathHelper.GetFiltersPath();
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
                int rows = IconPanel.Rows;
                int cols = IconPanel.Columns;

                double contentW = cols * TILE_WIDTH + (cols - 1) * TILE_SPACING;
                double contentH = rows * TILE_HEIGHT + (rows - 1) * TILE_SPACING;

                double width = contentW + RootBorder.Padding.Left + RootBorder.Padding.Right + EXTRA_PADDING;
                double height = contentH + RootBorder.Padding.Top + RootBorder.Padding.Bottom + EXTRA_PADDING;

                this.Width = Math.Min(width, SystemParameters.PrimaryScreenWidth - 40);
                this.Height = Math.Min(height, SystemParameters.PrimaryScreenHeight - 40);
            }, DispatcherPriority.Loaded);
        }

        // New Fade Helpers
        public void ShowWithFade()
        {
            if (_lastSnapRect.HasValue)
            {
                this.Left = _lastSnapRect.Value.Left;
                this.Top = _lastSnapRect.Value.Top;
                // ⚠️ Remove this.Width / Height reset
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

        private enum SnapZone
        {
            Left,
            Right,
            BottomCenter, // 🔄 Previously: HorizontalStrip
            Square,
            TopCenter      // 🔄 Previously: CenterBottom
        }

        private void ShowSnapHintForCurrentPosition()
        {
            SnapZone zone = DetermineBestSnapZone();
            int iconCount = IconPanel.Children.Count;

            // 🔁 Temporarily simulate the zone to recalculate layout
            SnapZone oldZone = _currentSnapZone;
            _currentSnapZone = zone;
            RecalculateGridLayout();
            _currentSnapZone = oldZone;

            Rect previewRect = GetDynamicSnapRect(zone, iconCount);
            _overlay?.ShowHint(previewRect);
        }

        private void SnapToNearestZone()
        {
            _currentSnapZone = DetermineBestSnapZone();
            SaveSnapZone(_currentSnapZone);

            // 🔧 Recalculate grid layout BEFORE calculating snap rect
            RecalculateGridLayout();

            int iconCount = IconPanel.Children.Count;
            Rect rect = GetDynamicSnapRect(_currentSnapZone, iconCount);
            _lastSnapRect = rect;
            _wasSnapped = true;
            FadeAndSnapTo(rect);

            ApplyOrientationForSnapZone();
        }

        private void RecalculateGridLayout()
        {
            int iconCount = IconPanel.Children.Count;
            int rows, cols;

            if (_currentSnapZone == SnapZone.Left || _currentSnapZone == SnapZone.Right)
            {
                // 🔁 Vertical layout: tall column
                int maxHeight = (int)(SystemParameters.PrimaryScreenHeight * 0.8);
                int maxRows = Math.Max(1, maxHeight / 112);
                rows = Math.Min(iconCount, maxRows);
                cols = (int)Math.Ceiling((double)iconCount / rows);

                // ✅ Adjust to remove empty rows
                rows = (int)Math.Ceiling((double)iconCount / cols);
            }
            else if (_currentSnapZone == SnapZone.BottomCenter || _currentSnapZone == SnapZone.TopCenter)
            {
                // 📏 Horizontal layout: wide row
                int maxWidth = (int)(SystemParameters.PrimaryScreenWidth * 0.95);
                int maxCols = Math.Max(1, maxWidth / 112);
                cols = Math.Min(iconCount, maxCols);
                rows = (int)Math.Ceiling((double)iconCount / cols);

                // ✅ Adjust to remove empty rows
                if ((rows - 1) * cols >= iconCount)
                    rows--;
            }
            else
            {
                // 🟦 Square or fallback
                cols = (int)Math.Ceiling(Math.Sqrt(iconCount));
                rows = (int)Math.Ceiling((double)iconCount / cols);
            }

            IconPanel.Rows = rows;
            IconPanel.Columns = cols;
        }

        private void SaveSnapZone(SnapZone zone)
        {
            try
            {
                string path = PathHelper.GetSettingsPath();
                Dictionary<string, object> settings;

                if (File.Exists(path))
                {
                    string existing = File.ReadAllText(path);
                    settings = JsonSerializer.Deserialize<Dictionary<string, object>>(existing) ?? new();
                }
                else settings = new();

                settings["SnapZone"] = zone.ToString();
                File.WriteAllText(path, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch
            {
                MessageBox.Show("Failed to save snap zone.", "DeskOp Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ApplyOrientationForSnapZone()
        {
            IconPanel.HorizontalAlignment =
                (_currentSnapZone == SnapZone.BottomCenter || _currentSnapZone == SnapZone.TopCenter)
                ? HorizontalAlignment.Center
                : HorizontalAlignment.Left;
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
                (SnapZone.Left,         Dist(left, top, screenWidth * 0.15, centerY)),
                (SnapZone.Right,        Dist(left, top, screenWidth * 0.85, centerY)),
                (SnapZone.BottomCenter, Dist(left, top, centerX, screenHeight - 60)),
                (SnapZone.Square,       Dist(left, top, centerX, centerY)),
                (SnapZone.TopCenter,    Dist(left, top, centerX, screenHeight * 0.15))
            };

            Array.Sort(distances, (a, b) => a.Item2.CompareTo(b.Item2));
            return distances[0].Item1;
        }

        private Rect GetDynamicSnapRect(SnapZone zone, int iconCount)
        {
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;
            double verticalOffset = 60; // Half inch gap
            double padding = 40;
            int rows = IconPanel.Rows;
            int cols = IconPanel.Columns;

            double contentWidth = cols * TILE_TOTAL_WIDTH;
            double contentHeight = rows * TILE_TOTAL_HEIGHT;

            double finalWidth = contentWidth + RootBorder.Padding.Left + RootBorder.Padding.Right + EXTRA_PADDING;
            double finalHeight = contentHeight + RootBorder.Padding.Top + RootBorder.Padding.Bottom + EXTRA_PADDING;

            Rect snapRect;

            switch (zone)
            {
                case SnapZone.Left:
                    snapRect = new Rect(
                        padding,                                 // Left edge with padding
                        (screenHeight - finalHeight) / 2,        // ✅ Vertically centered
                        finalWidth,
                        finalHeight);
                    break;

                case SnapZone.Right:
                    snapRect = new Rect(
                        screenWidth - finalWidth - padding,      // Right edge with padding
                        (screenHeight - finalHeight) / 2,        // ✅ Vertically centered
                        finalWidth,
                        finalHeight);
                    break;

                case SnapZone.BottomCenter:
                    snapRect = new Rect((screenWidth - finalWidth) / 2, screenHeight - finalHeight - verticalOffset, finalWidth, finalHeight);
                    break;

                case SnapZone.TopCenter:
                    snapRect = new Rect((screenWidth - finalWidth) / 2, verticalOffset, finalWidth, finalHeight);
                    break;

                case SnapZone.Square:
                    double idealSize = Math.Max(finalWidth, finalHeight);
                    double left = (screenWidth - idealSize) / 2;
                    double top = (screenHeight - idealSize) / 2;
                    snapRect = new Rect(left, top, idealSize, idealSize);
                    break;

                default:
                    snapRect = Rect.Empty;
                    break;
            }

            return snapRect;
        }

        public void ApplyFilter(string category)
        {
            _currentCategory = category;
            bool hasIcons = LoadIcons(category);

            if (!hasIcons)
            {
                this.HideWithFade();
                return;
            }

            RecalculateGridLayout();
            ResizeToFit();

            _wasSnapped = true;
            int iconCount = IconPanel.Children.Count;
            Rect snapRect = GetDynamicSnapRect(_currentSnapZone, iconCount);

            // Quick fade out
            var fadeOut = new DoubleAnimation(0, TimeSpan.FromMilliseconds(100));
            fadeOut.Completed += (s, e) =>
            {
                // Set new position instantly when faded out
                this.Left = snapRect.Left;
                this.Top = snapRect.Top;
                this.Width = snapRect.Width;
                this.Height = snapRect.Height;

                // Fade back in
                var fadeIn = new DoubleAnimation(1, TimeSpan.FromMilliseconds(100));
                this.BeginAnimation(Window.OpacityProperty, fadeIn);
            };
            this.BeginAnimation(Window.OpacityProperty, fadeOut);

            ApplyOrientationForSnapZone();
        }

        public void ApplyTheme(Brush defaultBg, Brush highlightBg, string mode)
        {
            _defaultBrush = defaultBg;
            _highlightBrush = highlightBg;
            _currentThemeMode = mode; // 💡 store for later use

            bool isLight = mode == "light";
            var fg = isLight ? Brushes.Black : Brushes.White;
            var bg = isLight ? Brushes.White : (Brush)new BrushConverter().ConvertFrom("#FF292B2F")!;

            this.Background = Brushes.Transparent;
            RootBorder.Background = bg;

            foreach (var child in IconPanel.Children)
            {
                if (child is Button btn)
                {
                    btn.Background = bg;
                    btn.Foreground = fg;
                }
            }
        }

        private void AnimateTo(Rect target)
        {
            this.BeginAnimation(Window.OpacityProperty, null); // Cancel any fades

            this.Left = target.Left;
            this.Top = target.Top;
            this.Width = target.Width;
            this.Height = target.Height;

            if (this.Visibility != Visibility.Visible)
            {
                this.Opacity = 1;
                this.Visibility = Visibility.Visible;
            }
        }

        private void FadeAndSnapTo(Rect target)
        {
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(120));
            fadeOut.Completed += (s, e) =>
            {
                AnimateTo(target); // snap & resize instantly
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180));
                this.BeginAnimation(Window.OpacityProperty, fadeIn);
            };

            this.BeginAnimation(Window.OpacityProperty, fadeOut);
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

        [StructLayout(LayoutKind.Sequential)]

        public struct SHFILEINFO
        {
            public IntPtr hIcon;
            public IntPtr iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes,
            ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool DestroyIcon(IntPtr hIcon);
    }
}