using System.Windows.Threading;

namespace LiveWallpaper.Desktop;

public static class UiDispatcher
{
    private static Dispatcher? _dispatcher;

    public static void Initialize(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public static void Run(Action action)
    {
        if (_dispatcher == null)
        {
            action();
            return;
        }

        if (_dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            _dispatcher.Invoke(action);
        }
    }

    public static void BeginRun(Action action)
    {
        if (_dispatcher == null)
        {
            action();
            return;
        }

        if (_dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            _dispatcher.BeginInvoke(action);
        }
    }

    public static T Run<T>(Func<T> func)
    {
        if (_dispatcher == null)
        {
            return func();
        }

        if (_dispatcher.CheckAccess())
        {
            return func();
        }

        return _dispatcher.Invoke(func);
    }

    public static Task RunAsync(Func<Task> action)
    {
        if (_dispatcher == null)
        {
            return action();
        }

        if (_dispatcher.CheckAccess())
        {
            return action();
        }

        return _dispatcher.InvokeAsync(action).Task.Unwrap();
    }
}
