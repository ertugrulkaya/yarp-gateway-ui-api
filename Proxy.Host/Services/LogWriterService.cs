namespace Proxy.Host.Services;

public class LogWriterService : BackgroundService
{
    private readonly LogService _logService;

    public LogWriterService(LogService logService) => _logService = logService;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var entry in _logService.Reader.ReadAllAsync(stoppingToken))
        {
            try { _logService.WriteToDb(entry); }
            catch { /* never let a write failure crash the background loop */ }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        // Signal the channel as complete so ReadAllAsync exits cleanly
        _logService.CompleteChannel();
        await base.StopAsync(cancellationToken);
    }
}
