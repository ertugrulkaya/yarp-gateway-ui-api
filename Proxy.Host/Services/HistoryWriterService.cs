namespace Proxy.Host.Services;

public class HistoryWriterService : BackgroundService
{
    private readonly HistoryService _historyService;

    public HistoryWriterService(HistoryService historyService) => _historyService = historyService;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var entry in _historyService.Reader.ReadAllAsync(stoppingToken))
        {
            try { _historyService.WriteToDb(entry); }
            catch { /* never crash the background loop */ }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _historyService.CompleteChannel();

        using var drainCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, drainCts.Token);
        try { await _historyService.Reader.Completion.WaitAsync(linked.Token); }
        catch (OperationCanceledException) { }

        await base.StopAsync(cancellationToken);
    }
}
