namespace RouteLab.Services;

public static class BusyIndicatorServiceExtensions
{
    public static void Run(
        this IBusyIndicatorService busyIndicatorService,
        string message,
        Action action)
    {
        ArgumentNullException.ThrowIfNull(busyIndicatorService);
        ArgumentNullException.ThrowIfNull(action);

        using var busy = busyIndicatorService.Show(message);
        action();
    }

    public static T Run<T>(
        this IBusyIndicatorService busyIndicatorService,
        string message,
        Func<T> action)
    {
        ArgumentNullException.ThrowIfNull(busyIndicatorService);
        ArgumentNullException.ThrowIfNull(action);

        using var busy = busyIndicatorService.Show(message);
        return action();
    }

    public static async Task RunAsync(
        this IBusyIndicatorService busyIndicatorService,
        string message,
        Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(busyIndicatorService);
        ArgumentNullException.ThrowIfNull(action);

        using var busy = busyIndicatorService.Show(message);
        await action();
    }

    public static async Task<T> RunAsync<T>(
        this IBusyIndicatorService busyIndicatorService,
        string message,
        Func<Task<T>> action)
    {
        ArgumentNullException.ThrowIfNull(busyIndicatorService);
        ArgumentNullException.ThrowIfNull(action);

        using var busy = busyIndicatorService.Show(message);
        return await action();
    }
}
