using CommunityToolkit.Mvvm.DependencyInjection;
using DeckSurf.App.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace DeckSurf.App
{
    /// <summary>
    /// DeckSurf desktop application entry point.
    /// </summary>
    public partial class App : Application
    {
        private const string SingleInstanceMutexName = "DeckSurf.App.SingleInstance";

        private Mutex? singleInstanceMutex;
        private Window? mainWindow;

        public App()
        {
            InitializeComponent();

            Ioc.Default.ConfigureServices(
                new ServiceCollection()
                    .AddSingleton<WindowService>()
                    .AddSingleton<ProfileService>()
                    .AddSingleton<PluginService>()
                    .AddSingleton<RuntimeService>()
                    .AddSingleton<DeviceService>()
                    .AddSingleton<TrayService>()
                    .AddSingleton<AppSettingsService>()

                    // ViewModels are singletons: they subscribe to service events, and
                    // per-navigation instances would leak through those subscriptions.
                    .AddSingleton<ViewModels.DevicesViewModel>()
                    .AddSingleton<ViewModels.PluginsViewModel>()
                    .AddSingleton<ViewModels.ProfileEditorViewModel>()
                    .BuildServiceProvider());
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var isFirstInstance);
            if (!isFirstInstance)
            {
                // Another instance already owns the device and profiles; exit quietly.
                Environment.Exit(0);
                return;
            }

            mainWindow = new MainWindow();
            var windowService = Ioc.Default.GetRequiredService<WindowService>();
            windowService.Initialize(mainWindow);
            Ioc.Default.GetRequiredService<DeviceService>().Initialize(mainWindow.DispatcherQueue);
            Ioc.Default.GetRequiredService<TrayService>().Initialize(mainWindow);
            Ioc.Default.GetRequiredService<AppSettingsService>().ApplySavedTheme();

            // The window has one fixed width, sized for the widest supported device
            // (the XL's eight-column grid) plus the inspector pane; only height is
            // user-resizable. Applied once the content tree can report its DPI scale.
            if (mainWindow.Content is FrameworkElement rootElement)
            {
                rootElement.Loaded += (_, _) => windowService.LockWindowWidth(1448, 560);
            }

            mainWindow.Activate();
        }
    }
}
