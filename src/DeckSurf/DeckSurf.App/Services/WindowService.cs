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
        /// Associates a WinRT picker with the main window, which is required for
        /// pickers to work in unpackaged apps.
        /// </summary>
        public void InitializePicker(object picker)
        {
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WindowHandle);
        }

        /// <summary>
        /// Constrains how small the window can be resized, in logical (DPI-independent)
        /// pixels. Used to keep the editor's key grid from underflowing.
        /// </summary>
        public void SetMinimumSize(int logicalWidth, int logicalHeight)
        {
            RunOnUIThread(() =>
            {
                if (MainWindow?.AppWindow?.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
                {
                    var scale = MainWindow.Content?.XamlRoot?.RasterizationScale ?? 1.0;
                    presenter.PreferredMinimumWidth = (int)(logicalWidth * scale);
                    presenter.PreferredMinimumHeight = (int)(logicalHeight * scale);
                }
            });
        }
    }
}
