using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace DeskOp
{
    public partial class BottomWindow : Window
    {
        private Point _dragOffset;
        private bool _isDragging = false;
        private SnapHintOverlay? _overlay;

        public BottomWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.Topmost = true;
            this.SizeToContent = SizeToContent.Manual;

            // Set to initial snap location
            Rect initial = GetSnapRect(SnapZone.CenterBottom);
            this.Left = initial.Left;
            this.Top = initial.Top;
            this.Width = initial.Width;
            this.Height = initial.Height;

            // Launch snap overlay
            _overlay = new SnapHintOverlay();
            _overlay.Owner = null;
            _overlay.Show();
        }

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
            Left, Right, HorizontalStrip, Square, CenterBottom
        }

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

            switch (zone)
            {
                case SnapZone.Left:
                    return new Rect(padding, screenHeight * 0.25, 300, 400);
                case SnapZone.Right:
                    return new Rect(screenWidth - 300 - padding, screenHeight * 0.25, 300, 400);
                case SnapZone.HorizontalStrip:
                    return new Rect((screenWidth - 600) / 2, screenHeight - 200 - padding, 600, 200);
                case SnapZone.Square:
                    return new Rect((screenWidth - 400) / 2, (screenHeight - 400) / 2, 400, 400);
                case SnapZone.CenterBottom:
                default:
                    return new Rect((screenWidth - 600) / 2, screenHeight - 180 - padding, 600, 180);
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

            DispatcherTimer timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(interval)
            };

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
    }
}
