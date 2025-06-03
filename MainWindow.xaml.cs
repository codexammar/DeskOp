using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace DeskOp
{
    public partial class MainWindow : Window
    {
        private Button? _selectedButton;

        public MainWindow()
        {
            InitializeComponent();
            MouseLeftButtonUp += Window_MouseLeftButtonUp;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            SendToBottom();
        }

        private void ModeButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button clickedButton)
            {
                if (_selectedButton is not null)
                    _selectedButton.Background = (Brush)new BrushConverter().ConvertFrom("#292B2F")!;

                _selectedButton = clickedButton;
                _selectedButton.Background = (Brush)new BrushConverter().ConvertFrom("#2ECC71")!;
            }
        }

        private void DragArea_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
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

        private void SendToBottom()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            SetWindowPos(hwnd, HWND_BOTTOM, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }

        private const int SWP_NOMOVE = 0x0002;
        private const int SWP_NOSIZE = 0x0001;
        private const int SWP_NOACTIVATE = 0x0010;
        private static readonly IntPtr HWND_BOTTOM = new(1);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);
    }
}
