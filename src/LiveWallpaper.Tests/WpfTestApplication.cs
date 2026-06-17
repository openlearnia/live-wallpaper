using System.Windows;
using System.Windows.Threading;

namespace LiveWallpaper.Tests;

internal static class WpfTestApplication
{
    private static readonly object Gate = new();
    private static Thread? _thread;
    private static Dispatcher? _dispatcher;

    public static void Invoke(Action action)
    {
        EnsureInitialized();
        _dispatcher!.Invoke(action);
    }

    public static Task InvokeAsync(Func<Task> action)
    {
        EnsureInitialized();
        return _dispatcher!.InvokeAsync(action).Task.Unwrap();
    }

    private static void EnsureInitialized()
    {
        if (_dispatcher != null)
        {
            return;
        }

        lock (Gate)
        {
            if (_dispatcher != null)
            {
                return;
            }

            var ready = new ManualResetEventSlim(false);
            _thread = new Thread(() =>
            {
                _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                _dispatcher = Dispatcher.CurrentDispatcher;
                ready.Set();
                Dispatcher.Run();
            });
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.IsBackground = true;
            _thread.Start();
            ready.Wait();
        }
    }
}
