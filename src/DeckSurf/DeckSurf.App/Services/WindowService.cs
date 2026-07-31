using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace DeckSurf.App.Services
{
    /// <summary>
    /// Provides access to the main window handle and dispatcher for services that
    /// need UI-thread marshaling or window interop (file pickers, tray).
    /// </summary>
    public sealed class WindowService
    {
        /// <summary>
        /// Raised when a page asks the shell to switch sections; the payload is
        /// the navigation tag ("devices", "editor", "plugins", "settings").
        /// </summary>
        public event EventHandler<string>? NavigationRequested;

        public Window? MainWindow { get; private set; }

        public DispatcherQueue? DispatcherQueue { get; private set; }

        public IntPtr WindowHandle => MainWindow is null ? IntPtr.Zero : WinRT.Interop.WindowNative.GetWindowHandle(MainWindow);

        public void Initialize(Window window)
        {
            MainWindow = window;
            DispatcherQueue = window.DispatcherQueue;
        }

        /// <summary>
        /// Runs an action on the UI thread; executes inline when already there.
        /// </summary>
        public void RunOnUIThread(Action action)
        {
            var queue = DispatcherQueue;
            if (queue is null || queue.HasThreadAccess)
            {
                action();
            }
            else
            {
                queue.TryEnqueue(() => action());
            }
        }

        /// <summary>
        /// Queues an action on the UI thread even when already there. Use from
        /// collection-changed handlers whose reaction must not run until every
        /// other subscriber (bound controls included) has processed the change:
        /// running inline can, for example, push a SelectedItem into a ComboBox
        /// whose items view has not seen the addition yet, which throws.
        /// </summary>
        public void PostToUIThread(Action action)
        {
            var queue = DispatcherQueue;
            if (queue is null)
            {
                action();
            }
            else
            {
                queue.TryEnqueue(() => action());
            }
        }

        /// <summary>
        /// Asks the shell to show the section with the given navigation tag.
        /// </summary>
        public void RequestNavigation(string tag)
        {
            RunOnUIThread(() => NavigationRequested?.Invoke(this, tag));
        }

        /// <summary>
        /// Associates a WinRT picker with the main window, which is required for
        /// pickers to work in unpackaged apps.
        /// </summary>
        public void InitializePicker(object picker)
        {
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WindowHandle);
        }

        /// <summary>
        /// Locks the window to a fixed width sized for the largest supported device
        /// layout, in logical (DPI-independent) pixels. Height stays user-resizable
        /// above the given minimum; maximize is disabled since width cannot grow.
        /// </summary>
        public void LockWindowWidth(int logicalWidth, int minLogicalHeight)
        {
            RunOnUIThread(() =>
            {
                if (MainWindow?.AppWindow?.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
                {
                    var scale = MainWindow.Content?.XamlRoot?.RasterizationScale ?? 1.0;
                    var physicalWidth = (int)(logicalWidth * scale);

                    presenter.PreferredMinimumWidth = physicalWidth;
                    presenter.PreferredMaximumWidth = physicalWidth;
                    presenter.PreferredMinimumHeight = (int)(minLogicalHeight * scale);
                    presenter.IsMaximizable = false;

                    MainWindow.AppWindow.Resize(new Windows.Graphics.SizeInt32(physicalWidth, MainWindow.AppWindow.Size.Height));
                }
            });
        }
    }
}
