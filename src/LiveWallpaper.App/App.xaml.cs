using System.Windows;
using LiveWallpaper.App.Services;
using LiveWallpaper.Core;
using LiveWallpaper.Desktop;

namespace LiveWallpaper.App;

public partial class App : System.Windows.Application
{
    private TrayIconService? _tray;
    private WallpaperOrchestratorService? _orchestratorService;
    private HotkeyService? _hotkeyService;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        UiDispatcher.Initialize(Dispatcher);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        AppLogger.Initialize();
        DesktopLog.Write = AppLogger.Info;
        AppLogger.Info("Application starting.");

        var singleInstance = new SingleInstanceManager();
        if (!singleInstance.TryAcquire())
        {
                System.Windows.MessageBox.Show("Live Wallpaper is already running.", "Live Wallpaper", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        _orchestratorService = new WallpaperOrchestratorService();
        await _orchestratorService.InitializeAsync();

        _tray = new TrayIconService(_orchestratorService);
        _tray.Show();

        _hotkeyService = new HotkeyService();
        _hotkeyService.HotkeyPressed += async (_, action) =>
        {
            await Dispatcher.InvokeAsync(async () =>
            {
                if (MainWindow is not MainWindow window)
                {
                    return;
                }

                switch (action)
                {
                    case HotkeyAction.PauseResume:
                        var paused = !_orchestratorService!.Orchestrator.IsPaused;
                        _orchestratorService.Orchestrator.SetPaused(paused);
                        window.SyncPauseUi(paused);
                        _tray?.UpdatePauseMenuLabel(paused);
                        break;
                    case HotkeyAction.NextWallpaper:
                        await window.CycleWallpaperAsync(1);
                        break;
                    case HotkeyAction.PreviousWallpaper:
                        await window.CycleWallpaperAsync(-1);
                        break;
                    case HotkeyAction.ShowWindow:
                        window.Show();
                        window.WindowState = WindowState.Normal;
                        window.Activate();
                        break;
                }
            });
        };

        var mainWindow = new MainWindow(_orchestratorService, _tray, _hotkeyService);
        MainWindow = mainWindow;
        mainWindow.Loaded += (_, _) => RegisterHotkeys(mainWindow);

        if (_orchestratorService.Orchestrator.Settings.StartMinimizedToTray)
        {
            mainWindow.Hide();
        }
        else
        {
            mainWindow.Show();
        }

        _orchestratorService.Orchestrator.PauseEngine.PauseStateChanged += (_, paused) =>
        {
            Dispatcher.Invoke(() => _tray?.UpdatePauseMenuLabel(paused));
            if (MainWindow is MainWindow window)
            {
                Dispatcher.Invoke(() => window.SyncPauseUi(paused));
            }
        };

        Exit += async (_, _) =>
        {
            AppLogger.Info("Application shutting down.");
            _hotkeyService?.Dispose();
            _tray?.Dispose();
            await _orchestratorService.ShutdownAsync();
            _orchestratorService.Dispose();
            singleInstance.Dispose();
        };
    }

    private void RegisterHotkeys(MainWindow window)
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;
        _hotkeyService?.Register(hwnd, _orchestratorService!.Orchestrator.Settings.Hotkeys);
    }

    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        AppLogger.Error("Unhandled UI exception", e.Exception);
        e.Handled = true;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            AppLogger.Error("Unhandled domain exception", ex);
        }
    }
}
