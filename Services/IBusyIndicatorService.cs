namespace RouteLab.Services;

public interface IBusyIndicatorService
{
    bool IsBusy { get; }

    void Attach(Window window);

    IDisposable Show(string message = "Operazione in corso...");
}
