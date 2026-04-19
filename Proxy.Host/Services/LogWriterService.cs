namespace Proxy.Host.Services;

public class LogWriterService : BackgroundService
{
    private readonly LogService _logService;
    private readonly ILogger<LogWriterService> _logger;

    public LogWriterService(LogService logService, ILogger<LogWriterService> logger)
    {
        _logService = logService;
        _logger     = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("LogWriterService started.");
        try
        {
            await foreach (var entry in _logService.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    _logService.WriteToDb(entry);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "WriteToDb failed for entry {Path} {Method}", entry.Path, entry.Method);
                }
            }
        }
        catch (OperationCanceledException) { /* normal shutdown */ }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "LogWriterService crashed — no more logs will be written!");
        }
        _logger.LogInformation("LogWriterService stopped.");
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
