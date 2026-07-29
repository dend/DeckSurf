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
            Ioc.Default.GetRequiredService<WindowService>().Initialize(mainWindow);
            Ioc.Default.GetRequiredService<DeviceService>().Initialize(mainWindow.DispatcherQueue);
            Ioc.Default.GetRequiredService<TrayService>().Initialize(mainWindow);
            mainWindow.Activate();
        }
    }
}
