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
        // Stop accepting new entries, then drain whatever is still in the channel
        _logService.CompleteChannel();

        // Wait for ExecuteAsync to finish draining (max 5 seconds)
        using var drainCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, drainCts.Token);
        try { await _logService.Reader.Completion.WaitAsync(linked.Token); }
        catch (OperationCanceledException) { /* timeout — accept partial drain */ }

        await base.StopAsync(cancellationToken);
    }
}
