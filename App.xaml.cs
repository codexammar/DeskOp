using System;
using System.Windows;
using Application = System.Windows.Application;

namespace DeskOp
{
    public partial class App : Application
    {
        private System.Windows.Forms.NotifyIcon? _trayIcon;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            this.DispatcherUnhandledException += (s, args) =>
            {
                MessageBox.Show($"Unhandled exception:\n\n{args.Exception.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };

            SetupTrayIcon();

            var bottomWindow = new BottomWindow();
            bottomWindow.Show();
        }

        private void SetupTrayIcon()
        {
            _trayIcon = new System.Windows.Forms.NotifyIcon
            {
                Icon = new System.Drawing.Icon("Assets/deskop.ico"),
                Visible = true,
                Text = "DeskOp"
            };

            var contextMenu = new System.Windows.Forms.ContextMenuStrip();

            var settingsItem = new System.Windows.Forms.ToolStripMenuItem("Settings");
            settingsItem.Click += (s, e) =>
            {
                var settingsWindow = new SettingsWindow();
                settingsWindow.ShowDialog();
            };

            var exitItem = new System.Windows.Forms.ToolStripMenuItem("Stop DeskOp");
            exitItem.Click += (s, e) =>
            {
                _trayIcon!.Visible = false;
                _trayIcon.Dispose();
                Shutdown();
            };

            contextMenu.Items.Add(settingsItem);
            contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            contextMenu.Items.Add(exitItem);

            _trayIcon.ContextMenuStrip = contextMenu;
        }
    }
}
