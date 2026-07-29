using H.NotifyIcon;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DeckSurf.App.Services
{
    /// <summary>
    /// Owns the notification-area icon and the hide-to-tray lifecycle. Closing the
    /// main window hides it while the runtime keeps executing; Exit from the tray
    /// menu shuts everything down.
    /// </summary>
    public sealed class TrayService : IDisposable
    {
        private readonly RuntimeService runtimeService;
        private readonly WindowService windowService;

        private TaskbarIcon? trayIcon;
        private Window? window;
        private bool exitRequested;

        public TrayService(RuntimeService runtimeService, WindowService windowService)
        {
            this.runtimeService = runtimeService;
            this.windowService = windowService;
        }

        public void Initialize(Window mainWindow)
        {
            window = mainWindow;

            mainWindow.AppWindow.Closing += (_, args) =>
            {
                if (!exitRequested)
                {
                    // Keep the runtime alive in the background; the tray icon stays.
                    args.Cancel = true;
                    mainWindow.AppWindow.Hide();
                }
            };

            var openItem = new MenuFlyoutItem { Text = "Open DeckSurf" };
            openItem.Click += (_, _) => ShowWindow();

            var stopItem = new MenuFlyoutItem { Text = "Stop runtime" };
            stopItem.Click += (_, _) => Task.Run(runtimeService.Stop);

            var exitItem = new MenuFlyoutItem { Text = "Exit" };
            exitItem.Click += (_, _) => Exit();

            var menu = new MenuFlyout();
            menu.Items.Add(openItem);
            menu.Items.Add(stopItem);
            menu.Items.Add(new MenuFlyoutSeparator());
            menu.Items.Add(exitItem);

            trayIcon = new TaskbarIcon
            {
                ToolTipText = "DeckSurf",
                Icon = new System.Drawing.Icon(Path.Combine(AppContext.BaseDirectory, "Assets", "piglet.ico")),
                ContextFlyout = menu,
                ContextMenuMode = ContextMenuMode.SecondWindow,
            };

            trayIcon.LeftClickCommand = new RelayCommandAdapter(ShowWindow);
            trayIcon.ForceCreate();
        }

        public void Dispose()
        {
            trayIcon?.Dispose();
            trayIcon = null;
        }

        private void ShowWindow()
        {
            windowService.RunOnUIThread(() =>
            {
                if (window is null)
                {
                    return;
                }

                window.AppWindow.Show();
                window.Activate();
            });
        }

        private void Exit()
        {
            exitRequested = true;

            try
            {
                runtimeService.Stop();
            }
            catch (Exception)
            {
                // Shutting down regardless.
            }

            windowService.RunOnUIThread(() =>
            {
                Dispose();
                window?.Close();
                Application.Current.Exit();
            });
        }

        private sealed class RelayCommandAdapter(Action action) : System.Windows.Input.ICommand
        {
            public event EventHandler? CanExecuteChanged
            {
                add { }
                remove { }
            }

            public bool CanExecute(object? parameter) => true;

            public void Execute(object? parameter) => action();
        }
    }
}
