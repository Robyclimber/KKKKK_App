using RouteLab.Services;

namespace RouteLab;

internal static class AppBusy
{
    public static IDisposable Show(string message)
    {
        return ((App)Application.Current!).BusyIndicatorService.Show(message);
    }

    public static Task RunAsync(string message, Func<Task> action)
    {
        return ((App)Application.Current!).BusyIndicatorService.RunAsync(message, action);
    }

    public static Task<T> RunAsync<T>(string message, Func<Task<T>> action)
    {
        return ((App)Application.Current!).BusyIndicatorService.RunAsync(message, action);
    }

    public static void Run(string message, Action action)
    {
        ((App)Application.Current!).BusyIndicatorService.Run(message, action);
    }

    public static T Run<T>(string message, Func<T> action)
    {
        return ((App)Application.Current!).BusyIndicatorService.Run(message, action);
    }
}
