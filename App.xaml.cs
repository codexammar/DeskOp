﻿using System;
using System.Windows;
using System.Windows.Media;
using System.Runtime.InteropServices;

namespace DeskOp
{
    public partial class App : Application
    {
        private System.Windows.Forms.NotifyIcon? _trayIcon;

        private Brush _defaultBrush = (Brush)new BrushConverter().ConvertFrom("#292B2F")!;
        private Brush _selectedBrush = (Brush)new BrushConverter().ConvertFrom("#2ECC71")!;
        private string _category = "None";

        // ✅ Add this: tells Windows you're DPI-aware
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        protected override void OnStartup(StartupEventArgs e)
        {
            // ✅ Call it before any windows open
            SetProcessDPIAware();

            base.OnStartup(e);

            this.DispatcherUnhandledException += (s, args) =>
            {
                MessageBox.Show($"Unhandled exception:\n\n{args.Exception.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };

            SetupTrayIcon();
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
